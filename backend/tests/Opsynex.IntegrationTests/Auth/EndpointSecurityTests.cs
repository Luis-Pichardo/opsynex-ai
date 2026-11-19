using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Opsynex.Domain.Enums;
using Opsynex.IntegrationTests.Infrastructure;
using Opsynex.IntegrationTests.Uploads;

namespace Opsynex.IntegrationTests.Auth;

/// <summary>
/// Recorre <b>todos</b> los endpoints registrados en el API (etapa 13): cada uno debe estar en la
/// tabla de acceso esperado, responder 401 sin sesión (salvo los públicos) y 403 a los roles sin
/// permiso. Un endpoint nuevo que no se agregue aquí hace fallar la prueba.
/// </summary>
public sealed partial class EndpointSecurityTests(OpsynexApiFactory factory) : IClassFixture<OpsynexApiFactory>
{
    private enum Access
    {
        Public,
        View,
        Analyze,
        Manage,
    }

    private static readonly Dictionary<string, Access> Expected = new(StringComparer.Ordinal)
    {
        ["POST auth/login"] = Access.Public,
        ["GET system/info"] = Access.Public,
        ["POST auth/register"] = Access.Manage,
        ["GET auth/me"] = Access.View,
        ["GET users"] = Access.Manage,
        ["GET users/{id:guid}"] = Access.Manage,
        ["POST users"] = Access.Manage,
        ["PUT users/{id:guid}"] = Access.Manage,
        ["PUT users/{id:guid}/password"] = Access.Manage,
        ["DELETE users/{id:guid}"] = Access.Manage,
        ["GET companies"] = Access.View,
        ["GET companies/{id:guid}"] = Access.View,
        ["POST companies"] = Access.Manage,
        ["PUT companies/{id:guid}"] = Access.Manage,
        ["DELETE companies/{id:guid}"] = Access.Manage,
        ["POST uploads"] = Access.Analyze,
        ["GET uploads"] = Access.View,
        ["GET uploads/{id:guid}"] = Access.View,
        ["GET uploads/templates/{fileType}"] = Access.Analyze,
        ["GET mappings"] = Access.Analyze,
        ["GET mappings/categories"] = Access.Analyze,
        ["GET mappings/pending"] = Access.Analyze,
        ["PUT mappings"] = Access.Analyze,
        ["POST mappings/consolidate"] = Access.Analyze,
        ["GET data"] = Access.View,
        ["GET kpis"] = Access.View,
        ["GET analysis/summary"] = Access.View,
        ["POST analysis/run"] = Access.Analyze,
        ["GET problems"] = Access.View,
        ["GET problems/{id:guid}"] = Access.View,
        ["PATCH problems/{id:guid}"] = Access.Analyze,
        ["GET score"] = Access.View,
        ["GET impact/summary"] = Access.View,
        ["GET reports/executive"] = Access.View,
        ["GET ai/summary"] = Access.View,
        ["POST ai/regenerate"] = Access.Analyze,
        ["GET industrial/kpis"] = Access.View,
        ["GET industrial/score"] = Access.View,
    };

    /// <summary>Endpoints de la API versionada descubiertos en el host (sin los de prueba).</summary>
    private List<(string Method, string Route)> DiscoverEndpoints()
    {
        var sources = factory.Services.GetRequiredService<IEnumerable<EndpointDataSource>>();
        return sources
            .SelectMany(s => s.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(e => (Methods: e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? [], Route: e.RoutePattern.RawText ?? string.Empty))
            .Where(e => e.Route.StartsWith("api/v{version:apiVersion}/", StringComparison.Ordinal) && !e.Route.Contains("__test", StringComparison.Ordinal))
            .SelectMany(e => e.Methods.Select(m => (m, e.Route["api/v{version:apiVersion}/".Length..])))
            .Distinct()
            .ToList();
    }

    [Fact]
    public void EveryEndpoint_IsInTheAccessTable()
    {
        var discovered = DiscoverEndpoints().Select(e => $"{e.Method} {e.Route}").ToList();

        discovered.Where(e => !Expected.ContainsKey(e)).ShouldBeEmpty("Endpoints sin acceso esperado: agrégalos a la tabla");
        Expected.Keys.Where(k => !discovered.Contains(k)).ShouldBeEmpty("La tabla tiene endpoints que ya no existen");
    }

    [Fact]
    public async Task EveryEndpoint_RespectsAuthenticationAndRoles()
    {
        var companyId = await TestUsers.CreateCompanyAsync(factory.Database);
        var clients = new Dictionary<string, HttpClient> { ["anónimo"] = factory.CreateClient() };
        foreach (var role in new[] { UserRole.Admin, UserRole.Analyst, UserRole.Client })
        {
            var (client, _) = await TestUsers.CreateAuthenticatedClientAsync(factory, role, role == UserRole.Admin ? null : companyId);
            if (role == UserRole.Admin)
            {
                client.DefaultRequestHeaders.Add("X-Company-Id", companyId.ToString());
            }

            clients[role.ToString()] = client;
        }

        var failures = new List<string>();
        try
        {
            foreach (var (method, route) in DiscoverEndpoints().Where(e => e.Route != "auth/login"))
            {
                var access = Expected[$"{method} {route}"];
                var path = "/api/v1/" + RouteParameter().Replace(route, m => m.Groups[1].Value switch
                {
                    "fileType" => "IncomeStatement",
                    _ => Guid.NewGuid().ToString(),
                });
                foreach (var (name, client) in clients)
                {
                    using var request = new HttpRequestMessage(new HttpMethod(method), path);
                    if (route == "uploads")
                    {
                        // Solo acepta multipart (con JSON respondería 415 antes de revisar el rol).
                        request.Content = new MultipartFormDataContent { { new StringContent("x"), "companyId" } };
                    }
                    else if (method is "POST" or "PUT" or "PATCH")
                    {
                        request.Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
                    }

                    using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
                    var status = response.StatusCode;
                    var allowed = access switch
                    {
                        Access.Public => true,
                        Access.View => name != "anónimo",
                        Access.Analyze => name is "Admin" or "Analyst",
                        _ => name == "Admin",
                    };
                    var ok = allowed
                        ? status is not (HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                        : status == (name == "anónimo" ? HttpStatusCode.Unauthorized : HttpStatusCode.Forbidden);
                    if (!ok)
                    {
                        failures.Add($"{method} {path} como {name}: {(int)status}");
                    }
                }
            }
        }
        finally
        {
            foreach (var client in clients.Values)
            {
                client.Dispose();
            }
        }

        failures.ShouldBeEmpty();
    }

    [Fact]
    public async Task CompanyData_IsIsolated_EvenIfAnotherCompanyIsRequested()
    {
        // Empresa B con libro contable y datos industriales.
        var companyB = await TestUsers.CreateCompanyAsync(factory.Database);
        var (analystB, _) = await TestUsers.CreateAuthenticatedClientAsync(factory, UserRole.Analyst, companyB);
        using (analystB)
        {
            (await analystB.UploadSampleAsync("libro_sintetico_2a.xls")).StatusCode.ShouldBe(HttpStatusCode.Created);
            (await analystB.UploadSampleAsync(Path.Combine("industrial", "produccion.xlsx"))).StatusCode.ShouldBe(HttpStatusCode.Created);
        }

        var problemB = await FirstIdAsync(companyB, "/api/v1/problems?pageSize=1", "items");
        var uploadB = await FirstIdAsync(companyB, "/api/v1/uploads", null);

        // Empresa A (sin datos): su Client y su Analyst piden los datos de B de todas las formas posibles.
        var companyA = await TestUsers.CreateCompanyAsync(factory.Database);
        foreach (var role in new[] { UserRole.Client, UserRole.Analyst })
        {
            var (client, _) = await TestUsers.CreateAuthenticatedClientAsync(factory, role, companyA);
            using (client)
            {
                client.DefaultRequestHeaders.Add("X-Company-Id", companyB.ToString());
                foreach (var path in new[]
                {
                    "/api/v1/kpis", "/api/v1/score", "/api/v1/impact/summary", "/api/v1/problems", "/api/v1/analysis/summary",
                    "/api/v1/data?type=Financial", "/api/v1/uploads", "/api/v1/industrial/kpis", "/api/v1/industrial/score",
                    "/api/v1/companies", "/api/v1/ai/summary",
                })
                {
                    using var response = await client.GetAsync($"{path}{(path.Contains('?') ? '&' : '?')}companyId={companyB}", TestContext.Current.CancellationToken);
                    var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
                    body.ShouldNotContain(companyB.ToString(), Case.Insensitive, $"{role} de A ve datos de B en {path}");
                }

                (await client.GetAsync($"/api/v1/problems/{problemB}", TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
                (await client.GetAsync($"/api/v1/uploads/{uploadB}", TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
                (await client.GetAsync($"/api/v1/companies/{companyB}", TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
                if (role == UserRole.Analyst)
                {
                    var patch = new StringContent("""{"status":"Resolved"}""", System.Text.Encoding.UTF8, "application/json");
                    (await client.PatchAsync($"/api/v1/problems/{problemB}", patch, TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
                }
            }
        }
    }

    private async Task<string> FirstIdAsync(Guid companyId, string path, string? property)
    {
        var (admin, _) = await TestUsers.CreateAuthenticatedClientAsync(factory, UserRole.Admin);
        using (admin)
        {
            admin.DefaultRequestHeaders.Add("X-Company-Id", companyId.ToString());
            var json = await (await admin.GetAsync(path, TestContext.Current.CancellationToken)).ReadAsync<System.Text.Json.JsonElement>();
            var list = property is null ? json : json.GetProperty(property);
            return list[0].GetProperty("id").GetString()!;
        }
    }

    [GeneratedRegex(@"\{(\w+)(:[^}]*)?\}")]
    private static partial Regex RouteParameter();
}
