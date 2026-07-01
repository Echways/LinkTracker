using LinkTracker.Scrapper.Infrastructure.Outbox.Configuration;
using LinkTracker.Scrapper.Infrastructure.Outbox.Jobs;
using LinkTracker.Scrapper.Infrastructure.Quartz.Configuration;
using LinkTracker.Scrapper.Infrastructure.Quartz.Jobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Quartz;

namespace LinkTracker.Scrapper.Infrastructure.Quartz.Registration;

public static class QuartzModule
{
    public static IServiceCollection AddQuartzScheduling(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var schedulingSection = configuration.GetSection("Scheduling");
        var outboxSection = configuration.GetSection("Outbox");

        services
            .AddOptions<LinkUpdatesSchedulingOptions>()
            .Bind(schedulingSection)
            .Validate(o => o.IntervalSeconds > 0, "Scheduling:IntervalSeconds must be greater than zero.")
            .Validate(o => o.BatchSize is >= 50 and <= 500, "Scheduling:BatchSize must be in range 50..500.")
            .Validate(
                o => o.MaxDegreeOfParallelism > 0,
                "Scheduling:MaxDegreeOfParallelism must be greater than zero.")
            .ValidateOnStart();

        var schedulingOptions = schedulingSection.Get<LinkUpdatesSchedulingOptions>() ?? new LinkUpdatesSchedulingOptions();
        var outboxOptions = outboxSection.Get<OutboxOptions>() ?? new OutboxOptions();

        services.AddQuartz(q =>
        {
            AddLinkUpdatesJob(q, schedulingOptions);
            AddOutboxDispatchJob(q, outboxOptions);
        });

        services.AddQuartzHostedService(options =>
        {
            options.WaitForJobsToComplete = true;
        });

        return services;
    }

    private static void AddLinkUpdatesJob(IServiceCollectionQuartzConfigurator q, LinkUpdatesSchedulingOptions options)
    {
        var jobKey = new JobKey(nameof(LinkUpdatesJob));

        q.AddJob<LinkUpdatesJob>(job => job.WithIdentity(jobKey));

        q.AddTrigger(trigger => trigger
            .ForJob(jobKey)
            .WithIdentity($"{nameof(LinkUpdatesJob)}-trigger")
            .WithSimpleSchedule(schedule => schedule
                .WithInterval(TimeSpan.FromSeconds(options.IntervalSeconds))
                .RepeatForever()));
    }

    private static void AddOutboxDispatchJob(IServiceCollectionQuartzConfigurator q, OutboxOptions options)
    {
        if (!options.Enabled)
        {
            return;
        }

        var jobKey = new JobKey(nameof(OutboxDispatchJob));

        q.AddJob<OutboxDispatchJob>(job => job.WithIdentity(jobKey));

        q.AddTrigger(trigger => trigger
            .ForJob(jobKey)
            .WithIdentity($"{nameof(OutboxDispatchJob)}-trigger")
            .WithSimpleSchedule(schedule => schedule
                .WithInterval(TimeSpan.FromSeconds(options.DispatchIntervalSeconds))
                .RepeatForever()));
    }
}