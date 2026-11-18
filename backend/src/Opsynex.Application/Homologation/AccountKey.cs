using Opsynex.Domain.Common;
using Opsynex.Domain.Entities;
using Opsynex.Domain.Enums;

namespace Opsynex.Application.Homologation;

/// <summary>
/// Identidad de una cuenta para homologar: alcance (estados o notas), sección (solo para los
/// subtotales de los estados, p. ej. "Total corriente" de activos o de pasivos) y la cuenta.
/// La misma cuenta en cargas siguientes de la empresa tiene la misma clave, así se reutiliza su mapeo.
/// </summary>
public sealed record AccountKey(MappingScope Scope, string Section, string ClientAccount)
{
    /// <summary>Clave de comparación (sin mayúsculas ni acentos).</summary>
    public string Id => $"{Scope}|{Section}|{TextKey.Normalize(ClientAccount)}";

    /// <summary>
    /// Las hojas de estados y plantillas son <c>Statement</c> (rol Primary), las notas son <c>Notes</c>
    /// (rol Breakdown). El balance de comprobación, la nómina y los datos operacionales no se homologan.
    /// </summary>
    public static MappingScope? ScopeFor(SheetType sheetType) => sheetType switch
    {
        SheetType.BalanceSheet or SheetType.IncomeStatement or SheetType.CashFlow or SheetType.CostStructure => MappingScope.Statement,
        SheetType.Notes => MappingScope.Notes,
        _ => null,
    };

    public static MappingRole RoleFor(MappingScope scope) => scope == MappingScope.Statement ? MappingRole.Primary : MappingRole.Breakdown;

    public static AccountKey? For(RawAccountValue raw)
    {
        ArgumentNullException.ThrowIfNull(raw);
        if (ScopeFor(raw.SheetType) is not { } scope)
        {
            return null;
        }

        var section = scope == MappingScope.Statement && raw.RowKind != RowKind.Detail ? TextKey.Normalize(raw.Section) : string.Empty;
        return new AccountKey(scope, section, raw.ClientAccount);
    }

    public static AccountKey For(AccountMapping mapping)
    {
        ArgumentNullException.ThrowIfNull(mapping);
        return new AccountKey(mapping.Scope, mapping.Section, mapping.ClientAccount);
    }
}
