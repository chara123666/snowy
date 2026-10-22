using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Snowy.Core;

internal sealed class AttemptLedger
{
    public readonly record struct Status(bool Locked, TimeSpan Remaining, int Failures);

    private struct Record
    {
        public int Failures;
        public long LastUnix;
        public long LockedUntilUnix;
    }

    private const int RecordSize = 20;
    private const long MaxLockSeconds = 86400;

    private readonly string _dir;
    private readonly Func<DateTimeOffset> _now;

    private static readonly Lazy<AttemptLedger> DefaultLedger = new(() => new AttemptLedger(
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Snowy", "attempts")));

    public static AttemptLedger Default => DefaultLedger.Value;

    public AttemptLedger(string directory, Func<DateTimeOffset>? clock = null)
    {
        _dir = directory;
        _now = clock ?? (() => DateTimeOffset.UtcNow);
    }

    internal static string IdFor(byte[] nonce, byte[] salt)
        => Convert.ToHexString(SHA256.HashData([.. "SNOWY-ATTEMPT"u8, .. nonce, .. salt]));

    internal static int DelaySeconds(int failures)
        => failures >= 20 ? 86400 : failures >= 10 ? 300 : failures >= 5 ? 30 : failures >= 3 ? 5 : 0;

    public Status BeginAttempt(string id)
    {
        using var f = Open(id);
        var rec = Read(f);
        long now = _now().ToUnixTimeSeconds();

        if (now < rec.LockedUntilUnix)
            return new Status(true, TimeSpan.FromSeconds(rec.LockedUntilUnix - now), rec.Failures);

        rec.Failures++;
        rec.LastUnix = now;
        Write(f, rec);
        return new Status(false, TimeSpan.Zero, rec.Failures);
    }

    public void RecordSuccess(string id)
    {
        try { File.Delete(PathFor(id)); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    public Status RecordFailure(string id)
    {
        using var f = Open(id);
        var rec = Read(f);
        long now = _now().ToUnixTimeSeconds();

        int delay = DelaySeconds(rec.Failures);
        if (delay > 0) rec.LockedUntilUnix = now + delay;
        Write(f, rec);

        return delay > 0
            ? new Status(true, TimeSpan.FromSeconds(delay), rec.Failures)
            : new Status(false, TimeSpan.Zero, rec.Failures);
    }

    public Status Peek(string id)
    {
        if (!File.Exists(PathFor(id))) return new Status(false, TimeSpan.Zero, 0);
        using var f = Open(id);
        var rec = Read(f);
        long now = _now().ToUnixTimeSeconds();
        return now < rec.LockedUntilUnix
            ? new Status(true, TimeSpan.FromSeconds(rec.LockedUntilUnix - now), rec.Failures)
            : new Status(false, TimeSpan.Zero, rec.Failures);
    }

    private string PathFor(string id) => Path.Combine(_dir, id + ".bin");

    private FileStream Open(string id)
    {
        Directory.CreateDirectory(_dir);
        for (int attempt = 0; ; attempt++)
        {
            try { return new FileStream(PathFor(id), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException) when (attempt < 20) { Thread.Sleep(50); }
        }
    }

    private Record Read(FileStream f)
    {
        if (f.Length < RecordSize) return default;
        var buf = new byte[RecordSize];
        f.Position = 0;
        f.ReadExactly(buf);

        var rec = new Record
        {
            Failures = (int)Math.Min(BinaryPrimitives.ReadUInt32LittleEndian(buf), 1_000_000u),
            LastUnix = BinaryPrimitives.ReadInt64LittleEndian(buf.AsSpan(4)),
            LockedUntilUnix = BinaryPrimitives.ReadInt64LittleEndian(buf.AsSpan(12))
        };

        long now = _now().ToUnixTimeSeconds();
        if (rec.LockedUntilUnix > now + MaxLockSeconds) rec.LockedUntilUnix = now + MaxLockSeconds;
        return rec;
    }

    private static void Write(FileStream f, Record rec)
    {
        var buf = new byte[RecordSize];
        BinaryPrimitives.WriteUInt32LittleEndian(buf, (uint)rec.Failures);
        BinaryPrimitives.WriteInt64LittleEndian(buf.AsSpan(4), rec.LastUnix);
        BinaryPrimitives.WriteInt64LittleEndian(buf.AsSpan(12), rec.LockedUntilUnix);
        f.Position = 0;
        f.Write(buf);
        f.SetLength(RecordSize);
        f.Flush(true);
    }
}
