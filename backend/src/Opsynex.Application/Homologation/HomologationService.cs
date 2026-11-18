using FluentValidation;
using Microsoft.Extensions.Logging;
using Opsynex.Application.Abstractions.Persistence;
using Opsynex.Application.Abstractions.Security;
using Opsynex.Application.Abstractions.Tenancy;
using Opsynex.Application.Common.Exceptions;
using Opsynex.Domain.Engines.Consolidation;
using Opsynex.Domain.Entities;
using Opsynex.Domain.Enums;

namespace Opsynex.Application.Homologation;

/// <summary>
/// Homologación contable: pide sugerencias al servicio de IA (o usa el fallback local), aplica a
/// cada cuenta el mapeo confirmado o la mejor sugerencia, consolida en <c>FinancialData</c> y
/// marca la carga como "revisión recomendada" si hay cuentas dudosas.
/// </summary>
public sealed partial class HomologationService(
    IRepository<Upload> uploads,
    IRepository<RawAccountValue> rawValues,
    IAccountMappingRepository mappings,
    ICompanyYearRepository<FinancialData> financialData,
    IAiServiceClient aiService,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    ICurrentTenant tenant,
    IValidator<SaveMappingsRequest> saveValidator,
    TimeProvider timeProvider,
    Analysis.AnalysisPipeline analysisPipeline,
    ILogger<HomologationService> logger)
{
    /// <summary>Confianza mínima para no marcar una cuenta de los estados como dudosa.</summary>
    public const decimal ReviewThreshold = 0.8m;

    private const string MappingWarningPrefix = "mapping.";

    /// <summary>Homologa y consolida una carga (todos sus años). Reutiliza los mapeos confirmados.</summary>
    public async Task<ProcessingResult> ProcessUploadAsync(Guid uploadId, CancellationToken cancellationToken)
    {
        var upload = await uploads.GetByIdAsync(uploadId, cancellationToken) ?? throw new NotFoundException("la carga", uploadId);
        if (upload.Status == UploadStatus.Failed)
        {
            throw new ConflictException("upload.failed", "La carga tiene errores bloqueantes y no se puede analizar.");
        }

        var raw = await rawValues.ListAsync(r => r.UploadId == uploadId, cancellationToken);
        var accounts = Accounts(raw);
        var (byKey, usedFallback) = await ApplyMappingsAsync(upload.CompanyId, accounts, cancellationToken);
        await ConsolidateAsync(upload, raw, byKey, cancellationToken);
        UpdateReviewState(upload, accounts, byKey);

        upload.Status = UploadStatus.Processed;
        upload.AnalysisStatus = AnalysisStatus.Completed;
        upload.ProcessedAt = timeProvider.GetUtcNow().UtcDateTime;
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Análisis de la empresa con los datos consolidados: KPIs (y desde las etapas 06–07,
        // diagnóstico, impacto y score).
        await analysisPipeline.RunAsync(upload.CompanyId, cancellationToken);

        return new ProcessingResult(upload.Id, upload.Status, upload.AnalysisStatus, upload.NeedsReview, upload.DoubtfulAccounts,
            usedFallback, [.. upload.Years]);
    }

    /// <summary>Mapeos de la empresa.</summary>
    public async Task<IReadOnlyList<MappingDto>> ListAsync(CancellationToken cancellationToken)
    {
        var companyId = CurrentCompany();
        var list = await mappings.ListByCompanyAsync(companyId, cancellationToken);
        return list.OrderBy(m => m.Scope).ThenBy(m => m.ClientAccount, StringComparer.CurrentCultureIgnoreCase).Select(MappingDto.From).ToList();
    }

    /// <summary>Cuentas de una carga sin confirmar, con su sugerencia; las dudosas primero.</summary>
    public async Task<IReadOnlyList<PendingAccountDto>> PendingAsync(Guid uploadId, CancellationToken cancellationToken)
    {
        var upload = await uploads.GetByIdAsync(uploadId, cancellationToken) ?? throw new NotFoundException("la carga", uploadId);
        var raw = await rawValues.ListAsync(r => r.UploadId == uploadId, cancellationToken);
        var byKey = (await mappings.ListByCompanyAsync(upload.CompanyId, cancellationToken)).ToDictionary(m => AccountKey.For(m).Id);

        return Accounts(raw)
            .Where(a => byKey.TryGetValue(a.Key.Id, out var m) && !m.IsConfirmed)
            .Select(a =>
            {
                var m = byKey[a.Key.Id];
                return new PendingAccountDto(m.Id, m.ClientAccount, a.Sample.OriginalLabel, a.Sample.SheetName, a.Sample.SheetType,
                    a.Sample.RowNumber, a.Sample.Section, m.Scope, m.Role, m.OpsynexCategory, m.Confidence, m.Method, m.IsConfirmed,
                    IsDoubtful(m), a.Year, a.Amount);
            })
            .OrderByDescending(p => p.IsDoubtful)
            .ThenByDescending(p => Math.Abs(p.Amount))
            .ToList();
    }

    /// <summary>Guarda o confirma mapeos (masivo). Quedan como confirmados por el usuario.</summary>
    public async Task<IReadOnlyList<MappingDto>> SaveAsync(SaveMappingsRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await saveValidator.ValidateAndThrowAsync(request, cancellationToken);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var updated = new List<AccountMapping>();
        foreach (var item in request.Items)
        {
            var mapping = await mappings.GetByIdAsync(item.Id, cancellationToken) ?? throw new NotFoundException("el mapeo", item.Id);
            mapping.OpsynexCategory = item.Category;
            mapping.Method = MappingMethod.Manual;
            mapping.Confidence = 1m;
            mapping.ConfirmedAt = now;
            mapping.ConfirmedBy = currentUser.UserId;
            updated.Add(mapping);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return updated.Select(MappingDto.From).ToList();
    }

    private Guid CurrentCompany() =>
        (currentUser.IsAdmin ? tenant.CompanyId : currentUser.CompanyId)
        ?? throw new BusinessValidationException("mapping.company_required", "companyId", "Indica la empresa (cabecera X-Company-Id o parámetro companyId).");

    private sealed record Account(AccountKey Key, RawAccountValue Sample, int Year, decimal Amount);

    /// <summary>Cuentas distintas de la carga (una por clave), con el monto del último año.</summary>
    private static List<Account> Accounts(IReadOnlyList<RawAccountValue> raw) =>
        raw.Select(r => (Key: AccountKey.For(r), Raw: r))
            .Where(x => x.Key is not null)
            .GroupBy(x => x.Key!.Id)
            .Select(g =>
            {
                var year = g.Max(x => x.Raw.Year);
                var sample = g.OrderBy(x => x.Raw.SheetType).ThenBy(x => x.Raw.RowNumber).First();
                return new Account(sample.Key!, sample.Raw, year, g.Where(x => x.Raw.Year == year).Sum(x => x.Raw.Value));
            })
            .ToList();

    /// <summary>Usa el mapeo confirmado o pide sugerencias (IA o fallback) y las guarda sin confirmar.</summary>
    private async Task<(Dictionary<string, AccountMapping> ByKey, bool UsedFallback)> ApplyMappingsAsync(
        Guid companyId, List<Account> accounts, CancellationToken cancellationToken)
    {
        var existing = (await mappings.ListByCompanyAsync(companyId, cancellationToken)).ToDictionary(m => AccountKey.For(m).Id);
        var toSuggest = accounts.Where(a => !existing.TryGetValue(a.Key.Id, out var m) || !m.IsConfirmed).ToList();
        var suggestions = new Dictionary<string, (OpsynexCategory Category, decimal Confidence, MappingMethod Method)>();
        var usedFallback = false;

        if (toSuggest.Count > 0)
        {
            var aiAccounts = toSuggest.Select((a, i) => new AiAccount(
                i.ToString(System.Globalization.CultureInfo.InvariantCulture), a.Sample.OriginalLabel, a.Sample.NormalizedLabel,
                a.Sample.SheetType, a.Sample.Section, a.Sample.AccountCode)).ToList();
            var known = existing.Values.Where(m => m.IsConfirmed)
                .Select(m => new AiKnownMapping(m.ClientAccount, m.OpsynexCategory, m.Section.Length > 0 ? m.Section : null))
                .ToList();
            try
            {
                var result = await aiService.SuggestMappingsAsync(aiAccounts, known, cancellationToken);
                foreach (var s in result)
                {
                    var account = toSuggest[int.Parse(s.Id, System.Globalization.CultureInfo.InvariantCulture)];
                    suggestions[account.Key.Id] = (s.Category, decimal.Round(s.Confidence, 4), MethodFrom(s.Method, s.Category));
                }
            }
            catch (AiServiceUnavailableException ex)
            {
                usedFallback = true;
                LogFallback(logger, ex.Message);
            }

            foreach (var account in toSuggest.Where(a => !suggestions.ContainsKey(a.Key.Id)))
            {
                usedFallback = true;
                suggestions[account.Key.Id] = FallbackSuggester.Find(account.Key, account.Key.ClientAccount) is { } category
                    ? (category, FallbackSuggester.Confidence, MappingMethod.Fallback)
                    : (OpsynexCategory.Other, 0m, MappingMethod.Unmatched);
            }
        }

        foreach (var account in toSuggest)
        {
            var (category, confidence, method) = suggestions[account.Key.Id];
            if (!existing.TryGetValue(account.Key.Id, out var mapping))
            {
                mapping = new AccountMapping
                {
                    CompanyId = companyId,
                    ClientAccount = account.Key.ClientAccount,
                    Scope = account.Key.Scope,
                    Section = account.Key.Section,
                    Role = AccountKey.RoleFor(account.Key.Scope),
                };
                await mappings.AddAsync(mapping, cancellationToken);
                existing[account.Key.Id] = mapping;
            }

            mapping.OpsynexCategory = category;
            mapping.Confidence = Math.Clamp(confidence, 0m, 1m);
            mapping.Method = method;
        }

        return (existing, usedFallback);
    }

    private static MappingMethod MethodFrom(string method, OpsynexCategory category) => method switch
    {
        "exact" => MappingMethod.Exact,
        "keyword" => MappingMethod.Keyword,
        "fuzzy" => MappingMethod.Fuzzy,
        "rule" => MappingMethod.Rule,
        "known" => MappingMethod.Known,
        "none" => MappingMethod.Unmatched,
        _ => category == OpsynexCategory.Other ? MappingMethod.Unmatched : MappingMethod.Ai,
    };

    /// <summary>Consolida por categoría y año y hace upsert en <c>FinancialData</c> (todos los años de la carga).</summary>
    private async Task ConsolidateAsync(
        Upload upload, IReadOnlyList<RawAccountValue> raw, Dictionary<string, AccountMapping> byKey, CancellationToken cancellationToken)
    {
        var rows = raw
            .Select(r => (Key: AccountKey.For(r), Raw: r))
            .Where(x => x.Key is not null && byKey.ContainsKey(x.Key.Id))
            .Select(x =>
            {
                var m = byKey[x.Key!.Id];
                return new ConsolidationRow(x.Raw.Year, m.OpsynexCategory, m.Role, x.Raw.RowKind, x.Raw.Value);
            });

        var hasIncome = raw.Any(r => r.SheetType is SheetType.IncomeStatement or SheetType.CostStructure or SheetType.Notes);
        var hasBalance = raw.Any(r => r.SheetType is SheetType.BalanceSheet);
        var hasCashFlow = raw.Any(r => r.SheetType is SheetType.CashFlow);

        foreach (var year in ConsolidationEngine.Consolidate(rows))
        {
            var data = await financialData.GetByCompanyAndYearAsync(upload.CompanyId, year.Year, cancellationToken);
            if (data is null)
            {
                data = new FinancialData { CompanyId = upload.CompanyId, Year = year.Year };
                await financialData.AddAsync(data, cancellationToken);
            }

            FinancialDataMapper.Apply(data, year, hasIncome, hasBalance, hasCashFlow);
        }
    }

    private static bool IsDoubtful(AccountMapping m) =>
        !m.IsConfirmed && m.Role == MappingRole.Primary && m.Confidence < ReviewThreshold;

    /// <summary>"Revisión recomendada" y advertencias de cuentas sin categoría (con su monto).</summary>
    private static void UpdateReviewState(Upload upload, List<Account> accounts, Dictionary<string, AccountMapping> byKey)
    {
        var doubtful = accounts.Where(a => IsDoubtful(byKey[a.Key.Id]))
            .OrderBy(a => a.Sample.SheetName, StringComparer.Ordinal)
            .ThenBy(a => a.Sample.RowNumber)
            .ToList();
        upload.DoubtfulAccounts = doubtful.Count;
        upload.NeedsReview = doubtful.Count > 0;

        var warnings = upload.Warnings.Where(w => !w.Code.StartsWith(MappingWarningPrefix, StringComparison.Ordinal)).ToList();
        var culture = System.Globalization.CultureInfo.GetCultureInfo("es-DO");
        foreach (var account in doubtful.Where(a => byKey[a.Key.Id].OpsynexCategory == OpsynexCategory.Other))
        {
            warnings.Add(new UploadIssue(
                "mapping.unmapped_account",
                $"La cuenta «{(account.Sample.OriginalLabel.Length > 0 ? account.Sample.OriginalLabel : account.Key.ClientAccount)}» no se pudo clasificar ({account.Amount.ToString("#,##0.00", culture)} en {account.Year}). Revísala en la homologación.",
                account.Sample.SheetName, account.Sample.RowNumber));
        }

        upload.Warnings = warnings;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Servicio de IA no disponible; se usa el fallback local: {Reason}")]
    private static partial void LogFallback(ILogger logger, string reason);
}

internal sealed class SaveMappingsRequestValidator : AbstractValidator<SaveMappingsRequest>
{
    public SaveMappingsRequestValidator()
    {
        RuleFor(r => r.Items).NotEmpty().WithMessage("Envía al menos un mapeo.");
        RuleForEach(r => r.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.Id).NotEmpty();
            item.RuleFor(i => i.Category).IsInEnum().WithMessage("La categoría no es válida.");
        });
    }
}
