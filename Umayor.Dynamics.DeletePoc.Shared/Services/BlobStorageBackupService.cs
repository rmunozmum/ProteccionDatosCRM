using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Azure.Storage.Blobs;
using Azure.Identity;
using Microsoft.Xrm.Sdk;
using Microsoft.Extensions.Configuration;

namespace Umayor.Dynamics.DeletePoc.Shared.Services;

public class BlobBackupMetadata
{
    public string BackupReference { get; set; } = "";
    public DateTime BackupDate { get; set; }
    public long BackupSize { get; set; }
    public string BackupHash { get; set; } = "";
}

public class BlobSourceFileMetadata
{
    public string BlobReference { get; set; } = "";
    public DateTime UploadDate { get; set; }
    public long Size { get; set; }
    public string Hash { get; set; } = "";
}

public class BlobStorageBackupService
{
    private readonly BlobContainerClient _containerClient;

    public BlobStorageBackupService(IConfiguration configuration)
    {
        string connectionString = configuration["AzureStorage:ConnectionString"] ?? "";
        string containerName = configuration["AzureStorage:ContainerName"] ?? "privacy-backups";
        string accountUrl = configuration["AzureStorage:AccountUrl"] ?? "";

        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            _containerClient = new BlobContainerClient(connectionString, containerName);
        }
        else if (!string.IsNullOrWhiteSpace(accountUrl))
        {
            var uri = new Uri(new Uri(accountUrl), containerName);
            _containerClient = new BlobContainerClient(uri, new DefaultAzureCredential());
        }
        else
        {
            // Fallback para pruebas locales si no hay configuración
            _containerClient = new BlobContainerClient("UseDevelopmentStorage=true", containerName);
        }
    }

    public async Task<BlobBackupMetadata> UploadMatrixBackupAsync(
        string massExecutionId, 
        string detailId, 
        Dictionary<string, List<Entity>> entitiesToBackup, 
        string identifier, 
        string environmentUrl, 
        string operationMode)
    {
        var consolidatedData = new
        {
            isMatrixBackup = true,
            executionId = detailId, // GUID de Detalle es el individualExecutionId
            parentExecutionId = massExecutionId,
            environmentUrl,
            rut = identifier,
            operationMode,
            retrievedAt = DateTime.UtcNow,
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

        var options = new JsonSerializerOptions 
        { 
            WriteIndented = true, 
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping 
        };

        string json = JsonSerializer.Serialize(consolidatedData, options);
        byte[] bytes = Encoding.UTF8.GetBytes(json);

        // Generar Hash SHA-256
        string hash = ComputeSha256Hash(bytes);
        long size = bytes.Length;
        DateTime uploadDate = DateTime.UtcNow;

        string blobName = $"mass-executions/{massExecutionId}/{detailId}_backup.json";
        
        await _containerClient.CreateIfNotExistsAsync();
        var blobClient = _containerClient.GetBlobClient(blobName);
        
        using (var stream = new MemoryStream(bytes))
        {
            await blobClient.UploadAsync(stream, overwrite: true);
        }

        return new BlobBackupMetadata
        {
            BackupReference = blobName,
            BackupDate = uploadDate,
            BackupSize = size,
            BackupHash = hash
        };
    }

    public async Task<string> DownloadBackupAsync(string blobReference)
    {
        var blobClient = _containerClient.GetBlobClient(blobReference);
        if (!await blobClient.ExistsAsync())
        {
            throw new FileNotFoundException($"El respaldo especificado no existe en Azure Blob Storage: {blobReference}");
        }

        var response = await blobClient.DownloadContentAsync();
        return response.Value.Content.ToString();
    }

    public async Task<BlobSourceFileMetadata> UploadSourceFileAsync(
        string massExecutionId,
        string originalFileName,
        byte[] bytes)
    {
        string safeName = string.Join("_", originalFileName.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
        if (string.IsNullOrWhiteSpace(safeName))
        {
            safeName = "nomina.txt";
        }

        string hash = ComputeSha256Hash(bytes);
        DateTime uploadDate = DateTime.UtcNow;
        string blobName = $"mass-executions/{massExecutionId}/source/{DateTime.UtcNow:yyyyMMddHHmmss}_{safeName}";

        await _containerClient.CreateIfNotExistsAsync();
        var blobClient = _containerClient.GetBlobClient(blobName);

        using (var stream = new MemoryStream(bytes))
        {
            await blobClient.UploadAsync(stream, overwrite: true);
        }

        return new BlobSourceFileMetadata
        {
            BlobReference = blobName,
            UploadDate = uploadDate,
            Size = bytes.Length,
            Hash = hash
        };
    }

    private static string ComputeSha256Hash(byte[] bytes)
    {
        using (var sha256 = SHA256.Create())
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
