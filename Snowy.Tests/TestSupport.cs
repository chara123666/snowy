using Snowy.Core;

namespace Snowy.Tests;

internal sealed class FakeClock
{
    public DateTimeOffset Now { get; private set; } = new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);
    public void Advance(TimeSpan by) => Now += by;
}

internal sealed class TestEnv : IDisposable
{
    private readonly AttemptLedger _oldLedger = VaultEngine.Ledger;

    public string Dir { get; }
    public FakeClock Clock { get; } = new();

    public static readonly KdfParams Fast = new(1024, 1, 2);

    public TestEnv()
    {
        Dir = Path.Combine(Path.GetTempPath(), "snowy-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Dir);
        VaultEngine.Ledger = new AttemptLedger(Path.Combine(Dir, "attempts"), () => Clock.Now);
    }

    public string Write(string name, byte[] content)
    {
        var path = Path.Combine(Dir, name);
        File.WriteAllBytes(path, content);
        return path;
    }

    public string OutDir => Path.Combine(Dir, "out");

    public static byte[] Random(int len, int seed = 1)
    {
        var b = new byte[len];
        new Random(seed).NextBytes(b);
        return b;
    }

    public void Dispose()
    {
        VaultEngine.Ledger = _oldLedger;
        try { Directory.Delete(Dir, recursive: true); } catch { }
    }
}
