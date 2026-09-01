using System.Text.Json;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Umayor.Dynamics.DeletePoc.Models;

namespace Umayor.Dynamics.DeletePoc.Services;

public class RutMatrixService
{
    private readonly ServiceClient _client;
    private readonly LogService _logService;
    private readonly AppSettings _settings;
    private readonly string _executionId;
    private readonly string _backupFolder;

    public RutMatrixService(ServiceClient client, LogService logService, AppSettings settings, string executionId)
    {
        _client = client;
        _logService = logService;
        _settings = settings;
        _executionId = executionId;
        
        _backupFolder = string.IsNullOrWhiteSpace(settings.Backups?.Directory) 
            ? @"C:\home\data\backups" 
            : settings.Backups.Directory;

        try
        {
            if (!Directory.Exists(_backupFolder))
            {
                Directory.CreateDirectory(_backupFolder);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Advertencia: No se pudo crear el directorio de backup {_backupFolder}: {ex.Message}");
        }
    }

    private string GetFormattedOrRawValue(Entity entity, string logicalName)
    {
        if (entity == null) return string.Empty;
        
        if (entity.FormattedValues.Contains(logicalName))
        {
            return entity.FormattedValues[logicalName] ?? string.Empty;
        }
        
        if (entity.Contains(logicalName) && entity[logicalName] != null)
        {
            var val = entity[logicalName];
            if (val is EntityReference er)
            {
                return er.Name ?? er.Id.ToString();
            }
            if (val is OptionSetValue osv)
            {
                return osv.Value.ToString();
            }
            if (val is Money money)
            {
                return money.Value.ToString();
            }
            return val.ToString() ?? string.Empty;
        }
        
        return string.Empty;
    }

    private string FindFieldAndGetValue(Entity entity, params string[] candidates)
    {
        foreach (var candidate in candidates)
        {
            if (entity.Contains(candidate) || entity.FormattedValues.Contains(candidate))
            {
                return GetFormattedOrRawValue(entity, candidate);
            }
        }
        foreach (var attr in entity.Attributes.Keys)
        {
            foreach (var candidate in candidates)
            {
                if (attr.Equals(candidate, StringComparison.OrdinalIgnoreCase))
                {
                    return GetFormattedOrRawValue(entity, attr);
                }
            }
        }
        return string.Empty;
    }

    public object? Execute(string phase = "Consultar")
    {
        var rut = _settings.Operation.Rut;
        var pasaporte = _settings.Operation.Pasaporte;
        
        if (string.IsNullOrWhiteSpace(rut) && string.IsNullOrWhiteSpace(pasaporte))
        {
            Console.WriteLine("Error: RUT y Pasaporte no están configurados en appsettings.json para el modo RutMatrix.");
            return new {
                isMatrix = true,
                executionId = _executionId,
                rut = rut,
                phase = phase,
                found = false,
                retrievedAt = DateTime.UtcNow.ToString("O"),
                message = "Error: RUT y Pasaporte no están configurados.",
                matrix = new List<object>()
            };
        }

        Console.WriteLine($"Conectando a Dataverse para buscar el contacto ({phase})...");

        // 1. Buscar Contacto
        var qe = new QueryExpression("contact")
        {
            ColumnSet = new ColumnSet(true)
        };
        var contactFilter = new FilterExpression(LogicalOperator.Or);
        if (!string.IsNullOrWhiteSpace(rut))
            contactFilter.AddCondition("wit_rut", ConditionOperator.Equal, rut);
        if (!string.IsNullOrWhiteSpace(pasaporte))
            contactFilter.AddCondition("wit_pasaporte", ConditionOperator.Equal, pasaporte);
            
        qe.Criteria.AddFilter(contactFilter);
        qe.AddOrder("modifiedon", OrderType.Descending);

        var contacts = _client.RetrieveMultiple(qe);

        if (contacts.Entities.Count == 0)
        {
            Console.WriteLine($"No se encontró ningún contacto con los datos proporcionados.");
            return new {
                isMatrix = true,
                executionId = _executionId,
                rut = rut,
                phase = phase,
                found = false,
                retrievedAt = DateTime.UtcNow.ToString("O"),
                message = $"No se encontró ningún contacto con los datos proporcionados.",
                matrix = new List<object>()
            };
        }

        var contact = contacts.Entities[0];
        var contactId = contact.Id;
        var fullname = contact.Contains("fullname") ? (contact["fullname"]?.ToString() ?? "") : "";
        var retrievedRut = contact.Contains("wit_rut") ? (contact["wit_rut"]?.ToString() ?? "") : "";
        var retrievedPasaporte = contact.Contains("wit_pasaporte") ? (contact["wit_pasaporte"]?.ToString() ?? "") : "";
        var dv = contact.Contains("wit_dv") ? (contact["wit_dv"]?.ToString() ?? "") : "";
        string rutCompleto = string.IsNullOrEmpty(dv) ? retrievedRut : $"{retrievedRut}-{dv}";

        var contactSummary = new {
            contactId = contactId.ToString(),
            fullname = fullname,
            rut = retrievedRut,
            pasaporte = retrievedPasaporte,
            dv = dv,
            rutCompleto = rutCompleto,
            tipoDocumento = FindFieldAndGetValue(contact, "wit_tipodedocumento", "wit_tipodocumento", "customertypecode"),
            emailPrincipal = FindFieldAndGetValue(contact, "emailaddress1"),
            emailSecundario = FindFieldAndGetValue(contact, "emailaddress2"),
            telefonoMovil = FindFieldAndGetValue(contact, "mobilephone"),
            telefonoFijo = FindFieldAndGetValue(contact, "telephone1"),
            fase = FindFieldAndGetValue(contact, "wit_fase", "wit_faseactual", "wit_estado"),
            clasificacionContacto = FindFieldAndGetValue(contact, "wit_clasificacioncontacto", "wit_clasificacion"),
            ownerName = FindFieldAndGetValue(contact, "ownerid"),
            carreraInteresActual = FindFieldAndGetValue(contact, "wit_carreradeinteres", "wit_carreradeinteresactual", "wit_carrerainteres", "wit_carrera"),
            sedeActual = FindFieldAndGetValue(contact, "wit_sede", "wit_sedeactual", "wit_sedeid"),
            score = FindFieldAndGetValue(contact, "wit_score", "wit_puntaje"),
            origen = FindFieldAndGetValue(contact, "wit_origen", "leadsourcecode"),
            subOrigen = FindFieldAndGetValue(contact, "wit_suborigen"),
            procesoAdmision = FindFieldAndGetValue(contact, "wit_proceso", "wit_procesoadmision", "wit_periododeadmision", "wit_periodoadmision"),
            createdOn = FindFieldAndGetValue(contact, "createdon"),
            modifiedOn = FindFieldAndGetValue(contact, "modifiedon")
        };

        var originatingLeadId = contact.Contains("originatingleadid") ? ((EntityReference)contact["originatingleadid"]).Id : (Guid?)null;
        var witCaso = contact.Contains("wit_caso") ? ((EntityReference)contact["wit_caso"]).Id : (Guid?)null;
        var witEventoOrigen = contact.Contains("wit_eventoorigen") ? ((EntityReference)contact["wit_eventoorigen"]).Id : (Guid?)null;
        var witColegio = contact.Contains("wit_colegio") ? ((EntityReference)contact["wit_colegio"]).Id : (Guid?)null;
        var witTramo = contact.Contains("wit_tramo") ? ((EntityReference)contact["wit_tramo"]).Id : (Guid?)null;
        var witIngresoBrutoFamiliar = contact.Contains("wit_ingresobrutofamiliar") ? ((EntityReference)contact["wit_ingresobrutofamiliar"]).Id : (Guid?)null;

        Console.WriteLine($"Contacto encontrado: {fullname} (ID: {contactId})");
        Console.WriteLine("Iniciando escaneo de entidades relacionadas (Batch Mode)...");

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

        var queryDefs = new List<(string EntidadRelacionada, string CampoRelacion, string FetchXml)>
        {
            ("contact", "contactid / wit_rut", $@"<fetch aggregate='true'><entity name='contact'><attribute name='contactid' aggregate='count' alias='total_count' /><filter type='or'><condition attribute='contactid' operator='eq' value='{contactId}' />" + (string.IsNullOrWhiteSpace(rut) ? "" : $"<condition attribute='wit_rut' operator='eq' value='{rut}' />") + @"</filter></entity></fetch>"),
            ("lead", "customerid / parentcontactid / originatingleadid", $@"<fetch aggregate='true'><entity name='lead'><attribute name='leadid' aggregate='count' alias='total_count' /><filter type='or'>{leadFilter}</filter></entity></fetch>"),
            ("incident", "customerid / primarycontactid / responsiblecontactid / msa_partnercontactid / wit_caso", $@"<fetch aggregate='true'><entity name='incident'><attribute name='incidentid' aggregate='count' alias='total_count' /><filter type='or'>{incidentFilter}</filter></entity></fetch>"),
            ("phonecall", "regardingobjectid / wit_rut", $@"<fetch aggregate='true'><entity name='phonecall'><attribute name='activityid' aggregate='count' alias='total_count' /><filter type='or'><condition attribute='regardingobjectid' operator='eq' value='{contactId}' />" + (string.IsNullOrWhiteSpace(rut) ? "" : $"<condition attribute='wit_rut' operator='eq' value='{rut}' />") + @"</filter></entity></fetch>"),
            ("email", "regardingobjectid", $@"<fetch aggregate='true'><entity name='email'><attribute name='activityid' aggregate='count' alias='total_count' /><filter><condition attribute='regardingobjectid' operator='eq' value='{contactId}' /></filter></entity></fetch>"),
            ("wit_actividadchat", "regardingobjectid / wit_rut", $@"<fetch aggregate='true'><entity name='wit_actividadchat'><attribute name='activityid' aggregate='count' alias='total_count' /><filter type='or'><condition attribute='regardingobjectid' operator='eq' value='{contactId}' />" + (string.IsNullOrWhiteSpace(rut) ? "" : $"<condition attribute='wit_rut' operator='eq' value='{rut}' />") + @"</filter></entity></fetch>"),
            ("wit_visitaweb", "regardingobjectid", $@"<fetch aggregate='true'><entity name='wit_visitaweb'><attribute name='activityid' aggregate='count' alias='total_count' /><filter><condition attribute='regardingobjectid' operator='eq' value='{contactId}' /></filter></entity></fetch>"),
            ("wit_visitapresencial", "regardingobjectid", $@"<fetch aggregate='true'><entity name='wit_visitapresencial'><attribute name='activityid' aggregate='count' alias='total_count' /><filter><condition attribute='regardingobjectid' operator='eq' value='{contactId}' /></filter></entity></fetch>"),
            ("wit_evento", "regardingobjectid / wit_eventoorigen", $@"<fetch aggregate='true'><entity name='wit_evento'><attribute name='activityid' aggregate='count' alias='total_count' /><filter type='or'>{eventoFilter}</filter></entity></fetch>"),
            ("wit_procesodepostulacion", "wit_contacto / wit_referente", $@"<fetch aggregate='true'><entity name='wit_procesodepostulacion'><attribute name='wit_procesodepostulacionid' aggregate='count' alias='total_count' /><filter type='or'><condition attribute='wit_contacto' operator='eq' value='{contactId}' /><condition attribute='wit_referente' operator='eq' value='{contactId}' /></filter></entity></fetch>"),
            ("wit_solicituddeadmisiondirecta", "wit_postulante / wit_rut", $@"<fetch aggregate='true'><entity name='wit_solicituddeadmisiondirecta'><attribute name='wit_solicituddeadmisiondirectaid' aggregate='count' alias='total_count' /><filter type='or'><condition attribute='wit_postulante' operator='eq' value='{contactId}' />" + (string.IsNullOrWhiteSpace(rut) ? "" : $"<condition attribute='wit_rut' operator='eq' value='{rut}' />") + @"</filter></entity></fetch>"),
            ("wit_historicocontactos", "wit_contact / wit_contactorelacionado", $@"<fetch aggregate='true'><entity name='wit_historicocontactos'><attribute name='wit_historicocontactosid' aggregate='count' alias='total_count' /><filter type='or'><condition attribute='wit_contact' operator='eq' value='{contactId}' /><condition attribute='wit_contactorelacionado' operator='eq' value='{contactId}' /></filter></entity></fetch>"),
            ("wit_historicodatosdecontactabilidad", "wit_contacto / wit_rut", $@"<fetch aggregate='true'><entity name='wit_historicodatosdecontactabilidad'><attribute name='wit_historicodatosdecontactabilidadid' aggregate='count' alias='total_count' /><filter type='or'><condition attribute='wit_contacto' operator='eq' value='{contactId}' />" + (string.IsNullOrWhiteSpace(rut) ? "" : $"<condition attribute='wit_rut' operator='eq' value='{rut}' />") + @"</filter></entity></fetch>"),
            ("wit_contactodelsegmentocomercial", "wit_contacto / wit_rut", $@"<fetch aggregate='true'><entity name='wit_contactodelsegmentocomercial'><attribute name='wit_contactodelsegmentocomercialid' aggregate='count' alias='total_count' /><filter type='or'><condition attribute='wit_contacto' operator='eq' value='{contactId}' />" + (string.IsNullOrWhiteSpace(rut) ? "" : $"<condition attribute='wit_rut' operator='eq' value='{rut}' />") + @"</filter></entity></fetch>"),
            ("wit_colegio", "coordinador / director / encargado / orientador / colegioid", $@"<fetch aggregate='true'><entity name='wit_colegio'><attribute name='wit_colegioid' aggregate='count' alias='total_count' /><filter type='or'>{colegioFilter}</filter></entity></fetch>"),
            ("wit_ingresofamiliarbruto", "wit_tramo / wit_ingresobrutofamiliar", string.IsNullOrEmpty(ingresoFilter) ? "" : $@"<fetch aggregate='true'><entity name='wit_ingresofamiliarbruto'><attribute name='wit_ingresofamiliarbrutoid' aggregate='count' alias='total_count' /><filter type='or'>{ingresoFilter}</filter></entity></fetch>"),
            ("email_attachment", "email.activityid -> activitymimeattachment.objectid", $@"<fetch aggregate='true'><entity name='activitymimeattachment'><attribute name='activitymimeattachmentid' aggregate='count' alias='total_count' /><link-entity name='email' from='activityid' to='objectid' link-type='inner'><filter><condition attribute='regardingobjectid' operator='eq' value='{contactId}' /></filter></link-entity></entity></fetch>"),
            ("msdyn_ocliveworkitem_regardingobjectid", "msdyn_ocliveworkitem.regardingobjectid", $@"<fetch aggregate='true'><entity name='msdyn_ocliveworkitem'><attribute name='msdyn_ocliveworkitemid' aggregate='count' alias='total_count' /><filter><condition attribute='regardingobjectid' operator='eq' value='{contactId}' /></filter></entity></fetch>"),
            ("socialprofile", "socialprofile.customerid", $@"<fetch aggregate='true'><entity name='socialprofile'><attribute name='socialprofileid' aggregate='count' alias='total_count' /><filter><condition attribute='customerid' operator='eq' value='{contactId}' /></filter></entity></fetch>"),
            ("wit_historicodatosofertaacademica", "wit_historicodatosofertaacademica.wit_contactoid", $@"<fetch aggregate='true'><entity name='wit_historicodatosofertaacademica'><attribute name='wit_historicodatosofertaacademicaid' aggregate='count' alias='total_count' /><filter><condition attribute='wit_contactoid' operator='eq' value='{contactId}' /></filter></entity></fetch>"),
            ("wit_historicocarreramatriculada", "wit_historicocarreramatriculada.wit_contacto", $@"<fetch aggregate='true'><entity name='wit_historicocarreramatriculada'><attribute name='wit_historicocarreramatriculadaid' aggregate='count' alias='total_count' /><filter><condition attribute='wit_contacto' operator='eq' value='{contactId}' /></filter></entity></fetch>"),
            ("wit_contactodelacuenta", "wit_contactodelacuenta.wit_contacto", $@"<fetch aggregate='true'><entity name='wit_contactodelacuenta'><attribute name='wit_contactodelacuentaid' aggregate='count' alias='total_count' /><filter><condition attribute='wit_contacto' operator='eq' value='{contactId}' /></filter></entity></fetch>"),
            ("wit_whatsapp", "wit_whatsapp.regardingobjectid", $@"<fetch aggregate='true'><entity name='wit_whatsapp'><attribute name='activityid' aggregate='count' alias='total_count' /><filter><condition attribute='regardingobjectid' operator='eq' value='{contactId}' /></filter></entity></fetch>"),
            ("wit_sms", "wit_sms.regardingobjectid", $@"<fetch aggregate='true'><entity name='wit_sms'><attribute name='activityid' aggregate='count' alias='total_count' /><filter><condition attribute='regardingobjectid' operator='eq' value='{contactId}' /></filter></entity></fetch>"),
            ("activityparty", "activityparty.partyid -> activitypointer.activityid", $@"<fetch aggregate='true'><entity name='activitypointer'><attribute name='activityid' aggregate='count' alias='total_count' /><link-entity name='activityparty' from='activityid' to='activityid' link-type='inner'><filter><condition attribute='partyid' operator='eq' value='{contactId}' /></filter></link-entity></entity></fetch>"),
            ("annotation_contact", "annotation.objectid", $@"<fetch aggregate='true'><entity name='annotation'><attribute name='annotationid' aggregate='count' alias='total_count' /><filter><condition attribute='objectid' operator='eq' value='{contactId}' /></filter></entity></fetch>"),
            ("customeraddress", "customeraddress.parentid", $@"<fetch aggregate='true'><entity name='customeraddress'><attribute name='customeraddressid' aggregate='count' alias='total_count' /><filter><condition attribute='parentid' operator='eq' value='{contactId}' /></filter></entity></fetch>"),
            ("annotation_incident", "incident.incidentid -> annotation.objectid", $@"<fetch aggregate='true'><entity name='annotation'><attribute name='annotationid' aggregate='count' alias='total_count' /><link-entity name='incident' from='incidentid' to='objectid' link-type='inner'><filter type='or'><condition attribute='customerid' operator='eq' value='{contactId}' /><condition attribute='primarycontactid' operator='eq' value='{contactId}' /><condition attribute='responsiblecontactid' operator='eq' value='{contactId}' /><condition attribute='msa_partnercontactid' operator='eq' value='{contactId}' />" + (witCaso.HasValue ? $"<condition attribute='incidentid' operator='eq' value='{witCaso.Value}' />" : "") + @"</filter></link-entity></entity></fetch>")
        };

        var multiReq = new Microsoft.Xrm.Sdk.Messages.ExecuteMultipleRequest
        {
            Requests = new OrganizationRequestCollection(),
            Settings = new Microsoft.Xrm.Sdk.ExecuteMultipleSettings
            {
                ContinueOnError = true,
                ReturnResponses = true
            }
        };

        var validQueryIndices = new List<int>();
        for (int qIdx = 0; qIdx < queryDefs.Count; qIdx++)
        {
            if (!string.IsNullOrEmpty(queryDefs[qIdx].FetchXml))
            {
                multiReq.Requests.Add(new Microsoft.Xrm.Sdk.Messages.RetrieveMultipleRequest
                {
                    Query = new FetchExpression(queryDefs[qIdx].FetchXml)
                });
                validQueryIndices.Add(qIdx);
            }
        }

        Microsoft.Xrm.Sdk.Messages.ExecuteMultipleResponse? multiResp = null;
        try
        {
            multiResp = (Microsoft.Xrm.Sdk.Messages.ExecuteMultipleResponse)_client.Execute(multiReq);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Advertencia: ExecuteMultiple para conteo de matriz falló ({ex.Message}), usando fallback individual.");
        }

        var counts = new int[queryDefs.Count];
        for (int i = 0; i < validQueryIndices.Count; i++)
        {
            int originalIdx = validQueryIndices[i];
            int count = 0;

            if (multiResp != null)
            {
                var respItem = multiResp.Responses.FirstOrDefault(r => r.RequestIndex == i);
                if (respItem != null && respItem.Fault == null && respItem.Response is Microsoft.Xrm.Sdk.Messages.RetrieveMultipleResponse rmr)
                {
                    if (rmr.EntityCollection.Entities.Count > 0 && rmr.EntityCollection.Entities[0].Contains("total_count"))
                    {
                        var countAttr = rmr.EntityCollection.Entities[0]["total_count"];
                        if (countAttr is AliasedValue aliasedValue)
                        {
                            count = (int)aliasedValue.Value;
                        }
                    }
                }
            }
            else
            {
                count = GetCount(queryDefs[originalIdx].EntidadRelacionada, "id", queryDefs[originalIdx].FetchXml);
            }

            counts[originalIdx] = count;
        }

        var matrixRows = new List<object>();
        for (int i = 0; i < queryDefs.Count; i++)
        {
            matrixRows.Add(new {
                EntidadPrincipal = "contact",
                EntidadRelacionada = queryDefs[i].EntidadRelacionada,
                CampoRelacion = queryDefs[i].CampoRelacion,
                CantidadTotal = counts[i]
            });
        }

        // Output and JSON Generation
        Console.WriteLine("\n+" + new string('-', 36) + "+" + new string('-', 9) + "+" + new string('-', 61) + "+");
        Console.WriteLine($"| {"Entidad Relacionada".PadRight(34)} | {"Total".PadLeft(7)} | {"Campo Relación".PadRight(59)} |");
        Console.WriteLine("+" + new string('-', 36) + "+" + new string('-', 9) + "+" + new string('-', 61) + "+");
        
        foreach(dynamic row in matrixRows)
        {
            string entidad = row.EntidadRelacionada;
            if (entidad.Length > 34) entidad = entidad.Substring(0, 31) + "...";
            
            string total = row.CantidadTotal.ToString();
            
            string campo = row.CampoRelacion;
            if (campo.Length > 59) campo = campo.Substring(0, 56) + "...";
            
            Console.WriteLine($"| {entidad.PadRight(34)} | {total.PadLeft(7)} | {campo.PadRight(59)} |");
        }
        Console.WriteLine("+" + new string('-', 36) + "+" + new string('-', 9) + "+" + new string('-', 61) + "+\n");

        var backupDataExtended = new {
            isMatrix = true,
            executionId = _executionId,
            environmentUrl = _settings.Dataverse.Url,
            operationMode = _settings.Operation.Mode,
            phase = phase,
            rut = rut,
            contactId = contactId,
            fullname = fullname,
            dv = dv,
            contactSummary = contactSummary,
            retrievedAt = DateTime.UtcNow.ToString("O"),
            found = true,
            matrix = matrixRows
        };

        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var fileName = $"matrix_{rut}_{timestamp}.json";
        var filePath = Path.Combine(_backupFolder, fileName);
        
        var options = new JsonSerializerOptions { 
            WriteIndented = true, 
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping 
        };
        
        try
        {
            File.WriteAllText(filePath, JsonSerializer.Serialize(backupDataExtended, options), new System.Text.UTF8Encoding(true));
            Console.WriteLine($"\nMatriz exportada exitosamente a: {filePath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nAdvertencia: No se pudo escribir el archivo de matriz en {filePath}: {ex.Message}");
        }
        
        _logService.LogExecution(_executionId, _settings.Dataverse.Url, _settings.Operation.Mode, rut, true, "Success", null, _client.CallerId.ToString());
        return backupDataExtended;
    }

    private int GetCount(string entityName, string primaryKey, string filterXml)
    {
        try
        {
            string fetchXml = $@"
                <fetch aggregate='true'>
                    <entity name='{entityName}'>
                        <attribute name='{primaryKey}' aggregate='count' alias='total_count' />
                        {filterXml}
                    </entity>
                </fetch>";

            var result = _client.RetrieveMultiple(new FetchExpression(fetchXml));
            if (result.Entities.Count > 0 && result.Entities[0].Contains("total_count"))
            {
                var countAttr = result.Entities[0]["total_count"];
                if (countAttr is AliasedValue aliasedValue)
                {
                    return (int)aliasedValue.Value;
                }
            }
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Advertencia: Error al contar en {entityName}: {ex.Message}");
            return 0;
        }
    }
}


