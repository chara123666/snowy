using System.Security.Cryptography;

namespace Snowy.Core;

public static class Keyfile
{
    public const int GeneratedSize = 32;

    public static void Generate(string path)
    {
        var bytes = RandomNumberGenerator.GetBytes(GeneratedSize);
        using (var fs = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            fs.Write(bytes);
        CryptographicOperations.ZeroMemory(bytes);
    }

    public static byte[] ReadDigest(string path)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return SHA256.HashData(fs);
    }
}
