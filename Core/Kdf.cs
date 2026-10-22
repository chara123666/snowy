using Konscious.Security.Cryptography;

namespace Snowy.Core;

public sealed record KdfParams(uint MemoryKiB, uint Iterations, byte Parallelism)
{
    public static readonly KdfParams Moderate = new(64 * 1024, 3, 4);
    public static readonly KdfParams TierA    = new(256 * 1024, 6, 4);
    public static readonly KdfParams TierB    = new(1024 * 1024, 10, 4);
    public static readonly KdfParams TierC    = new(2048 * 1024, 12, 4);

    public const uint MaxMemoryKiB = 2048 * 1024;
    public const uint MaxIterations = 64;
    public const byte MaxParallelism = 16;

    public bool IsValid(out string error)
    {
        if (Parallelism < 1 || Parallelism > MaxParallelism) { error = "KDF parallelism out of range"; return false; }
        if (Iterations < 1 || Iterations > MaxIterations)    { error = "KDF iteration count out of range"; return false; }
        if (MemoryKiB < 8u * Parallelism || MemoryKiB > MaxMemoryKiB) { error = "KDF memory size out of range"; return false; }
        error = "";
        return true;
    }

    public void Validate()
    {
        if (!IsValid(out var error)) throw new InvalidDataException(error);
    }
}

internal static class VaultKdf
{
    public static byte[] DeriveRoot(byte[] encodedSecret, byte[] salt, KdfParams p)
    {
        p.Validate();
        var argon2 = new Argon2id(encodedSecret)
        {
            Salt = salt,
            MemorySize = (int)p.MemoryKiB,
            Iterations = (int)p.Iterations,
            DegreeOfParallelism = p.Parallelism
        };
        try
        {
            return argon2.GetBytes(CryptoUtils.KeySize);
        }
        catch (OutOfMemoryException)
        {
            throw new InvalidOperationException(
                $"not enough free memory for this protection level (needs about {p.MemoryKiB / 1024} MiB)");
        }
        finally
        {
            (argon2 as IDisposable)?.Dispose();
        }
    }
}
