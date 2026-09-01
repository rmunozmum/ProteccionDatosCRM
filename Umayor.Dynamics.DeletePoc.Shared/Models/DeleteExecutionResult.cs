namespace Umayor.Dynamics.DeletePoc.Models;

public enum DeleteExecutionResult
{
    Success,
    Cancelled,
    NotFound,
    BlockedEntity,
    Error,
    Simulated
}
