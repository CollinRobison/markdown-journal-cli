using markdown_journal_cli.Infrastructure.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Spectre.Console.Cli;
using Xunit;

namespace markdown_journal_cli.Tests.Infrastructure;

/// <summary>
/// Unit tests for the <see cref="TypeRegistrar"/> and <see cref="TypeResolver"/> classes,
/// covering dependency injection registration and resolution functionality.
/// </summary>
public class TypeRegistrarTests
{
    [Fact]
    public void Constructor_Should_Initialize_Empty_ServiceCollection()
    {
        // When
        var registrar = new TypeRegistrar();

        // Then
        registrar.ShouldNotBeNull();
    }

    [Fact]
    public void Register_Should_Register_Service_And_Implementation()
    {
        // Given
        var registrar = new TypeRegistrar();

        // When
        registrar.Register(typeof(ITestService), typeof(TestService));
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
        var registrar = new TypeRegistrar();
        var instance = new TestService();

        // When
        registrar.RegisterInstance(typeof(ITestService), instance);
        var resolver = registrar.Build();

        // Then
        var service = resolver.Resolve(typeof(ITestService));
        service.ShouldBe(instance);
    }

    [Fact]
    public void RegisterInstance_Generic_Should_Register_Specific_Instance()
    {
        // Given
        var registrar = new TypeRegistrar();
        var instance = new TestService();

        // When
        var result = registrar.RegisterInstance(instance);
        var resolver = registrar.Build();

        // Then
        result.ShouldBe(registrar); // Should return self for fluent interface
        var service = resolver.Resolve(typeof(TestService));
        service.ShouldBe(instance);
    }

    [Fact]
    public void RegisterLazy_Should_Register_Factory_Function()
    {
        // Given
        var registrar = new TypeRegistrar();
        var factoryCalled = false;

        // When
        registrar.RegisterLazy(
            typeof(ITestService),
            () =>
            {
                factoryCalled = true;
                return new TestService();
            }
        );
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
        var registrar = new TypeRegistrar();

        // When
        var resolver = registrar.Build();

        // Then
        resolver.ShouldNotBeNull();
        resolver.ShouldBeOfType<TypeResolver>();
    }

    [Fact]
    public void RegisterInstance_Should_Support_Fluent_Interface()
    {
        // Given
        var registrar = new TypeRegistrar();
        var instance1 = new TestService();
        var instance2 = new AnotherTestService();

        // When
        var result = registrar.RegisterInstance(instance1).RegisterInstance(instance2);

        // Then
        result.ShouldBe(registrar);
        var resolver = registrar.Build();
        resolver.Resolve(typeof(TestService)).ShouldBe(instance1);
        resolver.Resolve(typeof(AnotherTestService)).ShouldBe(instance2);
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
        var registrar = new TypeRegistrar();
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
        var registrar = new TypeRegistrar();
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
        var registrar = new TypeRegistrar();
        registrar.Register(typeof(ITestService), typeof(TestService));
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
        var registrar = new TypeRegistrar();
        registrar.Register(typeof(ITestService), typeof(TestService));
        registrar.Register(typeof(IAnotherTestService), typeof(AnotherTestService));
        var resolver = registrar.Build();

        // When
        var service1 = resolver.Resolve(typeof(ITestService));
        var service2 = resolver.Resolve(typeof(IAnotherTestService));

        // Then
        service1.ShouldNotBeNull();
        service1.ShouldBeOfType<TestService>();
        service2.ShouldNotBeNull();
        service2.ShouldBeOfType<AnotherTestService>();
        service1.ShouldNotBe(service2);
    }

    // Test interfaces and classes
    private interface ITestService { }

    private class TestService : ITestService { }

    private interface IAnotherTestService { }

    private class AnotherTestService : IAnotherTestService { }
}
