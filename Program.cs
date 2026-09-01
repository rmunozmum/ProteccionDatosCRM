using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Linq;
using System.Text.Json;
using System.Collections.Generic;
using System;
using System.IO;
using Microsoft.Xrm.Sdk;
using Umayor.Dynamics.DeletePoc.Models;
using Umayor.Dynamics.DeletePoc.Services;
using Umayor.Dynamics.DeletePoc.Shared.Models;
using Umayor.Dynamics.DeletePoc.Shared.Services;
using Microsoft.Xrm.Sdk.Query;
using System.Text;

namespace Umayor.Dynamics.DeletePoc;

public class ExecutionResponse
{
    public string Message { get; set; } = "";
    public string ExecutionId { get; set; } = "";
    public List<RutResult> Results { get; set; } = new();
}

public class RutResult
{
    public string Rut { get; set; } = "";
    public string Status { get; set; } = "";
    public object? Data { get; set; }
}

public class ContactSummary
{
    public string ContactId { get; set; } = "";
    public string Fullname { get; set; } = "";
    public string Rut { get; set; } = "";
    public string Dv { get; set; } = "";
    public string RutCompleto { get; set; } = "";
    public string TipoDocumento { get; set; } = "";
    public string EmailPrincipal { get; set; } = "";
    public string EmailSecundario { get; set; } = "";
    public string TelefonoMovil { get; set; } = "";
    public string TelefonoFijo { get; set; } = "";
    public string Fase { get; set; } = "";
    public string ClasificacionContacto { get; set; } = "";
    public string OwnerName { get; set; } = "";
    public string CarreraInteresActual { get; set; } = "";
    public string SedeActual { get; set; } = "";
    public string Score { get; set; } = "";
    public string Origen { get; set; } = "";
    public string SubOrigen { get; set; } = "";
    public string ProcesoAdmision { get; set; } = "";
    public string CreatedOn { get; set; } = "";
    public string ModifiedOn { get; set; } = "";
}

public class MatrixRow
{
    public string EntidadPrincipal { get; set; } = "";
    public string EntidadRelacionada { get; set; } = "";
    public string CampoRelacion { get; set; } = "";
    public int CantidadTotal { get; set; }
}

public class ConsultationData
{
    public bool IsMatrix { get; set; }
    public string ExecutionId { get; set; } = "";
    public string EnvironmentUrl { get; set; } = "";
    public string OperationMode { get; set; } = "";
    public string Phase { get; set; } = "";
    public string Rut { get; set; } = "";
    public string ContactId { get; set; } = "";
    public string Fullname { get; set; } = "";
    public string Dv { get; set; } = "";
    public ContactSummary ContactSummary { get; set; } = new();
    [System.ComponentModel.DataAnnotations.DataType(System.ComponentModel.DataAnnotations.DataType.DateTime)]
    public string RetrievedAt { get; set; } = "";
    public bool Found { get; set; }
    public List<MatrixRow> Matrix { get; set; } = new();
}

public class Program
{
    private const string ApiBuild = "mass-orchestration-v1-20260804-file-mass";

    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // var builder = WebApplication.CreateBuilder(args);

        // Configuration
        builder.Configuration
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddEnvironmentVariables();

        // Bind settings and register as singleton
        var settings = new AppSettings();
        builder.Configuration.Bind(settings);
        builder.Services.AddSingleton(settings);

        // Validate on startup
        var validator = new SafetyValidator();
        validator.Validate(settings);

        // Register services
        builder.Services.AddSingleton<LogService>();
        builder.Services.AddSingleton<BackupService>();
        builder.Services.AddSingleton<DataverseConnectionFactory>();
        builder.Services.AddSingleton<ReportCatalogService>();
        builder.Services.AddSingleton<IReportQueryExecutor, ReportQueryExecutor>();
        builder.Services.AddSingleton<IReportAuditService, ReportAuditService>();
        // Registrar servicios de procesos masivos (Azure)
        builder.Services.AddSingleton<BlobStorageBackupService>();
        builder.Services.AddSingleton(sp =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            string conn = config["AzureStorage:ConnectionString"] ?? "";
            string queueName = config["AzureStorage:QueueName"] ?? "privacy-mass-executions";
            
            if (!string.IsNullOrWhiteSpace(conn))
            {
                return new Azure.Storage.Queues.QueueClient(conn, queueName);
            }
            else
            {
                string accountUrl = config["AzureStorage:AccountUrl"] ?? "";
                if (!string.IsNullOrWhiteSpace(accountUrl))
                {
                    var queueUri = new Uri($"{accountUrl.TrimEnd('/')}/{queueName}");
                    return new Azure.Storage.Queues.QueueClient(queueUri, new Azure.Identity.DefaultAzureCredential());
                }
                else
                {
                    return new Azure.Storage.Queues.QueueClient("UseDevelopmentStorage=true", queueName);
                }
            }
        });

        // Registrar Outbox Recovery Worker
        builder.Services.AddHostedService<OutboxRecoveryWorker>();
        // Swagger configuration
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(options =>
        {
            options.DocumentFilter<AdditionalSchemasDocumentFilter>();
        });

        // Allow CORS for local dev if needed, or just let static files run
        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(b => b.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
        });

        var app = builder.Build();

        app.UseSwagger();
        app.UseSwaggerUI();

        app.UseCors();
        app.UseStaticFiles(); // Serve wwwroot
        
        // Serve Default Document
        app.UseDefaultFiles(new DefaultFilesOptions { DefaultFileNames = new List<string> { "index.html" } });

        // Start Endpoints

        // Endpoints de Reportería
        app.MapGet("/api/reports/catalog", (ReportCatalogService catalogService) =>
        {
            return Results.Ok(catalogService.GetCatalog());
        }).WithName("GetReportsCatalog").Produces<List<ReportMetadata>>(200);

        app.MapPost("/api/reports/execute", async (ReportExecutionRequest req, HttpContext httpContext, IReportQueryExecutor executor, IReportAuditService audit) =>
        {
            string reqEmail = httpContext.Request.Headers["X-MS-CLIENT-PRINCIPAL-NAME"].ToString();
            if (string.IsNullOrEmpty(reqEmail))
            {
                reqEmail = httpContext.User.Identity?.Name ?? "Unknown";
            }

            string executionId = Guid.NewGuid().ToString("N");
            var result = await executor.ExecuteReportAsync(req, executionId, reqEmail);

            await audit.LogReportExecutionAsync(
                req.ReportCode,
                result.ReportName,
                reqEmail,
                DateTime.UtcNow,
                System.Text.Json.JsonSerializer.Serialize(req.Parameters),
                System.Text.Json.JsonSerializer.Serialize(result),
                result.Success,
                result.Errors.Count > 0 ? string.Join(", ", result.Errors) : "",
                result.Summary.TotalRows,
                0 // duration stub
            );

            return Results.Ok(result);
        }).WithName("ExecuteReport").Produces<ReportResponse>(200);

        app.MapPost("/api/reports/export", () =>
        {
            return Results.StatusCode(501); // Not Implemented
        }).WithName("ExportReport").Produces(501);

        app.MapPost("/api/execute-single", async (HttpContext httpContext, AppSettings baseSettings, LogService logService, BackupService backupService, DataverseConnectionFactory factory) =>
        {
            var req = new SingleRequest();
            string rawBody = "";
            
            httpContext.Request.EnableBuffering();
            using (var reader = new StreamReader(httpContext.Request.Body, Encoding.UTF8, leaveOpen: true))
            {
                rawBody = await reader.ReadToEndAsync();
                httpContext.Request.Body.Position = 0;
            }

            if (!string.IsNullOrWhiteSpace(rawBody))
            {
                try
                {
                    var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    req = System.Text.Json.JsonSerializer.Deserialize<SingleRequest>(rawBody, options) ?? new SingleRequest();
                }
                catch { }
            }
            
            // Fallback to query string if body is empty or fields are missing
            if (string.IsNullOrEmpty(req.Rut)) req.Rut = httpContext.Request.Query["rut"].ToString() ?? "";
            if (string.IsNullOrEmpty(req.Rut)) req.Rut = httpContext.Request.Query["Rut"].ToString() ?? "";
            
            if (string.IsNullOrEmpty(req.Pasaporte)) req.Pasaporte = httpContext.Request.Query["pasaporte"].ToString() ?? "";
            if (string.IsNullOrEmpty(req.Pasaporte)) req.Pasaporte = httpContext.Request.Query["Pasaporte"].ToString() ?? "";
            
            if (string.IsNullOrEmpty(req.Mode)) req.Mode = httpContext.Request.Query["mode"].ToString() ?? "";
            if (string.IsNullOrEmpty(req.Mode)) req.Mode = httpContext.Request.Query["Mode"].ToString() ?? "";
            
            if (string.IsNullOrEmpty(req.ConfirmationText)) req.ConfirmationText = httpContext.Request.Query["confirmationText"].ToString() ?? "";
            if (string.IsNullOrEmpty(req.ConfirmationText)) req.ConfirmationText = httpContext.Request.Query["ConfirmationText"].ToString() ?? "";

            // Auto-detect Pasaporte only when the value does not look like a RUT body or RUT with DV.
            if (!string.IsNullOrWhiteSpace(req.Rut) && string.IsNullOrWhiteSpace(req.Pasaporte))
            {
                string cleanRut = req.Rut.Replace(".", "").Replace("-", "").Trim();
                bool looksLikeRutBody = System.Text.RegularExpressions.Regex.IsMatch(cleanRut, @"^\d{7,8}$");
                bool looksLikeRutWithDv = System.Text.RegularExpressions.Regex.IsMatch(cleanRut, @"^\d{7,8}[0-9kK]$");
                if (!looksLikeRutBody && !looksLikeRutWithDv)
                {
                    req.Pasaporte = req.Rut;
                    req.Rut = "";
                }
            }

            // Default confirmationText for deletion modes if empty (since Power Apps UI performs the validation)
            if (req.Mode != "Consultar" && string.IsNullOrEmpty(req.ConfirmationText))
            {
                req.ConfirmationText = "ELIMINAR";
            }

            if (string.IsNullOrEmpty(req.Mode))
            {
                return Results.BadRequest("Modo no especificado.");
            }

            return ExecuteBatch(new List<SingleRequest> { req }, req.Mode, req.ConfirmationText, httpContext, baseSettings, logService, backupService, factory);
        }).WithName("ExecuteSingle").Produces<ExecutionResponse>(200);

        app.MapPost("/api/execute-batch", (BatchRequest req, HttpContext httpContext, AppSettings baseSettings, LogService logService, BackupService backupService, DataverseConnectionFactory factory) =>
        {
            var reqs = new List<SingleRequest>();
            if (req.Ruts != null)
            {
                foreach (var r in req.Ruts)
                {
                    if (string.IsNullOrWhiteSpace(r)) continue;
                    var tokens = r.Split(new[] { ';', ',', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    foreach (var t in tokens)
                    {
                        reqs.Add(new SingleRequest { Rut = t, Mode = req.Mode, ConfirmationText = req.ConfirmationText });
                    }
                }
            }
            if (req.Pasaportes != null)
            {
                foreach (var p in req.Pasaportes)
                {
                    if (string.IsNullOrWhiteSpace(p)) continue;
                    var tokens = p.Split(new[] { ';', ',', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    foreach (var t in tokens)
                    {
                        reqs.Add(new SingleRequest { Pasaporte = t, Mode = req.Mode, ConfirmationText = req.ConfirmationText });
                    }
                }
            }
            return ExecuteBatch(reqs, req.Mode, req.ConfirmationText, httpContext, baseSettings, logService, backupService, factory);
        }).WithName("ExecuteBatch").Produces<ExecutionResponse>(200);
        // Endpoints de Procesamiento Masivo (Azure)
        app.MapPost("/api/mass/create", (CreateMassLoteRequest req, HttpContext httpContext, AppSettings baseSettings, DataverseConnectionFactory factory) =>
        {
            var treatmentValidation = NormalizeMassTreatment(req.Tratamiento, req.ConfirmationText);
            if (!treatmentValidation.IsValid)
            {
                return Results.BadRequest(new { error = treatmentValidation.Error });
            }

            req.Tratamiento = treatmentValidation.Tratamiento;

            var expandedIds = new List<string>();
            if (req.Identificadores != null)
            {
                foreach (var rawItem in req.Identificadores)
                {
                    if (string.IsNullOrWhiteSpace(rawItem)) continue;
                    var tokens = rawItem.Split(new[] { ';', ',', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    foreach (var token in tokens)
                    {
                        var clean = token.Trim().Trim('"', '\'');
                        if (!string.IsNullOrWhiteSpace(clean))
                        {
                            expandedIds.Add(clean);
                        }
                    }
                }
            }

            if (expandedIds.Count == 0)
            {
                return Results.BadRequest(new { error = "La nómina está vacía." });
            }

            int maxBatchSize = app.Configuration.GetValue<int>("MassOrchestration:MaxBatchSize", 500);
            if (expandedIds.Count > maxBatchSize)
            {
                return Results.BadRequest(new { error = $"El lote supera el límite máximo de {maxBatchSize} registros." });
            }

            string reqEmail = httpContext.Request.Headers["X-MS-CLIENT-PRINCIPAL-NAME"].ToString();
            if (string.IsNullOrEmpty(reqEmail))
            {
                reqEmail = httpContext.User.Identity?.Name ?? "Unknown";
            }

            // Normalización y Deduplicación
            var validationResults = new List<InputValidator.ValidationResult>();
            var processedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var rawId in expandedIds)
            {
                var res = InputValidator.ValidateAndNormalize(rawId);
                string key = res.IsValid ? res.NormalizedValue : rawId.Replace(".", "").Replace("-", "").Replace(" ", "").Trim();

                if (processedKeys.Add(key))
                {
                    validationResults.Add(res);
                }
            }

            var headerId = Guid.NewGuid();
            using var client = factory.CreateClient(baseSettings.Dataverse);

            var header = new Entity("um_massexecution", headerId);
            header["um_name"] = $"Ejecución {req.Tratamiento} {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
            header["um_estado"] = new OptionSetValue(MassOptionSets.HeaderEstadoPendiente);
            header["um_tratamiento"] = new OptionSetValue(MassOptionSets.StringToTratamiento(req.Tratamiento));
            header["um_motivo"] = req.Motivo;
            header["um_totalregistros"] = validationResults.Count;
            header["um_invalidos"] = validationResults.Count(r => !r.IsValid).ToString();
            header["um_procesados"] = 0;
            header["um_exitosos"] = 0;
            header["um_noencontrados"] = 0;
            header["um_errores"] = "0";
            header["um_requiereconciliacion"] = 0;
            header["um_requestedbyemail"] = reqEmail;

            client.Create(header);

            // Crear detalles usando ExecuteMultiple para rendimiento
            var multipleRequest = new Microsoft.Xrm.Sdk.Messages.ExecuteMultipleRequest()
            {
                Settings = new ExecuteMultipleSettings()
                {
                    ContinueOnError = true,
                    ReturnResponses = true
                },
                Requests = new OrganizationRequestCollection()
            };

            foreach (var res in validationResults)
            {
                var detailId = Guid.NewGuid();
                var detail = new Entity("um_massexecutiondetail", detailId);
                detail["um_massexecutionid"] = new EntityReference("um_massexecution", headerId);
                detail["um_identificador"] = res.IsValid ? res.NormalizedValue : res.RawValue;
                detail["um_tipoidentificador"] = res.Type.ToString();
                detail["um_name"] = BuildMassDetailName(res.Type.ToString(), res.IsValid ? res.NormalizedValue : res.RawValue, res.IsValid ? "Pendiente" : "Invalido");

                if (res.IsValid)
                {
                    detail["um_estado"] = new OptionSetValue(MassOptionSets.DetailEstadoPendiente);
                }
                else
                {
                    detail["um_estado"] = new OptionSetValue(MassOptionSets.DetailEstadoInvalido);
                    detail["um_errormessage"] = res.ErrorMessage;
                    detail["um_resultado"] = JsonSerializer.Serialize(new
                    {
                        estado = "Invalido",
                        mensaje = res.ErrorMessage
                    });
                }

                multipleRequest.Requests.Add(new Microsoft.Xrm.Sdk.Messages.CreateRequest { Target = detail });
            }

            int batchLimit = 500;
            for (int i = 0; i < multipleRequest.Requests.Count; i += batchLimit)
            {
                var batchRequests = multipleRequest.Requests.Skip(i).Take(batchLimit).ToList();
                var subRequest = new Microsoft.Xrm.Sdk.Messages.ExecuteMultipleRequest()
                {
                    Settings = multipleRequest.Settings,
                    Requests = new OrganizationRequestCollection()
                };
                subRequest.Requests.AddRange(batchRequests);
                client.Execute(subRequest);
            }

            return Results.Accepted($"/api/mass/status/{headerId:N}", new CreateMassLoteResponse
            {
                ExecutionId = headerId.ToString("N"),
                TotalRegistros = validationResults.Count,
                RegistrosValidos = validationResults.Count(r => r.IsValid),
                RegistrosInvalidos = validationResults.Count(r => !r.IsValid)
            });
        }).WithName("CreateMassLote").Produces<CreateMassLoteResponse>(202);

        app.MapPost("/api/mass/upload", async (HttpContext httpContext, AppSettings baseSettings, DataverseConnectionFactory factory, BlobStorageBackupService blobService) =>
        {
            if (!httpContext.Request.HasFormContentType)
            {
                return Results.BadRequest(new { error = "La solicitud debe enviarse como multipart/form-data con un archivo CSV o TXT." });
            }

            var form = await httpContext.Request.ReadFormAsync();
            var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
            if (file == null || file.Length == 0)
            {
                return Results.BadRequest(new { error = "Debe adjuntar un archivo CSV o TXT con la nomina." });
            }

            string extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (extension != ".csv" && extension != ".txt")
            {
                return Results.BadRequest(new { error = "Solo se permiten archivos .csv o .txt." });
            }

            string tratamiento = form["tratamiento"].ToString();
            string motivo = form["motivo"].ToString();
            string confirmationText = form["confirmationText"].ToString();
            var treatmentValidation = NormalizeMassTreatment(tratamiento, confirmationText);
            if (!treatmentValidation.IsValid)
            {
                return Results.BadRequest(new { error = treatmentValidation.Error });
            }

            tratamiento = treatmentValidation.Tratamiento;

            if (string.IsNullOrWhiteSpace(motivo))
            {
                return Results.BadRequest(new { error = "El motivo del proceso masivo es obligatorio." });
            }

            byte[] bytes;
            using (var ms = new MemoryStream())
            {
                await file.CopyToAsync(ms);
                bytes = ms.ToArray();
            }

            string content = DecodeUploadedText(bytes);
            var identifiers = ParseMassIdentifierFile(content).ToList();
            int maxUploadRows = app.Configuration.GetValue<int>("MassOrchestration:MaxUploadRows", 50000);
            int partitionSize = app.Configuration.GetValue<int>("MassOrchestration:PartitionSize", 500);

            string reqEmail = httpContext.Request.Headers["X-MS-CLIENT-PRINCIPAL-NAME"].ToString();
            if (string.IsNullOrEmpty(reqEmail))
            {
                reqEmail = httpContext.User.Identity?.Name ?? "Unknown";
            }

            var createResult = CreateMassLoteFromIdentifiers(
                identifiers,
                tratamiento,
                motivo,
                reqEmail,
                maxUploadRows,
                partitionSize,
                baseSettings,
                factory,
                file.FileName,
                identifiers.Count);

            if (createResult is IValueHttpResult valueResult && valueResult.Value is CreateMassLoteResponse created)
            {
                var sourceMeta = await blobService.UploadSourceFileAsync(created.ExecutionId, file.FileName, bytes);
                created.SourceBlobReference = sourceMeta.BlobReference;
                created.SourceFileHash = sourceMeta.Hash;
                return Results.Accepted($"/api/mass/status/{created.ExecutionId}", created);
            }

            return createResult;
        }).WithName("UploadMassLoteFile").DisableAntiforgery().Produces<CreateMassLoteResponse>(202);

        app.MapPost("/api/mass/start/{executionId}", async (string executionId, Azure.Storage.Queues.QueueClient queueClient, AppSettings baseSettings, DataverseConnectionFactory factory) =>
        {
            if (!Guid.TryParse(executionId, out Guid headerId))
            {
                return Results.BadRequest("El ID de ejecuciÃ³n provisto no es un GUID vÃ¡lido.");
            }

            using var client = factory.CreateClient(baseSettings.Dataverse);
            try
            {
                var header = client.Retrieve("um_massexecution", headerId, new ColumnSet("um_massexecutionid", "um_estado"));
                if (header == null)
                {
                    return Results.NotFound($"No se encontrÃ³ ningÃºn lote con ID: {executionId}");
                }

                int currentStatus = ((OptionSetValue)header["um_estado"]).Value;
                if (currentStatus != MassOptionSets.HeaderEstadoPendiente && currentStatus != MassOptionSets.HeaderEstadoEnProceso)
                {
                    return Results.BadRequest($"El lote no se puede iniciar porque su estado actual es {MassOptionSets.HeaderEstadoToString(currentStatus)}.");
                }

                await queueClient.CreateIfNotExistsAsync();
                int partitionSize = Math.Max(1, app.Configuration.GetValue<int>("MassOrchestration:PartitionSize", 25));
                var pendingDetailIds = RetrievePendingMassDetailIds(client, headerId);
                int enqueuedPartitions = 0;

                if (pendingDetailIds.Count == 0)
                {
                    var emptyCheckMessage = JsonSerializer.Serialize(new { executionId = headerId.ToString("N") });
                    await queueClient.SendMessageAsync(Convert.ToBase64String(Encoding.UTF8.GetBytes(emptyCheckMessage)));
                }

                foreach (var chunk in pendingDetailIds.Chunk(partitionSize))
                {
                    enqueuedPartitions++;
                    var messageText = JsonSerializer.Serialize(new
                    {
                        executionId = headerId.ToString("N"),
                        partitionNumber = enqueuedPartitions,
                        partitionSize,
                        detailIds = chunk.Select(id => id.ToString("N")).ToList()
                    });
                    var messageBytes = Encoding.UTF8.GetBytes(messageText);
                    var base64Message = Convert.ToBase64String(messageBytes);
                    await queueClient.SendMessageAsync(base64Message);
                }

                return Results.Ok(new
                {
                    message = "Procesamiento en cola iniciado.",
                    executionId = headerId.ToString("N"),
                    partitionSize,
                    enqueuedPartitions,
                    pendingRecords = pendingDetailIds.Count
                });
            }
            catch (Exception ex)
            {
                return Results.Problem($"Error al iniciar el lote masivo: {ex.Message}");
            }
        }).WithName("StartMassLote").Produces<object>(200);

        app.MapPost("/api/mass/recover-stuck/{executionId}", (string executionId, AppSettings baseSettings, DataverseConnectionFactory factory) =>
        {
            if (!Guid.TryParse(executionId, out Guid headerId))
            {
                return Results.BadRequest("El ID de ejecucion provisto no es un GUID valido.");
            }

            using var client = factory.CreateClient(baseSettings.Dataverse);
            var query = new QueryExpression("um_massexecutiondetail")
            {
                ColumnSet = new ColumnSet("um_massexecutiondetailid", "um_estado", "um_resultado", "um_errormessage"),
                PageInfo = new PagingInfo
                {
                    PageNumber = 1,
                    Count = 5000,
                    ReturnTotalRecordCount = false
                }
            };
            query.Criteria.AddCondition("um_massexecutionid", ConditionOperator.Equal, headerId);

            var recoverableFilter = new FilterExpression(LogicalOperator.Or);
            recoverableFilter.AddCondition("um_estado", ConditionOperator.Equal, MassOptionSets.DetailEstadoEnProceso);

            var workerErrorFilter = new FilterExpression(LogicalOperator.And);
            workerErrorFilter.AddCondition("um_estado", ConditionOperator.Equal, MassOptionSets.DetailEstadoError);
            workerErrorFilter.AddCondition("um_errormessage", ConditionOperator.Like, "Error no controlado del worker:%");
            recoverableFilter.AddFilter(workerErrorFilter);
            query.Criteria.AddFilter(recoverableFilter);

            int recovered = 0;
            while (true)
            {
                var results = client.RetrieveMultiple(query);
                foreach (var stuck in results.Entities)
                {
                    var update = new Entity("um_massexecutiondetail", stuck.Id);
                    update["um_estado"] = new OptionSetValue(MassOptionSets.DetailEstadoPendiente);
                    update["um_errormessage"] = null;
                    update["um_workerleaseid"] = null;
                    update["um_leaseduntil"] = null;
                    client.Update(update);
                    recovered++;
                }

                if (!results.MoreRecords) break;
                query.PageInfo.PageNumber++;
                query.PageInfo.PagingCookie = results.PagingCookie;
            }

            return Results.Ok(new
            {
                executionId = headerId.ToString("N"),
                recovered,
                message = recovered == 0
                    ? "No habia detalles recuperables."
                    : "Detalles recuperables devueltos a Pendiente. Ejecute /api/mass/start para reencolar."
            });
        }).WithName("RecoverStuckMassLote").Produces<object>(200);

        app.MapPost("/api/mass/backfill-names/{executionId}", (string executionId, AppSettings baseSettings, DataverseConnectionFactory factory) =>
        {
            if (!Guid.TryParse(executionId, out Guid headerId))
            {
                return Results.BadRequest("El ID de ejecucion provisto no es un GUID valido.");
            }

            using var client = factory.CreateClient(baseSettings.Dataverse);
            var query = new QueryExpression("um_massexecutiondetail")
            {
                ColumnSet = new ColumnSet("um_massexecutiondetailid", "um_name", "um_identificador", "um_tipoidentificador", "um_estado"),
                PageInfo = new PagingInfo
                {
                    PageNumber = 1,
                    Count = 5000,
                    ReturnTotalRecordCount = false
                }
            };
            query.Criteria.AddCondition("um_massexecutionid", ConditionOperator.Equal, headerId);

            int updated = 0;
            while (true)
            {
                var results = client.RetrieveMultiple(query);
                foreach (var detail in results.Entities)
                {
                    string currentName = detail.Contains("um_name") ? detail["um_name"]?.ToString() ?? "" : "";
                    string identifier = detail.Contains("um_identificador") ? detail["um_identificador"]?.ToString() ?? "" : "";
                    string type = detail.Contains("um_tipoidentificador") ? detail["um_tipoidentificador"]?.ToString() ?? "" : "";
                    string state = detail.Contains("um_estado") ? MassOptionSets.DetailEstadoToString(((OptionSetValue)detail["um_estado"]).Value) : "";
                    string targetName = BuildMassDetailName(type, identifier, state);

                    if (!string.Equals(currentName, targetName, StringComparison.Ordinal))
                    {
                        var update = new Entity("um_massexecutiondetail", detail.Id);
                        update["um_name"] = targetName;
                        client.Update(update);
                        updated++;
                    }
                }

                if (!results.MoreRecords) break;
                query.PageInfo.PageNumber++;
                query.PageInfo.PagingCookie = results.PagingCookie;
            }

            return Results.Ok(new { executionId = headerId.ToString("N"), updated });
        }).WithName("BackfillMassDetailNames").Produces<object>(200);

        app.MapGet("/api/mass/status/{executionId}", (string executionId, AppSettings baseSettings, DataverseConnectionFactory factory) =>
        {
            if (!Guid.TryParse(executionId, out Guid headerId))
            {
                return Results.BadRequest("El ID de ejecuciÃ³n provisto no es un GUID vÃ¡lido.");
            }

            using var client = factory.CreateClient(baseSettings.Dataverse);
            try
            {
                var header = client.Retrieve("um_massexecution", headerId, new ColumnSet(
                    "um_massexecutionid", "um_tratamiento", "um_motivo", "um_estado",
                    "um_totalregistros", "um_procesados", "um_exitosos", "um_noencontrados",
                    "um_errores", "um_invalidos", "um_requiereconciliacion", "um_inicio", "um_termino", "um_requestedbyemail", "createdon"
                ));

                if (header == null)
                {
                    return Results.NotFound($"No se encontrÃ³ ningÃºn lote con ID: {executionId}");
                }

                var res = new MassLoteStatusResponse
                {
                    ExecutionId = header.Id.ToString("N"),
                    Tratamiento = header.Contains("um_tratamiento") ? MassOptionSets.TratamientoToString(((OptionSetValue)header["um_tratamiento"]).Value) : "Desconocido",
                    Motivo = header.Contains("um_motivo") ? header["um_motivo"].ToString() ?? "" : "",
                    Estado = header.Contains("um_estado") ? MassOptionSets.HeaderEstadoToString(((OptionSetValue)header["um_estado"]).Value) : "Desconocido",
                    TotalRegistros = ReadIntAttribute(header, "um_totalregistros"),
                    Procesados = ReadIntAttribute(header, "um_procesados"),
                    Exitosos = ReadIntAttribute(header, "um_exitosos"),
                    NoEncontrados = ReadIntAttribute(header, "um_noencontrados"),
                    Errores = ReadIntAttribute(header, "um_errores"),
                    Invalidos = ReadIntAttribute(header, "um_invalidos"),
                    RequiereConciliacion = ReadIntAttribute(header, "um_requiereconciliacion"),
                    Inicio = header.Contains("um_inicio") ? ((DateTime)header["um_inicio"]).ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") : null,
                    Termino = header.Contains("um_termino") ? ((DateTime)header["um_termino"]).ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") : null,
                    CreadoEl = header.Contains("createdon") ? ((DateTime)header["createdon"]).ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") : "",
                    SolicitadoPor = header.Contains("um_requestedbyemail") ? header["um_requestedbyemail"].ToString() ?? "" : ""
                };

                return Results.Ok(res);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Error al consultar el estado del lote masivo: {ex.Message}");
            }
        }).WithName("GetMassLoteStatus").Produces<MassLoteStatusResponse>(200);

        app.MapGet("/api/mass/list", (int? top, AppSettings baseSettings, DataverseConnectionFactory factory) =>
        {
            using var client = factory.CreateClient(baseSettings.Dataverse);
            int take = Math.Clamp(top ?? 50, 1, 200);

            var query = new QueryExpression("um_massexecution")
            {
                ColumnSet = new ColumnSet(
                    "um_massexecutionid", "um_name", "um_tratamiento", "um_estado",
                    "um_totalregistros", "um_procesados", "um_exitosos", "um_noencontrados",
                    "um_errores", "um_invalidos", "um_requiereconciliacion", "um_requestedbyemail", "createdon"
                ),
                TopCount = take
            };
            query.Orders.Add(new OrderExpression("createdon", OrderType.Descending));

            var results = client.RetrieveMultiple(query);
            var items = results.Entities.Select(header => new MassLoteListItem
            {
                ExecutionId = header.Id.ToString("N"),
                Nombre = header.Contains("um_name") ? header["um_name"].ToString() ?? "" : "",
                Tratamiento = header.Contains("um_tratamiento") ? MassOptionSets.TratamientoToString(((OptionSetValue)header["um_tratamiento"]).Value) : "Desconocido",
                Estado = header.Contains("um_estado") ? MassOptionSets.HeaderEstadoToString(((OptionSetValue)header["um_estado"]).Value) : "Desconocido",
                TotalRegistros = ReadIntAttribute(header, "um_totalregistros"),
                Procesados = ReadIntAttribute(header, "um_procesados"),
                Exitosos = ReadIntAttribute(header, "um_exitosos"),
                NoEncontrados = ReadIntAttribute(header, "um_noencontrados"),
                Errores = ReadIntAttribute(header, "um_errores"),
                Invalidos = ReadIntAttribute(header, "um_invalidos"),
                RequiereConciliacion = ReadIntAttribute(header, "um_requiereconciliacion"),
                CreadoEl = header.Contains("createdon") ? ((DateTime)header["createdon"]).ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") : "",
                SolicitadoPor = header.Contains("um_requestedbyemail") ? header["um_requestedbyemail"].ToString() ?? "" : ""
            }).ToList();

            return Results.Ok(items);
        }).WithName("ListMassLotes").Produces<List<MassLoteListItem>>(200);

        app.MapGet("/api/mass/details/{executionId}", (string executionId, string? status, int? page, int? pageSize, AppSettings baseSettings, DataverseConnectionFactory factory) =>
        {
            if (!Guid.TryParse(executionId, out Guid headerId))
            {
                return Results.BadRequest("El ID de ejecuciÃ³n provisto no es un GUID vÃ¡lido.");
            }

            using var client = factory.CreateClient(baseSettings.Dataverse);
            try
            {
                bool pagedRequest = page.HasValue || pageSize.HasValue;
                int effectivePage = Math.Max(1, page ?? 1);
                int effectivePageSize = Math.Clamp(pageSize ?? 200, 1, 500);

                var query = new QueryExpression("um_massexecutiondetail")
                {
                    ColumnSet = new ColumnSet(
                        "um_massexecutiondetailid", "um_identificador", "um_tipoidentificador", 
                        "um_estado", "um_resultado", "um_errormessage", "um_backupreference",
                        "um_backupdate", "um_backupsize", "um_backuphash"
                    )
                };

                query.Criteria.AddCondition("um_massexecutionid", ConditionOperator.Equal, headerId);
                query.Orders.Add(new OrderExpression("createdon", OrderType.Ascending));
                if (pagedRequest)
                {
                    query.PageInfo = new PagingInfo
                    {
                        PageNumber = effectivePage,
                        Count = effectivePageSize,
                        ReturnTotalRecordCount = false
                    };
                }
                else
                {
                    query.TopCount = 2000;
                }

                if (!string.IsNullOrWhiteSpace(status))
                {
                    int statusInt = status.ToLower() switch
                    {
                        "pendiente" => MassOptionSets.DetailEstadoPendiente,
                        "enproceso" => MassOptionSets.DetailEstadoEnProceso,
                        "consultado" => MassOptionSets.DetailEstadoConsultado,
                        "eliminado" => MassOptionSets.DetailEstadoEliminado,
                        "noencontrado" => MassOptionSets.DetailEstadoNoEncontrado,
                        "error" => MassOptionSets.DetailEstadoError,
                        "invalido" => MassOptionSets.DetailEstadoInvalido,
                        "requiereconciliacion" => MassOptionSets.DetailEstadoRequiereConciliacion,
                        _ => -1
                    };
                    if (statusInt != -1)
                    {
                        query.Criteria.AddCondition("um_estado", ConditionOperator.Equal, statusInt);
                    }
                }

                var results = client.RetrieveMultiple(query);
                var items = new List<MassLoteDetailItem>();

                foreach (var detail in results.Entities)
                {
                    items.Add(new MassLoteDetailItem
                    {
                        DetailId = detail.Id.ToString("N"),
                        Identificador = detail.Contains("um_identificador") ? detail["um_identificador"].ToString() ?? "" : "",
                        TipoIdentificador = detail.Contains("um_tipoidentificador") ? detail["um_tipoidentificador"].ToString() ?? "" : "",
                        Estado = detail.Contains("um_estado") ? MassOptionSets.DetailEstadoToString(((OptionSetValue)detail["um_estado"]).Value) : "Desconocido",
                        Resultado = detail.Contains("um_resultado") ? detail["um_resultado"].ToString() : null,
                        ErrorMessage = detail.Contains("um_errormessage") ? detail["um_errormessage"].ToString() : null,
                        BackupReference = detail.Contains("um_backupreference") ? detail["um_backupreference"].ToString() : null,
                        BackupDate = detail.Contains("um_backupdate") ? ((DateTime)detail["um_backupdate"]).ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") : null,
                        BackupSize = detail.Contains("um_backupsize") ? (int)detail["um_backupsize"] : null,
                        BackupHash = detail.Contains("um_backuphash") ? detail["um_backuphash"].ToString() : null
                    });
                }

                if (pagedRequest)
                {
                    return Results.Ok(new PagedMassLoteDetailsResponse
                    {
                        ExecutionId = headerId.ToString("N"),
                        Page = effectivePage,
                        PageSize = effectivePageSize,
                        HasMore = results.MoreRecords,
                        Items = items
                    });
                }

                return Results.Ok(items);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Error al consultar los detalles del lote masivo: {ex.Message}");
            }
        }).WithName("GetMassLoteDetails").Produces<List<MassLoteDetailItem>>(200);

        app.MapGet("/api/mass/backup/download", async (string blobReference, BlobStorageBackupService blobService) =>
        {
            if (string.IsNullOrWhiteSpace(blobReference))
            {
                return Results.BadRequest("El parÃ¡metro blobReference es obligatorio.");
            }

            try
            {
                string jsonContent = await blobService.DownloadBackupAsync(blobReference);
                var fileName = Path.GetFileName(blobReference);
                var bytes = Encoding.UTF8.GetBytes(jsonContent);
                return Results.File(bytes, "application/json", fileName);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Error al descargar el respaldo: {ex.Message}");
            }
        }).WithName("DownloadMassBackup").Produces(200);
        app.MapPost("/api/diagnostics/update-audit-metadata", (HttpContext httpContext, AppSettings baseSettings, DataverseConnectionFactory factory) =>
        {
            try
            {
                using var client = factory.CreateClient(baseSettings.Dataverse);
                var attributesToFetch = new string[] {
                    "um_errormessage",
                    "um_requestjson",
                    "um_responsejson",
                    "um_prematrixjson",
                    "um_postmatrixjson"
                };

                foreach (var attrName in attributesToFetch)
                {
                    var reqTrue = new Microsoft.Xrm.Sdk.Messages.RetrieveAttributeRequest
                    {
                        EntityLogicalName = "um_privacyoperationlog",
                        LogicalName = attrName,
                        RetrieveAsIfPublished = true
                    };
                    
                    var resTrue = (Microsoft.Xrm.Sdk.Messages.RetrieveAttributeResponse)client.Execute(reqTrue);
                    var metaTrue = resTrue.AttributeMetadata;
                    
                    if (metaTrue is Microsoft.Xrm.Sdk.Metadata.StringAttributeMetadata stringAttr)
                    {
                        stringAttr.MaxLength = 4000;
                        stringAttr.Format = Microsoft.Xrm.Sdk.Metadata.StringFormat.TextArea;
                        stringAttr.RequiredLevel = new Microsoft.Xrm.Sdk.Metadata.AttributeRequiredLevelManagedProperty(Microsoft.Xrm.Sdk.Metadata.AttributeRequiredLevel.None);
                        
                        var updateReq = new Microsoft.Xrm.Sdk.Messages.UpdateAttributeRequest
                        {
                            EntityName = "um_privacyoperationlog",
                            Attribute = stringAttr,
                            MergeLabels = false
                        };
                        client.Execute(updateReq);
                    }
                    else if (metaTrue is Microsoft.Xrm.Sdk.Metadata.MemoAttributeMetadata memoAttr)
                    {
                        memoAttr.MaxLength = 4000;
                        memoAttr.Format = Microsoft.Xrm.Sdk.Metadata.StringFormat.TextArea;
                        memoAttr.RequiredLevel = new Microsoft.Xrm.Sdk.Metadata.AttributeRequiredLevelManagedProperty(Microsoft.Xrm.Sdk.Metadata.AttributeRequiredLevel.None);
                        
                        var updateReq = new Microsoft.Xrm.Sdk.Messages.UpdateAttributeRequest
                        {
                            EntityName = "um_privacyoperationlog",
                            Attribute = memoAttr,
                            MergeLabels = false
                        };
                        client.Execute(updateReq);
                    }
                }

                var publishReq = new Microsoft.Crm.Sdk.Messages.PublishXmlRequest
                {
                    ParameterXml = "<importexportxml><entities><entity>um_privacyoperationlog</entity></entities></importexportxml>"
                };
                client.Execute(publishReq);
                System.Threading.Thread.Sleep(3000);

                var resultsList = new List<object>();
                foreach (var attrName in attributesToFetch)
                {
                    var reqFalse2 = new Microsoft.Xrm.Sdk.Messages.RetrieveAttributeRequest
                    {
                        EntityLogicalName = "um_privacyoperationlog",
                        LogicalName = attrName,
                        RetrieveAsIfPublished = false
                    };
                    var reqTrue2 = new Microsoft.Xrm.Sdk.Messages.RetrieveAttributeRequest
                    {
                        EntityLogicalName = "um_privacyoperationlog",
                        LogicalName = attrName,
                        RetrieveAsIfPublished = true
                    };

                    Microsoft.Xrm.Sdk.Metadata.AttributeMetadata? metaFalse2 = null;
                    Microsoft.Xrm.Sdk.Metadata.AttributeMetadata? metaTrue2 = null;

                    try { metaFalse2 = ((Microsoft.Xrm.Sdk.Messages.RetrieveAttributeResponse)client.Execute(reqFalse2)).AttributeMetadata; } catch { }
                    try { metaTrue2 = ((Microsoft.Xrm.Sdk.Messages.RetrieveAttributeResponse)client.Execute(reqTrue2)).AttributeMetadata; } catch { }

                    if (metaTrue2 != null)
                    {
                        int? maxLTrue2 = (metaTrue2 as Microsoft.Xrm.Sdk.Metadata.StringAttributeMetadata)?.MaxLength ?? (metaTrue2 as Microsoft.Xrm.Sdk.Metadata.MemoAttributeMetadata)?.MaxLength;
                        int? maxLFalse2 = (metaFalse2 as Microsoft.Xrm.Sdk.Metadata.StringAttributeMetadata)?.MaxLength ?? (metaFalse2 as Microsoft.Xrm.Sdk.Metadata.MemoAttributeMetadata)?.MaxLength;
                        
                        string? format = (metaTrue2 as Microsoft.Xrm.Sdk.Metadata.StringAttributeMetadata)?.Format?.ToString() 
                            ?? (metaTrue2 as Microsoft.Xrm.Sdk.Metadata.MemoAttributeMetadata)?.Format?.ToString();

                        resultsList.Add(new
                        {
                            logicalName = metaTrue2.LogicalName,
                            attributeType = metaTrue2.AttributeType?.ToString(),
                            format = format,
                            maxLength = maxLTrue2,
                            databaseLength = maxLTrue2.HasValue ? maxLTrue2.Value * 2 : (int?)null,
                            modifiedOn = "N/A",
                            retrieveAsIfPublishedFalseMaxLength = maxLFalse2,
                            retrieveAsIfPublishedTrueMaxLength = maxLTrue2
                        });
                    }
                }
                return Results.Ok(new { message = "Update applied and published.", results = resultsList });
            }
            catch (Exception ex)
            {
                return Results.Problem(ex.Message);
            }
        }).WithName("UpdateAuditMetadata").Produces<object>(200);

        app.MapGet("/api/diagnostics/audit-full-columns", (HttpContext httpContext, AppSettings baseSettings, DataverseConnectionFactory factory) =>
        {
            try
            {
                using var client = factory.CreateClient(baseSettings.Dataverse);
                var attributesToFetch = new string[] {
                    "um_responsejsonfull",
                    "um_requestjsonfull",
                    "um_prematrixjsonfull",
                    "um_postmatrixjsonfull",
                    "um_errormessagefull"
                };

                var resultsList = new List<object>();

                foreach (var attrName in attributesToFetch)
                {
                    var reqTrue = new Microsoft.Xrm.Sdk.Messages.RetrieveAttributeRequest
                    {
                        EntityLogicalName = "um_privacyoperationlog",
                        LogicalName = attrName,
                        RetrieveAsIfPublished = true
                    };

                    Microsoft.Xrm.Sdk.Metadata.AttributeMetadata? metaTrue = null;
                    try { metaTrue = ((Microsoft.Xrm.Sdk.Messages.RetrieveAttributeResponse)client.Execute(reqTrue)).AttributeMetadata; } catch { }

                    if (metaTrue != null)
                    {
                        int? maxLTrue = (metaTrue as Microsoft.Xrm.Sdk.Metadata.StringAttributeMetadata)?.MaxLength ?? (metaTrue as Microsoft.Xrm.Sdk.Metadata.MemoAttributeMetadata)?.MaxLength;
                        string? format = (metaTrue as Microsoft.Xrm.Sdk.Metadata.StringAttributeMetadata)?.Format?.ToString() 
                            ?? (metaTrue as Microsoft.Xrm.Sdk.Metadata.MemoAttributeMetadata)?.Format?.ToString();

                        resultsList.Add(new
                        {
                            logicalName = metaTrue.LogicalName,
                            attributeType = metaTrue.AttributeType?.ToString(),
                            maxLength = maxLTrue,
                            format = format
                        });
                    }
                    else
                    {
                        resultsList.Add(new
                        {
                            logicalName = attrName,
                            attributeType = "NOT FOUND",
                            maxLength = (int?)null,
                            format = "NOT FOUND"
                        });
                    }
                }
                return Results.Ok(resultsList);
            }
            catch (Exception ex)
            {
                return Results.Problem(ex.Message);
            }
        }).WithName("GetAuditFullColumns").Produces<List<object>>(200);

        app.MapPost("/api/diagnostics/test-audit-full-json", (HttpContext httpContext, AppSettings baseSettings, DataverseConnectionFactory factory) =>
        {
            try
            {
                using var client = factory.CreateClient(baseSettings.Dataverse);
                var auditSvc = new PrivacyOperationLogService(client, baseSettings);
                
                string hugeJson = new string('x', 5000);
                
                var report = auditSvc.LogOperation(
                    executionId: Guid.NewGuid().ToString(),
                    mode: "Consultar",
                    status: "Consultado",
                    rutIngresado: "11111111",
                    pasaporte: null,
                    rutNormalizado: "11111111",
                    dv: "1",
                    rutCompleto: "11111111-1",
                    contactIdText: null,
                    contactFullname: "Prueba Full JSON",
                    contactDeleted: false,
                    requestedByName: "Test",
                    requestedByEmail: "test@test.com",
                    confirmationProvided: false,
                    totalFoundBeforeDelete: 0,
                    totalDeleted: 0,
                    totalErrors: 0,
                    backupCreated: false,
                    backupFileName: null,
                    startedAt: DateTime.UtcNow,
                    finishedAt: DateTime.UtcNow,
                    errorMessage: null,
                    requestPayload: new { msg = "test" },
                    responsePayload: new { data = hugeJson },
                    preMatrix: null,
                    postMatrix: null
                );
                
                return Results.Ok(report);
            }
            catch (Exception ex)
            {
                return Results.Problem(ex.Message);
            }
        }).WithName("TestAuditFullJson").Produces<AuditDiagnosticReport>(200);

        app.MapGet("/api/diagnostics/audit-metadata", (HttpContext httpContext, AppSettings baseSettings, DataverseConnectionFactory factory) =>
        {
            try
            {
                using var client = factory.CreateClient(baseSettings.Dataverse);
                var attributesToFetch = new string[] {
                    "um_errormessage",
                    "um_requestjson",
                    "um_responsejson",
                    "um_prematrixjson",
                    "um_postmatrixjson"
                };

                var resultsList = new List<object>();

                foreach (var attrName in attributesToFetch)
                {
                    var reqFalse = new Microsoft.Xrm.Sdk.Messages.RetrieveAttributeRequest
                    {
                        EntityLogicalName = "um_privacyoperationlog",
                        LogicalName = attrName,
                        RetrieveAsIfPublished = false
                    };
                    var reqTrue = new Microsoft.Xrm.Sdk.Messages.RetrieveAttributeRequest
                    {
                        EntityLogicalName = "um_privacyoperationlog",
                        LogicalName = attrName,
                        RetrieveAsIfPublished = true
                    };

                    Microsoft.Xrm.Sdk.Metadata.AttributeMetadata? metaFalse = null;
                    Microsoft.Xrm.Sdk.Metadata.AttributeMetadata? metaTrue = null;

                    try { metaFalse = ((Microsoft.Xrm.Sdk.Messages.RetrieveAttributeResponse)client.Execute(reqFalse)).AttributeMetadata; } catch { }
                    try { metaTrue = ((Microsoft.Xrm.Sdk.Messages.RetrieveAttributeResponse)client.Execute(reqTrue)).AttributeMetadata; } catch { }

                    if (metaTrue != null)
                    {
                        int? maxLTrue = (metaTrue as Microsoft.Xrm.Sdk.Metadata.StringAttributeMetadata)?.MaxLength ?? (metaTrue as Microsoft.Xrm.Sdk.Metadata.MemoAttributeMetadata)?.MaxLength;
                        int? maxLFalse = (metaFalse as Microsoft.Xrm.Sdk.Metadata.StringAttributeMetadata)?.MaxLength ?? (metaFalse as Microsoft.Xrm.Sdk.Metadata.MemoAttributeMetadata)?.MaxLength;
                        
                        string? format = (metaTrue as Microsoft.Xrm.Sdk.Metadata.StringAttributeMetadata)?.Format?.ToString() 
                            ?? (metaTrue as Microsoft.Xrm.Sdk.Metadata.MemoAttributeMetadata)?.Format?.ToString();

                        resultsList.Add(new
                        {
                            logicalName = metaTrue.LogicalName,
                            attributeType = metaTrue.AttributeType?.ToString(),
                            format = format,
                            maxLength = maxLTrue,
                            databaseLength = maxLTrue,
                            modifiedOn = "N/A", // Not exposed directly via SDK metadata object usually
                            retrieveAsIfPublishedFalseMaxLength = maxLFalse,
                            retrieveAsIfPublishedTrueMaxLength = maxLTrue
                        });
                    }
                }
                return Results.Ok(resultsList);
            }
            catch (Exception ex)
            {
                return Results.Problem(ex.Message);
            }
        }).WithName("GetAuditMetadata").Produces<List<object>>(200);

        app.MapGet("/api/diagnostics/audit-last", (HttpContext httpContext, AppSettings baseSettings, DataverseConnectionFactory factory) =>
        {
            try
            {
                using var client = factory.CreateClient(baseSettings.Dataverse);
                var query = new Microsoft.Xrm.Sdk.Query.QueryExpression("um_privacyoperationlog")
                {
                    ColumnSet = new Microsoft.Xrm.Sdk.Query.ColumnSet(
                        "um_privacyoperationlogid",
                        "um_executionid",
                        "um_rutingresado",
                        "um_rutnormalizado",
                        "um_contactfullname",
                        "um_operationtype",
                        "um_operationstatus",
                        "um_source",
                        "createdon",
                        "ownerid"
                    ),
                    TopCount = 10
                };
                query.AddOrder("createdon", Microsoft.Xrm.Sdk.Query.OrderType.Descending);

                var retrieved = client.RetrieveMultiple(query);
                var list = new List<object>();
                foreach (var entity in retrieved.Entities)
                {
                    var opType = entity.Contains("um_operationtype") ? ((OptionSetValue)entity["um_operationtype"]).Value : (int?)null;
                    var opStatus = entity.Contains("um_operationstatus") ? ((OptionSetValue)entity["um_operationstatus"]).Value : (int?)null;
                    var source = entity.Contains("um_source") ? ((OptionSetValue)entity["um_source"]).Value : (int?)null;
                    var ownerId = entity.Contains("ownerid") ? ((EntityReference)entity["ownerid"]).Id : (Guid?)null;

                    list.Add(new
                    {
                        um_privacyoperationlogid = entity.Id,
                        um_executionid = entity.Contains("um_executionid") ? entity["um_executionid"]?.ToString() : null,
                        um_rutingresado = entity.Contains("um_rutingresado") ? entity["um_rutingresado"]?.ToString() : null,
                        um_rutnormalizado = entity.Contains("um_rutnormalizado") ? entity["um_rutnormalizado"]?.ToString() : null,
                        um_contactfullname = entity.Contains("um_contactfullname") ? entity["um_contactfullname"]?.ToString() : null,
                        um_operationtype = opType,
                        um_operationstatus = opStatus,
                        um_source = source,
                        createdon = entity.Contains("createdon") ? entity["createdon"] : null,
                        ownerid = ownerId
                    });
                }
                return Results.Ok(list);
            }
            catch (Exception ex)
            {
                return Results.Problem(ex.Message);
            }
        }).WithName("GetAuditLast").Produces<List<object>>(200);

        app.MapGet("/api/diagnostics/audit/{executionId}", (string executionId, HttpContext httpContext, AppSettings baseSettings, DataverseConnectionFactory factory) =>
        {
            try
            {
                using var client = factory.CreateClient(baseSettings.Dataverse);
                var query = new Microsoft.Xrm.Sdk.Query.QueryExpression("um_privacyoperationlog")
                {
                    ColumnSet = new Microsoft.Xrm.Sdk.Query.ColumnSet(
                        "um_privacyoperationlogid",
                        "um_executionid",
                        "um_rutingresado",
                        "um_rutnormalizado",
                        "um_contactfullname",
                        "um_operationtype",
                        "um_operationstatus",
                        "um_source",
                        "createdon",
                        "ownerid",
                        "um_requestjson",
                        "um_responsejson"
                    )
                };
                query.Criteria.AddCondition("um_executionid", Microsoft.Xrm.Sdk.Query.ConditionOperator.Equal, executionId);

                var retrieved = client.RetrieveMultiple(query);
                var list = new List<object>();
                foreach (var entity in retrieved.Entities)
                {
                    var opType = entity.Contains("um_operationtype") ? ((OptionSetValue)entity["um_operationtype"]).Value : (int?)null;
                    var opStatus = entity.Contains("um_operationstatus") ? ((OptionSetValue)entity["um_operationstatus"]).Value : (int?)null;
                    var source = entity.Contains("um_source") ? ((OptionSetValue)entity["um_source"]).Value : (int?)null;
                    var ownerId = entity.Contains("ownerid") ? ((EntityReference)entity["ownerid"]).Id : (Guid?)null;

                    list.Add(new
                    {
                        recordId = entity.Id,
                        createdon = entity.Contains("createdon") ? entity["createdon"] : null,
                        ownerid = ownerId,
                        operationtype = opType,
                        operationstatus = opStatus,
                        source = source,
                        requestjson = entity.Contains("um_requestjson") ? entity["um_requestjson"]?.ToString() : null,
                        responsejson = entity.Contains("um_responsejson") ? entity["um_responsejson"]?.ToString() : null
                    });
                }
                return Results.Ok(new
                {
                    cantidadEncontrada = retrieved.Entities.Count,
                    registrosEncontrados = list
                });
            }
            catch (Exception ex)
            {
                return Results.Problem(ex.Message);
            }
        }).WithName("GetAuditByExecutionId").Produces<object>(200);

        app.MapGet("/api/diagnostics/build", (AppSettings baseSettings) =>
        {
            return Results.Ok(new
            {
                apiBuild = ApiBuild,
                environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "N/A",
                dataverseUrl = baseSettings.Dataverse.Url,
                assemblyVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(),
                startedAtUtc = DateTime.UtcNow.ToString("o"),
                machineName = Environment.MachineName,
                authEnabledExpected = true
            });
        }).WithName("GetDiagnosticsBuild").Produces<object>(200);

        Console.WriteLine("========================================");
        Console.WriteLine("UMayor Dynamics Delete POC - Web API");
        Console.WriteLine($"Ambiente: {settings.Dataverse.Url}");
        Console.WriteLine("Escuchando en http://localhost:5000");
        Console.WriteLine("Abre la URL base en tu navegador para ver la Interfaz.");
        Console.WriteLine("========================================");

        app.Run();
    }

    public static string NormalizeRut(string rawRut)
    {
        if (string.IsNullOrWhiteSpace(rawRut)) return string.Empty;
        
        string clean = rawRut.Replace(".", "").Replace("-", "").Replace(" ", "").Trim();
        if (clean.Length == 0) return string.Empty;

        // Strip last character if it is a DV (digit or K/k)
        if (clean.EndsWith("k", StringComparison.OrdinalIgnoreCase))
        {
            return clean.Substring(0, clean.Length - 1);
        }
        
        // If clean consists of only digits:
        if (System.Text.RegularExpressions.Regex.IsMatch(clean, "^[0-9]+$"))
        {
            if (clean.Length == 9)
            {
                return clean.Substring(0, 8);
            }
            if (clean.Length == 8 && rawRut.Contains("-"))
            {
                return clean.Substring(0, 7);
            }
        }
        
        return clean;
    }

    private static (bool IsValid, string Tratamiento, string Error) NormalizeMassTreatment(string? tratamiento, string? confirmationText)
    {
        string normalized = (tratamiento ?? "").Trim();

        if (normalized.Equals("Consultar", StringComparison.OrdinalIgnoreCase))
        {
            return (true, "Consultar", "");
        }

        if (normalized.Equals("EliminarTodoMenosContacto", StringComparison.OrdinalIgnoreCase))
        {
            return HasDeleteConfirmation(confirmationText)
                ? (true, "EliminarTodoMenosContacto", "")
                : (false, "", "Para iniciar una eliminacion masiva debe confirmar escribiendo ELIMINAR.");
        }

        if (normalized.Equals("EliminarTodo", StringComparison.OrdinalIgnoreCase))
        {
            return HasDeleteConfirmation(confirmationText)
                ? (true, "EliminarTodo", "")
                : (false, "", "Para iniciar EliminarTodo masivo debe confirmar escribiendo ELIMINAR.");
        }

        return (false, "", "Tratamiento masivo invalido. Use Consultar, EliminarTodoMenosContacto o EliminarTodo.");
    }

    private static bool HasDeleteConfirmation(string? confirmationText)
    {
        return string.Equals((confirmationText ?? "").Trim(), "ELIMINAR", StringComparison.OrdinalIgnoreCase);
    }

    private static IResult CreateMassLoteFromIdentifiers(
        IEnumerable<string> rawIdentifiers,
        string tratamiento,
        string motivo,
        string requestedByEmail,
        int maxRecords,
        int partitionSize,
        AppSettings baseSettings,
        DataverseConnectionFactory factory,
        string? sourceFileName = null,
        int totalSourceRows = 0)
    {
        var rawList = rawIdentifiers
            .Where(i => !string.IsNullOrWhiteSpace(i))
            .Select(i => i.Trim())
            .ToList();

        if (rawList.Count == 0)
        {
            return Results.BadRequest(new { error = "La nomina esta vacia." });
        }

        if (rawList.Count > maxRecords)
        {
            return Results.BadRequest(new { error = $"El lote supera el limite maximo de {maxRecords} registros." });
        }

        var validationResults = new List<InputValidator.ValidationResult>();
        var processedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int duplicateCount = 0;

        foreach (var rawId in rawList)
        {
            var res = InputValidator.ValidateAndNormalize(rawId);
            string key = res.IsValid
                ? $"{res.Type}:{res.NormalizedValue}"
                : $"INVALID:{rawId.Replace(".", "").Replace("-", "").Replace(" ", "").Trim()}";

            if (processedKeys.Add(key))
            {
                validationResults.Add(res);
            }
            else
            {
                duplicateCount++;
            }
        }

        int validCount = validationResults.Count(r => r.IsValid);
        int invalidCount = validationResults.Count(r => !r.IsValid);
        int effectivePartitionSize = Math.Max(1, partitionSize);

        var headerId = Guid.NewGuid();
        using var client = factory.CreateClient(baseSettings.Dataverse);

        var header = new Entity("um_massexecution", headerId);
        header["um_name"] = string.IsNullOrWhiteSpace(sourceFileName)
            ? $"Ejecucion {tratamiento} {DateTime.Now:yyyy-MM-dd HH:mm:ss}"
            : $"Archivo {Path.GetFileName(sourceFileName)} - {tratamiento} {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
        header["um_estado"] = new OptionSetValue(MassOptionSets.HeaderEstadoPendiente);
        header["um_tratamiento"] = new OptionSetValue(MassOptionSets.StringToTratamiento(tratamiento));
        header["um_motivo"] = motivo;
        header["um_totalregistros"] = validationResults.Count;
        header["um_invalidos"] = invalidCount;
        header["um_procesados"] = 0;
        header["um_exitosos"] = 0;
        header["um_noencontrados"] = 0;
        header["um_errores"] = 0;
        header["um_requiereconciliacion"] = 0;
        header["um_requestedbyemail"] = requestedByEmail;

        client.Create(header);

        var multipleRequest = new Microsoft.Xrm.Sdk.Messages.ExecuteMultipleRequest
        {
            Settings = new ExecuteMultipleSettings
            {
                ContinueOnError = true,
                ReturnResponses = true
            },
            Requests = new OrganizationRequestCollection()
        };

        foreach (var res in validationResults)
        {
            var detailId = Guid.NewGuid();
            var detail = new Entity("um_massexecutiondetail", detailId);
            detail["um_massexecutionid"] = new EntityReference("um_massexecution", headerId);
            detail["um_identificador"] = res.IsValid ? res.NormalizedValue : res.RawValue;
            detail["um_tipoidentificador"] = res.Type.ToString();
            detail["um_name"] = BuildMassDetailName(res.Type.ToString(), res.IsValid ? res.NormalizedValue : res.RawValue, res.IsValid ? "Pendiente" : "Invalido");

            if (res.IsValid)
            {
                detail["um_estado"] = new OptionSetValue(MassOptionSets.DetailEstadoPendiente);
            }
            else
            {
                detail["um_estado"] = new OptionSetValue(MassOptionSets.DetailEstadoInvalido);
                detail["um_errormessage"] = res.ErrorMessage;
                detail["um_resultado"] = JsonSerializer.Serialize(new
                {
                    estado = "Invalido",
                    mensaje = res.ErrorMessage
                });
            }

            multipleRequest.Requests.Add(new Microsoft.Xrm.Sdk.Messages.CreateRequest { Target = detail });
        }

        const int executeMultipleLimit = 500;
        for (int i = 0; i < multipleRequest.Requests.Count; i += executeMultipleLimit)
        {
            var batchRequests = multipleRequest.Requests.Skip(i).Take(executeMultipleLimit).ToList();
            var subRequest = new Microsoft.Xrm.Sdk.Messages.ExecuteMultipleRequest
            {
                Settings = multipleRequest.Settings,
                Requests = new OrganizationRequestCollection()
            };
            subRequest.Requests.AddRange(batchRequests);
            client.Execute(subRequest);
        }

        var response = new CreateMassLoteResponse
        {
            ExecutionId = headerId.ToString("N"),
            TotalRegistros = validationResults.Count,
            RegistrosValidos = validCount,
            RegistrosInvalidos = invalidCount,
            RegistrosDuplicados = duplicateCount,
            TotalLineasArchivo = totalSourceRows,
            SourceFileName = sourceFileName,
            PartitionSize = effectivePartitionSize,
            EstimatedPartitions = validCount > 0 ? (int)Math.Ceiling((double)validCount / effectivePartitionSize) : 0
        };

        return Results.Accepted($"/api/mass/status/{headerId:N}", response);
    }

    private static IEnumerable<string> ParseMassIdentifierFile(string content)
    {
        if (string.IsNullOrWhiteSpace(content)) yield break;

        var normalized = content.Replace("\r\n", "\n").Replace('\r', '\n');
        var tokens = normalized.Split(new[] { '\n', ',', ';', '\t' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var token in tokens)
        {
            var value = token.Trim().Trim('"', '\'');
            if (string.IsNullOrWhiteSpace(value)) continue;

            if (value.Equals("rut", StringComparison.OrdinalIgnoreCase) ||
                value.Equals("ruts", StringComparison.OrdinalIgnoreCase) ||
                value.Equals("pasaporte", StringComparison.OrdinalIgnoreCase) ||
                value.Equals("identificador", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            yield return value;
        }
    }

    private static string DecodeUploadedText(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        }

        return Encoding.UTF8.GetString(bytes);
    }

    private static List<Guid> RetrievePendingMassDetailIds(Microsoft.PowerPlatform.Dataverse.Client.ServiceClient client, Guid headerId)
    {
        var ids = new List<Guid>();
        var query = new QueryExpression("um_massexecutiondetail")
        {
            ColumnSet = new ColumnSet("um_massexecutiondetailid"),
            PageInfo = new PagingInfo
            {
                PageNumber = 1,
                Count = 5000,
                ReturnTotalRecordCount = false
            }
        };
        query.Criteria.AddCondition("um_massexecutionid", ConditionOperator.Equal, headerId);

        var resumableFilter = new FilterExpression(LogicalOperator.Or);
        resumableFilter.AddCondition("um_estado", ConditionOperator.Equal, MassOptionSets.DetailEstadoPendiente);

        var expiredLeaseFilter = new FilterExpression(LogicalOperator.And);
        expiredLeaseFilter.AddCondition("um_estado", ConditionOperator.Equal, MassOptionSets.DetailEstadoEnProceso);
        expiredLeaseFilter.AddCondition("um_leaseduntil", ConditionOperator.LessThan, DateTime.UtcNow);
        resumableFilter.AddFilter(expiredLeaseFilter);

        query.Criteria.AddFilter(resumableFilter);
        query.Orders.Add(new OrderExpression("createdon", OrderType.Ascending));

        while (true)
        {
            var results = client.RetrieveMultiple(query);
            ids.AddRange(results.Entities.Select(e => e.Id));

            if (!results.MoreRecords) break;

            query.PageInfo.PageNumber++;
            query.PageInfo.PagingCookie = results.PagingCookie;
        }

        return ids;
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

    private static string BuildMassDetailName(string type, string identifier, string state)
    {
        string cleanType = string.IsNullOrWhiteSpace(type) ? "Identificador" : type.Trim();
        string cleanIdentifier = string.IsNullOrWhiteSpace(identifier) ? "Sin identificador" : identifier.Trim();
        string cleanState = string.IsNullOrWhiteSpace(state) ? "Pendiente" : state.Trim();
        return LimitLength($"{cleanType} {cleanIdentifier} - {cleanState}", 100);
    }

    public static string GetDv(string rawRut)
    {
        if (string.IsNullOrWhiteSpace(rawRut)) return string.Empty;
        string clean = rawRut.Replace(".", "").Replace("-", "").Replace(" ", "").Trim();
        if (clean.Length > 0)
        {
            return clean.Substring(clean.Length - 1).ToUpper();
        }
        return string.Empty;
    }

    public static ConsultationData? ParseConsultationData(object? obj)
    {
        if (obj == null) return null;
        try
        {
            var json = JsonSerializer.Serialize(obj);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<ConsultationData>(json, options);
        }
        catch
        {
            return null;
        }
    }

    public static List<string> GetBlockingResiduals(ConsultationData? postMatrix, string mode, IEnumerable<string>? sanitizedResidualEntities = null)
    {
        var residuals = new List<string>();
        if (postMatrix?.Matrix == null) return residuals;

        var allowedResiduals = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (mode == "EliminarTodoMenosContacto")
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

        foreach (var row in postMatrix.Matrix)
        {
            if (row.CantidadTotal <= 0) continue;
            var entityName = row.EntidadRelacionada ?? "";
            if (allowedResiduals.Contains(entityName)) continue;

            residuals.Add($"{entityName}={row.CantidadTotal}");
        }

        return residuals;
    }

    public static IResult ExecuteBatch(List<SingleRequest> requests, string mode, string confirmationText, HttpContext httpContext, AppSettings baseSettings, LogService logService, BackupService backupService, DataverseConnectionFactory factory)
    {
        string endpoint = requests.Count == 1 ? "execute-single" : "execute-batch";
        Console.WriteLine($"[REQUEST] {endpoint} recibido");
        Console.WriteLine($"[REQUEST] rut: {requests.Count.ToString()}");
        Console.WriteLine($"[REQUEST] mode: {mode}");
        Console.WriteLine($"[REQUEST] confirmationTextIsEmpty: {string.IsNullOrEmpty(confirmationText)}");
        Console.WriteLine($"[REQUEST] apiBuild: {ApiBuild}");
        Console.WriteLine($"[REQUEST] Dataverse URL: {baseSettings.Dataverse.Url}");

        if (string.IsNullOrEmpty(mode)) return Results.BadRequest("Modo no especificado.");
        mode = mode.Trim();
        if (mode.Equals("Consultar", StringComparison.OrdinalIgnoreCase))
        {
            mode = "Consultar";
        }
        else if (mode.Equals("EliminarTodo", StringComparison.OrdinalIgnoreCase))
        {
            mode = "EliminarTodo";
        }
        else if (mode.Equals("EliminarTodoMenosContacto", StringComparison.OrdinalIgnoreCase))
        {
            mode = "EliminarTodoMenosContacto";
        }

        var headerGuid = Guid.NewGuid();
        var executionId = headerGuid.ToString("N");
        using var client = factory.CreateClient(baseSettings.Dataverse);

        var results = new List<object>();

        // Extract requested user details from easy auth headers or User Identity
        string reqEmail = "";
        if (httpContext != null)
        {
            reqEmail = httpContext.Request.Headers["X-MS-CLIENT-PRINCIPAL-NAME"].ToString();
            if (string.IsNullOrEmpty(reqEmail))
            {
                reqEmail = httpContext.User.Identity?.Name ?? "";
            }
        }
        string reqName = string.IsNullOrEmpty(reqEmail) ? "Unknown" : reqEmail;
        reqEmail = reqName;

        // Crear la cabecera del proceso masivo de forma activa
        var header = new Entity("um_massexecution", headerGuid);
        header["um_name"] = $"Ejecución {mode} {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
        header["um_estado"] = new OptionSetValue(MassOptionSets.HeaderEstadoEnProceso);
        header["um_tratamiento"] = new OptionSetValue(MassOptionSets.StringToTratamiento(mode));
        header["um_motivo"] = "Ejecución Sincrónica Batch API";
        header["um_totalregistros"] = requests.Count;
        header["um_invalidos"] = "0";
        header["um_procesados"] = 0;
        header["um_exitosos"] = 0;
        header["um_noencontrados"] = 0;
        header["um_errores"] = "0";
        header["um_requiereconciliacion"] = 0;
        header["um_requestedbyemail"] = reqEmail;
        header["um_inicio"] = DateTime.UtcNow;

        client.Create(header);

        foreach (var reqItem in requests)
        {
            var rawRut = reqItem.Rut;
            var pasaporte = reqItem.Pasaporte;
            if (string.IsNullOrWhiteSpace(rawRut) && string.IsNullOrWhiteSpace(pasaporte)) continue;

            string rut = string.IsNullOrWhiteSpace(rawRut) ? "" : NormalizeRut(rawRut);
            if (!string.IsNullOrWhiteSpace(rawRut) && string.IsNullOrWhiteSpace(rut))
            {
                var startedTime = DateTime.Now;
                var finishedTime = DateTime.Now;
                var auditService = new PrivacyOperationLogService(client, baseSettings);
                var auditReport = auditService.LogOperation(
                    executionId: executionId,
                    mode: mode,
                    status: "Error",
                    rutIngresado: rawRut,
                    pasaporte: pasaporte,
                    rutNormalizado: "",
                    dv: GetDv(rawRut),
                    rutCompleto: rawRut,
                    contactIdText: "",
                    contactFullname: "",
                    contactDeleted: false,
                    requestedByName: reqName,
                    requestedByEmail: reqEmail,
                    confirmationProvided: !string.IsNullOrEmpty(confirmationText),
                    totalFoundBeforeDelete: 0,
                    totalDeleted: 0,
                    totalErrors: 0,
                    backupCreated: false,
                    backupFileName: "",
                    startedAt: startedTime,
                    finishedAt: finishedTime,
                    errorMessage: "RUT ingresado es inválido o no se pudo normalizar: " + rawRut,
                    requestPayload: new { rut = rawRut, mode = mode, confirmationText = confirmationText },
                    responsePayload: new { rut = rawRut, status = "RUT Inválido" },
                    preMatrix: null,
                    postMatrix: null
                );

                results.Add(new { rut = rawRut, status = "RUT Inválido", audit = auditReport });

                string idValErr = string.IsNullOrWhiteSpace(rawRut) ? pasaporte! : rawRut;
                string tipoValErr = string.IsNullOrWhiteSpace(rawRut) ? "Pasaporte" : "RUT";
                CreateExecutionDetailAndLinkAudit(client, headerGuid, idValErr, tipoValErr, "RUT Inválido", "RUT ingresado es inválido o no se pudo normalizar: " + rawRut, auditReport, null);
                continue;
            }

            var localSettings = new AppSettings
            {
                Dataverse = baseSettings.Dataverse,
                Safety = baseSettings.Safety,
                Operation = new OperationSettings { Rut = rut, Pasaporte = pasaporte, Mode = mode }
            };

            if (mode == "Consultar")
            {
                var startedTime = DateTime.Now;
                dynamic? matrixData = null;
                string status = "Consultado";
                string? contactIdText = null;
                string? contactFullname = null;
                string? errorMsg = null;
                object responseObj;

                try
                {
                    var rutService = new RutMatrixService(client, logService, localSettings, executionId);
                    matrixData = rutService.Execute("Solo Consulta");
                    
                    var parsed = ParseConsultationData(matrixData);
                    bool found = parsed?.Found ?? false;

                    if (found)
                    {
                        status = "Consultado";
                        contactIdText = parsed?.ContactId;
                        contactFullname = parsed?.Fullname;
                    }
                    else
                    {
                        status = "NoEncontrado";
                    }

                    Console.WriteLine($"ExecuteBatch: mode={mode}, parsedFound={found}, status={status}");

                    responseObj = new { rut, status, data = matrixData };
                }
                catch (Exception ex)
                {
                    status = "Error";
                    errorMsg = ex.Message;
                    responseObj = new { rut, status, error = ex.Message };
                }

                var finishedTime = DateTime.Now;
                var auditService = new PrivacyOperationLogService(client, baseSettings);
                var auditReport = auditService.LogOperation(
                    executionId: executionId,
                    mode: mode,
                    status: status,
                    rutIngresado: rawRut,
                    pasaporte: pasaporte,
                    rutNormalizado: rut,
                    dv: GetDv(rawRut),
                    rutCompleto: rut + "-" + GetDv(rawRut),
                    contactIdText: contactIdText,
                    contactFullname: contactFullname,
                    contactDeleted: false,
                    requestedByName: reqName,
                    requestedByEmail: reqEmail,
                    confirmationProvided: !string.IsNullOrEmpty(confirmationText),
                    totalFoundBeforeDelete: 0,
                    totalDeleted: 0,
                    totalErrors: 0,
                    backupCreated: false,
                    backupFileName: "",
                    startedAt: startedTime,
                    finishedAt: finishedTime,
                    errorMessage: errorMsg,
                    requestPayload: new { rut = rawRut, mode = mode, confirmationText = confirmationText },
                    responsePayload: responseObj,
                    preMatrix: null,
                    postMatrix: matrixData
                );

                if (status == "Error")
                {
                    results.Add(new { rut, status, error = errorMsg, audit = auditReport });
                }
                else
                {
                    results.Add(new { rut, status, data = matrixData, audit = auditReport });
                }

                string idVal = string.IsNullOrWhiteSpace(rut) ? pasaporte! : rut;
                string tipoVal = string.IsNullOrWhiteSpace(rut) ? "Pasaporte" : "RUT";
                CreateExecutionDetailAndLinkAudit(client, headerGuid, idVal, tipoVal, status, errorMsg, auditReport, null);
            }
            else if (mode == "EliminarTodo" || mode == "EliminarTodoMenosContacto")
            {
                // Check safety requirements
                bool deletionEnabled = baseSettings.Safety.DeletionEnabled;
                bool confirmationMatches = (confirmationText != null && confirmationText.Trim() == baseSettings.Safety.RequireConfirmationText);

                if (!deletionEnabled || !confirmationMatches)
                {
                    var startedTime = DateTime.Now;
                    var errors = new List<string>();
                    if (!deletionEnabled)
                    {
                        errors.Add("La eliminaciÃ³n no estÃ¡ habilitada en el servidor (Safety__DeletionEnabled es false).");
                    }
                    if (!confirmationMatches)
                    {
                        errors.Add($"El texto de confirmaciÃ³n es incorrecto o vacÃ­o. Provisto: '{confirmationText}', esperado: '{baseSettings.Safety.RequireConfirmationText}'.");
                    }

                    var report = new DeletionExecutionReport
                    {
                        OperationMode = mode,
                        ContactDeleted = false,
                        PreMatrix = null,
                        PostMatrix = null,
                        DeletionSummary = new DeletionSummary
                        {
                            TotalFoundBeforeDelete = 0,
                            TotalDeleted = 0,
                            TotalErrors = 0,
                            BackupCreated = false,
                            BackupFileName = "",
                            StartedAt = startedTime.ToString("yyyy-MM-dd HH:mm:ss"),
                            FinishedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                        },
                        Errors = errors
                    };

                    var finishedTime = DateTime.Now;
                    var auditService = new PrivacyOperationLogService(client, baseSettings);
                    var auditReport = auditService.LogOperation(
                        executionId: executionId,
                        mode: mode,
                        status: "Bloqueado",
                        rutIngresado: rawRut,
                        pasaporte: pasaporte,
                        rutNormalizado: rut,
                        dv: GetDv(rawRut),
                        rutCompleto: rut + "-" + GetDv(rawRut),
                        contactIdText: "",
                        contactFullname: "",
                        contactDeleted: false,
                        requestedByName: reqName,
                        requestedByEmail: reqEmail,
                        confirmationProvided: !string.IsNullOrEmpty(confirmationText),
                        totalFoundBeforeDelete: 0,
                        totalDeleted: 0,
                        totalErrors: 0,
                        backupCreated: false,
                        backupFileName: "",
                        startedAt: startedTime,
                        finishedAt: finishedTime,
                        errorMessage: string.Join("; ", errors),
                        requestPayload: new { rut = rawRut, mode = mode, confirmationText = confirmationText },
                        responsePayload: new { rut, status = "Bloqueado", data = report },
                        preMatrix: null,
                        postMatrix: null
                    );

                    results.Add(new
                    {
                        rut,
                        status = "Bloqueado",
                        data = report,
                        audit = auditReport
                    });

                    string idValB = string.IsNullOrWhiteSpace(rut) ? pasaporte! : rut;
                    string tipoValB = string.IsNullOrWhiteSpace(rut) ? "Pasaporte" : "RUT";
                    CreateExecutionDetailAndLinkAudit(client, headerGuid, idValB, tipoValB, "Bloqueado", string.Join("; ", errors), auditReport, null);
                    continue;
                }

                // Execution Flow when permitted
                var startedTimeReal = DateTime.Now;
                dynamic? preMatrix = null;
                dynamic? postMatrix = null;
                DeletionExecutionReport? reportReal = null;
                string statusReal = mode == "EliminarTodo" ? "Eliminado" : "EliminadoMenosContacto";
                string? contactIdTextReal = null;
                string? contactFullnameReal = null;
                bool contactDeletedFlag = false;
                string? errorMsgReal = null;

                try
                {
                    var rutService = new RutMatrixService(client, logService, localSettings, executionId);
                    preMatrix = rutService.Execute("Estado Pre-Eliminación");

                    var parsedPre = ParseConsultationData(preMatrix);
                    bool found = parsedPre?.Found ?? false;

                    object responsePayloadLocal;
                    if (found)
                    {
                        contactIdTextReal = parsedPre?.ContactId;
                        contactFullnameReal = parsedPre?.Fullname;

                        var deletionService = new MatrixDeletionService(client, logService, backupService, localSettings, executionId);
                        reportReal = deletionService.Execute(confirmationText ?? "", deletionEnabled);

                        postMatrix = rutService.Execute("Estado Post-Eliminación");

                        reportReal.PreMatrix = preMatrix;
                        reportReal.PostMatrix = postMatrix;

                        var parsedPost = ParseConsultationData(postMatrix);
                        bool postMatrixFound = parsedPost?.Found ?? false;
                        var blockingResiduals = GetBlockingResiduals(parsedPost, mode, reportReal.SanitizedResidualEntities);
                        
                        reportReal.PostMatrixFound = postMatrixFound;
                        contactDeletedFlag = reportReal.ContactDeleted;

                        if (mode == "EliminarTodo")
                        {
                            statusReal = (contactDeletedFlag && !postMatrixFound && blockingResiduals.Count == 0) ? "Eliminado" : "Error";
                            reportReal.OperationCompleted = (statusReal == "Eliminado");
                        }
                        else
                        {
                            statusReal = reportReal.Errors.Count == 0 && blockingResiduals.Count == 0 ? "EliminadoMenosContacto" : "Error";
                            reportReal.OperationCompleted = (statusReal == "EliminadoMenosContacto");
                        }

                        if (blockingResiduals.Count > 0)
                        {
                            reportReal.Errors.Add("Quedaron registros residuales no autorizados en la matriz post-eliminacion: " + string.Join(", ", blockingResiduals));
                        }

                        if (reportReal.Errors.Count > 0)
                        {
                            errorMsgReal = string.Join("; ", reportReal.Errors);
                        }

                        string frontMsg = reportReal.OperationCompleted 
                            ? $"Proceso finalizado para el RUT {rut}. Estado: {statusReal}" 
                            : $"Atención para el RUT {rut}. Estado: {statusReal}";

                        responsePayloadLocal = new { rut, status = statusReal, message = frontMsg, data = reportReal };
                    }
                    else
                    {
                        statusReal = "NoEncontrado";
                        responsePayloadLocal = new { rut, status = statusReal, data = new { message = $"No se encontró ningún contacto con wit_rut = {rut}" } };
                    }

                    var finishedTimeReal = DateTime.Now;
                    var auditService = new PrivacyOperationLogService(client, baseSettings);
                    var auditReport = auditService.LogOperation(
                        executionId: executionId,
                        mode: mode,
                        status: statusReal,
                        rutIngresado: rawRut,
                        pasaporte: pasaporte,
                        rutNormalizado: rut,
                        dv: GetDv(rawRut),
                        rutCompleto: rut + "-" + GetDv(rawRut),
                        contactIdText: contactIdTextReal,
                        contactFullname: contactFullnameReal,
                        contactDeleted: mode == "EliminarTodo" && contactDeletedFlag,
                        requestedByName: reqName,
                        requestedByEmail: reqEmail,
                        confirmationProvided: !string.IsNullOrEmpty(confirmationText),
                        totalFoundBeforeDelete: reportReal?.DeletionSummary?.TotalFoundBeforeDelete ?? 0,
                        totalDeleted: reportReal?.DeletionSummary?.TotalDeleted ?? 0,
                        totalErrors: reportReal?.DeletionSummary?.TotalErrors ?? 0,
                        backupCreated: reportReal?.DeletionSummary?.BackupCreated ?? false,
                        backupFileName: reportReal?.DeletionSummary?.BackupFileName,
                        startedAt: startedTimeReal,
                        finishedAt: finishedTimeReal,
                        errorMessage: errorMsgReal,
                        requestPayload: new { rut = rawRut, mode = mode, confirmationText = confirmationText },
                        responsePayload: responsePayloadLocal,
                        preMatrix: preMatrix,
                        postMatrix: postMatrix
                    );

                    if (found)
                    {
                        results.Add(new { rut, status = statusReal, data = reportReal, audit = auditReport });
                    }
                    else
                    {
                        results.Add(new { rut, status = statusReal, data = new { message = $"No se encontró ningún contacto con wit_rut = {rut}" }, audit = auditReport });
                    }

                    string idValD = string.IsNullOrWhiteSpace(rut) ? pasaporte! : rut;
                    string tipoValD = string.IsNullOrWhiteSpace(rut) ? "Pasaporte" : "RUT";
                    CreateExecutionDetailAndLinkAudit(client, headerGuid, idValD, tipoValD, statusReal, errorMsgReal, auditReport, reportReal);
                }
                catch (Exception ex)
                {
                    statusReal = "Error";
                    errorMsgReal = ex.Message;
                    var responsePayloadLocal = new { rut, status = statusReal, error = ex.Message };
                    var finishedTimeReal = DateTime.Now;
                    var auditService = new PrivacyOperationLogService(client, baseSettings);
                    var auditReport = auditService.LogOperation(
                        executionId: executionId,
                        mode: mode,
                        status: statusReal,
                        rutIngresado: rawRut,
                        pasaporte: pasaporte,
                        rutNormalizado: rut,
                        dv: GetDv(rawRut),
                        rutCompleto: rut + "-" + GetDv(rawRut),
                        contactIdText: contactIdTextReal,
                        contactFullname: contactFullnameReal,
                        contactDeleted: false,
                        requestedByName: reqName,
                        requestedByEmail: reqEmail,
                        confirmationProvided: !string.IsNullOrEmpty(confirmationText),
                        totalFoundBeforeDelete: 0,
                        totalDeleted: 0,
                        totalErrors: 0,
                        backupCreated: false,
                        backupFileName: "",
                        startedAt: startedTimeReal,
                        finishedAt: finishedTimeReal,
                        errorMessage: errorMsgReal,
                        requestPayload: new { rut = rawRut, mode = mode, confirmationText = confirmationText },
                        responsePayload: responsePayloadLocal,
                        preMatrix: preMatrix,
                        postMatrix: postMatrix
                    );
                    results.Add(new { rut, status = statusReal, error = ex.Message, audit = auditReport });

                    string idValErr = string.IsNullOrWhiteSpace(rut) ? pasaporte! : rut;
                    string tipoValErr = string.IsNullOrWhiteSpace(rut) ? "Pasaporte" : "RUT";
                    CreateExecutionDetailAndLinkAudit(client, headerGuid, idValErr, tipoValErr, statusReal, errorMsgReal, auditReport, null);
                }
            }
            else
            {
                var startedTime = DateTime.Now;
                var finishedTime = DateTime.Now;
                var auditService = new PrivacyOperationLogService(client, baseSettings);
                var auditReport = auditService.LogOperation(
                    executionId: executionId,
                    mode: mode,
                    status: "Error",
                    rutIngresado: rawRut,
                    pasaporte: pasaporte,
                    rutNormalizado: rut,
                    dv: GetDv(rawRut),
                    rutCompleto: rut + "-" + GetDv(rawRut),
                    contactIdText: "",
                    contactFullname: "",
                    contactDeleted: false,
                    requestedByName: reqName,
                    requestedByEmail: reqEmail,
                    confirmationProvided: !string.IsNullOrEmpty(confirmationText),
                    totalFoundBeforeDelete: 0,
                    totalDeleted: 0,
                    totalErrors: 0,
                    backupCreated: false,
                    backupFileName: "",
                    startedAt: startedTime,
                    finishedAt: finishedTime,
                    errorMessage: "Modo de operación no válido: " + mode,
                    requestPayload: new { rut = rawRut, mode = mode, confirmationText = confirmationText },
                    responsePayload: new { rut, status = "Modo Inválido" },
                    preMatrix: null,
                    postMatrix: null
                );
                
                results.Add(new { rut, status = "Modo Inválido", audit = auditReport });

                string idValM = string.IsNullOrWhiteSpace(rut) ? pasaporte! : rut;
                string tipoValM = string.IsNullOrWhiteSpace(rut) ? "Pasaporte" : "RUT";
                CreateExecutionDetailAndLinkAudit(client, headerGuid, idValM, tipoValM, "Modo Inválido", "Modo de operación no válido: " + mode, auditReport, null);
            }
        }

        int successful = 0;
        int notFound = 0;
        int failed = 0;

        foreach (dynamic res in results)
        {
            string status = res.status;
            if (status is "Consultado" or "Eliminado" or "EliminadoMenosContacto")
            {
                successful++;
            }
            else if (status is "NoEncontrado")
            {
                notFound++;
            }
            else
            {
                failed++;
            }
        }

        var summary = new
        {
            totalProcessed = requests.Count,
            successful,
            notFound,
            failed
        };

        // Actualizar la cabecera en Dataverse con totales y estado final
        try
        {
            var headerUpdate = new Entity("um_massexecution", headerGuid);
            headerUpdate["um_procesados"] = successful + notFound + failed;
            headerUpdate["um_exitosos"] = successful;
            headerUpdate["um_noencontrados"] = notFound;
            headerUpdate["um_errores"] = failed.ToString();
            headerUpdate["um_requiereconciliacion"] = 0;
            headerUpdate["um_estado"] = new OptionSetValue(failed > 0 ? MassOptionSets.HeaderEstadoCompletadoConErrores : MassOptionSets.HeaderEstadoCompletado);
            headerUpdate["um_termino"] = DateTime.UtcNow;

            client.Update(headerUpdate);
        }
        catch (Exception exHeader)
        {
            Console.WriteLine($"[ERROR] FAILED TO UPDATE EXECUTION HEADER: {exHeader.Message}");
        }

        return Results.Ok(new { executionId, summary, results, apiBuild = ApiBuild });
    }

    public static Guid CreateExecutionDetailAndLinkAudit(
        Microsoft.PowerPlatform.Dataverse.Client.ServiceClient client, 
        Guid headerGuid, 
        string identifier, 
        string tipoId, 
        string status, 
        string? errorMsg, 
        AuditDiagnosticReport? auditReport, 
        DeletionExecutionReport? deletionReport)
    {
        int detailState = status switch
        {
            "Consultado" => MassOptionSets.DetailEstadoConsultado,
            "Eliminado" => MassOptionSets.DetailEstadoEliminado,
            "EliminadoMenosContacto" => MassOptionSets.DetailEstadoEliminado,
            "NoEncontrado" => MassOptionSets.DetailEstadoNoEncontrado,
            "RUT Inválido" => MassOptionSets.DetailEstadoInvalido,
            "Modo Inválido" => MassOptionSets.DetailEstadoInvalido,
            "Bloqueado" => MassOptionSets.DetailEstadoInvalido,
            _ => MassOptionSets.DetailEstadoError
        };

        var detailGuid = Guid.NewGuid();
        var detail = new Entity("um_massexecutiondetail", detailGuid);
        detail["um_name"] = $"Detalle {LimitLength(identifier, 50)}";
        detail["um_massexecutionid"] = new EntityReference("um_massexecution", headerGuid);
        detail["um_identificador"] = LimitLength(identifier, 100);
        detail["um_tipoidentificador"] = LimitLength(tipoId, 100);
        detail["um_estado"] = new OptionSetValue(detailState);

        if (!string.IsNullOrEmpty(errorMsg))
        {
            detail["um_errormessage"] = LimitLength(errorMsg, 4000);
        }

        var outcome = new
        {
            estado = MassOptionSets.DetailEstadoToString(detailState),
            mensaje = errorMsg ?? (status == "NoEncontrado" ? "Contacto no encontrado en Dynamics 365" : "Procesado correctamente"),
            individualExecutionId = detailGuid.ToString("N"),
            auditLogId = auditReport?.RecordId,
            backupReference = deletionReport?.DeletionSummary?.BackupFileName,
            backupDate = deletionReport?.DeletionSummary?.StartedAt
        };
        detail["um_resultado"] = JsonSerializer.Serialize(outcome);

        if (deletionReport?.DeletionSummary != null)
        {
            var ds = deletionReport.DeletionSummary;
            if (!string.IsNullOrEmpty(ds.BackupFileName))
            {
                detail["um_backupreference"] = LimitLength(ds.BackupFileName, 100);
            }
            if (DateTime.TryParse(ds.StartedAt, out var bDate))
            {
                detail["um_backupdate"] = bDate.ToUniversalTime();
            }
        }

        client.Create(detail);

        if (auditReport != null && auditReport.Created && !string.IsNullOrEmpty(auditReport.RecordId))
        {
            try
            {
                var auditUpdate = new Entity("um_privacyoperationlog", new Guid(auditReport.RecordId));
                auditUpdate["um_massexecutionid"] = new EntityReference("um_massexecution", headerGuid);
                auditUpdate["um_massexecutiondetailid"] = new EntityReference("um_massexecutiondetail", detailGuid);
                client.Update(auditUpdate);
            }
            catch (Exception exLink)
            {
                Console.WriteLine($"[ERROR] FAILED TO LINK AUDIT LOG TO DETAIL: {exLink.Message}");
            }
        }

        return detailGuid;
    }

    private static string LimitLength(string? val, int maxChars = 4000)
    {
        if (string.IsNullOrEmpty(val)) return string.Empty;
        if (val.Length <= maxChars) return val;
        string marker = "...[TRUNC]";
        if (maxChars == 4000) return val.Substring(0, 3988) + marker;
        if (maxChars <= marker.Length) return val.Substring(0, maxChars);
        return val.Substring(0, maxChars - marker.Length) + marker;
    }
}

public class SingleRequest
{
    public string Rut { get; set; } = "";
    public string Pasaporte { get; set; } = "";
    public string Mode { get; set; } = "";
    public string ConfirmationText { get; set; } = "";
}

public class BatchRequest
{
    public List<string> Ruts { get; set; } = new();
    public List<string> Pasaportes { get; set; } = new();
    public string Mode { get; set; } = "";
    public string ConfirmationText { get; set; } = "";
}

public class AdditionalSchemasDocumentFilter : IDocumentFilter
{
    public void Apply(Microsoft.OpenApi.OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        context.SchemaGenerator.GenerateSchema(typeof(ContactSummary), context.SchemaRepository);
        context.SchemaGenerator.GenerateSchema(typeof(ConsultationData), context.SchemaRepository);
        context.SchemaGenerator.GenerateSchema(typeof(MatrixRow), context.SchemaRepository);
        context.SchemaGenerator.GenerateSchema(typeof(Umayor.Dynamics.DeletePoc.Services.DeletionExecutionReport), context.SchemaRepository);
        context.SchemaGenerator.GenerateSchema(typeof(Umayor.Dynamics.DeletePoc.Services.DeletionSummary), context.SchemaRepository);
    }
}
