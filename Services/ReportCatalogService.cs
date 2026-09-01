using System.Collections.Generic;
using Umayor.Dynamics.DeletePoc.Models;

namespace Umayor.Dynamics.DeletePoc.Services;

public class ReportCatalogService
{
    public List<ReportMetadata> GetCatalog()
    {
        return new List<ReportMetadata>
        {
            new ReportMetadata
            {
                ReportCode = "LPD-R01",
                ReportName = "Informe de Datos Personales",
                Description = "Consulta de datos personales e información asociada a un titular específico.",
                Module = "Derechos ARCO",
                ExecutionType = "Por RUT",
                RequiredParameters = new List<string> { "rut" },
                AllowedExportFormats = new List<string> { "JSON", "PDF", "Excel" },
                Status = "Disponible",
                Enabled = true
            },
            new ReportMetadata { ReportCode = "LPD-R02", ReportName = "Registro de Actividades de Tratamiento (RAT)", Status = "Pendiente", Enabled = false, Description = "Pendiente de implementación." },
            new ReportMetadata { ReportCode = "LPD-R03", ReportName = "Inventario de Datos", Status = "Pendiente", Enabled = false, Description = "Pendiente de implementación." },
            new ReportMetadata { ReportCode = "LPD-R04", ReportName = "Datos Sensibles", Status = "Pendiente", Enabled = false, Description = "Pendiente de implementación." },
            new ReportMetadata { ReportCode = "LPD-R05", ReportName = "Riesgo por Tabla", Status = "Pendiente", Enabled = false, Description = "Pendiente de implementación." },
            new ReportMetadata { ReportCode = "LPD-R06", ReportName = "Retención", Status = "Pendiente", Enabled = false, Description = "Pendiente de implementación." },
            new ReportMetadata { ReportCode = "LPD-R07", ReportName = "Cancelación / Supresión", Status = "Pendiente", Enabled = false, Description = "Pendiente de implementación." },
            new ReportMetadata { ReportCode = "LPD-R08", ReportName = "Calidad de Datos", Status = "Pendiente", Enabled = false, Description = "Pendiente de implementación." },
            new ReportMetadata { ReportCode = "LPD-R09", ReportName = "Relaciones del Titular", Status = "Pendiente", Enabled = false, Description = "Pendiente de implementación." },
            new ReportMetadata { ReportCode = "LPD-R10", ReportName = "Ejecutivo de Cumplimiento", Status = "Pendiente", Enabled = false, Description = "Pendiente de implementación." },
            new ReportMetadata { ReportCode = "LPD-R11", ReportName = "Auditoría y Accesos", Status = "Pendiente", Enabled = false, Description = "Pendiente de implementación." },
            new ReportMetadata { ReportCode = "LPD-R12", ReportName = "Evidencias e Implementación", Status = "Pendiente", Enabled = false, Description = "Pendiente de implementación." }
        };
    }
}
