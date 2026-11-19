using Opsynex.Domain.Common;

namespace Opsynex.Domain.Entities;

/// <summary>Resumen ejecutivo del año generado con IA (Claude). Único por empresa y año.</summary>
public sealed class ExecutiveSummary : Entity, ICompanyYearScoped
{
    public Guid CompanyId { get; set; }

    public int Year { get; set; }

    /// <summary>Párrafos del resumen (3 a 5) (columna JSON).</summary>
    public IList<string> Paragraphs { get; set; } = [];

    /// <summary>Tres prioridades para los próximos meses (columna JSON).</summary>
    public IList<string> Priorities { get; set; } = [];

    /// <summary>Modelo de Claude que lo generó.</summary>
    public required string Model { get; set; }

    /// <summary>Hash de los datos de entrada: si no cambian, no se vuelve a generar.</summary>
    public required string InputHash { get; set; }

    public DateTime GeneratedAt { get; set; }
}
