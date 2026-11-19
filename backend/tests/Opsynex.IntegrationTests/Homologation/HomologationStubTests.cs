using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Opsynex.Application.Homologation;
using Opsynex.Application.Uploads;
using Opsynex.Domain.Enums;
using Opsynex.IntegrationTests.Infrastructure;
using Opsynex.IntegrationTests.Uploads;

namespace Opsynex.IntegrationTests.Homologation;

/// <summary>Cliente de IA simulado: registra lo que recibe y responde reglas fijas.</summary>
public sealed class StubAiServiceClient : IAiServiceClient
{
    public List<(IReadOnlyList<AiAccount> Accounts, IReadOnlyList<AiKnownMapping> Known)> Calls { get; } = [];

    public Task<IReadOnlyList<AiSuggestion>> SuggestMappingsAsync(
        IReadOnlyList<AiAccount> accounts, IReadOnlyList<AiKnownMapping> knownMappings, CancellationToken cancellationToken)
    {
        lock (Calls)
        {
            Calls.Add((accounts, knownMappings));
        }

        IReadOnlyList<AiSuggestion> result = accounts
            .Select(a => a.NormalizedLabel == "Ventas netas"
                ? new AiSuggestion(a.Id, OpsynexCategory.GrossSales, 0.99m, "exact")
                : new AiSuggestion(a.Id, OpsynexCategory.Other, 0.9m, "fuzzy"))
            .ToList();
        return Task.FromResult(result);
    }
}

public sealed class StubAiApiFactory(DatabaseFixture database) : OpsynexApiFactory(database)
{
    public StubAiServiceClient Stub { get; } = new();

    protected override void ConfigureServices(IServiceCollection services) => services.AddSingleton<IAiServiceClient>(Stub);
}

public sealed class HomologationStubTests(StubAiApiFactory factory) : IClassFixture<StubAiApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Suggestions_AreAppliedAndConfirmedMappingsAreNotRequestedAgain()
    {
        var (client, analyst) = await TestUsers.CreateAuthenticatedClientAsync(factory, UserRole.Analyst);
        using (client)
        {
            var first = await (await client.UploadSampleAsync("libro_sintetico_2a.xls")).ReadAsync<UploadReportDto>(HttpStatusCode.Created);
            first.Status.ShouldBe(UploadStatus.Processed);

            var firstCall = factory.Stub.Calls.Last(c => c.Accounts.Any(a => a.NormalizedLabel == "Ventas netas"));
            // Solo etiquetas y contexto: nunca datos personales ni montos.
            firstCall.Accounts.ShouldNotContain(a => a.Label.Contains("EMPLEADO", StringComparison.Ordinal));
            firstCall.Accounts.ShouldNotContain(a => a.SheetType == SheetType.TrialBalance || a.SheetType == SheetType.EmployeeRoster);

            var mappings = await (await client.GetAsync("/api/v1/mappings", Ct)).ReadAsync<List<MappingDto>>();
            var sales = mappings.Single(m => m.ClientAccount == "Ventas netas");
            sales.Category.ShouldBe(OpsynexCategory.GrossSales);
            sales.Method.ShouldBe(MappingMethod.Exact);
            sales.Role.ShouldBe(MappingRole.Primary);
            sales.IsConfirmed.ShouldBeFalse();

            (await client.PutAsJsonAsync("/api/v1/mappings",
                new SaveMappingsRequest([new MappingUpdate(sales.Id, OpsynexCategory.GrossSales)]), Json.Options, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);

            var callsBefore = factory.Stub.Calls.Count;
            (await client.UploadSampleAsync("libro_sintetico_2a.xls")).StatusCode.ShouldBe(HttpStatusCode.Created);

            var secondCall = factory.Stub.Calls.Skip(callsBefore).Single();
            secondCall.Accounts.ShouldNotContain(a => a.NormalizedLabel == "Ventas netas");
            secondCall.Known.ShouldContain(k => k.NormalizedLabel == "Ventas netas" && k.Category == OpsynexCategory.GrossSales);

            await using var context = factory.Database.CreateContext();
            var data = await context.FinancialData.SingleAsync(f => f.CompanyId == analyst.CompanyId && f.Year == 2024, Ct);
            data.GrossSales.ShouldBe(Expected.V("libro_sintetico_2a.xls", 2024, "gross_sales"));
            data.Revenue.ShouldBe(Expected.V("libro_sintetico_2a.xls", 2024, "gross_sales")); // sin descuentos mapeados
        }
    }

    [Fact]
    public async Task MappingsEndpoints_RespectRolesAndCompany()
    {
        var (viewer, _) = await TestUsers.CreateAuthenticatedClientAsync(factory, UserRole.Client);
        var (admin, _) = await TestUsers.CreateAuthenticatedClientAsync(factory, UserRole.Admin);
        var (analyst, _) = await TestUsers.CreateAuthenticatedClientAsync(factory, UserRole.Analyst);
        var (otherAnalyst, _) = await TestUsers.CreateAuthenticatedClientAsync(factory, UserRole.Analyst);
        using (viewer)
        using (admin)
        using (analyst)
        using (otherAnalyst)
        {
            await (await viewer.GetAsync("/api/v1/mappings", Ct)).ShouldBeProblemAsync(HttpStatusCode.Forbidden, "auth.forbidden");
            await (await admin.GetAsync("/api/v1/mappings", Ct)).ShouldBeProblemAsync(HttpStatusCode.BadRequest, "mapping.company_required");

            var upload = await (await analyst.UploadSampleAsync("libro_sintetico_2a.xls")).ReadAsync<UploadReportDto>(HttpStatusCode.Created);
            await (await otherAnalyst.GetAsync($"/api/v1/mappings/pending?uploadId={upload.UploadId}", Ct))
                .ShouldBeProblemAsync(HttpStatusCode.NotFound, "not_found");
            await (await otherAnalyst.PostAsync($"/api/v1/mappings/consolidate?uploadId={upload.UploadId}", null, Ct))
                .ShouldBeProblemAsync(HttpStatusCode.NotFound, "not_found");

            var mine = await (await analyst.GetAsync("/api/v1/mappings", Ct)).ReadAsync<List<MappingDto>>();
            await (await otherAnalyst.PutAsJsonAsync("/api/v1/mappings",
                new SaveMappingsRequest([new MappingUpdate(mine[0].Id, OpsynexCategory.Other)]), Json.Options, Ct))
                .ShouldBeProblemAsync(HttpStatusCode.NotFound, "not_found");
            await (await analyst.PutAsJsonAsync("/api/v1/mappings", new SaveMappingsRequest([]), Json.Options, Ct))
                .ShouldBeProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        }
    }

    [Fact]
    public async Task Consolidate_FailedUpload_Returns409()
    {
        var (client, _) = await TestUsers.CreateAuthenticatedClientAsync(factory, UserRole.Analyst);
        using (client)
        {
            var failed = await (await client.UploadSampleAsync("invalido_vacio.xlsx")).ReadAsync<UploadReportDto>(HttpStatusCode.UnprocessableEntity);

            await (await client.PostAsync($"/api/v1/mappings/consolidate?uploadId={failed.UploadId}", null, Ct))
                .ShouldBeProblemAsync(HttpStatusCode.Conflict, "upload.failed");
        }
    }
}
