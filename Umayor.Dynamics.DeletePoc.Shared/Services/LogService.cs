using System;
using System.IO;
using Umayor.Dynamics.DeletePoc.Models;

namespace Umayor.Dynamics.DeletePoc.Services;

public class LogService
{
    private readonly string _logsFolder;

    public LogService(AppSettings settings)
    {
        _logsFolder = string.IsNullOrWhiteSpace(settings.Logs?.Directory) 
            ? @"C:\home\data\logs" 
            : settings.Logs.Directory;

        try
        {
            if (!Directory.Exists(_logsFolder))
            {
                Directory.CreateDirectory(_logsFolder);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Advertencia: No se pudo crear el directorio de logs {_logsFolder}: {ex.Message}");
        }
    }

    public void LogExecution(
        string executionId, 
        string environmentUrl, 
        string operationName, 
        string rut, 
        bool dryRun, 
        string status, 
        string? errorMessage = null,
        string? user = null)
    {
        // To mimic the future Dataverse Entity "um_logeliminacionlegal"
        var auditRecord = new {
            isAuditLog = true,
            um_logeliminacionlegalid = Guid.NewGuid().ToString(),
            um_usuario = user ?? Environment.UserName,
            um_rut = rut,
            um_fechaejecucion = DateTime.Now,
            um_resultado = status,
            um_operacion = operationName,
            um_ambiente = environmentUrl,
            um_detalles = errorMessage,
            um_executionid = executionId
        };

        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var fileName = $"audit_{rut}_{timestamp}.json";
        var filePath = Path.Combine(_logsFolder, fileName);

        var options = new System.Text.Json.JsonSerializerOptions { 
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        try
        {
            File.WriteAllText(filePath, System.Text.Json.JsonSerializer.Serialize(auditRecord, options), new System.Text.UTF8Encoding(true));

            // Also keep the simple flat text log for quick tailing
            var flatLogFile = Path.Combine(_logsFolder, $"poc_flat_{DateTime.Now:yyyyMMdd}.log");
            var logEntry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | ExecId: {executionId} | User: {auditRecord.um_usuario} | Env: {environmentUrl} | Op: {operationName} | RUT: {rut} | Result: {status} | Details: {errorMessage}";
            File.AppendAllText(flatLogFile, logEntry + Environment.NewLine);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Advertencia: No se pudo escribir log en disco: {ex.Message}");
        }
    }
}

