using System.Security.Cryptography;
using System.Text;

namespace Snowy.Core;

public enum VaultMode : byte { Simple = 0, Moderate = 1, Protected = 2 }
public enum PuzzleKind : byte { Question = 0, Pattern = 1 }

public enum ProtectedTier : byte { Puzzle = 1, A = 2, B = 3, C = 4 }

public enum DecryptStatus { Success, WrongSecret, Locked, MissingFactor, KeyfileUnreadable, Corrupt }

public readonly record struct DecryptResult(
    DecryptStatus Status, string? OutputPath = null, TimeSpan RetryAfter = default, Factors Missing = Factors.None);

public class ProtectedSecrets
{
    public string? Pattern { get; set; }
    public string? Answer { get; set; }
    public string? Password { get; set; }
    public string? KeyfilePath { get; set; }
}

public sealed class ProtectedRequest : ProtectedSecrets
{
    public ProtectedTier Tier { get; set; } = ProtectedTier.A;
    public string Hint { get; set; } = "";
    public PuzzleKind PuzzleKind { get; set; } = PuzzleKind.Pattern;
}

public sealed class VaultHeader
{
    internal const byte CurrentVersion = 2;
    public const int MaxTextBytes = 1024;

    private static readonly byte[] Magic = "SNOW"u8.ToArray();

    public byte Version = CurrentVersion;
    public VaultMode Mode;
    public string OriginalFileName = "";
    public long OriginalLength;
    public byte[] Nonce = new byte[CryptoUtils.NonceSize];
    public byte[] KeySalt = new byte[CryptoUtils.SaltSize];

    public KdfParams Kdf = KdfParams.Moderate;

    public ProtectedTier Tier;
    public Factors FactorsMask;
    public PuzzleKind PuzzleKind;
    public string PuzzleHint = "";

    public string TargetExtension => Mode == VaultMode.Protected ? ".snow" : ".sny";

    public byte[] Serialize()
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);

        w.Write(Magic);
        w.Write(Version);
        w.Write((byte)Mode);
        WriteString(w, OriginalFileName);
        w.Write(OriginalLength);
        w.Write(Nonce);
        w.Write(KeySalt);

        switch (Mode)
        {
            case VaultMode.Moderate:
                WriteKdf(w, Kdf);
                break;
            case VaultMode.Protected:
                w.Write((byte)Tier);
                w.Write((byte)FactorsMask);
                w.Write((byte)PuzzleKind);
                WriteString(w, PuzzleHint);
                WriteKdf(w, Kdf);
                break;
        }

        w.Flush();
        return ms.ToArray();
    }

    public static (VaultHeader Header, int BytesRead) Deserialize(byte[] buffer)
    {
        try { return DeserializeCore(buffer); }
        catch (EndOfStreamException) { throw new InvalidDataException("the file is truncated"); }
    }

    private static (VaultHeader, int) DeserializeCore(byte[] buffer)
    {
        using var ms = new MemoryStream(buffer);
        using var r = new BinaryReader(ms);

        var magic = r.ReadBytes(4);
        if (magic.Length != 4 || !magic.AsSpan().SequenceEqual(Magic))
            throw new InvalidDataException("not a Snowy container (bad signature)");

        byte version = r.ReadByte();
        if (version != CurrentVersion)
            throw new InvalidDataException(
                $"unsupported container version {version} — this build reads version {CurrentVersion}; files made by older Snowy builds can't be opened");

        byte mode = r.ReadByte();
        if (!Enum.IsDefined((VaultMode)mode)) throw new InvalidDataException("unknown protection mode");

        var h = new VaultHeader { Version = version, Mode = (VaultMode)mode };
        h.OriginalFileName = ReadString(r);
        h.OriginalLength = r.ReadInt64();
        if (h.OriginalLength < 0) throw new InvalidDataException("invalid length in header");
        h.Nonce = ReadExact(r, CryptoUtils.NonceSize);
        h.KeySalt = ReadExact(r, CryptoUtils.SaltSize);

        switch (h.Mode)
        {
            case VaultMode.Moderate:
                h.Kdf = ReadKdf(r);
                break;
            case VaultMode.Protected:
                byte tier = r.ReadByte();
                if (!Enum.IsDefined((ProtectedTier)tier)) throw new InvalidDataException("unknown protection tier");
                h.Tier = (ProtectedTier)tier;
                h.FactorsMask = (Factors)r.ReadByte();
                byte kind = r.ReadByte();
                if (!Enum.IsDefined((PuzzleKind)kind)) throw new InvalidDataException("unknown puzzle kind");
                h.PuzzleKind = (PuzzleKind)kind;
                h.PuzzleHint = ReadString(r);
                h.Kdf = ReadKdf(r);
                break;
        }

        return (h, (int)ms.Position);
    }

    private static void WriteKdf(BinaryWriter w, KdfParams p)
    {
        w.Write(p.MemoryKiB);
        w.Write(p.Iterations);
        w.Write(p.Parallelism);
    }

    private static KdfParams ReadKdf(BinaryReader r)
    {
        var p = new KdfParams(r.ReadUInt32(), r.ReadUInt32(), r.ReadByte());
        p.Validate();
        return p;
    }

    private static void WriteString(BinaryWriter w, string s)
    {
        var bytes = Encoding.UTF8.GetBytes(s);
        if (bytes.Length > MaxTextBytes) throw new ArgumentException($"text is too long (max {MaxTextBytes} bytes)");
        w.Write((ushort)bytes.Length);
        w.Write(bytes);
    }

    private static string ReadString(BinaryReader r)
    {
        int len = r.ReadUInt16();
        if (len > MaxTextBytes) throw new InvalidDataException("invalid text length in header");
        return Encoding.UTF8.GetString(ReadExact(r, len));
    }

    private static byte[] ReadExact(BinaryReader r, int n)
    {
        var b = r.ReadBytes(n);
        if (b.Length != n) throw new EndOfStreamException();
        return b;
    }
}

public static class VaultEngine
{
    internal readonly record struct Keys(byte[] Cipher, byte[] Mac)
    {
        public void Zero() { CryptoUtils.Zero(Cipher); CryptoUtils.Zero(Mac); }
    }

    internal static AttemptLedger Ledger { get; set; } = AttemptLedger.Default;

    public static KdfParams KdfFor(ProtectedTier tier) => tier switch
    {
        ProtectedTier.A => KdfParams.TierA,
        ProtectedTier.B => KdfParams.TierB,
        ProtectedTier.C => KdfParams.TierC,
        _ => KdfParams.Moderate
    };

    public static string EncryptSimple(string inputPath)
    {
        var header = NewHeader(inputPath, VaultMode.Simple);
        return Seal(inputPath, header, SimpleKeys(header.KeySalt));
    }

    public static string EncryptModerate(string inputPath, string password)
        => EncryptModerate(inputPath, password, KdfParams.Moderate);

    internal static string EncryptModerate(string inputPath, string password, KdfParams kdf)
    {
        if (string.IsNullOrEmpty(password)) throw new ArgumentException("password is empty", nameof(password));

        var header = NewHeader(inputPath, VaultMode.Moderate);
        header.Kdf = kdf;
        var secret = FactorEncoder.Encode(Factors.Password, null, null, password, null);
        return Seal(inputPath, header, DeriveKeys(secret, header));
    }

    public static string EncryptProtected(string inputPath, PuzzleKind kind, string hint, string answer)
        => EncryptProtected(inputPath, PuzzleRequest(kind, hint, answer), KdfParams.Moderate);

    internal static string EncryptProtected(string inputPath, PuzzleKind kind, string hint, string answer, KdfParams kdf)
        => EncryptProtected(inputPath, PuzzleRequest(kind, hint, answer), kdf);

    public static string EncryptProtected(string inputPath, ProtectedRequest request)
        => EncryptProtected(inputPath, request, KdfFor(request.Tier));

    internal static string EncryptProtected(string inputPath, ProtectedRequest request, KdfParams kdf)
    {
        var issues = ProtectedPolicy.Check(request);
        if (issues.Count > 0) throw new PolicyViolationException(issues);

        var mask = ProtectedPolicy.MaskOf(request);
        var header = NewHeader(inputPath, VaultMode.Protected);
        header.Tier = request.Tier;
        header.FactorsMask = mask;
        header.PuzzleKind = request.PuzzleKind;
        header.PuzzleHint = request.Hint;
        header.Kdf = kdf;

        byte[]? keyfileDigest = mask.HasFlag(Factors.Keyfile) ? Keyfile.ReadDigest(request.KeyfilePath!) : null;
        var secret = FactorEncoder.Encode(mask, request.Pattern, request.Answer, request.Password, keyfileDigest);
        CryptoUtils.Zero(keyfileDigest);
        return Seal(inputPath, header, DeriveKeys(secret, header));
    }

    private static ProtectedRequest PuzzleRequest(PuzzleKind kind, string hint, string answer)
    {
        var r = new ProtectedRequest { Tier = ProtectedTier.Puzzle, Hint = hint, PuzzleKind = kind };
        if (kind == PuzzleKind.Pattern) r.Pattern = answer; else r.Answer = answer;
        return r;
    }

    public static VaultHeader ReadHeader(string vaultPath)
    {
        using var fs = File.OpenRead(vaultPath);
        var buf = new byte[(int)Math.Min(4096, fs.Length)];
        fs.ReadExactly(buf);
        return VaultHeader.Deserialize(buf).Header;
    }

    public static string DecryptSimple(string vaultPath, string outputDir)
    {
        var full = File.ReadAllBytes(vaultPath);
        var (header, headerLen) = VaultHeader.Deserialize(full);
        if (header.Mode != VaultMode.Simple) throw new InvalidDataException("this is not a Simple-tier file");
        if (!LayoutOk(full, header, headerLen)) throw new InvalidDataException("the file is damaged");

        return OpenAndWrite(full, header, headerLen, SimpleKeys(header.KeySalt), outputDir)
            ?? throw new InvalidDataException("the file is damaged or has been modified");
    }

    public static bool TryDecryptModerate(string vaultPath, string password, string outputDir, out string? outputPath)
    {
        outputPath = null;
        var full = File.ReadAllBytes(vaultPath);
        var (header, headerLen) = VaultHeader.Deserialize(full);
        if (header.Mode != VaultMode.Moderate || !LayoutOk(full, header, headerLen) || string.IsNullOrEmpty(password))
            return false;

        var secret = FactorEncoder.Encode(Factors.Password, null, null, password, null);
        outputPath = OpenAndWrite(full, header, headerLen, DeriveKeys(secret, header), outputDir);
        return outputPath != null;
    }

    public static bool VerifyPuzzleAnswer(VaultHeader header, string answer) => true;

    public static bool TryDecryptProtected(string vaultPath, string answer, string outputDir, out string? outputPath)
    {
        var header = ReadHeader(vaultPath);
        var secrets = new ProtectedSecrets();
        if (header.FactorsMask.HasFlag(Factors.Pattern)) secrets.Pattern = answer;
        if (header.FactorsMask.HasFlag(Factors.Question)) secrets.Answer = answer;

        var result = DecryptProtected(vaultPath, secrets, outputDir);
        outputPath = result.OutputPath;
        return result.Status == DecryptStatus.Success;
    }

    public static DecryptResult DecryptProtected(string vaultPath, ProtectedSecrets secrets, string outputDir)
    {
        var full = File.ReadAllBytes(vaultPath);
        var (header, headerLen) = VaultHeader.Deserialize(full);
        if (header.Mode != VaultMode.Protected || !LayoutOk(full, header, headerLen))
            return new DecryptResult(DecryptStatus.Corrupt);

        var mask = header.FactorsMask;
        var missing = Factors.None;
        if (mask.HasFlag(Factors.Pattern) && string.IsNullOrEmpty(secrets.Pattern)) missing |= Factors.Pattern;
        if (mask.HasFlag(Factors.Password) && string.IsNullOrEmpty(secrets.Password)) missing |= Factors.Password;
        if (mask.HasFlag(Factors.Keyfile) && string.IsNullOrEmpty(secrets.KeyfilePath)) missing |= Factors.Keyfile;
        if (mask.HasFlag(Factors.Question) && string.IsNullOrWhiteSpace(secrets.Answer)) missing |= Factors.Question;
        if (mask.HasFlag(Factors.Totp)) missing |= Factors.Totp;
        if (missing != Factors.None) return new DecryptResult(DecryptStatus.MissingFactor, Missing: missing);

        byte[]? keyfileDigest = null;
        if (mask.HasFlag(Factors.Keyfile))
        {
            try { keyfileDigest = Keyfile.ReadDigest(secrets.KeyfilePath!); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
            {
                return new DecryptResult(DecryptStatus.KeyfileUnreadable);
            }
        }

        string id = AttemptLedger.IdFor(header.Nonce, header.KeySalt);
        var gate = Ledger.BeginAttempt(id);
        if (gate.Locked)
        {
            CryptoUtils.Zero(keyfileDigest);
            return new DecryptResult(DecryptStatus.Locked, RetryAfter: gate.Remaining);
        }

        var secret = FactorEncoder.Encode(mask, secrets.Pattern, secrets.Answer, secrets.Password, keyfileDigest);
        CryptoUtils.Zero(keyfileDigest);

        var path = OpenAndWrite(full, header, headerLen, DeriveKeys(secret, header), outputDir);
        if (path != null)
        {
            Ledger.RecordSuccess(id);
            return new DecryptResult(DecryptStatus.Success, path);
        }

        var after = Ledger.RecordFailure(id);
        return new DecryptResult(DecryptStatus.WrongSecret, RetryAfter: after.Locked ? after.Remaining : default);
    }

    private static VaultHeader NewHeader(string inputPath, VaultMode mode) => new()
    {
        Mode = mode,
        OriginalFileName = Path.GetFileName(inputPath),
        Nonce = CryptoUtils.RandomBytes(CryptoUtils.NonceSize),
        KeySalt = CryptoUtils.RandomBytes(CryptoUtils.SaltSize)
    };

    private static Keys DeriveKeys(byte[] encodedSecret, VaultHeader header)
    {
        byte[] root;
        try { root = VaultKdf.DeriveRoot(encodedSecret, header.KeySalt, header.Kdf); }
        finally { CryptoUtils.Zero(encodedSecret); }

        var (cipher, mac) = CryptoUtils.SplitKeys(root);
        CryptoUtils.Zero(root);
        return new Keys(cipher, mac);
    }

    private static Keys SimpleKeys(byte[] salt)
    {
        var prk = HKDF.Extract(HashAlgorithmName.SHA256, CryptoUtils.ObfuscationSeed, salt);
        var (cipher, mac) = CryptoUtils.SplitKeys(prk);
        CryptoUtils.Zero(prk);
        return new Keys(cipher, mac);
    }

    private static bool LayoutOk(byte[] full, VaultHeader header, int headerLen)
        => (long)full.Length == headerLen + header.OriginalLength + CryptoUtils.MacSize;

    private static string Seal(string inputPath, VaultHeader header, Keys keys)
    {
        byte[]? plaintext = null;
        try
        {
            plaintext = File.ReadAllBytes(inputPath);
            header.OriginalLength = plaintext.Length;

            var ciphertext = SnowCipher.Transform(plaintext, keys.Cipher, header.Nonce);
            var headerBytes = header.Serialize();
            var mac = CryptoUtils.ComputeMac(keys.Mac, headerBytes, ciphertext);

            var outputPath = Path.Combine(
                Path.GetDirectoryName(Path.GetFullPath(inputPath)) ?? ".",
                Path.GetFileNameWithoutExtension(inputPath) + header.TargetExtension);

            using (var fs = File.Create(outputPath))
            {
                fs.Write(headerBytes);
                fs.Write(ciphertext);
                fs.Write(mac);
            }
            return outputPath;
        }
        finally
        {
            keys.Zero();
            CryptoUtils.Zero(plaintext);
        }
    }

    private static string? OpenAndWrite(byte[] full, VaultHeader header, int headerLen, Keys keys, string outputDir)
    {
        byte[]? plaintext = null;
        try
        {
            int macStart = full.Length - CryptoUtils.MacSize;
            var expected = CryptoUtils.ComputeMac(
                keys.Mac, full.AsSpan(0, headerLen), full.AsSpan(headerLen, macStart - headerLen));
            if (!CryptographicOperations.FixedTimeEquals(expected, full.AsSpan(macStart, CryptoUtils.MacSize)))
                return null;

            plaintext = SnowCipher.Transform(full, headerLen, macStart - headerLen, keys.Cipher, header.Nonce, allowParallel: true);

            Directory.CreateDirectory(outputDir);
            var safeName = Path.GetFileName(header.OriginalFileName);
            if (string.IsNullOrEmpty(safeName)) safeName = "decrypted.bin";
            var outPath = Path.Combine(outputDir, safeName);
            File.WriteAllBytes(outPath, plaintext);
            return outPath;
        }
        finally
        {
            keys.Zero();
            CryptoUtils.Zero(plaintext);
        }
    }
}
