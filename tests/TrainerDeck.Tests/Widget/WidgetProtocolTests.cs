using TrainerDeck.Core.Widget;
using Xunit;

namespace TrainerDeck.Tests.Widget;

public class PackageIdentityTests
{
    [Fact]
    public void Publisher_id_matches_windows_for_microsoft()
    {
        Assert.Equal("8wekyb3d8bbwe", PackageIdentity.PublisherId("CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US"));
    }

    [Fact]
    public void Family_name_is_name_and_publisher_id()
    {
        string family = PackageIdentity.WidgetFamilyName;
        Assert.StartsWith("TrainerDeck.GameBarWidget_", family);
        Assert.Equal(13, family.Length - "TrainerDeck.GameBarWidget_".Length);
    }
}

public class WidgetProtocolTests
{
    [Theory]
    [InlineData("""{"op":"state"}""", "state", null, null)]
    [InlineData("""{"op":"activate","row":3}""", "activate", 3, null)]
    [InlineData("""{"op":"adjust","row":5,"delta":-1}""", "adjust", 5, -1)]
    public void Parses_requests(string json, string op, int? row, int? delta)
    {
        WidgetRequest? r = WidgetProtocol.ParseRequest(json);
        Assert.NotNull(r);
        Assert.Equal(op, r!.Op);
        Assert.Equal(row, r.Row);
        Assert.Equal(delta, r.Delta);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("""{"op":"format-disk"}""")]
    [InlineData("""{"row":1}""")]
    public void Rejects_bad_requests(string? json) => Assert.Null(WidgetProtocol.ParseRequest(json));

    [Fact]
    public void State_round_trips_on_one_line()
    {
        var state = new WidgetState(WidgetProtocol.Version, "Hades", "15 cheats", new[]
        {
            new WidgetRow(0, WidgetRowKinds.Header, "Battle Options"),
            new WidgetRow(1, WidgetRowKinds.Cheat, "God Mode", Hotkey: "Num 1", IsOn: true, IsToggle: true),
            new WidgetRow(2, WidgetRowKinds.Cheat, "Game Speed", Value: "2.0", CanAdjust: true),
        }, "God Mode applied");

        string line = WidgetProtocol.Serialize(state);
        Assert.DoesNotContain('\n', line);
        Assert.Contains("\"isOn\":true", line);

        WidgetState? back = WidgetProtocol.ParseState(line);
        Assert.NotNull(back);
        Assert.Equal("Hades", back!.Trainer);
        Assert.Equal(3, back.Rows.Count);
        Assert.Equal("2.0", back.Rows[2].Value);
        Assert.True(back.Rows[2].CanAdjust);
    }
}
