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
        // If we have an existing provider, we need to combine it with new registrations
        if (_provider != null)
        {
            // Build new services and create a resolver that tries new services first, then falls back to existing provider
            var newProvider = _services.BuildServiceProvider();
            return new TypeResolver(newProvider, _provider);
        }
        
        return new TypeResolver(_services.BuildServiceProvider());
    }
}

public sealed class TypeResolver : ITypeResolver
{
    private readonly IServiceProvider _provider;
    private readonly IServiceProvider? _fallbackProvider;

    public TypeResolver(IServiceProvider provider, IServiceProvider? fallbackProvider = null)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _fallbackProvider = fallbackProvider;
    }

    public object? Resolve(Type? type)
    {
        if (type == null) return null;
        
        // Try fallback provider first (if it exists) since it has our pre-registered services
        if (_fallbackProvider != null)
        {
            var service = _fallbackProvider.GetService(type);
            if (service != null) return service;
        }
        
        // Then try primary provider for anything registered by Spectre.Console
        return _provider.GetService(type);
    }
}
