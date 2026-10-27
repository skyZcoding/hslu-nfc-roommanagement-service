using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace RoomManagement.Application.Tests;

/// <summary>Guards against adding a handler or validator and forgetting to register it in <c>AddApplication</c>.</summary>
public class DependencyInjectionTests
{
    private static readonly IEnumerable<Type> ConcreteTypes =
        Assembly.GetAssembly(typeof(DependencyInjection))!.GetTypes().Where(t => t is { IsClass: true, IsAbstract: false });

    private readonly IServiceCollection _services = new ServiceCollection().AddApplication();

    [Fact]
    public void AddApplication_RegistersEveryHandler()
    {
        var handlers = ConcreteTypes.Where(t => t.Name.EndsWith("Handler", StringComparison.Ordinal)).ToList();

        Assert.NotEmpty(handlers);
        Assert.All(handlers, handler => Assert.Contains(_services, d => d.ServiceType == handler));
    }

    [Fact]
    public void AddApplication_RegistersEveryValidator()
    {
        var validators = ConcreteTypes
            .SelectMany(t => t.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IValidator<>))
                .Select(i => (Implementation: t, Service: i)))
            .ToList();

        Assert.NotEmpty(validators);
        Assert.All(validators, v => Assert.Contains(
            _services, d => d.ServiceType == v.Service && d.ImplementationType == v.Implementation));
    }
}
