using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LinkTracker.Shared.Infrastructure.Authentication;

public static class ServiceAuthExtensions
{
    public static IServiceCollection AddServiceAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddServiceAuthOptions(configuration);

        services
            .AddAuthentication(ServiceAuthDefaults.AuthenticationScheme)
            .AddScheme<AuthenticationSchemeOptions, ServiceTokenAuthenticationHandler>(
                ServiceAuthDefaults.AuthenticationScheme,
                configureOptions: null);

        services
            .AddAuthorizationBuilder()
            .AddPolicy(
                ServiceAuthDefaults.PolicyName,
                policy => policy
                    .AddAuthenticationSchemes(ServiceAuthDefaults.AuthenticationScheme)
                    .RequireAuthenticatedUser());

        return services;
    }

    public static IServiceCollection AddServiceAuthClients(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddServiceAuthOptions(configuration);

        services.AddTransient<ServiceAuthHeaderHandler>();
        services.AddSingleton<ServiceAuthClientInterceptor>();

        return services;
    }

    public static TBuilder RequireServiceAuthorization<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.RequireAuthorization(ServiceAuthDefaults.PolicyName);

        return builder;
    }

    private static IServiceCollection AddServiceAuthOptions(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<ServiceAuthOptions>()
            .Bind(configuration.GetSection(ServiceAuthOptions.SectionName))
            .Validate(
                o => !string.IsNullOrWhiteSpace(o.Secret),
                $"{ServiceAuthOptions.SectionName}:Secret must be set")
            .ValidateOnStart();

        return services;
    }
}
