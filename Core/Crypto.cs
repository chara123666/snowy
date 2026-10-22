using System.Security.Cryptography;
using System.Text;

namespace Snowy.Core;

public static class CryptoUtils
{
    public const int SaltSize  = 32;
    public const int NonceSize = SnowCipher.NonceSize;
    public const int KeySize   = SnowCipher.KeySize;
    public const int MacSize   = 32;

    internal static readonly byte[] ObfuscationSeed = SHA256.HashData("Snowy::SimpleTier::ObfuscationSeed::v2"u8);

    public static byte[] RandomBytes(int len)
    {
        var b = new byte[len];
        RandomNumberGenerator.Fill(b);
        return b;
    }

    public static string GenerateStrongPassword(int length = 40)
    {
        const string alphabet =
            "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#$%^&*-_=+";
        var sb = new StringBuilder(length);
        for (int i = 0; i < length; i++)
            sb.Append(alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)]);
        return sb.ToString();
    }

    internal static (byte[] Cipher, byte[] Mac) SplitKeys(byte[] root) => (
        HKDF.Expand(HashAlgorithmName.SHA256, root, KeySize, "SNOWY-CIPH"u8.ToArray()),
        HKDF.Expand(HashAlgorithmName.SHA256, root, KeySize, "SNOWY-MAC"u8.ToArray()));

    public static byte[] ComputeMac(byte[] macKey, ReadOnlySpan<byte> header, ReadOnlySpan<byte> ciphertext)
    {
        using var h = IncrementalHash.CreateHMAC(HashAlgorithmName.SHA256, macKey);
        h.AppendData(header);
        h.AppendData(ciphertext);
        return h.GetHashAndReset();
    }

    internal static string NormalizePassword(string s) => s.Normalize(NormalizationForm.FormC);

    internal static string NormalizeAnswer(string s) => s.Trim().Normalize(NormalizationForm.FormC).ToLowerInvariant();

    internal static void Zero(byte[]? data)
    {
        if (data != null) CryptographicOperations.ZeroMemory(data);
    }
}

public static class PatternCodec
{
    public static string Encode(IEnumerable<int> nodes) => string.Join("-", nodes);

    public static int[] Decode(string pattern) =>
        pattern.Split('-', StringSplitOptions.RemoveEmptyEntries)
               .Select(int.Parse)
               .ToArray();
}

[Flags]
public enum Factors : byte
{
    None     = 0,
    Pattern  = 1,
    Password = 2,
    Keyfile  = 4,
    Totp     = 8,
    Question = 16
}

internal static class FactorEncoder
{
    public static byte[] Encode(Factors mask, string? pattern, string? answer, string? password, byte[]? keyfileDigest)
    {
        using var ms = new MemoryStream();

        if (mask.HasFlag(Factors.Pattern))
            Write(ms, (byte)Factors.Pattern, Encoding.UTF8.GetBytes(pattern ?? throw Missing("pattern")));

        if (mask.HasFlag(Factors.Password))
            Write(ms, (byte)Factors.Password, Encoding.UTF8.GetBytes(CryptoUtils.NormalizePassword(password ?? throw Missing("password"))));

        if (mask.HasFlag(Factors.Keyfile))
            Write(ms, (byte)Factors.Keyfile, keyfileDigest ?? throw Missing("keyfile"));

        if (mask.HasFlag(Factors.Question))
            Write(ms, (byte)Factors.Question, Encoding.UTF8.GetBytes(CryptoUtils.NormalizeAnswer(answer ?? throw Missing("answer"))));

        return ms.ToArray();
    }

    private static void Write(MemoryStream ms, byte tag, byte[] data)
    {
        ms.WriteByte(tag);
        Span<byte> len = stackalloc byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(len, (uint)data.Length);
        ms.Write(len);
        ms.Write(data);
    }

    private static ArgumentException Missing(string what) => new($"factor '{what}' is required but was not supplied");
}
