using System.Collections.Generic;

namespace Umayor.Dynamics.DeletePoc.Models;

public class ReportMetadata
{
    public string ReportCode { get; set; } = "";
    public string ReportName { get; set; } = "";
    public string Description { get; set; } = "";
    public string Module { get; set; } = "";
    public string ExecutionType { get; set; } = "";
    public List<string> RequiredParameters { get; set; } = new();
    public List<string> AllowedExportFormats { get; set; } = new();
    public string Status { get; set; } = ""; // "Disponible", "Pendiente", "Deshabilitado"
    public bool Enabled { get; set; }
}

public class ReportExecutionRequest
{
    public string ReportCode { get; set; } = "";
    public Dictionary<string, string> Parameters { get; set; } = new();
}

public class ReportResponse
{
    public bool Success { get; set; }
    public string ReportCode { get; set; } = "";
    public string ReportName { get; set; } = "";
    public string ExecutionId { get; set; } = "";
    public string ExecutedAt { get; set; } = "";
    public Dictionary<string, string> Parameters { get; set; } = new();
    public ReportSummary Summary { get; set; } = new();
    public object Data { get; set; } = new { };
    public List<string> Warnings { get; set; } = new();
    public List<string> Errors { get; set; } = new();
}

public class ReportSummary
{
    public int TotalRows { get; set; }
    public List<string> Sections { get; set; } = new();
}
