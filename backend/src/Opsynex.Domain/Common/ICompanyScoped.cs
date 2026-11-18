namespace Opsynex.Domain.Common;

/// <summary>Entidad de datos que pertenece a una empresa (multi-empresa).</summary>
public interface ICompanyScoped
{
    Guid CompanyId { get; }
}

/// <summary>Entidad de datos anual de una empresa (único por CompanyId + Year).</summary>
public interface ICompanyYearScoped : ICompanyScoped
{
    int Year { get; }
}
