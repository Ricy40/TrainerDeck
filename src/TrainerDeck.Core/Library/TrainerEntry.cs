namespace TrainerDeck.Core.Library;


public sealed class TrainerEntry
{
    public TrainerEntry(TrainerFile file, TrainerMetadata meta)
    {
        File = file;
        Meta = meta;
    }

    public TrainerFile File { get; }

    public TrainerMetadata Meta { get; }

    public string Id => File.Id;

    public string DisplayName => string.IsNullOrWhiteSpace(Meta.DisplayName) ? File.Parsed.GameName : Meta.DisplayName!.Trim();

    public string SortKey => FlingNameParser.MakeSortKey(DisplayName);

    public char JumpLetter => FlingNameParser.MakeJumpLetter(DisplayName);

    public override string ToString() => DisplayName;
}
