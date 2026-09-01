using System;
using System.Threading.Tasks;

namespace Umayor.Dynamics.DeletePoc.Services;

public class ReportAuditService : IReportAuditService
{
    public Task LogReportExecutionAsync(
        string reportCode,
        string reportName,
        string executedBy,
        DateTime executionDate,
        string parametersJsonFull,
        string responseJsonFull,
        bool success,
        string errorMessage,
        int totalRows,
        long durationMs)
    {
        // Placeholder temporal. La recomendación del usuario es crear `um_privacyreportexecutionlog`.
        // Por ahora, encapsulamos la lógica para que no rompa y la firma quede lista.
        Console.WriteLine($"[AUDIT] Report {reportCode} executed by {executedBy}. Success: {success}. Rows: {totalRows}");
        return Task.CompletedTask;
    }
}
