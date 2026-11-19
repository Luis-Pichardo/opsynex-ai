using System.Net;
using Opsynex.Domain.Enums;
using Opsynex.IntegrationTests.Infrastructure;

namespace Opsynex.IntegrationTests.Auth;

/// <summary>
/// Matriz de permisos de la etapa 02:
/// gestionar (Admin) · analizar (Admin, Analyst) · ver (todos). Sin sesión → 401.
/// </summary>
public sealed class PermissionMatrixTests(OpsynexApiFactory factory) : IClassFixture<OpsynexApiFactory>
{
    private const int Anonymous = -1;

    public static TheoryData<string, string, int, HttpStatusCode> Matrix()
    {
        var data = new TheoryData<string, string, int, HttpStatusCode>();
        void Row(string method, string path, HttpStatusCode admin, HttpStatusCode analyst, HttpStatusCode client)
        {
            data.Add(method, path, Anonymous, HttpStatusCode.Unauthorized);
            data.Add(method, path, (int)UserRole.Admin, admin);
            data.Add(method, path, (int)UserRole.Analyst, analyst);
            data.Add(method, path, (int)UserRole.Client, client);
        }

        const HttpStatusCode Ok = HttpStatusCode.OK;
        const HttpStatusCode Forbidden = HttpStatusCode.Forbidden;

        // Políticas (endpoints de prueba).
        Row("GET", "/api/v1/__test/policies/manage", Ok, Forbidden, Forbidden);
        Row("GET", "/api/v1/__test/policies/analyze", Ok, Ok, Forbidden);
        Row("GET", "/api/v1/__test/policies/view", Ok, Ok, Ok);
        Row("GET", "/api/v1/__test/policies/default", Ok, Ok, Ok); // política por defecto: sesión iniciada

        // Endpoints reales.
        Row("GET", "/api/v1/users", Ok, Forbidden, Forbidden);
        Row("GET", "/api/v1/companies", Ok, Ok, Ok);
        Row("GET", "/api/v1/auth/me", Ok, Ok, Ok);
        Row("POST", "/api/v1/auth/register", HttpStatusCode.BadRequest, Forbidden, Forbidden); // cuerpo vacío → 400 para Admin
        Row("POST", "/api/v1/companies", HttpStatusCode.BadRequest, Forbidden, Forbidden);
        return data;
    }

    [Theory]
    [MemberData(nameof(Matrix))]
    public async Task Endpoint_RespectsRole(string method, string path, int role, HttpStatusCode expected)
    {
        using var client = role == Anonymous
            ? factory.CreateClient()
            : (await TestUsers.CreateAuthenticatedClientAsync(factory, (UserRole)role)).Client;
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (method == "POST")
        {
            request.Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
        }

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(expected, $"{method} {path} como {(role == Anonymous ? "anónimo" : ((UserRole)role).ToString())}");
    }

    [Theory]
    [InlineData("/health")]
    [InlineData("/api/v1/system/info")]
    [InlineData("/openapi/v1.json")]
    public async Task PublicEndpoints_DoNotRequireToken(string path)
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
