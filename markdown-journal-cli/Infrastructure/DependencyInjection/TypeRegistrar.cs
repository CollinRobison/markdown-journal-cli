using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;

namespace markdown_journal_cli.Infrastructure.DependencyInjection;

public sealed class TypeRegistrar : ITypeRegistrar
{
    private readonly IServiceCollection _services;
    private readonly IServiceProvider? _provider;

    // Constructor for building services
    public TypeRegistrar(Microsoft.Extensions.Hosting.IHost host)
    {
        _services = new ServiceCollection();
    }
    
    // Constructor for using existing service provider
    public TypeRegistrar(IServiceProvider provider)
    {
        _provider = provider;
        _services = new ServiceCollection(); // Not used but required
    }

    public void Register(Type service, Type implementation)
    {
        _services.AddSingleton(service, implementation);
    }

    public void RegisterInstance(Type service, object implementation)
    {
        _services.AddSingleton(service, implementation);
    }

    public void RegisterLazy(Type service, Func<object> factory)
    {
        _services.AddSingleton(service, _ => factory());
    }

    public ITypeResolver Build()
    {
        return new TypeResolver(_provider ?? _services.BuildServiceProvider());
    }
}

public sealed class TypeResolver : ITypeResolver
{
    private readonly IServiceProvider _provider;

    public TypeResolver(IServiceProvider provider)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
    }

    public object? Resolve(Type? type)
    {
        return type == null ? null : _provider.GetService(type);
    }
}
