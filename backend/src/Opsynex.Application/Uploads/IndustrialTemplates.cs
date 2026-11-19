using Opsynex.Domain.Common;
using Opsynex.Domain.Enums;

namespace Opsynex.Application.Uploads;

/// <summary>
/// Plantillas tabulares del módulo industrial: una fila por registro y una columna por campo.
/// Los encabezados se reconocen sin importar mayúsculas, acentos ni guiones bajos, en español o
/// con los nombres de la especificación (<c>machine_id</c>, <c>downtime_minutes</c>…). Las demás
/// columnas se ignoran (por ejemplo, el nombre de un operador nunca se lee).
/// </summary>
public static class IndustrialTemplates
{
    /// <summary>Tipo de valor de una columna.</summary>
    public enum ColumnKind
    {
        Date,
        Text,
        Number,
    }

    /// <summary>Columna de una plantilla.</summary>
    /// <param name="Key">Clave interna (nombre de la especificación).</param>
    /// <param name="Header">Encabezado en la plantilla.</param>
    /// <param name="Kind">Tipo de valor.</param>
    /// <param name="Required">Si es obligatoria.</param>
    /// <param name="Help">Explicación para la hoja de instrucciones.</param>
    /// <param name="Synonyms">Otros encabezados aceptados.</param>
    public sealed record Column(string Key, string Header, ColumnKind Kind, bool Required, string Help, params string[] Synonyms)
    {
        /// <summary>Encabezados aceptados, normalizados.</summary>
        public IReadOnlyList<string> Accepted { get; } = [.. new[] { Header, Key }.Concat(Synonyms).Select(TextKey.Normalize).Distinct()];
    }

    public sealed record Template(
        UploadFileType FileType,
        SheetType SheetType,
        string Title,
        string FileName,
        string SheetName,
        IReadOnlyList<Column> Columns,
        IReadOnlyList<object?[]> Example)
    {
        public IEnumerable<Column> RequiredColumns => Columns.Where(c => c.Required);
    }

    public const string Date = "date";
    public const string Machine = "machine_id";
    public const string Line = "line_id";
    public const string UnitsProduced = "units_produced";
    public const string DowntimeMinutes = "downtime_minutes";
    public const string PlannedMinutes = "planned_minutes";
    public const string CycleTime = "cycle_time";
    public const string GoodUnits = "good_units";
    public const string TotalUnits = "total_units";
    public const string LaborCost = "labor_cost";
    public const string MaterialCost = "material_cost";
    public const string EnergyCost = "energy_cost";
    public const string OverheadCost = "overhead_cost";
    public const string ProcessName = "process_name";
    public const string StandardCycleTime = "standard_cycle_time";
    public const string StandardCost = "standard_cost";
    public const string CostPerMinute = "cost_per_minute";

    private static readonly Column DateColumn = new(Date, "Fecha", ColumnKind.Date, true, "Día del registro (AAAA-MM-DD o DD/MM/AAAA).", "dia", "fecha de produccion");
    private static readonly Column MachineColumn = new(Machine, "Máquina", ColumnKind.Text, true, "Código de la máquina, igual en los tres archivos (p. ej. MAQ-01).", "maquina id", "machine", "equipo", "codigo de maquina");

    public static readonly Template Production = new(
        UploadFileType.Production,
        SheetType.Production,
        "PRODUCCIÓN DIARIA POR MÁQUINA",
        "plantilla_produccion.xlsx",
        "Produccion",
        [
            DateColumn,
            MachineColumn,
            new(Line, "Línea", ColumnKind.Text, false, "Línea de producción a la que pertenece la máquina.", "linea id", "line", "linea de produccion"),
            new(PlannedMinutes, "Minutos planificados", ColumnKind.Number, true, "Tiempo planificado de producción del día, en minutos.", "tiempo planificado", "planned time", "minutos programados"),
            new(DowntimeMinutes, "Minutos de parada", ColumnKind.Number, true, "Minutos detenidos dentro del tiempo planificado.", "downtime", "paradas", "minutos parada", "tiempo de parada"),
            new(TotalUnits, "Unidades totales", ColumnKind.Number, true, "Unidades procesadas (buenas + defectuosas).", "unidades procesadas", "total units"),
            new(GoodUnits, "Unidades buenas", ColumnKind.Number, true, "Unidades sin defectos.", "good units", "unidades conformes"),
            new(UnitsProduced, "Unidades producidas", ColumnKind.Number, false, "Unidades terminadas y entregadas (si se deja vacío, se usan las buenas).", "units produced", "produccion"),
            new(CycleTime, "Tiempo de ciclo (s)", ColumnKind.Number, false, "Tiempo de ciclo real promedio, en segundos por unidad (informativo).", "tiempo de ciclo", "ciclo real", "cycle time"),
        ],
        [
            ["2024-10-01", "MAQ-01", "LINEA-1", 960, 45, 1650, 1617, 1617, 33],
            ["2024-10-01", "MAQ-02", "LINEA-1", 960, 60, 1980, 1840, 1840, 26],
        ]);

    public static readonly Template Costs = new(
        UploadFileType.Costs,
        SheetType.Costs,
        "COSTOS POR MÁQUINA",
        "plantilla_costos.xlsx",
        "Costos",
        [
            DateColumn,
            MachineColumn,
            new(LaborCost, "Mano de obra", ColumnKind.Number, false, "Costo de mano de obra asignado a la máquina.", "costo de mano de obra", "labor"),
            new(MaterialCost, "Materiales", ColumnKind.Number, false, "Costo de materiales consumidos.", "costo de materiales", "materia prima", "material"),
            new(EnergyCost, "Energía", ColumnKind.Number, false, "Costo de energía.", "costo de energia", "energy"),
            new(OverheadCost, "Gastos generales", ColumnKind.Number, false, "Gastos generales asignados (mantenimiento, supervisión…).", "costos indirectos", "overhead"),
        ],
        [
            ["2024-10-31", "MAQ-01", 210000, 640000, 95000, 120000],
            ["2024-10-31", "MAQ-02", 230000, 700000, 110000, 125000],
        ]);

    public static readonly Template Standards = new(
        UploadFileType.Standards,
        SheetType.Standards,
        "ESTÁNDARES POR MÁQUINA",
        "plantilla_estandares.xlsx",
        "Estandares",
        [
            MachineColumn,
            new(ProcessName, "Proceso", ColumnKind.Text, false, "Nombre del proceso (descriptivo).", "nombre del proceso", "process"),
            new(StandardCycleTime, "Ciclo estándar (s)", ColumnKind.Number, true, "Tiempo de ciclo estándar en segundos por unidad.", "ciclo estandar", "tiempo de ciclo estandar"),
            new(StandardCost, "Costo estándar por unidad", ColumnKind.Number, false, "Costo estándar de una unidad.", "costo estandar", "costo unitario estandar"),
            new(CostPerMinute, "Costo por minuto", ColumnKind.Number, false, "Costo de un minuto de máquina (para valorar las paradas).", "costo minuto", "costo de un minuto"),
        ],
        [
            ["MAQ-01", "Inyección", 30, 32.5, 55],
            ["MAQ-02", "Inyección", 26, 30, 60],
        ]);

    public static readonly IReadOnlyList<Template> All = [Production, Costs, Standards];

    public static Template? Find(UploadFileType fileType) => All.FirstOrDefault(t => t.FileType == fileType);

    public static bool IsIndustrial(UploadFileType fileType) => fileType is UploadFileType.Production or UploadFileType.Costs or UploadFileType.Standards;

    /// <summary>Al menos una columna de costo es obligatoria en la plantilla de costos.</summary>
    public static readonly IReadOnlyList<string> CostColumns = [LaborCost, MaterialCost, EnergyCost, OverheadCost];
}
