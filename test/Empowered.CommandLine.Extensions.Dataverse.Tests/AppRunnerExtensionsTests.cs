using System.ServiceModel;
using CommandDotNet;
using FluentAssertions;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Organization;
using Microsoft.Xrm.Sdk.Query;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Spectre.Console;

namespace Empowered.CommandLine.Extensions.Dataverse.Tests;

public class AppRunnerExtensionsTests
{
    private class TestCommand()
    {
        [DefaultCommand]
        public async Task<int> Hello(IConsole console)
        {
            console.WriteLine("Hello World");
            return await ExitCodes.Success;
        }
    }

    [Fact]
    public void ShouldTestConnectionBeforeCommandExecution()
    {
        var organizationService = Substitute.For<IOrganizationService>();
        var systemUser = new Entity("systemuser", Guid.NewGuid())
        {
            ["domainname"] = "someone@example.com"
        };
        var organizationId = Guid.NewGuid();
        organizationService
            .Execute(Arg.Any<WhoAmIRequest>())
            .Returns(new WhoAmIResponse
            {
                [nameof(WhoAmIResponse.OrganizationId)] = organizationId,
                [nameof(WhoAmIResponse.BusinessUnitId)] = Guid.NewGuid(),
                [nameof(WhoAmIResponse.UserId)] = systemUser.Id
            });
        const string webApplicationEndpoint = "https://localhost";
        organizationService
            .Execute(Arg.Any<RetrieveCurrentOrganizationRequest>())
            .Returns(new RetrieveCurrentOrganizationResponse
            {
                [nameof(RetrieveCurrentOrganizationResponse.Detail)] = new OrganizationDetail
                {
                    OrganizationId = organizationId,
                    Endpoints = new EndpointCollection
                    {
                        { EndpointType.WebApplication, webApplicationEndpoint }
                    }
                }
            });
        organizationService
            .Retrieve(systemUser.LogicalName, systemUser.Id, Arg.Any<ColumnSet>())
            .Returns(systemUser);
        var appRunner = new EmpoweredAppRunner<TestCommand>("test",
            (collection, _) => { collection.AddSingleton(organizationService); });
        appRunner.UseDataverseConnectionTest<IOrganizationService>();

        appRunner.Run().Should().Be(0);
        organizationService
            .Received()
            .Execute(Arg.Any<WhoAmIRequest>());
        organizationService
            .Received()
            .Execute(Arg.Any<RetrieveCurrentOrganizationRequest>());
        organizationService
            .Received()
            .Retrieve(systemUser.LogicalName, systemUser.Id, Arg.Any<ColumnSet>());
    }

    [Fact]
    public void ShouldSkipExecutionOnMissingDependencyResolver()
    {
        var appRunner = new AppRunner(typeof(TestCommand))
            .UseDataverseConnectionTest<IOrganizationService>();

        appRunner.Run().Should().Be(0);
    }

    [Fact]
    public void ShouldSkipExecutionOnMissingOrganizationService()
    {
        var appRunner = new EmpoweredAppRunner<TestCommand>("test")
            .UseDataverseConnectionTest<IOrganizationService>();

        appRunner.Run().Should().Be(0);
    }

    [Fact]
    public void ShouldFailOnThrowingOrganizationService()
    {
        var organizationService = Substitute.For<IOrganizationService>();
        organizationService.Execute(Arg.Any<OrganizationRequest>())
            .Throws(new FaultException<OrganizationServiceFault>(new OrganizationServiceFault
                {
                    Message = "No!"
                },
                new FaultReason("reason!")));
        var appRunner = new EmpoweredAppRunner<TestCommand>("test",
                (collection, builder) =>
                {
                    collection.Remove(new ServiceDescriptor(typeof(IAnsiConsole), typeof(AnsiConsole),
                        ServiceLifetime.Singleton));
                    collection.AddSingleton(organizationService);
                })
            .UseDataverseConnectionTest<IOrganizationService>();

        appRunner.Run().Should().Be(1);
    }
}
