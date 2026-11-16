using System.Reflection;
using Arvan.Capacity.Application.Abstractions;
using NetArchTest.Rules;

namespace Arvan.Capacity.ArchitectureTests;

/// <summary>
/// The dependency rule of the clean architecture and the tier rules of the Operation / Flow /
/// UseCase framework, as facts that fail the build rather than conventions.
/// </summary>
public sealed class ArchitectureTests
{
    private static readonly Assembly Domain = typeof(Domain.Entities.Reservations.Reservation).Assembly;
    private static readonly Assembly Application = typeof(IUseCase<,>).Assembly;
    private static readonly Assembly Infrastructure = typeof(Infrastructure.DependencyInjection).Assembly;
    private static readonly Assembly FrameworkPresentation = typeof(Framework.Presentation.DependencyInjection).Assembly;

    private static void AssertNoReference(Assembly from, params string[] forbidden)
    {
        var referenced = from.GetReferencedAssemblies().Select(a => a.Name ?? "").ToList();
        Assert.DoesNotContain(referenced, name => forbidden.Any(f => name.StartsWith(f, StringComparison.Ordinal)));
    }

    [Fact]
    public void Domain_depends_on_nothing_but_the_framework_base() =>
        AssertNoReference(Domain, "Arvan.Capacity.Application", "Arvan.Capacity.Infrastructure", "Arvan.Capacity.Presentation",
            "Npgsql", "Dapper", "RabbitMQ", "Microsoft.AspNetCore", "Microsoft.Extensions");

    [Fact]
    public void Application_depends_on_Domain_only_never_on_Infrastructure_Presentation_or_a_driver() =>
        AssertNoReference(Application, "Arvan.Capacity.Infrastructure", "Arvan.Capacity.Presentation", "Arvan.Capacity.Framework.Presentation",
            "Npgsql", "Dapper", "RabbitMQ", "Microsoft.AspNetCore");

    [Fact]
    public void Infrastructure_does_not_depend_on_Presentation() =>
        AssertNoReference(Infrastructure, "Arvan.Capacity.Presentation", "Arvan.Capacity.Framework.Presentation", "Microsoft.AspNetCore");

    [Fact]
    public void Application_never_reads_the_host_clock()
    {
        // Only the database's clock may decide. DateTime.UtcNow / DateTimeOffset.UtcNow are forbidden in Application and Domain.
        foreach (var assembly in new[] { Application, Domain })
        {
            var result = Types.InAssembly(assembly)
                .ShouldNot().HaveDependencyOn("System.TimeProvider")
                .GetResult();

            Assert.True(result.IsSuccessful);
        }
    }

    [Fact]
    public void UseCases_are_named_UseCase_and_implement_IUseCase_and_nothing_else_is_a_UseCase()
    {
        var implementers = Application.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition().Name.StartsWith("IUseCase")))
            .ToList();

        Assert.NotEmpty(implementers);
        Assert.All(implementers, t => Assert.EndsWith("UseCase", t.Name));
        Assert.All(implementers, t => Assert.StartsWith("Arvan.Capacity.Application.UseCases.", t.Namespace));

        var named = Application.GetTypes().Where(t => t.IsClass && t.Name.EndsWith("UseCase")).ToList();
        Assert.All(named, t => Assert.Contains(t, implementers));
    }

    [Fact]
    public void Every_UseCase_has_exactly_one_Input_in_its_own_folder()
    {
        foreach (var useCase in Application.GetTypes().Where(t => t.IsClass && t.Name.EndsWith("UseCase")))
        {
            var expected = useCase.Name[..^"UseCase".Length] + "Input";
            Assert.Single(Application.GetTypes().Where(t => t.Namespace == useCase.Namespace && t.Name == expected));
        }
    }

    [Fact]
    public void Only_UseCases_may_depend_on_ITransactionManager()
    {
        var offenders = Types.InAssembly(Application)
            .That().HaveDependencyOn(typeof(ITransactionManager).FullName!)
            .And().DoNotResideInNamespaceStartingWith("Arvan.Capacity.Application.UseCases")
            .And().DoNotHaveName(nameof(ITransactionManager))
            .GetTypes().ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Operations_and_Flows_never_depend_on_a_UseCase_or_on_ITransactionManager()
    {
        foreach (var ns in new[] { "Arvan.Capacity.Application.Operations", "Arvan.Capacity.Application.Flows" })
        {
            var result = Types.InAssembly(Application).That().ResideInNamespaceStartingWith(ns)
                .ShouldNot().HaveDependencyOnAny("Arvan.Capacity.Application.UseCases", typeof(ITransactionManager).FullName!)
                .GetResult();

            Assert.True(result.IsSuccessful, string.Join(", ", result.FailingTypeNames ?? []));
        }
    }

    [Fact]
    public void Flows_reach_persistence_only_through_Operations_never_a_repository()
    {
        var result = Types.InAssembly(Application).That().ResideInNamespaceStartingWith("Arvan.Capacity.Application.Flows")
            .ShouldNot().HaveDependencyOn("Arvan.Capacity.Application.Repositories")
            .GetResult();

        Assert.True(result.IsSuccessful, string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Controllers_never_bind_to_Input_or_Output_types_of_other_layers_than_through_mappers()
    {
        // Request/Response are Presentation's names; Input/Output are Application's, and neither crosses over.
        var domainAndApplication = Types.InAssembly(Application).GetTypes()
            .Where(t => t.Name.EndsWith("Request") || t.Name.EndsWith("Response")).ToList();

        Assert.Empty(domainAndApplication);
    }

    [Fact]
    public void No_Query_or_Command_or_Result_tier_exists_in_Application()
    {
        var names = Types.InAssembly(Application).GetTypes().Select(t => t.Name).ToList();

        Assert.DoesNotContain(names, n => n.EndsWith("Query") || n.EndsWith("Command") || n.EndsWith("Handler"));
    }

    [Fact]
    public void Repository_implementations_stay_internal_to_Infrastructure()
    {
        var publicRepositories = Types.InAssembly(Infrastructure).That().HaveNameEndingWith("Repository")
            .And().AreClasses().And().ArePublic().GetTypes();

        Assert.Empty(publicRepositories);
    }

    [Fact]
    public void Framework_presentation_depends_on_Application_only_for_exceptions_and_telemetry()
    {
        Assert.Contains(FrameworkPresentation.GetReferencedAssemblies(), a => a.Name == "Arvan.Capacity.Application");
        AssertNoReference(FrameworkPresentation, "Npgsql", "Dapper", "Arvan.Capacity.Infrastructure");
    }
}
