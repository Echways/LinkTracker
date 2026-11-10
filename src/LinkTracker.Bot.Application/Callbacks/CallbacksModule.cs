using Microsoft.Extensions.DependencyInjection;

namespace LinkTracker.Bot.Application.Callbacks;

public static class CallbacksModule
{
    public static IServiceCollection AddCallbacks(this IServiceCollection services)
    {
        services.AddSingleton<ICallbackHandler, LanguageCallbackHandler>();
        services.AddSingleton<CallbackRouter>();

        return services;
    }
}
