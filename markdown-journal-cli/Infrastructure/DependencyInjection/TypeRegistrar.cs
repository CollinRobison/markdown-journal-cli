using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;

namespace markdown_journal_cli.Infrastructure.DependencyInjection;

public sealed class TypeRegistrar : ITypeRegistrar
{
    private readonly IServiceCollection _services;

    public TypeRegistrar()
    {
        _services = new ServiceCollection();
    }

    public ITypeResolver Build()
    {
        return new TypeResolver(_services.BuildServiceProvider());
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

    public TypeRegistrar RegisterInstance<TService>(TService implementation)
        where TService : class
    {
        _services.AddSingleton(implementation);
        return this;
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
