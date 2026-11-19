using System.Text.Json.Nodes;
using Opsynex.Domain.Engines.Diagnosis;
using Opsynex.Domain.Engines.Impact;
using Opsynex.Domain.Enums;

namespace Opsynex.Domain.Engines.Industrial;

/// <summary>Problema industrial detectado, con su impacto ya valorado.</summary>
public sealed record IndustrialProblem(
    string Code,
    string Title,
    ProblemCategory Category,
    ImpactCategory ImpactCategory,
    Severity Severity,
    string Description,
    string Recommendation,
    JsonObject Evidence,
    ImpactEstimate Impact);

/// <summary>
/// Reglas industriales (PLATAFORMA_DE_ANALISIS §6) e impacto (§7). Se evalúan con los agregados
/// anuales de cada máquina; cada regla genera como mucho un problema por año que lista las
/// máquinas afectadas.
/// <list type="bullet">
/// <item><b>HIGH_DOWNTIME</b> (paradas / planificado &gt; umbral): impacto = minutos de parada × costo por minuto.</item>
/// <item><b>HIGH_SCRAP</b> (scrap &gt; 5 %): impacto = (unidades totales − buenas) × costo unitario.</item>
/// <item><b>LOW_OEE</b> (OEE &lt; 60 %): impacto = pérdida por velocidad + paradas y scrap de la máquina que
/// no estén ya en HIGH_DOWNTIME o HIGH_SCRAP (así el total no cuenta dos veces lo mismo).</item>
/// <item><b>COST_PER_UNIT_ABOVE_STANDARD</b>: impacto = (costo por unidad − estándar) × unidades producidas,
/// menos las pérdidas ya atribuidas a la máquina por las otras reglas.</item>
/// </list>
/// </summary>
public static class IndustrialDiagnosisEngine
{
    public const string LowOee = "LOW_OEE";
    public const string HighScrap = "HIGH_SCRAP";
    public const string HighDowntime = "HIGH_DOWNTIME";
    public const string CostAboveStandard = "COST_PER_UNIT_ABOVE_STANDARD";

    /// <summary>Códigos de los problemas industriales (el pipeline financiero no los toca).</summary>
    public static readonly IReadOnlySet<string> Codes = new HashSet<string>(StringComparer.Ordinal) { LowOee, HighScrap, HighDowntime, CostAboveStandard };

    private static readonly string[] MonthNames = ["ene", "feb", "mar", "abr", "may", "jun", "jul", "ago", "sep", "oct", "nov", "dic"];

    /// <param name="machines">Agregados anuales por máquina (nivel <c>Machine</c>, sin mes).</param>
    /// <param name="total">Agregado anual de la planta (para la evidencia).</param>
    /// <param name="from">Primer día con datos del año.</param>
    /// <param name="to">Último día con datos del año.</param>
    /// <param name="settings">Umbrales y severidades.</param>
    /// <param name="currency">Moneda de la empresa (para los textos).</param>
    public static IReadOnlyList<IndustrialProblem> Evaluate(
        IReadOnlyList<IndustrialAggregate> machines,
        IndustrialAggregate? total,
        DateOnly from,
        DateOnly to,
        IndustrialSettings settings,
        string? currency)
    {
        ArgumentNullException.ThrowIfNull(machines);
        ArgumentNullException.ThrowIfNull(settings);
        var period = PeriodLabel(from, to);
        var problems = new List<IndustrialProblem>();

        // Pérdidas ya atribuidas por máquina (para no contarlas dos veces).
        var attributed = machines.ToDictionary(m => m.MachineId!, _ => 0m, StringComparer.OrdinalIgnoreCase);

        var downtime = machines.Where(m => m.Kpis.DowntimeRatio > settings.HighDowntimeThreshold).ToList();
        if (downtime.Count > 0)
        {
            problems.Add(HighDowntimeProblem(downtime, total, settings, currency, period, from, to, attributed));
        }

        var scrap = machines.Where(m => m.Kpis.ScrapRate > settings.HighScrapThreshold).ToList();
        if (scrap.Count > 0)
        {
            problems.Add(HighScrapProblem(scrap, total, settings, currency, period, from, to, attributed));
        }

        var oee = machines.Where(m => m.Kpis.Oee < settings.LowOeeThreshold).ToList();
        if (oee.Count > 0)
        {
            problems.Add(LowOeeProblem(oee, total, settings, currency, period, from, to, downtime, scrap, attributed));
        }

        var cost = machines
            .Where(m => m.Kpis.CostPerUnit is { } cpu && m.Kpis.StandardCostPerUnit is > 0m && cpu > m.Kpis.StandardCostPerUnit * (1m + settings.CostAboveStandardTolerance))
            .ToList();
        if (cost.Count > 0)
        {
            problems.Add(CostProblem(cost, total, settings, currency, period, from, to, attributed));
        }

        return problems;
    }

    /// <summary>Texto del período: «oct 2024», «oct–dic 2024» u «oct 2024–feb 2025».</summary>
    public static string PeriodLabel(DateOnly from, DateOnly to)
    {
        var start = MonthNames[from.Month - 1];
        var end = MonthNames[to.Month - 1];
        if (from.Year == to.Year)
        {
            return from.Month == to.Month ? $"{start} {from.Year}" : $"{start}–{end} {from.Year}";
        }

        return $"{start} {from.Year}–{end} {to.Year}";
    }

    /// <summary>Frase de impacto que se agrega a la recomendación.</summary>
    public static string? Sentence(ImpactEstimate impact, string? currency, string period)
    {
        ArgumentNullException.ThrowIfNull(impact);
        return impact.EstimatedLoss is { } loss && loss > 0m
            ? $"Impacto estimado: {Fmt.Money(loss, currency)} en el período analizado ({period})."
            : null;
    }

    private static IndustrialProblem HighDowntimeProblem(
        List<IndustrialAggregate> affected, IndustrialAggregate? total, IndustrialSettings settings, string? currency,
        string period, DateOnly from, DateOnly to, Dictionary<string, decimal> attributed)
    {
        var threshold = settings.HighDowntimeThreshold;
        var loss = SumOrNull(affected.Select(m => m.Losses.DowntimeCost));
        Attribute(affected, m => m.Losses.DowntimeCost, attributed);
        var worst = affected.Max(m => m.Kpis.DowntimeRatio!.Value);
        var minutes = affected.Sum(m => m.Sums.DowntimeMinutes);
        var severity = settings.SeverityOf(HighDowntime);
        var list = MachineList(affected, m => Fmt.Pct(m.Kpis.DowntimeRatio!.Value));
        var impact = new ImpactEstimate(
            loss,
            null,
            Fmt.R((worst - threshold) / threshold * 100m, 2),
            severity,
            "minutos de parada × costo por minuto",
            loss is null ? "Faltan los costos o el costo por minuto de las máquinas para valorar las paradas." : null,
            new JsonObject
            {
                ["downtimeMinutes"] = minutes,
                ["machines"] = MachinesJson(affected, m => new JsonObject
                {
                    ["downtimeMinutes"] = m.Sums.DowntimeMinutes,
                    ["costPerMinute"] = m.CostPerMinute,
                    ["downtimeCost"] = m.Losses.DowntimeCost,
                }),
            });

        return new IndustrialProblem(
            HighDowntime,
            "Paradas de máquina elevadas",
            ProblemCategory.Operations,
            ImpactCategory.OperationalWaste,
            severity,
            $"Las paradas consumen más del {Fmt.Pct(threshold)} del tiempo planificado en {list}: {Number(minutes)} minutos detenidos en {period}.",
            "Registra el motivo de cada parada y analízalas con un Pareto (averías, cambios de formato, falta de material); " +
            "implementa mantenimiento preventivo en las máquinas afectadas y reduce los tiempos de cambio (SMED).",
            Evidence("DowntimeRatio", affected, total?.Kpis.DowntimeRatio, threshold, from, to, m => m.Kpis.DowntimeRatio),
            impact);
    }

    private static IndustrialProblem HighScrapProblem(
        List<IndustrialAggregate> affected, IndustrialAggregate? total, IndustrialSettings settings, string? currency,
        string period, DateOnly from, DateOnly to, Dictionary<string, decimal> attributed)
    {
        var threshold = settings.HighScrapThreshold;
        var loss = SumOrNull(affected.Select(m => m.Losses.ScrapCost));
        Attribute(affected, m => m.Losses.ScrapCost, attributed);
        var worst = affected.Max(m => m.Kpis.ScrapRate!.Value);
        var units = affected.Sum(m => m.Sums.ScrapUnits);
        var severity = settings.SeverityOf(HighScrap);
        var list = MachineList(affected, m => Fmt.Pct(m.Kpis.ScrapRate!.Value));
        var impact = new ImpactEstimate(
            loss,
            null,
            Fmt.R((worst - threshold) / threshold * 100m, 2),
            severity,
            "(unidades totales − unidades buenas) × costo unitario",
            loss is null ? "Faltan el costo estándar o los costos de las máquinas para valorar el scrap." : null,
            new JsonObject
            {
                ["scrapUnits"] = units,
                ["machines"] = MachinesJson(affected, m => new JsonObject
                {
                    ["totalUnits"] = m.Sums.TotalUnits,
                    ["goodUnits"] = m.Sums.GoodUnits,
                    ["unitCost"] = m.UnitCost,
                    ["scrapCost"] = m.Losses.ScrapCost,
                }),
            });

        return new IndustrialProblem(
            HighScrap,
            "Scrap (desperdicio) elevado",
            ProblemCategory.Operations,
            ImpactCategory.OperationalWaste,
            severity,
            $"El scrap supera el {Fmt.Pct(threshold)} en {list}: {Number(units)} unidades defectuosas en {period}.",
            "Clasifica los defectos por tipo y causa, revisa los parámetros del proceso y la calidad de la materia prima, " +
            "y agrega controles en la estación (poka-yoke) para detectar los defectos antes de que se acumulen.",
            Evidence("ScrapRate", affected, total?.Kpis.ScrapRate, threshold, from, to, m => m.Kpis.ScrapRate),
            impact);
    }

    private static IndustrialProblem LowOeeProblem(
        List<IndustrialAggregate> affected, IndustrialAggregate? total, IndustrialSettings settings, string? currency,
        string period, DateOnly from, DateOnly to, List<IndustrialAggregate> downtime, List<IndustrialAggregate> scrap,
        Dictionary<string, decimal> attributed)
    {
        var threshold = settings.LowOeeThreshold;
        var breakdown = new JsonObject();
        decimal? loss = null;
        foreach (var machine in affected)
        {
            var countDowntime = !downtime.Any(d => Same(d, machine));
            var countScrap = !scrap.Any(s => Same(s, machine));
            var parts = new[]
            {
                machine.Losses.SpeedLossCost,
                countDowntime ? machine.Losses.DowntimeCost : 0m,
                countScrap ? machine.Losses.ScrapCost : 0m,
            };
            var machineLoss = SumOrNull(parts);
            loss = machineLoss is null ? loss : (loss ?? 0m) + machineLoss.Value;
            attributed[machine.MachineId!] += machineLoss ?? 0m;
            breakdown[machine.MachineId!] = new JsonObject
            {
                ["oee"] = machine.Kpis.Oee,
                ["availability"] = machine.Kpis.Availability,
                ["performance"] = machine.Kpis.Performance,
                ["quality"] = machine.Kpis.Quality,
                ["speedLossCost"] = machine.Losses.SpeedLossCost,
                ["downtimeCostIncluded"] = countDowntime ? machine.Losses.DowntimeCost : null,
                ["scrapCostIncluded"] = countScrap ? machine.Losses.ScrapCost : null,
            };
        }

        var worst = affected.Min(m => m.Kpis.Oee!.Value);
        var severity = settings.SeverityOf(LowOee);
        var list = MachineList(affected, m => Fmt.Pct(m.Kpis.Oee!.Value));
        var weakest = affected.OrderBy(m => m.Kpis.Oee).First();
        var impact = new ImpactEstimate(
            loss,
            null,
            Fmt.R((threshold - worst) / threshold * 100m, 2),
            severity,
            "pérdida por velocidad + paradas y scrap no contabilizados en otros problemas",
            loss is null ? "Faltan los costos o el costo por minuto de las máquinas para valorar la pérdida de eficiencia." : null,
            new JsonObject { ["machines"] = breakdown });

        return new IndustrialProblem(
            LowOee,
            "OEE bajo (eficiencia de equipos)",
            ProblemCategory.Operations,
            ImpactCategory.ProductivityLoss,
            severity,
            $"El OEE está por debajo del {Fmt.Pct(threshold)} en {list} en {period}. En {weakest.MachineId}: disponibilidad " +
            $"{Fmt.Pct(weakest.Kpis.Availability ?? 0m)}, rendimiento {Fmt.Pct(weakest.Kpis.Performance ?? 0m)} y calidad {Fmt.Pct(weakest.Kpis.Quality ?? 0m)}.",
            $"Ataca primero el componente más bajo del OEE ({Weakest(weakest.Kpis)}): {Advice(weakest.Kpis)} " +
            "Mide el OEE por turno para seguir la mejora semana a semana.",
            Evidence("Oee", affected, total?.Kpis.Oee, threshold, from, to, m => m.Kpis.Oee),
            impact);
    }

    private static IndustrialProblem CostProblem(
        List<IndustrialAggregate> affected, IndustrialAggregate? total, IndustrialSettings settings, string? currency,
        string period, DateOnly from, DateOnly to, Dictionary<string, decimal> attributed)
    {
        var breakdown = new JsonObject();
        decimal loss = 0m;
        foreach (var machine in affected)
        {
            // Con las sumas exactas (no con los costos por unidad redondeados): costo real − costo estándar de lo producido.
            var excess = machine.Sums.Costs is { } costs && machine.Sums.StandardCostAmount is { } standard
                ? Math.Max(0m, costs.Total - standard)
                : Math.Max(0m, (machine.Kpis.CostPerUnit!.Value - machine.Kpis.StandardCostPerUnit!.Value) * machine.Sums.UnitsProduced);
            var already = attributed.GetValueOrDefault(machine.MachineId!);
            var residual = decimal.Round(Math.Max(0m, excess - already), 2);
            loss += residual;
            breakdown[machine.MachineId!] = new JsonObject
            {
                ["costPerUnit"] = machine.Kpis.CostPerUnit,
                ["standardCostPerUnit"] = machine.Kpis.StandardCostPerUnit,
                ["unitsProduced"] = machine.Sums.UnitsProduced,
                ["excessCost"] = decimal.Round(excess, 2),
                ["alreadyAttributed"] = decimal.Round(already, 2),
            };
        }

        var worst = affected.Max(m => (m.Kpis.CostPerUnit!.Value / m.Kpis.StandardCostPerUnit!.Value) - 1m);
        var severity = settings.SeverityOf(CostAboveStandard);
        var list = MachineList(affected, m => $"{Fmt.Money2(m.Kpis.CostPerUnit!.Value, currency)} frente a {Fmt.Money2(m.Kpis.StandardCostPerUnit!.Value, currency)}");
        var impact = new ImpactEstimate(
            loss,
            null,
            Fmt.R(worst * 100m, 2),
            severity,
            "(costo por unidad − costo estándar) × unidades producidas − pérdidas ya atribuidas a la máquina",
            null,
            new JsonObject { ["machines"] = breakdown });

        return new IndustrialProblem(
            CostAboveStandard,
            "Costo por unidad sobre el estándar",
            ProblemCategory.CostEfficiency,
            ImpactCategory.ExcessCost,
            severity,
            $"El costo por unidad supera el estándar en más de un {Fmt.Pct(settings.CostAboveStandardTolerance)} en {list} ({period}).",
            "Compara la composición del costo (mano de obra, materiales, energía y gastos generales) con la del estándar, " +
            "revisa los consumos de material por unidad y actualiza el estándar si el proceso cambió.",
            Evidence("CostPerUnit", affected, total?.Kpis.CostPerUnit, settings.CostAboveStandardTolerance, from, to, m => m.Kpis.CostPerUnit),
            impact);
    }

    private static JsonObject Evidence(
        string kpi, List<IndustrialAggregate> affected, decimal? plantValue, decimal threshold, DateOnly from, DateOnly to,
        Func<IndustrialAggregate, decimal?> value) => new()
    {
        ["kind"] = "industrial",
        ["kpi"] = kpi,
        ["threshold"] = threshold,
        ["plantValue"] = plantValue,
        ["machinesAffected"] = string.Join(", ", affected.Select(m => m.MachineId)),
        ["from"] = from.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
        ["to"] = to.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
        ["values"] = MachinesJson(affected, m => new JsonObject { ["lineId"] = m.LineId, ["value"] = value(m) }),
    };

    private static JsonObject MachinesJson(IEnumerable<IndustrialAggregate> machines, Func<IndustrialAggregate, JsonObject> build)
    {
        var json = new JsonObject();
        foreach (var machine in machines)
        {
            json[machine.MachineId!] = build(machine);
        }

        return json;
    }

    private static string MachineList(List<IndustrialAggregate> machines, Func<IndustrialAggregate, string> value) =>
        string.Join(", ", machines.Select(m => $"{m.MachineId} ({value(m)})"));

    private static void Attribute(List<IndustrialAggregate> machines, Func<IndustrialAggregate, decimal?> loss, Dictionary<string, decimal> attributed)
    {
        foreach (var machine in machines)
        {
            attributed[machine.MachineId!] += loss(machine) ?? 0m;
        }
    }

    private static decimal? SumOrNull(IEnumerable<decimal?> values)
    {
        decimal? sum = null;
        foreach (var value in values)
        {
            if (value is { } v)
            {
                sum = (sum ?? 0m) + v;
            }
        }

        return sum;
    }

    private static bool Same(IndustrialAggregate a, IndustrialAggregate b) =>
        string.Equals(a.MachineId, b.MachineId, StringComparison.OrdinalIgnoreCase);

    private static string Weakest(IndustrialKpis kpis) =>
        new[] { ("disponibilidad", kpis.Availability), ("rendimiento", kpis.Performance), ("calidad", kpis.Quality) }
            .Where(c => c.Item2 is not null)
            .OrderBy(c => c.Item2)
            .Select(c => c.Item1)
            .FirstOrDefault() ?? "disponibilidad";

    private static string Advice(IndustrialKpis kpis) => Weakest(kpis) switch
    {
        "rendimiento" => "la máquina trabaja más lento que su ciclo estándar; revisa microparadas, velocidad de operación y el abastecimiento de material.",
        "calidad" => "las unidades defectuosas restan producción útil; estabiliza el proceso y revisa los defectos más frecuentes.",
        _ => "las paradas restan tiempo productivo; prioriza el mantenimiento preventivo y la reducción de cambios de formato.",
    };

    private static string Number(decimal value) => decimal.Round(value, 0).ToString("#,##0", System.Globalization.CultureInfo.GetCultureInfo("es"));
}
