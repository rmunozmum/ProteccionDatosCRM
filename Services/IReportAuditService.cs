using System;
using System.Threading.Tasks;

namespace Umayor.Dynamics.DeletePoc.Services;

public interface IReportAuditService
{
    Task LogReportExecutionAsync(
        string reportCode,
        string reportName,
        string executedBy,
        DateTime executionDate,
        string parametersJsonFull,
        string responseJsonFull,
        bool success,
        string errorMessage,
        int totalRows,
        long durationMs);
}
