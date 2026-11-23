using System.Text.Json;
using System.Text.Json.Serialization;

namespace TrainerDeck.Core.Widget;


public static class WidgetProtocol
{
    public const int Version = 1;
    public const string PipeName = "TrainerDeck.GameBarWidget";

    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);

    public static WidgetRequest? ParseRequest(string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return null;
        }

        try
        {
            WidgetRequest? request = JsonSerializer.Deserialize<WidgetRequest>(line, Options);
            return request is { Op: not null } && WidgetOps.All.Contains(request.Op) ? request : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static WidgetState? ParseState(string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<WidgetState>(line, Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

public static class WidgetOps
{
    public const string State = "state";
    public const string Activate = "activate";
    public const string Adjust = "adjust";
    public const string Open = "open";

    public static readonly IReadOnlySet<string> All = new HashSet<string> { State, Activate, Adjust, Open };
}

public static class WidgetRowKinds
{
    public const string Header = "header";
    public const string Action = "action";
    public const string Cheat = "cheat";
}

public sealed record WidgetRequest(string Op, int? Row = null, int? Delta = null);

public sealed record WidgetRow(
    int Id,
    string Kind,
    string Label,
    string? Detail = null,
    string? Hotkey = null,
    bool? IsOn = null,
    bool IsToggle = false,
    string? Value = null,
    bool CanAdjust = false,
    bool IsEnabled = true);

public sealed record WidgetState(
    int Version,
    string? Trainer,
    string Status,
    IReadOnlyList<WidgetRow> Rows,
    string? Message = null,
    bool MessageIsError = false);
