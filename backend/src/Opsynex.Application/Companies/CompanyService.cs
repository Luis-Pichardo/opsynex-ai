using FluentValidation;
using Opsynex.Application.Abstractions.Persistence;
using Opsynex.Application.Common.Exceptions;
using Opsynex.Domain.Entities;

namespace Opsynex.Application.Companies;

/// <summary>
/// Empresas. Listar y ver: todos los roles (el filtro por empresa limita a la propia).
/// Crear, modificar y eliminar: solo Admin (política aplicada en el API).
/// </summary>
public sealed class CompanyService(
    ICompanyRepository companies,
    IUnitOfWork unitOfWork,
    IValidator<CompanyRequest> validator)
{
    public const string DefaultCurrency = "USD";

    public async Task<IReadOnlyList<CompanyDto>> ListAsync(string? search, CancellationToken cancellationToken)
    {
        var term = search?.Trim();
        var result = await companies.ListAsync(c => string.IsNullOrEmpty(term) || c.Name.Contains(term), cancellationToken);
        return result
            .OrderBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(CompanyDto.From)
            .ToList();
    }

    public async Task<CompanyDto> GetAsync(Guid id, CancellationToken cancellationToken) =>
        CompanyDto.From(await FindAsync(id, cancellationToken));

    public async Task<CompanyDto> CreateAsync(CompanyRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await validator.ValidateAndThrowAsync(request, cancellationToken);

        var company = new Company { Name = request.Name.Trim() };
        Apply(company, request);
        await companies.AddAsync(company, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return CompanyDto.From(company);
    }

    public async Task<CompanyDto> UpdateAsync(Guid id, CompanyRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await validator.ValidateAndThrowAsync(request, cancellationToken);
        var company = await FindAsync(id, cancellationToken);

        Apply(company, request);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return CompanyDto.From(company);
    }

    /// <summary>Elimina una empresa sin usuarios ni datos; si los tiene responde 409.</summary>
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var company = await FindAsync(id, cancellationToken);
        companies.Remove(company);
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ReferenceConstraintViolationException ex)
        {
            throw new ConflictException(
                "company.in_use",
                "La empresa tiene usuarios o datos asociados y no se puede eliminar.",
                ex);
        }
    }

    private async Task<Company> FindAsync(Guid id, CancellationToken cancellationToken) =>
        await companies.GetByIdAsync(id, cancellationToken) ?? throw new NotFoundException("la empresa", id);

    private static void Apply(Company company, CompanyRequest request)
    {
        company.Name = request.Name.Trim();
        company.Industry = request.Industry;
        company.Sector = string.IsNullOrWhiteSpace(request.Sector) ? null : request.Sector.Trim();
        company.Currency = request.Currency ?? DefaultCurrency;
    }
}
