using FluentValidation;
using Opsynex.Application.Abstractions.Persistence;
using Opsynex.Application.Abstractions.Security;
using Opsynex.Application.Auth;
using Opsynex.Application.Common.Exceptions;
using Opsynex.Domain.Entities;
using Opsynex.Domain.Enums;

namespace Opsynex.Application.Users;

/// <summary>Gestión de usuarios (solo Admin; la política la aplica el API).</summary>
public sealed class UserService(
    IUserRepository users,
    ICompanyRepository companies,
    IUnitOfWork unitOfWork,
    IPasswordService passwords,
    ICurrentUser currentUser,
    IValidator<CreateUserRequest> createValidator,
    IValidator<UpdateUserRequest> updateValidator,
    IValidator<SetPasswordRequest> passwordValidator)
{
    public async Task<IReadOnlyList<UserDto>> ListAsync(UserQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var search = query.Search?.Trim();

        var result = await users.ListAsync(
            u => (query.CompanyId == null || u.CompanyId == query.CompanyId)
                && (query.Role == null || u.Role == query.Role)
                && (string.IsNullOrEmpty(search) || u.Email.Contains(search) || u.FullName.Contains(search)),
            cancellationToken);

        var names = await CompanyNamesAsync(result, cancellationToken);
        return result
            .OrderBy(u => u.FullName, StringComparer.CurrentCultureIgnoreCase)
            .Select(u => UserDto.From(u, NameOf(names, u.CompanyId)))
            .ToList();
    }

    public async Task<UserDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var user = await FindAsync(id, cancellationToken);
        return await ToDtoAsync(user, cancellationToken);
    }

    public async Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await createValidator.ValidateAndThrowAsync(request, cancellationToken);
        await EnsureCompanyExistsAsync(request.CompanyId, cancellationToken);
        await EnsureEmailAvailableAsync(request.Email, exceptUserId: null, cancellationToken);

        var user = new User
        {
            Email = request.Email.Trim(),
            FullName = request.FullName.Trim(),
            Role = request.Role,
            CompanyId = request.CompanyId,
            PasswordHash = string.Empty,
        };
        user.PasswordHash = passwords.Hash(user, request.Password);

        await users.AddAsync(user, cancellationToken);
        await SaveAsync(cancellationToken);
        return await ToDtoAsync(user, cancellationToken);
    }

    public async Task<UserDto> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await updateValidator.ValidateAndThrowAsync(request, cancellationToken);
        var user = await FindAsync(id, cancellationToken);

        if (IsCurrentUser(user) && (request.Role != UserRole.Admin || !request.IsActive))
        {
            throw new ConflictException("user.self_modification", "No puedes quitarte el rol de Admin ni desactivarte a ti mismo.");
        }

        await EnsureCompanyExistsAsync(request.CompanyId, cancellationToken);
        await EnsureEmailAvailableAsync(request.Email, user.Id, cancellationToken);

        user.Email = request.Email.Trim();
        user.FullName = request.FullName.Trim();
        user.Role = request.Role;
        user.CompanyId = request.CompanyId;
        user.IsActive = request.IsActive;

        await SaveAsync(cancellationToken);
        return await ToDtoAsync(user, cancellationToken);
    }

    /// <summary>El Admin asigna una contraseña nueva; también quita el bloqueo temporal.</summary>
    public async Task SetPasswordAsync(Guid id, SetPasswordRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await passwordValidator.ValidateAndThrowAsync(request, cancellationToken);
        var user = await FindAsync(id, cancellationToken);

        user.PasswordHash = passwords.Hash(user, request.NewPassword);
        user.ClearLockout();
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Desactiva el usuario (no se borra: sus cargas y confirmaciones lo referencian).</summary>
    public async Task DeactivateAsync(Guid id, CancellationToken cancellationToken)
    {
        var user = await FindAsync(id, cancellationToken);
        if (IsCurrentUser(user))
        {
            throw new ConflictException("user.self_modification", "No puedes desactivarte a ti mismo.");
        }

        user.IsActive = false;
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private bool IsCurrentUser(User user) => currentUser.UserId == user.Id;

    private async Task<User> FindAsync(Guid id, CancellationToken cancellationToken) =>
        await users.GetByIdAsync(id, cancellationToken) ?? throw new NotFoundException("el usuario", id);

    private async Task EnsureCompanyExistsAsync(Guid? companyId, CancellationToken cancellationToken)
    {
        if (companyId is { } id && !await companies.AnyAsync(c => c.Id == id, cancellationToken))
        {
            throw new BusinessValidationException("user.company_not_found", "companyId", $"No existe la empresa «{id}».");
        }
    }

    private async Task EnsureEmailAvailableAsync(string email, Guid? exceptUserId, CancellationToken cancellationToken)
    {
        // Se busca sin filtro por empresa: el correo es único en toda la plataforma.
        var existing = await users.GetByEmailAsync(email, cancellationToken);
        if (existing is not null && existing.Id != exceptUserId)
        {
            throw new ConflictException("user.email_taken", "Ya existe un usuario con ese correo.");
        }
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueConstraintViolationException ex)
        {
            throw new ConflictException("user.email_taken", "Ya existe un usuario con ese correo.", ex);
        }
    }

    private async Task<UserDto> ToDtoAsync(User user, CancellationToken cancellationToken)
    {
        var name = user.CompanyId is { } companyId
            ? await companies.FindNameIgnoringFiltersAsync(companyId, cancellationToken)
            : null;
        return UserDto.From(user, name);
    }

    private async Task<Dictionary<Guid, string>> CompanyNamesAsync(IEnumerable<User> list, CancellationToken cancellationToken)
    {
        var ids = list.Where(u => u.CompanyId.HasValue).Select(u => u.CompanyId!.Value).Distinct().ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        var found = await companies.ListAsync(c => ids.Contains(c.Id), cancellationToken);
        return found.ToDictionary(c => c.Id, c => c.Name);
    }

    private static string? NameOf(Dictionary<Guid, string> names, Guid? id) =>
        id is { } key && names.TryGetValue(key, out var name) ? name : null;
}
