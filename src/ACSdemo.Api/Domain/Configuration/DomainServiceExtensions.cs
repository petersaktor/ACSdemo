using Microsoft.Extensions.DependencyInjection;

namespace ACSdemo.Domain.Configuration;

public static class DomainServiceExtensions
{
    public static IServiceCollection AddDomainServices(this IServiceCollection services)
    {
        return services;
    }
}
