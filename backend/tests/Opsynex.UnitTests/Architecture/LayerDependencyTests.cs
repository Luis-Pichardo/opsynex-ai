using System.Reflection;
using Opsynex.Application;
using Opsynex.Domain;

namespace Opsynex.UnitTests.Architecture;

/// <summary>Verifica que se respeten las dependencias de la arquitectura limpia.</summary>
public sealed class LayerDependencyTests
{
    private static string[] OpsynexReferences(Assembly assembly) => assembly
        .GetReferencedAssemblies()
        .Select(a => a.Name ?? string.Empty)
        .Where(n => n.StartsWith("Opsynex.", StringComparison.Ordinal))
        .ToArray();

    [Fact]
    public void Domain_DoesNotDependOnOtherLayers()
    {
        OpsynexReferences(typeof(DomainAssemblyMarker).Assembly).ShouldBeEmpty();
    }

    [Fact]
    public void Domain_DoesNotDependOnFrameworkPackages()
    {
        var forbidden = new[] { "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore" };

        typeof(DomainAssemblyMarker).Assembly.GetReferencedAssemblies()
            .Select(a => a.Name ?? string.Empty)
            .ShouldNotContain(n => forbidden.Any(f => n.StartsWith(f, StringComparison.Ordinal)));
    }

    [Fact]
    public void Application_DependsOnlyOnDomain()
    {
        var references = OpsynexReferences(typeof(Application.DependencyInjection).Assembly);

        references.ShouldNotContain("Opsynex.Infrastructure");
        references.ShouldNotContain("Opsynex.Api");
    }

    [Fact]
    public void Infrastructure_DoesNotDependOnApi()
    {
        OpsynexReferences(typeof(Infrastructure.DependencyInjection).Assembly).ShouldNotContain("Opsynex.Api");
    }
}
