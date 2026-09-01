namespace Umayor.Dynamics.DeletePoc.Models;

public class AppSettings
{
    public DataverseSettings Dataverse { get; set; } = new();
    public OperationSettings Operation { get; set; } = new();
    public SafetySettings Safety { get; set; } = new();
    public BackupsSettings Backups { get; set; } = new();
    public LogsSettings Logs { get; set; } = new();
}

public class LogsSettings
{
    public string Directory { get; set; } = @"C:\home\data\logs";
}

public class BackupsSettings
{
    public string Directory { get; set; } = @"C:\home\data\backups";
}

public class DataverseSettings
{
    public string Url { get; set; } = string.Empty;
    public string AuthType { get; set; } = "ClientSecret";
    public string TenantId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string TdsServer { get; set; } = string.Empty;
    public string TdsDatabase { get; set; } = string.Empty;
}
