using System;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.PowerPlatform.Dataverse.Client;
using Umayor.Dynamics.DeletePoc.Models;
using Umayor.Dynamics.DeletePoc.Services;
using Umayor.Dynamics.DeletePoc.Shared.Models;
using Umayor.Dynamics.DeletePoc.Shared.Services;

namespace Umayor.Dynamics.DeletePoc.Functions;

public class QueuePayload
{
    public string executionId { get; set; } = "";
    public int? partitionNumber { get; set; }
    public int? partitionSize { get; set; }
    public List<string>? detailIds { get; set; }
}

public class QueueProcessorFunction
{
    private readonly AppSettings _settings;
    private readonly DataverseConnectionFactory _factory;
    private readonly BlobStorageBackupService _blobBackupService;
    private readonly LogService _logService;
    private readonly ILogger<QueueProcessorFunction> _logger;

    public QueueProcessorFunction(
        AppSettings settings,
        DataverseConnectionFactory factory,
        BlobStorageBackupService blobBackupService,
        LogService logService,
        ILogger<QueueProcessorFunction> logger)
    {
        _settings = settings;
        _factory = factory;
        _blobBackupService = blobBackupService;
        _logService = logService;
        _logger = logger;
    }

    [Function("QueueProcessorFunction")]
    public async Task Run([QueueTrigger("privacy-mass-executions", Connection = "AzureWebJobsStorage")] string messageText)
    {
        _logger.LogInformation($"Mensaje de cola recibido: {messageText}");

        QueuePayload? payload = null;
        try
        {
            payload = JsonSerializer.Deserialize<QueuePayload>(messageText);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al deserializar el mensaje de la cola.");
            return;
        }

        if (payload == null || !Guid.TryParse(payload.executionId, out Guid headerId))
        {
            _logger.LogError("El executionId recibido no es un GUID vÃ¡lido.");
            return;
        }

        using var client = _factory.CreateClient(_settings.Dataverse);

        // 1. Obtener cabecera
        Entity header;
        try
        {
            header = client.Retrieve("um_massexecution", headerId, new ColumnSet(
                "um_massexecutionid", "um_estado", "um_tratamiento", "um_motivo", "um_inicio", "um_requestedbyemail"
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"No se pudo recuperar la cabecera {headerId} de Dataverse.");
            return;
        }

        int currentStatus = ((OptionSetValue)header["um_estado"]).Value;
        if (currentStatus != MassOptionSets.HeaderEstadoPendiente && currentStatus != MassOptionSets.HeaderEstadoEnProceso)
        {
            _logger.LogWarning($"El lote {headerId} ya tiene estado {MassOptionSets.HeaderEstadoToString(currentStatus)}. Abortando.");
            return;
        }

        // Si estaba Pendiente, cambiar a EnProceso y fijar fecha inicio
        if (currentStatus == MassOptionSets.HeaderEstadoPendiente)
        {
            header["um_estado"] = new OptionSetValue(MassOptionSets.HeaderEstadoEnProceso);
            header["um_inicio"] = DateTime.UtcNow;
            client.Update(header);
        }

        // 2. Obtener detalles para procesar. Los mensajes nuevos traen ids de particion;
        // los mensajes antiguos siguen consultando pendientes del lote completo.
        var details = LoadDetailsForQueuePayload(client, headerId, payload);
        _logger.LogInformation($"Se encontraron {details.Count} detalles para procesar en el lote {headerId}. Particion: {payload.partitionNumber?.ToString() ?? "legacy"}.");

        if (details.Count == 0)
        {
            // Validar si el lote completo ya finalizÃ³
            await CheckAndCompleteHeaderAsync(client, headerId);
            return;
        }

        // Generar un ID Ãºnico para este worker
        string workerInstanceId = Guid.NewGuid().ToString("N");
        string tratamientoStr = MassOptionSets.TratamientoToString(((OptionSetValue)header["um_tratamiento"]).Value);
        string motivo = header.Contains("um_motivo") ? header["um_motivo"].ToString() ?? "" : "";
        string requestedByEmail = header.Contains("um_requestedbyemail") ? header["um_requestedbyemail"].ToString() ?? "" : "";

        // Usar un SemaphoreSlim de 8 para la concurrencia local por instancia worker
        using var semaphore = new SemaphoreSlim(8, 8);
        var tasks = new List<Task>();

        foreach (var detail in details)
        {
            await semaphore.WaitAsync();

            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    await ProcessDetailRecordAsync(headerId, detail, workerInstanceId, tratamientoStr, motivo, requestedByEmail);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error procesando detalle {detail.Id}");
                    await MarkDetailAsUnhandledWorkerErrorAsync(headerId, detail.Id, ex.Message);
                }
                finally
                {
                    semaphore.Release();
                }
            }));
        }

        await Task.WhenAll(tasks);

        // Volver a validar si todo el lote estÃ¡ finalizado y actualizar cabecera
        await CheckAndCompleteHeaderAsync(client, headerId);
    }

    private List<Entity> LoadDetailsForQueuePayload(ServiceClient client, Guid headerId, QueuePayload payload)
    {
        var details = new List<Entity>();
        var columns = new ColumnSet(
            "um_massexecutiondetailid", "um_identificador", "um_tipoidentificador",
            "um_estado", "um_workerleaseid", "um_leaseduntil"
        );

        if (payload.detailIds != null && payload.detailIds.Count > 0)
        {
            foreach (var idText in payload.detailIds)
            {
                if (!Guid.TryParse(idText, out var detailId)) continue;

                try
                {
                    var detail = client.Retrieve("um_massexecutiondetail", detailId, columns);
                    if (!detail.Contains("um_estado")) continue;

                    int state = ((OptionSetValue)detail["um_estado"]).Value;
                    bool isPending = state == MassOptionSets.DetailEstadoPendiente;
                    bool hasExpiredLease = state == MassOptionSets.DetailEstadoEnProceso &&
                        (!detail.Contains("um_leaseduntil") || (DateTime)detail["um_leaseduntil"] < DateTime.UtcNow);

                    if (isPending || hasExpiredLease)
                    {
                        details.Add(detail);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, $"No se pudo cargar el detalle particionado {idText}.");
                }
            }

            return details;
        }

        var queryDetails = new QueryExpression("um_massexecutiondetail")
        {
            ColumnSet = columns
        };
        queryDetails.Criteria.AddCondition("um_massexecutionid", ConditionOperator.Equal, headerId);

        var filterPending = new FilterExpression(LogicalOperator.Or);
        filterPending.AddCondition("um_estado", ConditionOperator.Equal, MassOptionSets.DetailEstadoPendiente);

        var filterExpiredLease = new FilterExpression(LogicalOperator.And);
        filterExpiredLease.AddCondition("um_estado", ConditionOperator.Equal, MassOptionSets.DetailEstadoEnProceso);
        filterExpiredLease.AddCondition("um_leaseduntil", ConditionOperator.LessThan, DateTime.UtcNow);
        filterPending.AddFilter(filterExpiredLease);

        queryDetails.Criteria.AddFilter(filterPending);

        var detailsResult = client.RetrieveMultiple(queryDetails);
        return detailsResult.Entities.ToList();
    }

    private async Task ProcessDetailRecordAsync(
        Guid headerId, 
        Entity detail, 
        string workerInstanceId, 
        string tratamiento, 
        string motivo, 
        string requestedByEmail)
    {
        Guid detailId = detail.Id;
        string identifier = detail.Contains("um_identificador") ? detail["um_identificador"].ToString() ?? "" : "";
        string tipoId = detail.Contains("um_tipoidentificador") ? detail["um_tipoidentificador"].ToString() ?? "" : "RUT";

        using var client = _factory.CreateClient(_settings.Dataverse);

        // 1. Reclamar el detalle de forma atómica (Optimistic Concurrency)
        Entity latestDetail;
        try
        {
            latestDetail = client.Retrieve("um_massexecutiondetail", detailId, new ColumnSet("um_massexecutiondetailid", "um_estado", "um_workerleaseid", "um_leaseduntil"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"No se pudo re-consultar el detalle {detailId}. Saltando.");
            return;
        }

        int latestState = ((OptionSetValue)latestDetail["um_estado"]).Value;
        if (latestState != MassOptionSets.DetailEstadoPendiente)
        {
            // Si ya no está pendiente, validar si el lease expiró
            if (latestState == MassOptionSets.DetailEstadoEnProceso)
            {
                DateTime leasedUntil = latestDetail.Contains("um_leaseduntil") ? (DateTime)latestDetail["um_leaseduntil"] : DateTime.MinValue;
                if (leasedUntil > DateTime.UtcNow)
                {
                    _logger.LogInformation($"El registro {identifier} ya está siendo procesado con un lease activo por otro worker. Saltando.");
                    return;
                }
            }
            else
            {
                _logger.LogInformation($"El registro {identifier} ya está completado con estado {MassOptionSets.DetailEstadoToString(latestState)}. Saltando.");
                return;
            }
        }

        // Actualizar lease de forma exclusiva
        try
        {
            latestDetail["um_estado"] = new OptionSetValue(MassOptionSets.DetailEstadoEnProceso);
            latestDetail["um_workerleaseid"] = workerInstanceId;
            latestDetail["um_leaseduntil"] = DateTime.UtcNow.AddMinutes(5);

            var updateReq = new Microsoft.Xrm.Sdk.Messages.UpdateRequest
            {
                Target = latestDetail,
                ConcurrencyBehavior = ConcurrencyBehavior.IfRowVersionMatches
            };
            client.Execute(updateReq);
            _logger.LogInformation($"Registro {identifier} reclamado con Ã©xito por worker {workerInstanceId}.");
        }
        catch (Exception)
        {
            _logger.LogWarning($"Conflicto de concurrencia al reclamar el registro {identifier}. Saltando.");
            return;
        }

        // 3. Comprobar si ya existe una auditorÃ­a para evitar ejecuciones duplicadas
        var queryAudit = new QueryExpression("um_privacyoperationlog")
        {
            ColumnSet = new ColumnSet("um_privacyoperationlogid", "um_operationstatus")
        };
        queryAudit.Criteria.AddCondition("um_executionid", ConditionOperator.Equal, detailId.ToString("N"));
        var audits = client.RetrieveMultiple(queryAudit);

        if (audits.Entities.Count > 0)
        {
            var existingAudit = audits.Entities[0];
            int auditStatus = ((OptionSetValue)existingAudit["um_operationstatus"]).Value;

            _logger.LogWarning($"Se encontrÃ³ log de auditorÃ­a previo para {identifier} con estado {MassOptionSets.DetailEstadoToString(auditStatus)}.");

            if (auditStatus != MassOptionSets.AuditStatusEnProceso)
            {
                // Reconciliar con estado finalizado
                int targetState = auditStatus switch
                {
                    MassOptionSets.AuditStatusConsultado => MassOptionSets.DetailEstadoConsultado,
                    MassOptionSets.AuditStatusEliminado => MassOptionSets.DetailEstadoEliminado,
                    MassOptionSets.AuditStatusNoEncontrado => MassOptionSets.DetailEstadoNoEncontrado,
                    _ => MassOptionSets.DetailEstadoError
                };

                await FinalizeDetailAndAuditAsync(client, headerId, detailId, existingAudit, targetState, "Resultado recuperado de auditorÃ­a previa.", null);
                return;
            }
            else
            {
                // ConciliaciÃ³n de CaÃ­da (Estado Ambiguo)
                _logger.LogWarning($"AuditorÃ­a para {identifier} estÃ¡ en estado EnProceso. Iniciando conciliaciÃ³n de evidencia...");
                await ReconcileAmbiguousStateAsync(client, headerId, detailId, existingAudit, identifier, tipoId, tratamiento, motivo, requestedByEmail);
                return;
            }
        }

        // 4. Flujo de EjecuciÃ³n Normal (Sin auditorÃ­a previa)
        await ExecuteContactOperationAsync(client, headerId, detailId, identifier, tipoId, tratamiento, motivo, requestedByEmail);
    }

    private async Task ReconcileAmbiguousStateAsync(
        ServiceClient client, 
        Guid headerId, 
        Guid detailId, 
        Entity existingAudit, 
        string identifier, 
        string tipoId, 
        string tratamiento, 
        string motivo, 
        string requestedByEmail)
    {
        // RecuperaciÃ³n de evidencia
        string backupName = $"mass-executions/{headerId}/{detailId}_backup.json";
        
        // Consultar Dynamics si el contacto existe
        var (contactExists, contactEntity) = await CheckContactExistsAsync(client, identifier, tipoId);

        // Si es consulta, re-ejecutar sin problemas (es idempotente)
        if (tratamiento == "Consultar")
        {
            await ExecuteContactOperationAsync(client, headerId, detailId, identifier, tipoId, tratamiento, motivo, requestedByEmail);
            return;
        }

        // Si es eliminaciÃ³n:
        // A. El contacto no existe en Dynamics
        if (!contactExists)
        {
            // Verificar si el archivo de respaldo existe en Blob Storage
            bool backupExistsInBlob = false;
            long blobSize = 0;
            string blobHash = "";
            DateTime blobDate = DateTime.UtcNow;

            try
            {
                string json = await _blobBackupService.DownloadBackupAsync(backupName);
                backupExistsInBlob = true;
                var bytes = Encoding.UTF8.GetBytes(json);
                blobSize = bytes.Length;
                blobHash = ComputeSha256Hash(bytes);
            }
            catch {}

            if (backupExistsInBlob)
            {
                _logger.LogInformation($"ConciliaciÃ³n Exitosa para {identifier}: El contacto fue eliminado y existe el respaldo en Blob Storage.");
                
                // Actualizar metadatos en la auditorÃ­a
                existingAudit["um_operationstatus"] = new OptionSetValue(MassOptionSets.AuditStatusEliminado);
                existingAudit["um_backupcreated"] = true;
                existingAudit["um_backupfilename"] = LimitLength(backupName, 100);
                UpdateAuditWithFallback(client, existingAudit);

                // Finalizar detalle masivo
                var meta = new BlobBackupMetadata
                {
                    BackupReference = backupName,
                    BackupDate = blobDate,
                    BackupSize = blobSize,
                    BackupHash = blobHash
                };
                await FinalizeDetailAndAuditAsync(client, headerId, detailId, existingAudit, MassOptionSets.DetailEstadoEliminado, "EliminaciÃ³n conciliada tras recuperaciÃ³n de backup.", meta);
            }
            else
            {
                // Contacto no existe pero no hay respaldo -> Inconsistencia
                _logger.LogError($"ConciliaciÃ³n Fallida para {identifier}: El contacto no existe pero NO se encontrÃ³ el respaldo de respaldo.");
                await MarkAsRequiresReconciliationAsync(client, headerId, detailId, existingAudit, "Contacto no existe pero no hay evidencia de respaldo.");
            }
        }
        else
        {
            // B. El contacto sigue existiendo en Dynamics (la eliminaciÃ³n no se ejecutÃ³)
            _logger.LogInformation($"ConciliaciÃ³n: El contacto {identifier} sigue existiendo. Re-ejecutando eliminaciÃ³n de forma normal.");
            await ExecuteContactOperationAsync(client, headerId, detailId, identifier, tipoId, tratamiento, motivo, requestedByEmail);
        }
    }

    private async Task ExecuteContactOperationAsync(
        ServiceClient client, 
        Guid headerId, 
        Guid detailId, 
        string identifier, 
        string tipoId, 
        string tratamiento, 
        string motivo, 
        string requestedByEmail)
    {
        var localSettings = new AppSettings
        {
            Dataverse = _settings.Dataverse,
            Safety = _settings.Safety,
            Operation = new OperationSettings
            {
                Rut = tipoId == "RUT" ? identifier : "",
                Pasaporte = tipoId == "Pasaporte" ? identifier : "",
                Mode = tratamiento
            }
        };
        var startedAtUtc = DateTime.UtcNow;
        var requestPayload = BuildMassAuditRequestPayload(
            headerId,
            detailId,
            identifier,
            tipoId,
            tratamiento,
            motivo,
            requestedByEmail,
            startedAtUtc);

        // 1. Verificar si existe en Dynamics
        var (contactExists, contactEntity) = await CheckContactExistsAsync(client, identifier, tipoId);

        if (!contactExists)
        {
            // Registrar auditorÃ­a de no encontrado
            var auditSvc = new PrivacyOperationLogService(client, _settings);
            var report = auditSvc.LogOperation(
                executionId: detailId.ToString("N"),
                mode: tratamiento,
                status: "NoEncontrado",
                rutIngresado: identifier,
                pasaporte: tipoId == "Pasaporte" ? identifier : null,
                rutNormalizado: tipoId == "RUT" ? identifier : null,
                dv: tipoId == "RUT" ? InputValidator.CalculateDv(int.Parse(identifier)) : null,
                rutCompleto: tipoId == "RUT" ? $"{identifier}-{InputValidator.CalculateDv(int.Parse(identifier))}" : identifier,
                contactIdText: null,
                contactFullname: "",
                contactDeleted: false,
                requestedByName: requestedByEmail,
                requestedByEmail: requestedByEmail,
                confirmationProvided: true,
                totalFoundBeforeDelete: 0,
                totalDeleted: 0,
                totalErrors: 0,
                backupCreated: false,
                backupFileName: null,
                startedAt: DateTime.UtcNow,
                finishedAt: DateTime.UtcNow,
                errorMessage: "Contacto no encontrado en Dynamics 365.",
                requestPayload: requestPayload,
                responsePayload: new { message = "No encontrado" },
                preMatrix: null,
                postMatrix: null
            );

            // Relacionar AuditorÃ­a con cabecera y detalle
            Guid auditId = report.RecordId != null ? Guid.Parse(report.RecordId) : Guid.Empty;
            if (auditId != Guid.Empty)
            {
                var auditUpdate = new Entity("um_privacyoperationlog", auditId);
                auditUpdate["um_massexecutionid"] = new EntityReference("um_massexecution", headerId);
                auditUpdate["um_massexecutiondetailid"] = new EntityReference("um_massexecutiondetail", detailId);
                client.Update(auditUpdate);
            }

            var auditRef = client.Retrieve("um_privacyoperationlog", auditId, new ColumnSet("um_privacyoperationlogid"));
            await FinalizeDetailAndAuditAsync(client, headerId, detailId, auditRef, MassOptionSets.DetailEstadoNoEncontrado, "Contacto no encontrado.", null);
            return;
        }

        // 2. Crear auditorÃ­a en estado "EnProceso" antes de cualquier eliminaciÃ³n
        var auditEntity = new Entity("um_privacyoperationlog");
        auditEntity["um_executionid"] = detailId.ToString("N");
        
        int operationTypeValue = tratamiento switch
        {
            "Consultar" => 127120000,
            "EliminarTodoMenosContacto" => 127120001,
            "EliminarTodo" => 127120002,
            _ => 127120007
        };
        auditEntity["um_operationtype"] = new OptionSetValue(operationTypeValue);
        auditEntity["um_operationstatus"] = new OptionSetValue(MassOptionSets.AuditStatusEnProceso);
        auditEntity["um_source"] = new OptionSetValue(127120002); // API
        
        if (tipoId == "Pasaporte")
        {
            auditEntity["um_pasaporte"] = identifier;
        }
        else
        {
            auditEntity["um_rutingresado"] = identifier;
            auditEntity["um_rutnormalizado"] = identifier;
            auditEntity["um_dv"] = InputValidator.CalculateDv(int.Parse(identifier));
            auditEntity["um_rutcompleto"] = $"{identifier}-{InputValidator.CalculateDv(int.Parse(identifier))}";
        }

        string fullname = contactEntity.Contains("fullname") ? contactEntity["fullname"]?.ToString() ?? "" : "";
        auditEntity["um_contactfullname"] = fullname;
        auditEntity["um_contactidtext"] = contactEntity.Id.ToString();
        auditEntity["um_requestedbyname"] = requestedByEmail;
        auditEntity["um_requestedbyemail"] = requestedByEmail;
        auditEntity["um_environmenturl"] = _settings.Dataverse.Url;
        auditEntity["um_massexecutionid"] = new EntityReference("um_massexecution", headerId);
        auditEntity["um_massexecutiondetailid"] = new EntityReference("um_massexecutiondetail", detailId);
        auditEntity["um_startedat"] = startedAtUtc;
        auditEntity["um_confirmationprovided"] = tratamiento != "Consultar";
        auditEntity["um_deletionenabled"] = _settings.Safety.DeletionEnabled;
        auditEntity["um_requestjsonfull"] = SerializeForAudit(requestPayload);

        Guid auditLogId = client.Create(auditEntity);
        var auditRefObj = new Entity("um_privacyoperationlog", auditLogId);

        // 3. Ejecutar LÃ³gica de Negocio
        if (tratamiento == "Consultar")
        {
            try
            {
                var rutService = new RutMatrixService(client, _logService, localSettings, detailId.ToString("N"));
                var matrixData = rutService.Execute("Solo Consulta");
                var responsePayload = new
                {
                    identifier,
                    tipoId,
                    tratamiento,
                    status = "Consultado",
                    message = "Consulta exitosa.",
                    data = matrixData
                };

                // Actualizar log a Consultado
                var finishedAt = DateTime.UtcNow;
                auditRefObj["um_operationstatus"] = new OptionSetValue(MassOptionSets.AuditStatusConsultado);
                auditRefObj["um_finishedat"] = finishedAt;
                auditRefObj["um_durationms"] = (int)(finishedAt - startedAtUtc).TotalMilliseconds;
                ApplyAuditJsonPayloads(auditRefObj, requestPayload, responsePayload, null, matrixData, null);
                UpdateAuditWithFallback(client, auditRefObj);

                await FinalizeDetailAndAuditAsync(client, headerId, detailId, auditRefObj, MassOptionSets.DetailEstadoConsultado, "Consulta exitosa.", null);
            }
            catch (Exception ex)
            {
                await MarkAsErrorAsync(
                    client,
                    headerId,
                    detailId,
                    auditRefObj,
                    ex.Message,
                    startedAtUtc,
                    requestPayload,
                    new { identifier, tipoId, tratamiento, status = "Error", error = ex.Message });
            }
        }
        else // Deletion
        {
            try
            {
                var deletionService = new MatrixDeletionService(client, _logService, new BackupService(localSettings), localSettings, detailId.ToString("N"));
                
                // Ejecutar eliminación directa reutilizando el contacto ya localizado (0 roundtrips extra)
                var report = deletionService.Execute("ELIMINAR", _settings.Safety.DeletionEnabled, contactEntity);

                if (report.Errors.Count > 0)
                {
                    throw new Exception(string.Join("; ", report.Errors));
                }

                // Generar y subir respaldo a Blob Storage
                var backupMeta = await _blobBackupService.UploadMatrixBackupAsync(
                    headerId.ToString("N"),
                    detailId.ToString("N"),
                    deletionService.GetEntitiesToDeleteSnapshot(),
                    identifier,
                    _settings.Dataverse.Url,
                    tratamiento
                );

                // Actualizar log y detalle
                var finishedAt = DateTime.UtcNow;
                auditRefObj["um_operationstatus"] = new OptionSetValue(MassOptionSets.AuditStatusEliminado);
                auditRefObj["um_finishedat"] = finishedAt;
                auditRefObj["um_durationms"] = (int)(finishedAt - startedAtUtc).TotalMilliseconds;
                auditRefObj["um_backupcreated"] = true;
                auditRefObj["um_backupfilename"] = LimitLength(backupMeta.BackupReference, 100);
                auditRefObj["um_totaldeleted"] = report.DeletionSummary.TotalDeleted;
                ApplyAuditJsonPayloads(
                    auditRefObj,
                    requestPayload,
                    new
                    {
                        identifier,
                        tipoId,
                        tratamiento,
                        status = "Eliminado",
                        message = "Eliminación exitosa.",
                        data = report,
                        backup = backupMeta
                    },
                    report.PreMatrix,
                    report.PostMatrix,
                    null);
                UpdateAuditWithFallback(client, auditRefObj);

                await FinalizeDetailAndAuditAsync(client, headerId, detailId, auditRefObj, MassOptionSets.DetailEstadoEliminado, "Eliminación exitosa.", backupMeta);
            }
            catch (Exception ex)
            {
                await MarkAsErrorAsync(
                    client,
                    headerId,
                    detailId,
                    auditRefObj,
                    ex.Message,
                    startedAtUtc,
                    requestPayload,
                    new
                    {
                        identifier,
                        tipoId,
                        tratamiento,
                        status = "Error",
                        error = ex.Message
                    });
            }
        }
    }

    private Task<(bool, Entity)> CheckContactExistsAsync(ServiceClient client, string identifier, string tipoId)
    {
        var query = new QueryExpression("contact")
        {
            ColumnSet = new ColumnSet(
                "contactid", "fullname", "wit_rut", "wit_pasaporte", 
                "originatingleadid", "wit_caso", "wit_eventoorigen", "wit_colegio", 
                "wit_tramo", "wit_ingresobrutofamiliar",
                "address1_line1", "address1_line2", "address1_line3", "address1_city", 
                "address1_postalcode", "address1_telephone1", "address1_telephone2", "address1_telephone3"
            )
        };
        
        if (tipoId == "Pasaporte")
        {
            query.Criteria.AddCondition("wit_pasaporte", ConditionOperator.Equal, identifier);
        }
        else
        {
            query.Criteria.AddCondition("wit_rut", ConditionOperator.Equal, identifier);
        }

        var results = client.RetrieveMultiple(query);
        if (results.Entities.Count > 0)
        {
            return Task.FromResult((true, results.Entities[0]));
        }
        return Task.FromResult((false, new Entity()));
    }

    private static List<string> GetBlockingResiduals(object? postMatrix, string tratamiento, IEnumerable<string>? sanitizedResidualEntities = null)
    {
        var residuals = new List<string>();
        if (postMatrix == null) return residuals;

        var allowedResiduals = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (tratamiento == "EliminarTodoMenosContacto")
        {
            allowedResiduals.Add("contact");
        }
        if (sanitizedResidualEntities != null)
        {
            foreach (var entityName in sanitizedResidualEntities)
            {
                allowedResiduals.Add(entityName);
            }
        }

        try
        {
            var json = JsonSerializer.Serialize(postMatrix);
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("matrix", out var matrix) &&
                !document.RootElement.TryGetProperty("Matrix", out matrix))
            {
                return residuals;
            }

            foreach (var row in matrix.EnumerateArray())
            {
                var entityName = ReadString(row, "EntidadRelacionada", "entidadRelacionada");
                var total = ReadInt(row, "CantidadTotal", "cantidadTotal");
                if (total <= 0) continue;
                if (allowedResiduals.Contains(entityName)) continue;

                residuals.Add($"{entityName}={total}");
            }
        }
        catch (Exception ex)
        {
            residuals.Add("No se pudo validar la matriz post-eliminacion: " + ex.Message);
        }

        return residuals;
    }

    private static string ReadString(JsonElement row, params string[] names)
    {
        foreach (var name in names)
        {
            if (row.TryGetProperty(name, out var value))
            {
                return value.GetString() ?? "";
            }
        }

        return "";
    }

    private static int ReadInt(JsonElement row, params string[] names)
    {
        foreach (var name in names)
        {
            if (row.TryGetProperty(name, out var value))
            {
                if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var parsed)) return parsed;
                if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out parsed)) return parsed;
            }
        }

        return 0;
    }

    private async Task FinalizeDetailAndAuditAsync(
        ServiceClient client, 
        Guid headerId, 
        Guid detailId, 
        Entity auditRef, 
        int detailState, 
        string message, 
        BlobBackupMetadata? backup)
    {
        var detail = new Entity("um_massexecutiondetail", detailId);
        detail["um_estado"] = new OptionSetValue(detailState);
        detail["um_errormessage"] = null;
        var (identifier, tipoId) = ReadDetailIdentity(client, detailId);
        detail["um_name"] = BuildMassDetailName(tipoId, identifier, MassOptionSets.DetailEstadoToString(detailState));

        var outcome = new
        {
            estado = MassOptionSets.DetailEstadoToString(detailState),
            mensaje = message,
            individualExecutionId = detailId.ToString("N"),
            auditLogId = auditRef.Id.ToString("N"),
            backupReference = backup?.BackupReference,
            backupDate = backup?.BackupDate.ToString("o"),
            backupSize = backup?.BackupSize,
            backupHash = backup?.BackupHash
        };
        detail["um_resultado"] = JsonSerializer.Serialize(outcome);

        if (backup != null)
        {
            detail["um_backupreference"] = backup.BackupReference;
            detail["um_backupdate"] = backup.BackupDate;
            detail["um_backupsize"] = (int)backup.BackupSize;
            detail["um_backuphash"] = backup.BackupHash;
        }

        client.Update(detail);
    }

    private async Task MarkAsErrorAsync(
        ServiceClient client, 
        Guid headerId, 
        Guid detailId, 
        Entity auditRef, 
        string error,
        DateTime startedAtUtc = default,
        object? requestPayload = null,
        object? responsePayload = null,
        object? preMatrix = null,
        object? postMatrix = null)
    {
        // Actualizar Auditoría a Error
        var finishedAt = DateTime.UtcNow;
        auditRef["um_operationstatus"] = new OptionSetValue(MassOptionSets.AuditStatusError);
        auditRef["um_errormessagefull"] = error;
        auditRef["um_finishedat"] = finishedAt;
        if (startedAtUtc != default)
        {
            auditRef["um_durationms"] = (int)(finishedAt - startedAtUtc).TotalMilliseconds;
        }
        ApplyAuditJsonPayloads(auditRef, requestPayload, responsePayload, preMatrix, postMatrix, error);
        UpdateAuditWithFallback(client, auditRef);

        // Actualizar Detalle a Error
        var detail = new Entity("um_massexecutiondetail", detailId);
        detail["um_estado"] = new OptionSetValue(MassOptionSets.DetailEstadoError);
        detail["um_errormessage"] = error;
        var (identifier, tipoId) = ReadDetailIdentity(client, detailId);
        detail["um_name"] = BuildMassDetailName(tipoId, identifier, "Error");
        
        var outcome = new
        {
            estado = "Error",
            mensaje = error,
            individualExecutionId = detailId.ToString("N"),
            auditLogId = auditRef.Id.ToString("N")
        };
        detail["um_resultado"] = JsonSerializer.Serialize(outcome);
        client.Update(detail);
    }

    private async Task MarkAsRequiresReconciliationAsync(
        ServiceClient client, 
        Guid headerId, 
        Guid detailId, 
        Entity auditRef, 
        string reason,
        DateTime startedAtUtc = default)
    {
        // Actualizar Auditoría
        var finishedAt = DateTime.UtcNow;
        auditRef["um_operationstatus"] = new OptionSetValue(MassOptionSets.AuditStatusRequiereConciliacion);
        auditRef["um_errormessagefull"] = reason;
        auditRef["um_finishedat"] = finishedAt;
        if (startedAtUtc != default)
        {
            auditRef["um_durationms"] = (int)(finishedAt - startedAtUtc).TotalMilliseconds;
        }
        UpdateAuditWithFallback(client, auditRef);

        // Actualizar Detalle
        var detail = new Entity("um_massexecutiondetail", detailId);
        detail["um_estado"] = new OptionSetValue(MassOptionSets.DetailEstadoRequiereConciliacion);
        detail["um_errormessage"] = reason;
        var (identifier, tipoId) = ReadDetailIdentity(client, detailId);
        detail["um_name"] = BuildMassDetailName(tipoId, identifier, "RequiereConciliacion");

        var outcome = new
        {
            estado = "RequiereConciliacion",
            mensaje = reason,
            individualExecutionId = detailId.ToString("N"),
            auditLogId = auditRef.Id.ToString("N")
        };
        detail["um_resultado"] = JsonSerializer.Serialize(outcome);
        client.Update(detail);
    }

    private static Task IncrementHeaderCountersAsync(ServiceClient client, Guid headerId, string status)
    {
        // Los contadores de cabecera se actualizan de forma consolidada al final de la partición
        return Task.CompletedTask;
    }

    private Task CheckAndCompleteHeaderAsync(ServiceClient client, Guid headerId)
    {
        try
        {
            var queryDetails = new QueryExpression("um_massexecutiondetail")
            {
                ColumnSet = new ColumnSet("um_estado")
            };
            queryDetails.Criteria.AddCondition("um_massexecutionid", ConditionOperator.Equal, headerId);
            var allDetails = client.RetrieveMultiple(queryDetails).Entities;

            int total = allDetails.Count;
            int pendientes = allDetails.Count(d => d.Contains("um_estado") && ((OptionSetValue)d["um_estado"]).Value == MassOptionSets.DetailEstadoPendiente);
            int enProceso = allDetails.Count(d => d.Contains("um_estado") && ((OptionSetValue)d["um_estado"]).Value == MassOptionSets.DetailEstadoEnProceso);
            int exitosos = allDetails.Count(d => d.Contains("um_estado") && (((OptionSetValue)d["um_estado"]).Value == MassOptionSets.DetailEstadoEliminado || ((OptionSetValue)d["um_estado"]).Value == MassOptionSets.DetailEstadoConsultado));
            int noEncontrados = allDetails.Count(d => d.Contains("um_estado") && ((OptionSetValue)d["um_estado"]).Value == MassOptionSets.DetailEstadoNoEncontrado);
            int errores = allDetails.Count(d => d.Contains("um_estado") && ((OptionSetValue)d["um_estado"]).Value == MassOptionSets.DetailEstadoError);
            int invalidos = allDetails.Count(d => d.Contains("um_estado") && ((OptionSetValue)d["um_estado"]).Value == MassOptionSets.DetailEstadoInvalido);
            int requiereConciliacion = allDetails.Count(d => d.Contains("um_estado") && ((OptionSetValue)d["um_estado"]).Value == MassOptionSets.DetailEstadoRequiereConciliacion);
            int procesados = exitosos + noEncontrados + errores + invalidos + requiereConciliacion;

            var header = new Entity("um_massexecution", headerId);
            header["um_totalregistros"] = total;
            header["um_procesados"] = procesados;
            header["um_exitosos"] = exitosos;
            header["um_noencontrados"] = noEncontrados;
            header["um_errores"] = errores.ToString();
            header["um_invalidos"] = invalidos.ToString();
            header["um_requiereconciliacion"] = requiereConciliacion;

            if (pendientes == 0 && enProceso == 0)
            {
                header["um_estado"] = new OptionSetValue((errores > 0 || requiereConciliacion > 0) ? MassOptionSets.HeaderEstadoCompletadoConErrores : MassOptionSets.HeaderEstadoCompletado);
                header["um_termino"] = DateTime.UtcNow;
            }
            else
            {
                header["um_estado"] = new OptionSetValue(MassOptionSets.HeaderEstadoEnProceso);
            }

            client.Update(header);
            _logger.LogInformation($"Lote masivo {headerId} actualizado: Total={total}, Procesados={procesados}, Exitosos={exitosos}, Errores={errores}.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error al actualizar contadores consolidados de la cabecera {headerId}");
        }
        return Task.CompletedTask;
    }

    private async Task MarkDetailAsUnhandledWorkerErrorAsync(Guid headerId, Guid detailId, string error)
    {
        try
        {
            using var client = _factory.CreateClient(_settings.Dataverse);
            var detail = new Entity("um_massexecutiondetail", detailId);
            detail["um_estado"] = new OptionSetValue(MassOptionSets.DetailEstadoError);
            detail["um_errormessage"] = $"Error no controlado del worker: {error}";
            var (identifier, tipoId) = ReadDetailIdentity(client, detailId);
            detail["um_name"] = BuildMassDetailName(tipoId, identifier, "Error");
            detail["um_resultado"] = JsonSerializer.Serialize(new
            {
                estado = "Error",
                mensaje = $"Error no controlado del worker: {error}",
                individualExecutionId = detailId.ToString("N")
            });
            client.Update(detail);
            await IncrementHeaderCountersAsync(client, headerId, "Error");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"No se pudo marcar el detalle {detailId} como error no controlado.");
        }
    }

    private static int ReadIntAttribute(Entity entity, string attributeName)
    {
        if (!entity.Contains(attributeName) || entity[attributeName] == null) return 0;

        return entity[attributeName] switch
        {
            int i => i,
            long l => (int)l,
            string s when int.TryParse(s, out var parsed) => parsed,
            _ => 0
        };
    }

    private static void SetCounterAttribute(Entity entity, string attributeName, int value)
    {
        if (entity.Contains(attributeName) && entity[attributeName] is string)
        {
            entity[attributeName] = value.ToString();
            return;
        }

        entity[attributeName] = value;
    }

    private static object BuildMassAuditRequestPayload(
        Guid headerId,
        Guid detailId,
        string identifier,
        string tipoId,
        string tratamiento,
        string motivo,
        string requestedByEmail,
        DateTime startedAtUtc)
    {
        return new
        {
            source = "MassOrchestration",
            massExecutionId = headerId.ToString("N"),
            massExecutionDetailId = detailId.ToString("N"),
            identifier,
            tipoId,
            tratamiento,
            motivo,
            requestedByEmail,
            confirmationText = tratamiento == "Consultar" ? null : "ELIMINAR",
            requestedAt = startedAtUtc.ToString("o")
        };
    }

    private static void ApplyAuditJsonPayloads(
        Entity auditRef,
        object? requestPayload,
        object? responsePayload,
        object? preMatrix,
        object? postMatrix,
        string? error)
    {
        if (requestPayload != null)
        {
            auditRef["um_requestjsonfull"] = SerializeForAudit(requestPayload);
        }

        if (responsePayload != null)
        {
            auditRef["um_responsejsonfull"] = SerializeForAudit(responsePayload);
        }

        if (preMatrix != null)
        {
            auditRef["um_prematrixjsonfull"] = SerializeForAudit(preMatrix);
        }

        if (postMatrix != null)
        {
            auditRef["um_postmatrixjsonfull"] = SerializeForAudit(postMatrix);
        }

        if (!string.IsNullOrWhiteSpace(error))
        {
            auditRef["um_errormessagefull"] = error;
        }
    }

    private static string SerializeForAudit(object value)
    {
        try
        {
            return JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = false });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { serializationError = ex.Message });
        }
    }

    private static void UpdateAuditWithFallback(ServiceClient client, Entity auditRef)
    {
        try
        {
            client.Update(auditRef);
            return;
        }
        catch
        {
            TruncateAuditFullJsonFields(auditRef);
            client.Update(auditRef);
        }
    }

    private static void TruncateAuditFullJsonFields(Entity auditRef)
    {
        string[] fields =
        {
            "um_errormessagefull",
            "um_requestjsonfull",
            "um_responsejsonfull",
            "um_prematrixjsonfull",
            "um_postmatrixjsonfull"
        };

        foreach (var field in fields)
        {
            if (auditRef.Contains(field) && auditRef[field] is string value)
            {
                auditRef[field] = LimitLength(value, 4000);
            }
        }
    }

    private static (string Identifier, string Type) ReadDetailIdentity(ServiceClient client, Guid detailId)
    {
        try
        {
            var detail = client.Retrieve("um_massexecutiondetail", detailId, new ColumnSet("um_identificador", "um_tipoidentificador"));
            string identifier = detail.Contains("um_identificador") ? detail["um_identificador"]?.ToString() ?? "" : "";
            string type = detail.Contains("um_tipoidentificador") ? detail["um_tipoidentificador"]?.ToString() ?? "" : "";
            return (identifier, type);
        }
        catch
        {
            return ("", "");
        }
    }

    private static string BuildMassDetailName(string type, string identifier, string state)
    {
        string cleanType = string.IsNullOrWhiteSpace(type) ? "Identificador" : type.Trim();
        string cleanIdentifier = string.IsNullOrWhiteSpace(identifier) ? "Sin identificador" : identifier.Trim();
        string cleanState = string.IsNullOrWhiteSpace(state) ? "Pendiente" : state.Trim();
        string name = $"{cleanType} {cleanIdentifier} - {cleanState}";
        return name.Length <= 100 ? name : name.Substring(0, 100);
    }

    private static string LimitLength(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return "";
        return value.Length <= maxLength ? value : value.Substring(0, maxLength);
    }

    private static string ComputeSha256Hash(byte[] bytes)
    {
        using (var sha256 = System.Security.Cryptography.SHA256.Create())
        {
            byte[] hashBytes = sha256.ComputeHash(bytes);
            var sb = new StringBuilder();
            foreach (var b in hashBytes)
            {
                sb.Append(b.ToString("x2"));
            }
            return sb.ToString();
        }
    }
}
