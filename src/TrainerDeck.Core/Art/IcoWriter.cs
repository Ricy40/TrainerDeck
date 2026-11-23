namespace TrainerDeck.Core.Art;


public static class IcoWriter
{
    public const int MaxSize = 256;

    public sealed record IconImage(int Width, int Height, byte[] Png);

    public static IconImage? FromPng(byte[] png)
    {
        ReadOnlySpan<byte> signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        if (png.Length < 24 || !png.AsSpan(0, 8).SequenceEqual(signature))
        {
            return null;
        }

        int width = (png[16] << 24) | (png[17] << 16) | (png[18] << 8) | png[19];
        int height = (png[20] << 24) | (png[21] << 16) | (png[22] << 8) | png[23];
        return new IconImage(width, height, png);
    }

    public static List<IconImage> Select(IEnumerable<IconImage> images) => images
        .Where(i => i.Width == i.Height && i.Width is > 0 and <= MaxSize)
        .GroupBy(i => i.Width)
        .Select(g => g.First())
        .OrderBy(i => i.Width)
        .ToList();

    public static byte[] Write(IReadOnlyList<IconImage> images)
    {
        if (images.Count == 0)
        {
            throw new ArgumentException("An icon needs at least one image.", nameof(images));
        }

        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        w.Write((short)0);
        w.Write((short)1);
        w.Write((short)images.Count);
        int offset = 6 + (16 * images.Count);
        foreach (IconImage image in images)
        {
            w.Write((byte)(image.Width >= MaxSize ? 0 : image.Width));
            w.Write((byte)(image.Height >= MaxSize ? 0 : image.Height));
            w.Write((byte)0);
            w.Write((byte)0);
            w.Write((short)1);
            w.Write((short)32);
            w.Write(image.Png.Length);
            w.Write(offset);
            offset += image.Png.Length;
        }

        foreach (IconImage image in images)
        {
            w.Write(image.Png);
        }

        w.Flush();
        return ms.ToArray();
    }

    public static IReadOnlyList<int> PackFolder(string folder, string outputIco)
    {
        List<IconImage> images = Select(Directory.EnumerateFiles(folder, "*.png")
            .Select(f => FromPng(File.ReadAllBytes(f)))
            .OfType<IconImage>());
        File.WriteAllBytes(outputIco, Write(images));
        return images.Select(i => i.Width).ToList();
    }
}
