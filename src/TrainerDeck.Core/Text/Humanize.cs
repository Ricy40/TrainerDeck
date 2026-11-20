namespace TrainerDeck.Core.Text;

public static class Humanize
{
    public static string Ago(DateTime utc, DateTime nowUtc)
    {
        TimeSpan d = nowUtc - utc;
        if (d < TimeSpan.FromMinutes(1))
        {
            return "just now";
        }

        if (d < TimeSpan.FromHours(1))
        {
            return Plural((int)d.TotalMinutes, "minute") + " ago";
        }

        if (d < TimeSpan.FromDays(1))
        {
            return Plural((int)d.TotalHours, "hour") + " ago";
        }

        if (d < TimeSpan.FromDays(2))
        {
            return "yesterday";
        }

        if (d < TimeSpan.FromDays(30))
        {
            return Plural((int)d.TotalDays, "day") + " ago";
        }

        return utc.ToLocalTime().ToString("d MMM yyyy");
    }

    public static string Plural(int n, string word) => n == 1 ? $"1 {word}" : $"{n} {word}s";
}
