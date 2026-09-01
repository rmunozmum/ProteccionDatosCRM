using System;
using System.Collections.Generic;

namespace Umayor.Dynamics.DeletePoc.Shared.Models;

public static class MassOptionSets
{
    // um_massexecution.um_estado
    public const int HeaderEstadoPendiente = 127120100;
    public const int HeaderEstadoEnProceso = 127120101;
    public const int HeaderEstadoCompletado = 127120102;
    public const int HeaderEstadoCompletadoConErrores = 127120103;

    // um_massexecutiondetail.um_estado
    public const int DetailEstadoPendiente = 127120200;
    public const int DetailEstadoEnProceso = 127120201;
    public const int DetailEstadoConsultado = 127120202;
    public const int DetailEstadoEliminado = 127120203;
    public const int DetailEstadoNoEncontrado = 127120204;
    public const int DetailEstadoError = 127120205;
    public const int DetailEstadoInvalido = 127120206;
    public const int DetailEstadoRequiereConciliacion = 127120207;

    // um_privacyoperationlog.um_operationstatus
    public const int AuditStatusConsultado = 127120000;
    public const int AuditStatusEliminado = 127120001;
    public const int AuditStatusBloqueado = 127120002;
    public const int AuditStatusError = 127120003;
    public const int AuditStatusParcial = 127120004;
    public const int AuditStatusNoEncontrado = 127120005;
    public const int AuditStatusEnProceso = 127120006;
    public const int AuditStatusRequiereConciliacion = 127120008;

    // um_massexecution.um_tratamiento y um_privacyoperationlog.um_operationtype
    public const int TratamientoConsultar = 127120000;
    public const int TratamientoEliminarTodoMenosContacto = 127120001;
    public const int TratamientoEliminarTodo = 127120002;

    public static string HeaderEstadoToString(int value) => value switch
    {
        HeaderEstadoPendiente => "Pendiente",
        HeaderEstadoEnProceso => "EnProceso",
        HeaderEstadoCompletado => "Completado",
        HeaderEstadoCompletadoConErrores => "CompletadoConErrores",
        _ => "Desconocido"
    };

    public static string DetailEstadoToString(int value) => value switch
    {
        DetailEstadoPendiente => "Pendiente",
        DetailEstadoEnProceso => "EnProceso",
        DetailEstadoConsultado => "Consultado",
        DetailEstadoEliminado => "Eliminado",
        DetailEstadoNoEncontrado => "NoEncontrado",
        DetailEstadoError => "Error",
        DetailEstadoInvalido => "Invalido",
        DetailEstadoRequiereConciliacion => "RequiereConciliacion",
        _ => "Desconocido"
    };

    public static string TratamientoToString(int value) => value switch
    {
        TratamientoConsultar => "Consultar",
        TratamientoEliminarTodoMenosContacto => "EliminarTodoMenosContacto",
        TratamientoEliminarTodo => "EliminarTodo",
        _ => "Desconocido"
    };

    public static int StringToTratamiento(string value) => value switch
    {
        "Consultar" => TratamientoConsultar,
        "EliminarTodoMenosContacto" => TratamientoEliminarTodoMenosContacto,
        "EliminarTodo" => TratamientoEliminarTodo,
        _ => 127120007 // Otro
    };
}

public class CreateMassLoteRequest
{
    public string Tratamiento { get; set; } = ""; // "Consultar", "EliminarTodoMenosContacto", "EliminarTodo"
    public string Motivo { get; set; } = "";
    public string ConfirmationText { get; set; } = "";
    public List<string> Identificadores { get; set; } = new();
}

public class CreateMassLoteResponse
{
    public string ExecutionId { get; set; } = ""; // Guid string de um_massexecutionid
    public int TotalRegistros { get; set; }
    public int RegistrosValidos { get; set; }
    public int RegistrosInvalidos { get; set; }
    public int RegistrosDuplicados { get; set; }
    public int TotalLineasArchivo { get; set; }
    public string? SourceFileName { get; set; }
    public string? SourceBlobReference { get; set; }
    public string? SourceFileHash { get; set; }
    public int PartitionSize { get; set; }
    public int EstimatedPartitions { get; set; }
}

public class MassLoteStatusResponse
{
    public string ExecutionId { get; set; } = "";
    public string Tratamiento { get; set; } = "";
    public string Motivo { get; set; } = "";
    public string Estado { get; set; } = "";
    public int TotalRegistros { get; set; }
    public int Procesados { get; set; }
    public int Exitosos { get; set; }
    public int NoEncontrados { get; set; }
    public int Errores { get; set; }
    public int Invalidos { get; set; }
    public int RequiereConciliacion { get; set; }
    public string? Inicio { get; set; }
    public string? Termino { get; set; }
    public string CreadoEl { get; set; } = "";
    public string SolicitadoPor { get; set; } = "";
    public double ProgresoPorcentaje => TotalRegistros > 0 ? Math.Round((double)Procesados / TotalRegistros * 100, 2) : 0;
}

public class MassLoteDetailItem
{
    public string DetailId { get; set; } = "";
    public string Identificador { get; set; } = "";
    public string TipoIdentificador { get; set; } = "";
    public string Estado { get; set; } = "";
    public string? Resultado { get; set; }
    public string? ErrorMessage { get; set; }
    public string? BackupReference { get; set; }
    public string? BackupDate { get; set; }
    public long? BackupSize { get; set; }
    public string? BackupHash { get; set; }
}

public class MassLoteListItem
{
    public string ExecutionId { get; set; } = "";
    public string Nombre { get; set; } = "";
    public string Tratamiento { get; set; } = "";
    public string Estado { get; set; } = "";
    public int TotalRegistros { get; set; }
    public int Procesados { get; set; }
    public int Exitosos { get; set; }
    public int NoEncontrados { get; set; }
    public int Errores { get; set; }
    public int Invalidos { get; set; }
    public int RequiereConciliacion { get; set; }
    public string CreadoEl { get; set; } = "";
    public string SolicitadoPor { get; set; } = "";
}

public class PagedMassLoteDetailsResponse
{
    public string ExecutionId { get; set; } = "";
    public int Page { get; set; }
    public int PageSize { get; set; }
    public bool HasMore { get; set; }
    public List<MassLoteDetailItem> Items { get; set; } = new();
}
