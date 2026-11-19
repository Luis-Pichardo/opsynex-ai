using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

[assembly: AssemblyFixture(typeof(Opsynex.IntegrationTests.Infrastructure.AiServiceFixture))]

namespace Opsynex.IntegrationTests.Infrastructure;

/// <summary>
/// Servicio de IA real (Python/FastAPI) para las pruebas de punta a punta.
/// <list type="bullet">
/// <item><c>OPSYNEX_AI_URL</c> (+ <c>OPSYNEX_AI_KEY</c>): usa un servicio ya levantado.</item>
/// <item>Si no, lo arranca con uvicorn usando <c>OPSYNEX_AI_PYTHON</c> o <c>ai-service/.venv</c>.</item>
/// <item>Si no hay Python, las pruebas que lo necesitan se omiten, salvo que
/// <c>OPSYNEX_REQUIRE_AI=true</c> (la CI lo exige).</item>
/// </list>
/// </summary>
public sealed class AiServiceFixture : IAsyncLifetime
{
    private Process? _process;

    public string? BaseUrl { get; private set; }

    public string ApiKey { get; private set; } = "test-ai-key";

    public string? UnavailableReason { get; private set; }

    public bool IsAvailable => BaseUrl is not null;

    public async ValueTask InitializeAsync()
    {
        var external = Environment.GetEnvironmentVariable("OPSYNEX_AI_URL");
        if (!string.IsNullOrWhiteSpace(external))
        {
            BaseUrl = external.TrimEnd('/');
            ApiKey = Environment.GetEnvironmentVariable("OPSYNEX_AI_KEY") ?? ApiKey;
            return;
        }

        var serviceDir = FindAiServiceDirectory();
        var python = Environment.GetEnvironmentVariable("OPSYNEX_AI_PYTHON");
        if (string.IsNullOrWhiteSpace(python) && serviceDir is not null)
        {
            var venv = Path.Combine(serviceDir, ".venv", OperatingSystem.IsWindows() ? "Scripts/python.exe" : "bin/python");
            python = File.Exists(venv) ? venv : null;
        }

        if (serviceDir is null || string.IsNullOrWhiteSpace(python))
        {
            Fail("No se encontró ai-service ni un intérprete de Python (OPSYNEX_AI_PYTHON o ai-service/.venv).");
            return;
        }

        var port = FreePort();
        var start = new ProcessStartInfo(python, ["-m", "uvicorn", "app.main:app", "--host", "127.0.0.1", "--port", port.ToString(System.Globalization.CultureInfo.InvariantCulture)])
        {
            WorkingDirectory = serviceDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        start.Environment["AI_SERVICE_API_KEY"] = ApiKey;
        start.Environment["PYTHONDONTWRITEBYTECODE"] = "1";
        start.Environment.Remove("ANTHROPIC_API_KEY");

        try
        {
            _process = Process.Start(start);
            _process!.OutputDataReceived += (_, _) => { };
            _process.ErrorDataReceived += (_, _) => { };
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            Fail($"No se pudo arrancar el servicio de IA: {ex.Message}");
            return;
        }

        var url = $"http://127.0.0.1:{port}";
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        for (var i = 0; i < 60; i++)
        {
            if (_process.HasExited)
            {
                Fail($"El servicio de IA terminó al arrancar (código {_process.ExitCode}).");
                return;
            }

            try
            {
                using var response = await http.GetAsync(new Uri($"{url}/health"));
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    BaseUrl = url;
                    return;
                }
            }
            catch (HttpRequestException)
            {
            }
            catch (TaskCanceledException)
            {
            }

            await Task.Delay(500);
        }

        Fail("El servicio de IA no respondió /health en 30 s.");
    }

    /// <summary>Omite la prueba si el servicio no está disponible (o falla si la CI lo exige).</summary>
    public void RequireAvailable() =>
        Assert.SkipUnless(IsAvailable, $"Servicio de IA no disponible: {UnavailableReason}");

    private void Fail(string reason)
    {
        UnavailableReason = reason;
        if (string.Equals(Environment.GetEnvironmentVariable("OPSYNEX_REQUIRE_AI"), "true", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(reason);
        }
    }

    private static string? FindAiServiceDirectory()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "ai-service");
            if (File.Exists(Path.Combine(candidate, "app", "main.py")))
            {
                return candidate;
            }
        }

        return null;
    }

    private static int FreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    public ValueTask DisposeAsync()
    {
        if (_process is { HasExited: false })
        {
            _process.Kill(entireProcessTree: true);
        }

        _process?.Dispose();
        return ValueTask.CompletedTask;
    }
}
