using ACSdemo.Application.Ports;
using ACSdemo.Infrastructure.Persistence;
using Azure.Identity;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ACSdemo.Infrastructure.Configuration;

public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var accountEndpoint = configuration["CosmosDb:AccountEndpoint"]
            ?? throw new InvalidOperationException("CosmosDb:AccountEndpoint is not configured.");

        services.AddSingleton(new CosmosClient(accountEndpoint, new DefaultAzureCredential()));
        services.AddScoped<IEmailDeliveryReportRepository, CosmosEmailDeliveryReportRepository>();

        return services;
    }
}
