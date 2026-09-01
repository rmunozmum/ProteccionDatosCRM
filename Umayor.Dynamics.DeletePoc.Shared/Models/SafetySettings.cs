namespace Umayor.Dynamics.DeletePoc.Models;

public class SafetySettings
{
    public string RequireEnvironmentContains { get; set; } = "qas";
    public string RequireConfirmationText { get; set; } = "ELIMINAR";
    public bool DeletionEnabled { get; set; } = false;
}
