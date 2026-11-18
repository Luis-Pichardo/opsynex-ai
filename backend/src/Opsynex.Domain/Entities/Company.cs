using Opsynex.Domain.Common;
using Opsynex.Domain.Enums;

namespace Opsynex.Domain.Entities;

/// <summary>Empresa cliente analizada por la plataforma.</summary>
public sealed class Company : Entity
{
    public required string Name { get; set; }

    public Industry Industry { get; set; }

    /// <summary>Sector o subindustria libre (p. ej. "Alimentos", "Metalmecánica").</summary>
    public string? Sector { get; set; }

    /// <summary>Moneda ISO 4217 de los estados financieros.</summary>
    public string Currency { get; set; } = "USD";
}
