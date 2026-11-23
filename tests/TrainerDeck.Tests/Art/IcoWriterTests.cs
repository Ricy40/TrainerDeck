using TrainerDeck.Core.Art;

namespace TrainerDeck.Tests.Art;

public class IcoWriterTests
{
    private static byte[] FakePng(int w, int h, int extra = 10)
    {
        var b = new byte[24 + extra];
        new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, (byte)'I', (byte)'H', (byte)'D', (byte)'R' }.CopyTo(b, 0);
        b[16] = (byte)(w >> 24); b[17] = (byte)(w >> 16); b[18] = (byte)(w >> 8); b[19] = (byte)w;
        b[20] = (byte)(h >> 24); b[21] = (byte)(h >> 16); b[22] = (byte)(h >> 8); b[23] = (byte)h;
        return b;
    }

    [Fact]
    public void Reads_size_from_png_header()
    {
        IcoWriter.IconImage i = IcoWriter.FromPng(FakePng(48, 48))!;
        Assert.Equal(48, i.Width);
        Assert.Null(IcoWriter.FromPng([1, 2, 3]));
    }

    [Fact]
    public void Selects_square_sizes_up_to_256_smallest_first()
    {
        var picked = IcoWriter.Select(
        [
            IcoWriter.FromPng(FakePng(512, 512))!,
            IcoWriter.FromPng(FakePng(48, 48))!,
            IcoWriter.FromPng(FakePng(256, 256))!,
            IcoWriter.FromPng(FakePng(16, 16))!,
            IcoWriter.FromPng(FakePng(48, 48))!,
            IcoWriter.FromPng(FakePng(30, 20))!,
        ]);
        Assert.Equal([16, 48, 256], picked.Select(i => i.Width));
    }

    [Fact]
    public void Writes_a_valid_directory()
    {
        var images = IcoWriter.Select([IcoWriter.FromPng(FakePng(16, 16, 5))!, IcoWriter.FromPng(FakePng(256, 256, 7))!]);
        byte[] ico = IcoWriter.Write(images);

        Assert.Equal(1, BitConverter.ToInt16(ico, 2));
        Assert.Equal(2, BitConverter.ToInt16(ico, 4));
        Assert.Equal(16, ico[6]);
        Assert.Equal(0, ico[6 + 16]);
        int firstOffset = BitConverter.ToInt32(ico, 6 + 12);
        Assert.Equal(6 + 32, firstOffset);
        Assert.Equal(0x89, ico[firstOffset]);
        Assert.Equal(6 + 32 + 29 + 31, ico.Length);
    }

    [Fact]
    public void Packs_a_folder()
    {
        using var dir = new TempDir();
        File.WriteAllBytes(Path.Combine(dir.Path, "Icon-24.png"), FakePng(24, 24));
        File.WriteAllBytes(Path.Combine(dir.Path, "Icon-512.png"), FakePng(512, 512));
        File.WriteAllText(Path.Combine(dir.Path, "notes.txt"), "x");
        string ico = Path.Combine(dir.Path, "out.ico");

        Assert.Equal([24], IcoWriter.PackFolder(dir.Path, ico));
        Assert.True(File.Exists(ico));
    }
}
