using System.Text.Json;
using Microsoft.Xrm.Sdk;
using Umayor.Dynamics.DeletePoc.Models;

namespace Umayor.Dynamics.DeletePoc.Services;

public class BackupService
{
    private readonly string _backupFolder;

    public BackupService(AppSettings settings)
    {
        var home = Environment.GetEnvironmentVariable("HOME");
        var isAzure = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME"));

        if (isAzure && !string.IsNullOrEmpty(home))
        {
            _backupFolder = Path.Combine(home, "data", "backups");
        }
        else
        {
            _backupFolder = string.IsNullOrWhiteSpace(settings.Backups?.Directory)
                ? Path.Combine(AppContext.BaseDirectory, "backups")
                : (Path.IsPathRooted(settings.Backups.Directory)
                    ? settings.Backups.Directory
                    : Path.Combine(AppContext.BaseDirectory, settings.Backups.Directory));
        }

        if (!Directory.Exists(_backupFolder))
        {
            Directory.CreateDirectory(_backupFolder);
        }
    }

    public string CreateBackup(Entity entity, string executionId, string environmentUrl)
    {
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var fileName = $"backup_{entity.LogicalName}_{entity.Id}_{timestamp}.json";
        var filePath = Path.Combine(_backupFolder, fileName);

        var attributesDict = new Dictionary<string, object?>();
        var formattedValuesDict = new Dictionary<string, string>();

        foreach (var attr in entity.Attributes)
        {
            attributesDict[attr.Key] = attr.Value;
        }

        foreach (var formatted in entity.FormattedValues)
        {
            formattedValuesDict[formatted.Key] = formatted.Value;
        }

        var backupData = new
        {
            executionId,
            environmentUrl,
            entityLogicalName = entity.LogicalName,
            recordId = entity.Id,
            retrievedAt = DateTime.UtcNow.ToString("O"),
            attributes = attributesDict,
            formattedValues = formattedValuesDict
        };

        var json = JsonSerializer.Serialize(backupData, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(filePath, json, new System.Text.UTF8Encoding(true));

        return filePath;
    }

    public string CreateMatrixBackup(Dictionary<string, List<Entity>> entitiesToBackup, string rut, string executionId, string environmentUrl, string operationMode)
    {
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var fileName = $"backup_matrix_{rut}_{timestamp}.json";
        var filePath = Path.Combine(_backupFolder, fileName);

        var consolidatedData = new
        {
            isMatrixBackup = true,
            executionId,
            environmentUrl,
            rut,
            operationMode,
            retrievedAt = DateTime.UtcNow.ToString("O"),
            totalRecords = entitiesToBackup.Sum(kvp => kvp.Value.Count),
            entities = entitiesToBackup.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.Select(e => new
                {
                    recordId = e.Id,
                    attributes = e.Attributes.ToDictionary(a => a.Key, a => a.Value),
                    formattedValues = e.FormattedValues.ToDictionary(f => f.Key, f => f.Value)
                }).ToList()
            )
        };

        var options = new JsonSerializerOptions { 
            WriteIndented = true, 
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping 
        };
        var json = JsonSerializer.Serialize(consolidatedData, options);
        File.WriteAllText(filePath, json, new System.Text.UTF8Encoding(true));

        return filePath;
    }
}
