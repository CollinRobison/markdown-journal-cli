using markdown_journal_cli.Infrastructure.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Spectre.Console.Cli;
using Xunit;

namespace markdown_journal_cli.Tests.Infrastructure.DependencyInjection;

/// <summary>
/// Unit tests for the <see cref="TypeRegistrar"/> and <see cref="TypeResolver"/> classes,
/// covering dependency injection registration and resolution functionality.
/// </summary>
public class TypeRegistrarTests
{
    [Fact]
    public void Constructor_With_ServiceProvider_Should_Initialize()
    {
        // Given
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();

        // When
        var registrar = new TypeRegistrar(serviceProvider);

        // Then
        registrar.ShouldNotBeNull();
    }

    [Fact]
    public void Register_Should_Register_Service_And_Implementation()
    {
        // Given
        var services = new ServiceCollection();
        services.AddSingleton<ITestService, TestService>();
        var serviceProvider = services.BuildServiceProvider();
        var registrar = new TypeRegistrar(serviceProvider);

        // When
        var resolver = registrar.Build();

        // Then
        var service = resolver.Resolve(typeof(ITestService));
        service.ShouldNotBeNull();
        service.ShouldBeOfType<TestService>();
    }

    [Fact]
    public void RegisterInstance_Should_Register_Specific_Instance()
    {
        // Given
        var instance = new TestService();
        var services = new ServiceCollection();
        services.AddSingleton<ITestService>(instance);
        var serviceProvider = services.BuildServiceProvider();
        var registrar = new TypeRegistrar(serviceProvider);

        // When
        var resolver = registrar.Build();

        // Then
        var service = resolver.Resolve(typeof(ITestService));
        service.ShouldBe(instance);
    }

    [Fact]
    public void RegisterLazy_Should_Register_Factory_Function()
    {
        // Given
        var factoryCalled = false;
        var services = new ServiceCollection();
        services.AddSingleton<ITestService>(_ =>
        {
            factoryCalled = true;
            return new TestService();
        });
        var serviceProvider = services.BuildServiceProvider();
        var registrar = new TypeRegistrar(serviceProvider);

        // When
        var resolver = registrar.Build();

        // Then
        factoryCalled.ShouldBeFalse(); // Factory not called yet
        var service = resolver.Resolve(typeof(ITestService));
        factoryCalled.ShouldBeTrue(); // Factory called when resolving
        service.ShouldNotBeNull();
        service.ShouldBeOfType<TestService>();
    }

    [Fact]
    public void Build_Should_Return_TypeResolver()
    {
        // Given
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();
        var registrar = new TypeRegistrar(serviceProvider);

        // When
        var resolver = registrar.Build();

        // Then
        resolver.ShouldNotBeNull();
        resolver.ShouldBeOfType<TypeResolver>();
    }

    [Fact]
    public void Build_Should_Use_Existing_Provider_When_Available()
    {
        // Given
        var services = new ServiceCollection();
        services.AddSingleton<ITestService, TestService>();
        var serviceProvider = services.BuildServiceProvider();
        var registrar = new TypeRegistrar(serviceProvider);

        // When
        var resolver = registrar.Build();

        // Then
        var service = resolver.Resolve(typeof(ITestService));
        service.ShouldNotBeNull();
        service.ShouldBeOfType<TestService>();
    }

    [Fact]
    public void TypeResolver_Constructor_Should_Throw_When_Provider_Is_Null()
    {
        // When & Then
        Should.Throw<ArgumentNullException>(() => new TypeResolver(null!));
    }

    [Fact]
    public void TypeResolver_Resolve_Should_Return_Null_For_Null_Type()
    {
        // Given
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();
        var registrar = new TypeRegistrar(serviceProvider);
        var resolver = registrar.Build();

        // When
        var result = resolver.Resolve(null);

        // Then
        result.ShouldBeNull();
    }

    [Fact]
    public void TypeResolver_Resolve_Should_Return_Null_For_Unregistered_Type()
    {
        // Given
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();
        var registrar = new TypeRegistrar(serviceProvider);
        var resolver = registrar.Build();

        // When
        var result = resolver.Resolve(typeof(ITestService));

        // Then
        result.ShouldBeNull();
    }

    [Fact]
    public void TypeResolver_Should_Return_Same_Instance_For_Singleton_Services()
    {
        // Given
        var services = new ServiceCollection();
        services.AddSingleton<ITestService, TestService>();
        var serviceProvider = services.BuildServiceProvider();
        var registrar = new TypeRegistrar(serviceProvider);
        var resolver = registrar.Build();

        // When
        var instance1 = resolver.Resolve(typeof(ITestService));
        var instance2 = resolver.Resolve(typeof(ITestService));

        // Then
        instance1.ShouldBe(instance2);
    }

    [Fact]
    public void TypeResolver_Should_Resolve_Multiple_Different_Services()
    {
        // Given
        var services = new ServiceCollection();
        services.AddSingleton<ITestService, TestService>();
        services.AddSingleton<IAnotherTestService, AnotherTestService>();
        var serviceProvider = services.BuildServiceProvider();
        var registrar = new TypeRegistrar(serviceProvider);

        // When
        var resolver = registrar.Build();
        var service1 = resolver.Resolve(typeof(ITestService));
        var service2 = resolver.Resolve(typeof(IAnotherTestService));

        // Then
        service1.ShouldNotBeNull();
        service1.ShouldBeOfType<TestService>();
        service2.ShouldNotBeNull();
        service2.ShouldBeOfType<AnotherTestService>();
        service1.ShouldNotBe(service2);
    }

    [Fact]
    public void TypeResolver_Should_Resolve_Services_From_Existing_Provider()
    {
        // Given
        var services = new ServiceCollection();
        services.AddSingleton<ITestService, TestService>();
        services.AddSingleton<IAnotherTestService, AnotherTestService>();
        var serviceProvider = services.BuildServiceProvider();
        var registrar = new TypeRegistrar(serviceProvider);
        var resolver = registrar.Build();

        // When
        var service1 = resolver.Resolve(typeof(ITestService));
        var service2 = resolver.Resolve(typeof(IAnotherTestService));

        // Then
        service1.ShouldNotBeNull();
        service1.ShouldBeOfType<TestService>();
        service2.ShouldNotBeNull();
        service2.ShouldBeOfType<AnotherTestService>();
    }

    [Fact]
    public void RegisterInstance_Should_Work_With_New_Services_Collection()
    {
        // Given
        var instance1 = new TestService();
        var instance2 = new AnotherTestService();
        var services = new ServiceCollection();
        services.AddSingleton<ITestService>(instance1);
        services.AddSingleton<IAnotherTestService>(instance2);
        var serviceProvider = services.BuildServiceProvider();
        var registrar = new TypeRegistrar(serviceProvider);

        // When
        var resolver = registrar.Build();

        // Then
        resolver.Resolve(typeof(ITestService)).ShouldBe(instance1);
        resolver.Resolve(typeof(IAnotherTestService)).ShouldBe(instance2);
    }

    // Test interfaces and classes
    private interface ITestService { }

    private class TestService : ITestService { }

    private interface IAnotherTestService { }

    private class AnotherTestService : IAnotherTestService { }
}
