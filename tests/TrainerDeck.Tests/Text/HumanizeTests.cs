using TrainerDeck.Core.Text;

namespace TrainerDeck.Tests.Text;

public class HumanizeTests
{
    private static readonly DateTime Now = new(2026, 6, 15, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(10, "just now")]
    [InlineData(60, "1 minute ago")]
    [InlineData(150, "2 minutes ago")]
    [InlineData(3 * 3600, "3 hours ago")]
    [InlineData(30 * 3600, "yesterday")]
    [InlineData(5 * 86400, "5 days ago")]
    public void Describes_recent_times(int secondsAgo, string expected) =>
        Assert.Equal(expected, Humanize.Ago(Now.AddSeconds(-secondsAgo), Now));

    [Fact]
    public void Old_times_show_a_date() =>
        Assert.Contains("2026", Humanize.Ago(Now.AddDays(-90), Now));
}
