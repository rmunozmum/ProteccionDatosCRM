using System.Text.Json;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Umayor.Dynamics.DeletePoc.Models;

namespace Umayor.Dynamics.DeletePoc.Services;

public class DeletionExecutionReport
{
    public string OperationMode { get; set; } = string.Empty;
    public bool ContactDeleted { get; set; }
    public bool PostMatrixFound { get; set; }
    public bool OperationCompleted { get; set; }
    public object? PreMatrix { get; set; }
    public object? PostMatrix { get; set; }
    public DeletionSummary DeletionSummary { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public int WarningCount => Warnings.Count;
    public List<string> Errors { get; set; } = new();
    public List<string> SanitizedResidualEntities { get; set; } = new();
    public int SanitizedRecordCount { get; set; }
}

public class DeletionSummary
{
    public int TotalFoundBeforeDelete { get; set; }
    public int TotalDeleted { get; set; }
    public int TotalErrors { get; set; }
    public bool BackupCreated { get; set; }
    public string BackupFileName { get; set; } = string.Empty;
    [System.ComponentModel.DataAnnotations.DataType(System.ComponentModel.DataAnnotations.DataType.DateTime)]
    public string StartedAt { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.DataType(System.ComponentModel.DataAnnotations.DataType.DateTime)]
    public string FinishedAt { get; set; } = string.Empty;
}

public class MatrixDeletionService
{
    private readonly ServiceClient _client;
    private readonly LogService _logService;
    private readonly BackupService _backupService;
    private readonly AppSettings _settings;
    private readonly string _executionId;
    
    // We store the found entities grouped by their logical name to process later.
    private Dictionary<string, List<Entity>> _entitiesToDelete = new();

    public MatrixDeletionService(ServiceClient client, LogService logService, BackupService backupService, AppSettings settings, string executionId)
    {
        _client = client;
        _logService = logService;
        _backupService = backupService;
        _settings = settings;
        _executionId = executionId;
    }

    public Dictionary<string, List<Entity>> GetEntitiesToDeleteSnapshot()
    {
        return _entitiesToDelete.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.ToList());
    }

    public DeletionExecutionReport Execute(string confirmationText, bool deletionEnabled, Entity? preFetchedContact = null)
    {
        var rut = _settings.Operation.Rut;
        var pasaporte = _settings.Operation.Pasaporte;
        var mode = _settings.Operation.Mode; // ""EliminarTodoMenosContacto"" or ""EliminarTodo""
        
        var report = new DeletionExecutionReport
        {
            OperationMode = mode,
            DeletionSummary = new DeletionSummary
            {
                StartedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            }
        };

        Console.WriteLine($"Iniciando modo: {mode} para RUT {rut}");

        // Security check
        if (!deletionEnabled)
        {
            string errMsg = "La eliminación no está habilitada en el servidor (Safety:DeletionEnabled es false).";
            Console.WriteLine($"[BLOQUEADO] {errMsg}");
            report.Errors.Add(errMsg);
            report.DeletionSummary.FinishedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            return report;
        }

        if (string.IsNullOrWhiteSpace(confirmationText) || confirmationText.Trim() != _settings.Safety.RequireConfirmationText)
        {
            string errMsg = $"Se requiere confirmación explícita para eliminar. Texto provisto: '{confirmationText}', esperado: '{_settings.Safety.RequireConfirmationText}'.";
            Console.WriteLine($"[BLOQUEADO] {errMsg}");
            report.Errors.Add(errMsg);
            report.DeletionSummary.FinishedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            return report;
        }

        Console.WriteLine("Paso 1: Identificando registros a eliminar...");

        // 1. Buscar Contacto maestro (o usar el pre-consultado)
        Entity? contact = preFetchedContact;
        if (contact == null)
        {
            var qe = new QueryExpression("contact")
            {
                ColumnSet = new ColumnSet(
                    "contactid", "fullname", "wit_rut", "wit_pasaporte", 
                    "originatingleadid", "wit_caso", "wit_eventoorigen", "wit_colegio", 
                    "wit_tramo", "wit_ingresobrutofamiliar",
                    "address1_line1", "address1_line2", "address1_line3", "address1_city", 
                    "address1_postalcode", "address1_telephone1", "address1_telephone2", "address1_telephone3"
                )
            };
            var contactFilter = new FilterExpression(LogicalOperator.Or);
            if (!string.IsNullOrWhiteSpace(rut))
                contactFilter.AddCondition("wit_rut", ConditionOperator.Equal, rut);
            if (!string.IsNullOrWhiteSpace(pasaporte))
                contactFilter.AddCondition("wit_pasaporte", ConditionOperator.Equal, pasaporte);
                
            qe.Criteria.AddFilter(contactFilter);
            var contacts = _client.RetrieveMultiple(qe);

            if (contacts.Entities.Count == 0)
            {
                string errMsg = "No se encontró el contacto principal. Abortando.";
                Console.WriteLine(errMsg);
                report.Errors.Add(errMsg);
                report.DeletionSummary.FinishedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                return report;
            }

            contact = contacts.Entities[0];
        }

        var contactId = contact.Id;
        
        // Save the contact record just in case
        _entitiesToDelete["contact"] = new List<Entity> { contact };

        // Helper to extract fields safely
        Guid? GetGuid(string field) => contact.Contains(field) ? ((EntityReference)contact[field]).Id : null;

        var originatingLeadId = GetGuid("originatingleadid");
        var witCaso = GetGuid("wit_caso");
        var witEventoOrigen = GetGuid("wit_eventoorigen");
        var witColegio = GetGuid("wit_colegio");
        var witTramo = GetGuid("wit_tramo");
        var witIngresoBrutoFamiliar = GetGuid("wit_ingresobrutofamiliar");
        
        // 2. Fetch all dependent entities
        Console.WriteLine("Analizando red de dependencias (Batch Scan)...");

        var leadFilter = $@"<condition attribute='customerid' operator='eq' value='{contactId}' /><condition attribute='parentcontactid' operator='eq' value='{contactId}' />";
        if (originatingLeadId.HasValue) leadFilter += $"<condition attribute='leadid' operator='eq' value='{originatingLeadId.Value}' />";

        var incidentFilter = $@"<condition attribute='customerid' operator='eq' value='{contactId}' /><condition attribute='primarycontactid' operator='eq' value='{contactId}' />
                                <condition attribute='responsiblecontactid' operator='eq' value='{contactId}' /><condition attribute='msa_partnercontactid' operator='eq' value='{contactId}' />";
        if (witCaso.HasValue) incidentFilter += $"<condition attribute='incidentid' operator='eq' value='{witCaso.Value}' />";

        var eventoFilter = $"<condition attribute='regardingobjectid' operator='eq' value='{contactId}' />";
        if (witEventoOrigen.HasValue) eventoFilter += $"<condition attribute='activityid' operator='eq' value='{witEventoOrigen.Value}' />";

        var colegioFilter = $@"<condition attribute='wit_coordinador' operator='eq' value='{contactId}' />
                               <condition attribute='wit_director' operator='eq' value='{contactId}' />
                               <condition attribute='wit_encargado' operator='eq' value='{contactId}' />
                               <condition attribute='wit_orientador' operator='eq' value='{contactId}' />";
        if (witColegio.HasValue) colegioFilter += $"<condition attribute='wit_colegioid' operator='eq' value='{witColegio.Value}' />";

        var ingresoFilter = "";
        if (witTramo.HasValue) ingresoFilter += $"<condition attribute='wit_ingresofamiliarbrutoid' operator='eq' value='{witTramo.Value}' />";
        if (witIngresoBrutoFamiliar.HasValue) ingresoFilter += $"<condition attribute='wit_ingresofamiliarbrutoid' operator='eq' value='{witIngresoBrutoFamiliar.Value}' />";

        var fetchDefs = new List<(string EntityName, string FetchXml)>
        {
            ("lead", $@"<fetch><entity name='lead'><all-attributes /><filter type='or'>{leadFilter}</filter></entity></fetch>"),
            ("incident", $@"<fetch><entity name='incident'><all-attributes /><filter type='or'>{incidentFilter}</filter></entity></fetch>"),
            ("phonecall", $@"<fetch><entity name='phonecall'><all-attributes /><filter type='or'><condition attribute='regardingobjectid' operator='eq' value='{contactId}' />" + (string.IsNullOrWhiteSpace(rut) ? "" : $"<condition attribute='wit_rut' operator='eq' value='{rut}' />") + @"</filter></entity></fetch>"),
            ("email", $@"<fetch><entity name='email'><all-attributes /><filter><condition attribute='regardingobjectid' operator='eq' value='{contactId}' /></filter></entity></fetch>"),
            ("activitymimeattachment", $@"<fetch><entity name='activitymimeattachment'><all-attributes /><link-entity name='email' from='activityid' to='objectid' link-type='inner'><filter><condition attribute='regardingobjectid' operator='eq' value='{contactId}' /></filter></link-entity></entity></fetch>"),
            ("wit_actividadchat", $@"<fetch><entity name='wit_actividadchat'><all-attributes /><filter type='or'><condition attribute='regardingobjectid' operator='eq' value='{contactId}' />" + (string.IsNullOrWhiteSpace(rut) ? "" : $"<condition attribute='wit_rut' operator='eq' value='{rut}' />") + @"</filter></entity></fetch>"),
            ("wit_visitaweb", $@"<fetch><entity name='wit_visitaweb'><all-attributes /><filter><condition attribute='regardingobjectid' operator='eq' value='{contactId}' /></filter></entity></fetch>"),
            ("wit_visitapresencial", $@"<fetch><entity name='wit_visitapresencial'><all-attributes /><filter><condition attribute='regardingobjectid' operator='eq' value='{contactId}' /></filter></entity></fetch>"),
            ("wit_evento", $@"<fetch><entity name='wit_evento'><all-attributes /><filter type='or'>{eventoFilter}</filter></entity></fetch>"),
            ("wit_procesodepostulacion", $@"<fetch><entity name='wit_procesodepostulacion'><all-attributes /><filter type='or'><condition attribute='wit_contacto' operator='eq' value='{contactId}' /><condition attribute='wit_referente' operator='eq' value='{contactId}' /></filter></entity></fetch>"),
            ("wit_solicituddeadmisiondirecta", $@"<fetch><entity name='wit_solicituddeadmisiondirecta'><all-attributes /><filter type='or'><condition attribute='wit_postulante' operator='eq' value='{contactId}' />" + (string.IsNullOrWhiteSpace(rut) ? "" : $"<condition attribute='wit_rut' operator='eq' value='{rut}' />") + @"</filter></entity></fetch>"),
            ("wit_historicocontactos", $@"<fetch><entity name='wit_historicocontactos'><all-attributes /><filter type='or'><condition attribute='wit_contact' operator='eq' value='{contactId}' /><condition attribute='wit_contactorelacionado' operator='eq' value='{contactId}' /></filter></entity></fetch>"),
            ("wit_historicodatosdecontactabilidad", $@"<fetch><entity name='wit_historicodatosdecontactabilidad'><all-attributes /><filter type='or'><condition attribute='wit_contacto' operator='eq' value='{contactId}' />" + (string.IsNullOrWhiteSpace(rut) ? "" : $"<condition attribute='wit_rut' operator='eq' value='{rut}' />") + @"</filter></entity></fetch>"),
            ("wit_contactodelsegmentocomercial", $@"<fetch><entity name='wit_contactodelsegmentocomercial'><all-attributes /><filter type='or'><condition attribute='wit_contacto' operator='eq' value='{contactId}' />" + (string.IsNullOrWhiteSpace(rut) ? "" : $"<condition attribute='wit_rut' operator='eq' value='{rut}' />") + @"</filter></entity></fetch>"),
            ("wit_colegio", $@"<fetch><entity name='wit_colegio'><all-attributes /><filter type='or'>{colegioFilter}</filter></entity></fetch>"),
            ("wit_ingresofamiliarbruto", string.IsNullOrEmpty(ingresoFilter) ? "" : $@"<fetch><entity name='wit_ingresofamiliarbruto'><all-attributes /><filter type='or'>{ingresoFilter}</filter></entity></fetch>"),
            ("msdyn_ocliveworkitem", $@"<fetch><entity name='msdyn_ocliveworkitem'><all-attributes /><filter><condition attribute='msdyn_customer' operator='eq' value='{contactId}' /></filter></entity></fetch>"),
            ("msdyn_ocliveworkitem_social", $@"<fetch><entity name='msdyn_ocliveworkitem'><all-attributes /><link-entity name='socialprofile' from='socialprofileid' to='msdyn_socialprofileid' link-type='inner'><filter><condition attribute='customerid' operator='eq' value='{contactId}' /></filter></link-entity></entity></fetch>"),
            ("msdyn_ocliveworkitem_reg", $@"<fetch><entity name='msdyn_ocliveworkitem'><all-attributes /><filter><condition attribute='regardingobjectid' operator='eq' value='{contactId}' /></filter></entity></fetch>"),
            ("socialprofile", $@"<fetch><entity name='socialprofile'><all-attributes /><filter><condition attribute='customerid' operator='eq' value='{contactId}' /></filter></entity></fetch>"),
            ("wit_historicodatosofertaacademica", $@"<fetch><entity name='wit_historicodatosofertaacademica'><all-attributes /><filter><condition attribute='wit_contactoid' operator='eq' value='{contactId}' /></filter></entity></fetch>"),
            ("wit_historicocarreramatriculada", $@"<fetch><entity name='wit_historicocarreramatriculada'><all-attributes /><filter><condition attribute='wit_contacto' operator='eq' value='{contactId}' /></filter></entity></fetch>"),
            ("wit_contactodelacuenta", $@"<fetch><entity name='wit_contactodelacuenta'><all-attributes /><filter><condition attribute='wit_contacto' operator='eq' value='{contactId}' /></filter></entity></fetch>"),
            ("wit_whatsapp", $@"<fetch><entity name='wit_whatsapp'><all-attributes /><filter><condition attribute='regardingobjectid' operator='eq' value='{contactId}' /></filter></entity></fetch>"),
            ("wit_sms", $@"<fetch><entity name='wit_sms'><all-attributes /><filter><condition attribute='regardingobjectid' operator='eq' value='{contactId}' /></filter></entity></fetch>"),
            ("activitypointer", $@"<fetch><entity name='activitypointer'><all-attributes /><link-entity name='activityparty' from='activityid' to='activityid' link-type='inner'><filter><condition attribute='partyid' operator='eq' value='{contactId}' /></filter></link-entity></entity></fetch>"),
            ("annotation", $@"<fetch><entity name='annotation'><all-attributes /><filter><condition attribute='objectid' operator='eq' value='{contactId}' /></filter></entity></fetch>"),
            ("customeraddress", $@"<fetch><entity name='customeraddress'><all-attributes /><filter><condition attribute='parentid' operator='eq' value='{contactId}' /></filter></entity></fetch>"),
            ("annotation_inc", $@"<fetch><entity name='annotation'><all-attributes /><link-entity name='incident' from='incidentid' to='objectid' link-type='inner'><filter type='or'><condition attribute='customerid' operator='eq' value='{contactId}' /><condition attribute='primarycontactid' operator='eq' value='{contactId}' /><condition attribute='responsiblecontactid' operator='eq' value='{contactId}' /><condition attribute='msa_partnercontactid' operator='eq' value='{contactId}' />" + (witCaso.HasValue ? $"<condition attribute='incidentid' operator='eq' value='{witCaso.Value}' />" : "") + @"</filter></link-entity></entity></fetch>")
        };

        var fetchMultiReq = new Microsoft.Xrm.Sdk.Messages.ExecuteMultipleRequest
        {
            Requests = new OrganizationRequestCollection(),
            Settings = new Microsoft.Xrm.Sdk.ExecuteMultipleSettings
            {
                ContinueOnError = true,
                ReturnResponses = true
            }
        };

        var validFetchIndices = new List<int>();
        for (int fIdx = 0; fIdx < fetchDefs.Count; fIdx++)
        {
            if (!string.IsNullOrEmpty(fetchDefs[fIdx].FetchXml))
            {
                fetchMultiReq.Requests.Add(new Microsoft.Xrm.Sdk.Messages.RetrieveMultipleRequest
                {
                    Query = new FetchExpression(fetchDefs[fIdx].FetchXml)
                });
                validFetchIndices.Add(fIdx);
            }
        }

        Microsoft.Xrm.Sdk.Messages.ExecuteMultipleResponse? fetchMultiResp = null;
        try
        {
            fetchMultiResp = (Microsoft.Xrm.Sdk.Messages.ExecuteMultipleResponse)_client.Execute(fetchMultiReq);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Advertencia: ExecuteMultiple para escaneo de dependencias falló ({ex.Message}), ejecutando fallback.");
        }

        if (fetchMultiResp != null)
        {
            for (int i = 0; i < validFetchIndices.Count; i++)
            {
                int defIdx = validFetchIndices[i];
                string targetEntity = fetchDefs[defIdx].EntityName;
                if (targetEntity.StartsWith("annotation_")) targetEntity = "annotation";
                if (targetEntity.StartsWith("msdyn_ocliveworkitem_")) targetEntity = "msdyn_ocliveworkitem";

                var respItem = fetchMultiResp.Responses.FirstOrDefault(r => r.RequestIndex == i);
                if (respItem != null && respItem.Fault == null && respItem.Response is Microsoft.Xrm.Sdk.Messages.RetrieveMultipleResponse rmr)
                {
                    foreach (var entity in rmr.EntityCollection.Entities)
                    {
                        AddToDictionary(targetEntity, entity);
                    }
                }
            }
        }
        else
        {
            // Fallback secuencial
            foreach (var def in fetchDefs)
            {
                if (string.IsNullOrEmpty(def.FetchXml)) continue;
                string targetEntity = def.EntityName;
                if (targetEntity.StartsWith("annotation_")) targetEntity = "annotation";
                if (targetEntity.StartsWith("msdyn_ocliveworkitem_")) targetEntity = "msdyn_ocliveworkitem";
                FetchRecords(targetEntity, def.FetchXml.Replace("<fetch><entity name='" + def.EntityName + "'><all-attributes />", "").Replace("</entity></fetch>", ""));
            }
        }

        // Remove Contact from deletion list if Dependencies Only mode
        if (mode == "EliminarTodoMenosContacto")
        {
            _entitiesToDelete.Remove("contact");
        }

        // Count totals
        int totalRecordsToDelete = _entitiesToDelete.Sum(kvp => kvp.Value.Count);
        Console.WriteLine($"\n--- RESUMEN DE IMPACTO ---");
        foreach(var kvp in _entitiesToDelete) Console.WriteLine($"{kvp.Key.PadRight(35)}: {kvp.Value.Count} registros");
        Console.WriteLine($"TOTAL A ELIMINAR: {totalRecordsToDelete}");

        report.DeletionSummary.TotalFoundBeforeDelete = totalRecordsToDelete;

        if (totalRecordsToDelete == 0)
        {
            string errMsg = "No hay registros para eliminar.";
            Console.WriteLine(errMsg);
            report.Errors.Add(errMsg);
            report.DeletionSummary.FinishedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            return report;
        }

        // 4. Respaldo Masivo
        Console.WriteLine("\nGenerando respaldo consolidado antes de la destrucción...");
        var backupPath = _backupService.CreateMatrixBackup(_entitiesToDelete, rut, _executionId, _settings.Dataverse.Url, mode);
        var backupFileName = Path.GetFileName(backupPath);
        report.DeletionSummary.BackupCreated = true;
        report.DeletionSummary.BackupFileName = backupFileName;

        var deletionOrder = new List<string>
        {
            "annotation", "customeraddress", "wit_visitaweb", "wit_visitapresencial", "wit_actividadchat", "phonecall", "activitymimeattachment", "email", "wit_evento", 
            "wit_whatsapp", "wit_sms", "activitypointer", "socialprofile", "msdyn_ocliveworkitem", "wit_solicituddeadmisiondirecta", "wit_procesodepostulacion", 
            "wit_historicodatosdecontactabilidad", "wit_historicocontactos", "wit_contactodelsegmentocomercial",
            "wit_historicodatosofertaacademica", "wit_historicocarreramatriculada", "wit_contactodelacuenta",
            "incident", "lead", "wit_ingresofamiliarbruto", "wit_colegio", "contact"
        };

        Console.WriteLine("\nIniciando purga en bloque (Batch Cascade Delete)...");
        int successCount = 0;
        int errorCount = 0;
        bool contactWasDeleted = false;
        int sanitizedCount = 0;
        bool customerAddressSanitized = false;

        var pendingDeletes = new List<(string EntityName, Entity Record)>();
        foreach (var entityName in deletionOrder)
        {
            if (!_entitiesToDelete.ContainsKey(entityName)) continue;
            var records = _entitiesToDelete[entityName];
            if (records.Count == 0) continue;

            foreach (var rec in records)
            {
                if (entityName == "activitypointer")
                {
                    string warnDetail = $"[SKIPPED] No se borra activitypointer directamente. Dataverse no soporta Delete sobre la entidad base de actividades. ID {rec.Id}";
                    report.Warnings.Add(warnDetail);
                    continue;
                }

                if (mode == "EliminarTodo" && entityName == "customeraddress")
                {
                    string warnDetail = $"[SKIPPED] No se borra customeraddress directamente en EliminarTodo. Dataverse administra estas direcciones asociadas al contacto. ID {rec.Id}";
                    report.Warnings.Add(warnDetail);
                    continue;
                }

                if (entityName == "contact" && rec.Id == contactId)
                {
                    continue; // contact se elimina al final
                }

                pendingDeletes.Add((entityName, rec));
            }
        }

        const int deleteChunkSize = 200;
        for (int i = 0; i < pendingDeletes.Count; i += deleteChunkSize)
        {
            var chunk = pendingDeletes.Skip(i).Take(deleteChunkSize).ToList();
            var multiDeleteReq = new Microsoft.Xrm.Sdk.Messages.ExecuteMultipleRequest
            {
                Requests = new OrganizationRequestCollection(),
                Settings = new Microsoft.Xrm.Sdk.ExecuteMultipleSettings
                {
                    ContinueOnError = true,
                    ReturnResponses = true
                }
            };

            foreach (var item in chunk)
            {
                multiDeleteReq.Requests.Add(new Microsoft.Xrm.Sdk.Messages.DeleteRequest
                {
                    Target = new EntityReference(item.EntityName, item.Record.Id)
                });
            }

            Microsoft.Xrm.Sdk.Messages.ExecuteMultipleResponse? multiDeleteResp = null;
            try
            {
                multiDeleteResp = (Microsoft.Xrm.Sdk.Messages.ExecuteMultipleResponse)_client.Execute(multiDeleteReq);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Advertencia: ExecuteMultiple de borrado falló ({ex.Message}), ejecutando fallback secuencial.");
            }

            for (int rIdx = 0; rIdx < chunk.Count; rIdx++)
            {
                var item = chunk[rIdx];
                var respItem = multiDeleteResp?.Responses.FirstOrDefault(r => r.RequestIndex == rIdx);

                if (multiDeleteResp != null && respItem != null && respItem.Fault == null)
                {
                    successCount++;
                }
                else
                {
                    string errorMsg = respItem?.Fault?.Message ?? "Error al ejecutar eliminación";

                    if (mode == "EliminarTodoMenosContacto" && item.EntityName == "customeraddress")
                    {
                        try
                        {
                            ScrubCustomerAddress(item.Record);
                            sanitizedCount++;
                            customerAddressSanitized = true;
                            string warnDetail = $"[SANITIZED] No se pudo borrar {item.EntityName} ID {item.Record.Id}; se limpiaron sus campos de direccion/contactabilidad. Error original: {errorMsg}";
                            Console.WriteLine(warnDetail);
                            report.Warnings.Add(warnDetail);
                            continue;
                        }
                        catch (Exception scrubEx)
                        {
                            string errDetailScrub = $"[ERROR] No se pudo borrar ni limpiar {item.EntityName} ID {item.Record.Id}. Error borrado: {errorMsg}. Error limpieza: {scrubEx.Message}";
                            Console.WriteLine(errDetailScrub);
                            report.Errors.Add(errDetailScrub);
                            errorCount++;
                            continue;
                        }
                    }

                    if (item.EntityName == "wit_colegio" || item.EntityName == "wit_ingresofamiliarbruto" || item.EntityName == "phonecall" || item.EntityName == "email" || item.EntityName == "wit_actividadchat" || item.EntityName == "wit_whatsapp" || item.EntityName == "wit_sms" || item.EntityName == "activitypointer")
                    {
                        try
                        {
                            UnlinkChildEntity(item.EntityName, item.Record, contactId);
                            string warnDetail = $"[UNLINKED] No se pudo borrar {item.EntityName} ID {item.Record.Id} debido a plugin CRM ({errorMsg}); se desvinculó la relación con el contacto.";
                            Console.WriteLine(warnDetail);
                            report.Warnings.Add(warnDetail);
                            report.SanitizedResidualEntities.Add(item.EntityName);
                            continue;
                        }
                        catch (Exception unlinkEx)
                        {
                            string errDetailUnlink = $"[ERROR] No se pudo borrar ni desvincular {item.EntityName} ID {item.Record.Id}. Error borrado: {errorMsg}. Error desvinculación: {unlinkEx.Message}";
                            Console.WriteLine(errDetailUnlink);
                            report.Errors.Add(errDetailUnlink);
                            errorCount++;
                            continue;
                        }
                    }

                    string errDetail = $"[ERROR] Falla al borrar {item.EntityName} ID {item.Record.Id}: {errorMsg}";
                    Console.WriteLine(errDetail);
                    report.Errors.Add(errDetail);
                    errorCount++;
                }
            }
        }

        // Borrar el contacto si el modo es EliminarTodo
        if (mode == "EliminarTodo" && _entitiesToDelete.ContainsKey("contact"))
        {
            try
            {
                _client.Delete("contact", contactId);
                successCount++;
                contactWasDeleted = true;
            }
            catch (Exception ex)
            {
                string errDetail = $"[ERROR] Falla al borrar contact ID {contactId}: {ex.Message}";
                Console.WriteLine(errDetail);
                report.Errors.Add(errDetail);
                errorCount++;
            }
        }

        Console.WriteLine($"\n--- PURGA FINALIZADA ---");
        Console.WriteLine($"Registros eliminados exitosamente: {successCount}");
        Console.WriteLine($"Errores durante eliminación: {errorCount}");
        
        if (mode == "EliminarTodoMenosContacto" && customerAddressSanitized)
        {
            ScrubContactAddressFields(contact);
            report.SanitizedResidualEntities.Add("customeraddress");
            report.SanitizedRecordCount = sanitizedCount;
            Console.WriteLine($"Registros customeraddress saneados: {sanitizedCount}");
        }

        report.ContactDeleted = contactWasDeleted;
        report.DeletionSummary.TotalDeleted = successCount;
        report.DeletionSummary.TotalErrors = errorCount;
        report.DeletionSummary.FinishedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        // Construir matrices en memoria para auditoría instantánea
        var preMatrixRows = _entitiesToDelete.Select(kvp => new {
            EntidadPrincipal = "contact",
            EntidadRelacionada = kvp.Key,
            CampoRelacion = kvp.Key,
            CantidadTotal = kvp.Value.Count
        }).ToList();

        report.PreMatrix = new {
            isMatrix = true,
            executionId = _executionId,
            environmentUrl = _settings.Dataverse.Url,
            operationMode = mode,
            rut = rut,
            contactId = contactId,
            fullname = contact.Contains("fullname") ? contact["fullname"].ToString() : "",
            matrix = preMatrixRows
        };

        var postMatrixRows = new List<object>();
        if (mode == "EliminarTodoMenosContacto")
        {
            postMatrixRows.Add(new {
                EntidadPrincipal = "contact",
                EntidadRelacionada = "contact",
                CampoRelacion = "contactid",
                CantidadTotal = 1
            });
        }
        foreach (var sanitized in report.SanitizedResidualEntities)
        {
            postMatrixRows.Add(new {
                EntidadPrincipal = "contact",
                EntidadRelacionada = sanitized,
                CampoRelacion = sanitized,
                CantidadTotal = _entitiesToDelete.ContainsKey(sanitized) ? _entitiesToDelete[sanitized].Count : 0
            });
        }

        report.PostMatrix = new {
            isMatrix = true,
            executionId = _executionId,
            environmentUrl = _settings.Dataverse.Url,
            operationMode = mode,
            rut = rut,
            contactId = contactId,
            fullname = contact.Contains("fullname") ? contact["fullname"].ToString() : "",
            matrix = postMatrixRows
        };

        return report;
    }

    private void FetchRecords(string entityName, string filterXml)
    {
        try
        {
            var fetchXml = $@"<fetch><entity name='{entityName}'><all-attributes />{filterXml}</entity></fetch>";
            var result = _client.RetrieveMultiple(new FetchExpression(fetchXml));
            foreach (var entity in result.Entities)
            {
                AddToDictionary(entityName, entity);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error obteniendo {entityName}: {ex.Message}");
        }
    }

    private void AddToDictionary(string entityName, Entity entity)
    {
        if (!_entitiesToDelete.ContainsKey(entityName))
        {
            _entitiesToDelete[entityName] = new List<Entity>();
        }
        // Evitar duplicados exactos (ej. msdyn_ocliveworkitem vÃ­a UNION simulado)
        if (!_entitiesToDelete[entityName].Any(e => e.Id == entity.Id))
        {
            _entitiesToDelete[entityName].Add(entity);
        }
    }

    private void UnlinkChildEntity(string entityName, Entity record, Guid contactId)
    {
        var update = new Entity(entityName, record.Id);
        if (entityName == "wit_colegio")
        {
            AddNullIfMatches(update, record, "wit_coordinador", contactId);
            AddNullIfMatches(update, record, "wit_director", contactId);
            AddNullIfMatches(update, record, "wit_encargado", contactId);
            AddNullIfMatches(update, record, "wit_orientador", contactId);
        }
        else if (entityName == "wit_ingresofamiliarbruto")
        {
            AddNullIfMatches(update, record, "wit_contactoid", contactId);
            AddNullIfMatches(update, record, "wit_contacto", contactId);
        }
        else if (entityName == "phonecall" || entityName == "email" || entityName == "wit_actividadchat" || entityName == "wit_whatsapp" || entityName == "wit_sms" || entityName == "activitypointer")
        {
            AddNullIfMatches(update, record, "regardingobjectid", contactId);
        }

        if (update.Attributes.Count > 0)
        {
            _client.Update(update);
        }
    }

    private static void AddNullIfMatches(Entity update, Entity record, string attribute, Guid contactId)
    {
        if (record.Contains(attribute) && record[attribute] is EntityReference pref && pref.Id == contactId)
        {
            update[attribute] = null;
        }
    }

    private void ScrubCustomerAddress(Entity address)
    {
        var update = new Entity("customeraddress", address.Id);
        AddNullsForExistingAttributes(update, address,
            "name",
            "line1", "line2", "line3",
            "city", "stateorprovince", "county", "country", "postalcode", "postofficebox",
            "telephone1", "telephone2", "telephone3", "fax",
            "primarycontactname", "upszone",
            "latitude", "longitude");

        if (update.Attributes.Count > 0)
        {
            _client.Update(update);
        }
    }

    private void ScrubContactAddressFields(Entity contact)
    {
        var update = new Entity("contact", contact.Id);
        var prefixes = new[] { "address1", "address2", "address3" };
        var suffixes = new[]
        {
            "name",
            "line1", "line2", "line3",
            "city", "stateorprovince", "county", "country", "postalcode", "postofficebox",
            "telephone1", "telephone2", "telephone3", "fax",
            "primarycontactname", "upszone",
            "latitude", "longitude"
        };

        foreach (var prefix in prefixes)
        {
            AddNullsForExistingAttributes(update, contact, suffixes.Select(s => $"{prefix}_{s}").ToArray());
        }

        if (update.Attributes.Count > 0)
        {
            _client.Update(update);
        }
    }

    private static void AddNullsForExistingAttributes(Entity update, Entity source, params string[] attributes)
    {
        foreach (var attribute in attributes)
        {
            if (source.Contains(attribute))
            {
                update[attribute] = null;
            }
        }
    }
}
