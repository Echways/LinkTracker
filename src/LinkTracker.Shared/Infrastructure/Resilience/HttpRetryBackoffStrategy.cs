namespace LinkTracker.Shared.Infrastructure.Resilience;

public enum HttpRetryBackoffStrategy
{
    Constant = 0,
    Exponential = 1
}