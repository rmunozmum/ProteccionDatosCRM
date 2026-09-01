using System.Threading.Tasks;
using Umayor.Dynamics.DeletePoc.Models;

namespace Umayor.Dynamics.DeletePoc.Services;

public interface IReportQueryExecutor
{
    Task<ReportResponse> ExecuteReportAsync(ReportExecutionRequest request, string executionId, string executedBy);
}
