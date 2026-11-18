using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Opsynex.Application.Common.Exceptions;
using Opsynex.IntegrationTests.Infrastructure;

namespace Opsynex.IntegrationTests.Api;

public sealed class ConflictExceptionHandlerTests(OpsynexApiFactory factory) : IClassFixture<OpsynexApiFactory>
{
    [Fact]
    public async Task UniqueViolation_IsWrittenAs409ProblemDetails()
    {
        using var scope = factory.Services.CreateScope();
        var handlers = scope.ServiceProvider.GetServices<IExceptionHandler>();
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        context.Response.Body = new MemoryStream();

        var handled = false;
        foreach (var handler in handlers)
        {
            handled |= await handler.TryHandleAsync(context, new UniqueConstraintViolationException(), TestContext.Current.CancellationToken);
        }

        handled.ShouldBeTrue();
        context.Response.StatusCode.ShouldBe(StatusCodes.Status409Conflict);
        context.Response.Body.Position = 0;
        using var json = await JsonDocument.ParseAsync(context.Response.Body, cancellationToken: TestContext.Current.CancellationToken);
        json.RootElement.GetProperty("code").GetString().ShouldBe("conflict");
        json.RootElement.GetProperty("status").GetInt32().ShouldBe(409);
    }

    [Fact]
    public async Task OtherExceptions_AreNotHandled()
    {
        using var scope = factory.Services.CreateScope();
        var handler = scope.ServiceProvider.GetServices<IExceptionHandler>().Single();

        var handled = await handler.TryHandleAsync(
            new DefaultHttpContext { RequestServices = scope.ServiceProvider },
            new InvalidOperationException(),
            TestContext.Current.CancellationToken);

        handled.ShouldBeFalse();
    }
}
