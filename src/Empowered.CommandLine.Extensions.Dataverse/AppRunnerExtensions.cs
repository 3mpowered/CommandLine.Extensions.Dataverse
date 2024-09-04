using CommandDotNet;
using CommandDotNet.Builders;
using CommandDotNet.Execution;
using Empowered.CommandLine.Extensions.Extensions;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Organization;
using Microsoft.Xrm.Sdk.Query;
using Spectre.Console;

namespace Empowered.CommandLine.Extensions.Dataverse;

public static class AppRunnerExtensions
{
    public static AppRunner UseDataverseConnectionTest<TOrganizationService>(this AppRunner appRunner)
        where TOrganizationService : class, IOrganizationService
    {
        return appRunner.Configure(context =>
            context.UseMiddleware(TestConnection<TOrganizationService>, MiddlewareStages.PostBindValuesPreInvoke)
        );
    }

    private static Task<int> TestConnection<TOrganizationService>(CommandContext commandContext, ExecutionDelegate next)
        where TOrganizationService : class, IOrganizationService
    {
        if (commandContext.DependencyResolver == null)
        {
            return next(commandContext);
        }

        var organizationService = commandContext.DependencyResolver.ResolveOrDefault<TOrganizationService>();
        if (organizationService == null)
        {
            return next(commandContext);
        }

        var console = commandContext.DependencyResolver.ResolveOrDefault<IAnsiConsole>();
        if (console == null)
        {
            return next(commandContext);
        }

        var whoAmI = (WhoAmIResponse)organizationService.Execute(new WhoAmIRequest());
        var user = organizationService.Retrieve("systemuser", whoAmI.UserId, new ColumnSet("domainname"))
            .GetAttributeValue<string>("domainname");
        var currentOrganization =
            (RetrieveCurrentOrganizationResponse)organizationService.Execute(new RetrieveCurrentOrganizationRequest
                { AccessType = EndpointAccessType.Default });
        console.Success(
            $"Connected to environment {currentOrganization.Detail.Endpoints[EndpointType.WebApplication].Italic()} as {user.Italic()}");
        return next(commandContext);
    }
}
