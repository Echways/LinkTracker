using LinkTracker.Bot.Application.Dialogs.Abstractions;

namespace LinkTracker.Bot.Application.Dialogs.Implementations.Track;

public static class TrackDialogContextExtensions
{
    public static void SetPendingUrl(this DialogContext ctx, string url)
    {
        ctx.Set(TrackKeys.PendingUrl, url);
    }

    public static string? GetPendingUrl(this DialogContext ctx)
    {
        return ctx.Get(TrackKeys.PendingUrl);
    }

    public static void ClearPendingUrl(this DialogContext ctx)
    {
        ctx.Remove(TrackKeys.PendingUrl);
    }

    public static void SetTagsCsv(this DialogContext ctx, string tagsCsv)
    {
        ctx.Set(TrackKeys.TagsCsv, tagsCsv);
    }

    public static string? GetTagsCsv(this DialogContext ctx)
    {
        return ctx.Get(TrackKeys.TagsCsv);
    }

    public static void ClearTagsCsv(this DialogContext ctx)
    {
        ctx.Remove(TrackKeys.TagsCsv);
    }

    public static void ClearTrackState(this DialogContext ctx)
    {
        ctx.ClearPendingUrl();
        ctx.ClearTagsCsv();
    }
}