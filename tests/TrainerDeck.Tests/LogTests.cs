using TrainerDeck.Core.Diagnostics;

namespace TrainerDeck.Tests;

[CollectionDefinition("Log", DisableParallelization = true)]
public class LogCollection
{
}

[Collection("Log")]
public class LogTests
{
    [Fact]
    public void Writes_and_rolls()
    {
        using var dir = new TempDir();
        string path = Path.Combine(dir.Path, "trainerdeck.log");
        long oldMax = Log.MaxBytes;
        try
        {
            Log.MaxBytes = 200;
            Log.Initialize(path);
            for (int i = 0; i < 40; i++)
            {
                Log.Info($"line {i} with some padding to fill the file up quickly");
            }

            Assert.True(File.Exists(path));
            Assert.True(File.Exists(Path.Combine(dir.Path, "trainerdeck.1.log")));
            Assert.True(File.Exists(Path.Combine(dir.Path, "trainerdeck.3.log")));
            Assert.False(File.Exists(Path.Combine(dir.Path, "trainerdeck.4.log")));
            Assert.Contains("line 39", File.ReadAllText(path));
        }
        finally
        {
            Log.MaxBytes = oldMax;
        }
    }
}
