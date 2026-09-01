using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Umayor.Dynamics.DeletePoc.Models;

namespace Umayor.Dynamics.DeletePoc.Services;

public class ReportQueryExecutor : IReportQueryExecutor
{
    private readonly AppSettings _settings;
    private readonly DataverseConnectionFactory _factory;
    private readonly LogService _logService;

    public ReportQueryExecutor(AppSettings settings, DataverseConnectionFactory factory, LogService logService)
    {
        _settings = settings;
        _factory = factory;
        _logService = logService;
    }

    public async Task<ReportResponse> ExecuteReportAsync(ReportExecutionRequest request, string executionId, string executedBy)
    {
        var response = new ReportResponse
        {
            ExecutionId = executionId,
            ReportCode = request.ReportCode,
            ExecutedAt = DateTime.UtcNow.ToString("o"),
            Parameters = request.Parameters
        };

        if (request.ReportCode != "LPD-R01")
        {
            response.Success = false;
            response.Errors.Add("Reporte no disponible o no implementado.");
            return response;
        }

        response.ReportName = "Informe de Datos Personales";

        request.Parameters.TryGetValue("rut", out string? rut);
        request.Parameters.TryGetValue("pasaporte", out string? pasaporte);
        request.Parameters.TryGetValue("motivo", out string? motivo);
        request.Parameters.TryGetValue("usuarioEjecutor", out string? usuarioEjecutor);
        if (string.IsNullOrEmpty(usuarioEjecutor)) usuarioEjecutor = executedBy;
        request.Parameters.TryGetValue("areaEjecutor", out string? areaEjecutor);

        try
        {
            using var client = _factory.CreateClient(_settings.Dataverse);

            var localSettings = new AppSettings
            {
                Dataverse = _settings.Dataverse,
                Safety = _settings.Safety,
                Operation = new OperationSettings { Rut = rut ?? "", Pasaporte = pasaporte ?? "", Mode = "Consultar" }
            };

            var rutService = new RutMatrixService(client, _logService, localSettings, executionId);
            dynamic? matrixData = rutService.Execute("Consultar");

            if (matrixData?.found != true)
            {
                response.Success = false;
                response.Errors.Add(matrixData?.message ?? "RUT no encontrado o error en matriz.");
                return response;
            }

            // Mapear resultado final
            var encabezado = new List<object>
            {
                new { campo = "Fecha emisión", valor = DateTime.Now.ToString("dd/MM/yyyy HH:mm") },
                new { campo = "Solicitante", valor = usuarioEjecutor },
                new { campo = "Área", valor = areaEjecutor ?? "N/A" },
                new { campo = "Motivo", valor = motivo ?? "N/A" }
            };

            var titularInfo = matrixData?.contactSummary;
            var titular = new
            {
                nombre = titularInfo?.fullname ?? "",
                rut = titularInfo?.rutCompleto ?? "",
                pasaporte = titularInfo?.pasaporte ?? "",
                correoPrincipal = titularInfo?.emailPrincipal ?? "",
                telefonoPrincipal = titularInfo?.telefonoMovil ?? titularInfo?.telefonoFijo ?? ""
            };

            var entidades = new List<object>();
            int totalRows = 0;
            
            var matrix = matrixData?.matrix;
            if (matrix != null)
            {
                var sensitiveTables = new[] { "incident", "wit_procesodepostulacion", "wit_solicituddeadmisiondirecta", "wit_historicodatosofertaacademica", "wit_historicocarreramatriculada", "annotation_incident" };
                
                foreach (var row in matrix)
                {
                    int qty = (int)(row.CantidadTotal ?? 0);
                    totalRows += qty;
                    
                    string tablaName = row.EntidadRelacionada?.ToString() ?? "";
                    bool contieneDatosPersonales = true;
                    bool contieneDatosSensibles = sensitiveTables.Contains(tablaName);
                    
                    string criticidad = "Baja";
                    if (contieneDatosSensibles)
                    {
                        criticidad = "Alta";
                    }
                    else if (contieneDatosPersonales)
                    {
                        criticidad = "Media";
                    }

                    entidades.Add(new
                    {
                        tabla = tablaName,
                        criterioRelacion = row.CampoRelacion?.ToString() ?? "",
                        contieneDatosPersonales = contieneDatosPersonales,
                        contieneDatosSensibles = contieneDatosSensibles,
                        criticidad = criticidad,
                        cantidadRegistros = qty
                    });
                    response.Summary.Sections.Add(tablaName);
                }
            }

            response.Summary.TotalRows = totalRows;

            var observaciones = new List<object>
            {
                new { campo = "Marco Legal Principal", valor = "Ley 21.719 sobre Protección de Datos Personales, en el contexto del ejercicio de derechos ARCO." },
                new { campo = "Marco Legal Secundario", valor = "Ley 19.628 de Protección de Datos de Carácter Personal (Chile)." },
                new { campo = "Limitaciones", valor = "Este reporte extrae datos de Dataverse usando el servicio RutMatrixService." }
            };

            var trazabilidad = new List<object>
            {
                new { campo = "Execution ID", valor = executionId },
                new { campo = "Origen", valor = "API.Dataverse.SDK" },
                new { campo = "Operation Mode", valor = "Consultar" }
            };

            response.Data = new
            {
                encabezado,
                titular,
                entidades,
                observacionesLegales = observaciones,
                trazabilidad
            };

            response.Success = true;
        }
        catch (Exception ex)
        {
            response.Success = false;
            response.Errors.Add($"Error general: {ex.Message}");
        }

        return await Task.FromResult(response);
    }
}
