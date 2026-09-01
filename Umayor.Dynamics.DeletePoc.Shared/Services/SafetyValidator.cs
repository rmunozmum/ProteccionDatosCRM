using System;
using Umayor.Dynamics.DeletePoc.Models;

namespace Umayor.Dynamics.DeletePoc.Services;

public class SafetyValidator
{
    public void Validate(AppSettings settings)
    {
        // 1. Environment validation applies to ALL operations (Consult, Delete, etc.) to ensure we never touch PROD by accident
        if (!settings.Dataverse.Url.Contains(settings.Safety.RequireEnvironmentContains, StringComparison.OrdinalIgnoreCase))
        {
            throw new Exception($"SEGURIDAD: La URL del ambiente ({settings.Dataverse.Url}) no contiene la palabra requerida '{settings.Safety.RequireEnvironmentContains}'. ABORTANDO.");
        }
    }
}
