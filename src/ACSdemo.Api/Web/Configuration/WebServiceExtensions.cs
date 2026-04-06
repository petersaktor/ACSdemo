using ACSdemo.Web.GraphQL;
using HotChocolate.AspNetCore;
using Microsoft.Extensions.DependencyInjection;

namespace ACSdemo.Web.Configuration;

public static class WebServiceExtensions
{
    public static IServiceCollection AddWebServices(this IServiceCollection services)
    {
        services
            .AddGraphQLServer()
            .AddQueryType<EmailNotificationQueries>()
            .AddApolloFederation();

        return services;
    }
}
