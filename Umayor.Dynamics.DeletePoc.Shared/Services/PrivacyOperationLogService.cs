using System;
using System.Text.Json;
using System.Net.Http;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Umayor.Dynamics.DeletePoc.Models;

namespace Umayor.Dynamics.DeletePoc.Services;

public class PrivacyOperationLogService
{
    private readonly ServiceClient _client;
    private readonly AppSettings _settings;

    public PrivacyOperationLogService(ServiceClient client, AppSettings settings)
    {
        _client = client;
        _settings = settings;
    }

    public AuditDiagnosticReport LogOperation(
        string executionId,
        string mode,
        string status,
        string rutIngresado,
        string? pasaporte,
        string? rutNormalizado,
        string? dv,
        string? rutCompleto,
        string? contactIdText,
        string? contactFullname,
        bool contactDeleted,
        string? requestedByName,
        string? requestedByEmail,
        bool confirmationProvided,
        int totalFoundBeforeDelete,
        int totalDeleted,
        int totalErrors,
        bool backupCreated,
        string? backupFileName,
        DateTime startedAt,
        DateTime finishedAt,
        string? errorMessage,
        object requestPayload,
        object responsePayload,
        object? preMatrix,
        object? postMatrix
    )
    {
        var report = new AuditDiagnosticReport
        {
            Attempted = true,
            Created = false,
            RecordId = null,
            RetrieveAfterCreateCount = 0,
            WebApiRetrieveCount = 0,
            Error = null,
            RetryUsed = false,
            Warning = null,
            OriginalResponseJsonLength = null,
            SavedResponseJsonFullLength = null,
            FullJsonColumnsUsed = false,
            LegacyJsonColumnsUsed = false
        };

        try
        {
            var entity = new Entity("um_privacyoperationlog");

            entity["um_executionid"] = LimitLength(executionId, 100);

            // OptionSet values
            // um_operationtype: Consultar = 127120000, EliminarTodoMenosContacto = 127120001, EliminarTodo = 127120002
            int operationTypeValue = mode switch
            {
                "Consultar" => 127120000,
                "EliminarTodoMenosContacto" => 127120001,
                "EliminarTodo" => 127120002,
                _ => 127120007 // Otro
            };
            entity["um_operationtype"] = new OptionSetValue(operationTypeValue);

            // um_operationstatus: Consultado = 127120000, Eliminado = 127120001, Bloqueado = 127120002, Error = 127120003, Parcial = 127120004, NoEncontrado = 127120005
            int operationStatusValue = status switch
            {
                "Consultado" => 127120000,
                "Eliminado" => 127120001,
                "Eliminación completada" => 127120001,
                "Bloqueado" => 127120002,
                "Error" => 127120003,
                "Error en eliminación" => 127120003,
                "Parcial" => 127120004,
                "Eliminación Parcial" => 127120004,
                "NoEncontrado" => 127120005,
                _ => 127120003 // Default to Error if unknown
            };
            entity["um_operationstatus"] = new OptionSetValue(operationStatusValue);

            // um_source: CanvasApp = 127120000, Swagger = 127120001, API = 127120002, Batch = 127120003, ProcesoAutomatico = 127120004
            entity["um_source"] = new OptionSetValue(127120002); // API

            if (!string.IsNullOrWhiteSpace(pasaporte))
            {
                entity["um_pasaporte"] = LimitLength(pasaporte, 100);
                entity["um_rutingresado"] = null;
                entity["um_rutnormalizado"] = null;
                entity["um_dv"] = null;
                entity["um_rutcompleto"] = null;
            }
            else
            {
                entity["um_rutingresado"] = LimitLength(rutIngresado, 100);
                entity["um_rutnormalizado"] = LimitLength(rutNormalizado, 100);
                entity["um_dv"] = LimitLength(dv, 10);
                entity["um_rutcompleto"] = LimitLength(rutCompleto, 100);
            }
            entity["um_contactidtext"] = LimitLength(contactIdText, 100);
            entity["um_contactfullname"] = LimitLength(contactFullname, 100);
            entity["um_contactdeleted"] = contactDeleted;
            entity["um_requestedbyname"] = LimitLength(requestedByName, 100);
            entity["um_requestedbyemail"] = LimitLength(requestedByEmail, 100);
            entity["um_environmenturl"] = LimitLength(_settings.Dataverse.Url, 100);
            entity["um_confirmationprovided"] = confirmationProvided;
            entity["um_deletionenabled"] = _settings.Safety.DeletionEnabled;

            entity["um_totalfoundbeforedelete"] = totalFoundBeforeDelete;
            entity["um_totaldeleted"] = totalDeleted;
            entity["um_totalerrors"] = totalErrors;
            entity["um_backupcreated"] = backupCreated;
            entity["um_backupfilename"] = LimitLength(backupFileName, 100);

            entity["um_startedat"] = startedAt.ToUniversalTime();
            entity["um_finishedat"] = finishedAt.ToUniversalTime();
            entity["um_durationms"] = (int)(finishedAt - startedAt).TotalMilliseconds;

            entity["um_errormessagefull"] = errorMessage;

            var options = new JsonSerializerOptions { WriteIndented = false };
            
            string rawRequest = JsonSerializer.Serialize(requestPayload, options);
            string rawResponse = JsonSerializer.Serialize(responsePayload, options);
            string rawPreMatrix = preMatrix != null ? JsonSerializer.Serialize(preMatrix, options) : "";
            string rawPostMatrix = postMatrix != null ? JsonSerializer.Serialize(postMatrix, options) : "";
            
            report.OriginalResponseJsonLength = rawResponse.Length;
            report.SavedResponseJsonFullLength = rawResponse.Length;

            entity["um_requestjsonfull"] = rawRequest;
            entity["um_responsejsonfull"] = rawResponse;
            entity["um_prematrixjsonfull"] = rawPreMatrix;
            entity["um_postmatrixjsonfull"] = rawPostMatrix;

            report.FullJsonColumnsUsed = true;
            report.LegacyJsonColumnsUsed = false;

            Console.WriteLine("[AUDIT LOG] Intentando crear registro...");
            Console.WriteLine("[AUDIT LOG] Entity logical name: um_privacyoperationlog");
            Console.WriteLine($"[AUDIT LOG] ExecutionId: {executionId}");
            Console.WriteLine($"[AUDIT LOG] Dataverse URL: {_settings.Dataverse.Url}");

            Guid createdGuid = Guid.Empty;
            try
            {
                createdGuid = _client.Create(entity);
            }
            catch (Exception createEx)
            {
                Console.WriteLine($"[AUDIT LOG] Create failed: {createEx.Message}. Retrying with fallback on full columns...");
                
                entity["um_errormessagefull"] = LimitLength(errorMessage, 4000);
                entity["um_requestjsonfull"] = LimitLength(rawRequest, 4000);
                
                string fallbackResponse = LimitLength(rawResponse, 4000);
                entity["um_responsejsonfull"] = fallbackResponse;
                report.SavedResponseJsonFullLength = fallbackResponse.Length;

                entity["um_prematrixjsonfull"] = LimitLength(rawPreMatrix, 4000);
                entity["um_postmatrixjsonfull"] = LimitLength(rawPostMatrix, 4000);
                
                createdGuid = _client.Create(entity);
                
                report.RetryUsed = true;
                report.Warning = $"Insert failed; retried with 4000 limit due to possible physical column length. Original Error: {createEx.Message}";
                report.Error = createEx.Message;
            }
            
            report.Created = true;
            report.RecordId = createdGuid.ToString();
            
            Console.WriteLine($"[AUDIT LOG] Create returned recordId: {createdGuid}");

            // 1. Retrieve by ID
            try
            {
                var retrievedById = _client.Retrieve("um_privacyoperationlog", createdGuid, new Microsoft.Xrm.Sdk.Query.ColumnSet("um_privacyoperationlogid"));
                if (retrievedById != null && retrievedById.Id == createdGuid)
                {
                    Console.WriteLine("[AUDIT LOG] Retrieve by ID OK");
                }
                else
                {
                    Console.WriteLine("[AUDIT LOG] Retrieve by ID ERROR: null or mismatch");
                }
            }
            catch (Exception exVal)
            {
                Console.WriteLine($"[AUDIT LOG] Retrieve by ID ERROR: {exVal.Message}");
            }

            // 2. RetrieveMultiple by executionId
            var query = new Microsoft.Xrm.Sdk.Query.QueryExpression("um_privacyoperationlog")
            {
                ColumnSet = new Microsoft.Xrm.Sdk.Query.ColumnSet(
                    "um_privacyoperationlogid",
                    "um_executionid",
                    "um_operationtype",
                    "um_operationstatus",
                    "um_source",
                    "um_rutnormalizado",
                    "um_contactfullname",
                    "createdon",
                    "ownerid"
                )
            };
            query.Criteria.AddCondition("um_executionid", Microsoft.Xrm.Sdk.Query.ConditionOperator.Equal, executionId);

            var retrieved = _client.RetrieveMultiple(query);
            report.RetrieveAfterCreateCount = retrieved.Entities.Count;

            Console.WriteLine($"[AUDIT LOG] RetrieveMultiple by executionId count: {retrieved.Entities.Count}");

            // 3. Web API Retrieve
            try
            {
                using var httpClient = new HttpClient();
                string baseUrl = _settings.Dataverse.Url;
                if (!baseUrl.EndsWith("/")) baseUrl += "/";
                httpClient.BaseAddress = new Uri(baseUrl);
                httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _client.CurrentAccessToken);
                
                HttpResponseMessage response = httpClient.GetAsync($"api/data/v9.2/um_privacyoperationlogs?$filter=um_executionid eq '{executionId}'").GetAwaiter().GetResult();
                if (response.IsSuccessStatusCode)
                {
                    string json = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("value", out var valueProp) && valueProp.ValueKind == JsonValueKind.Array)
                    {
                        report.WebApiRetrieveCount = valueProp.GetArrayLength();
                    }
                }
                Console.WriteLine($"[AUDIT LOG] WebApi retrieve count: {report.WebApiRetrieveCount}");
            }
            catch (Exception exWeb)
            {
                Console.WriteLine($"[AUDIT LOG] WebApi retrieve ERROR: {exWeb.Message}");
            }
        }
        catch (Exception ex)
        {
            report.Error = ex.Message;
            Console.WriteLine($"[AUDIT LOG] Error: {ex.Message}");
        }

        return report;
    }

    private static string LimitLength(string? val, int maxChars = 4000)
    {
        if (string.IsNullOrEmpty(val)) return string.Empty;
        if (val.Length <= maxChars) return val;
        
        string marker = "...[TRUNC]";
        // User requested: Si supera 4000, truncar a 3988 y agregar "...[TRUNC]".
        if (maxChars == 4000)
        {
            return val.Substring(0, 3988) + marker;
        }

        if (maxChars <= marker.Length) return val.Substring(0, maxChars);
        return val.Substring(0, maxChars - marker.Length) + marker;
    }
}

public class AuditDiagnosticReport
{
    public bool Attempted { get; set; }
    public bool Created { get; set; }
    public string? RecordId { get; set; }
    public int RetrieveAfterCreateCount { get; set; }
    public int WebApiRetrieveCount { get; set; }
    public string? Error { get; set; }
    public bool RetryUsed { get; set; }
    public string? Warning { get; set; }
    public int? OriginalResponseJsonLength { get; set; }
    public bool FullJsonColumnsUsed { get; set; }
    public bool LegacyJsonColumnsUsed { get; set; }
    public int? SavedResponseJsonFullLength { get; set; }
}
