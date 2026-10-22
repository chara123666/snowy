using System.Security.Cryptography;
using Snowy.Core;
using Xunit;

namespace Snowy.Tests;

public class VaultEngineTests
{
    private static readonly KdfParams Fast = TestEnv.Fast;

    private const string Pw16 = "correct-horse-battery";
    private const string Pattern8 = "0-4-8-2-6-1-7-3";

    private static ProtectedRequest A(string pattern = Pattern8, string pw = "correct horse")
        => new() { Tier = ProtectedTier.A, Pattern = pattern, Password = pw };

    [Fact]
    public void Moderate_RoundTrip_RestoresContentAndName()
    {
        using var env = new TestEnv();
        var data = TestEnv.Random(5000);
        var input = env.Write("secret.bin", data);

        var vault = VaultEngine.EncryptModerate(input, Pw16, Fast);
        Assert.EndsWith(".sny", vault);

        var header = VaultEngine.ReadHeader(vault);
        Assert.Equal(VaultMode.Moderate, header.Mode);
        Assert.Equal(Fast, header.Kdf);
        Assert.Equal(5000L, header.OriginalLength);

        Assert.True(VaultEngine.TryDecryptModerate(vault, Pw16, env.OutDir, out var restored));
        Assert.Equal("secret.bin", Path.GetFileName(restored));
        Assert.Equal(data, File.ReadAllBytes(restored!));
    }

    [Fact]
    public void Moderate_PlaintextNeverAppearsInTheVault()
    {
        using var env = new TestEnv();
        var marker = System.Text.Encoding.ASCII.GetBytes("TOP-SECRET-MARKER-TOP-SECRET-MARKER");
        var input = env.Write("note.txt", marker);
        var vault = File.ReadAllBytes(VaultEngine.EncryptModerate(input, Pw16, Fast));
        Assert.True(vault.AsSpan().IndexOf(marker) < 0);
    }

    [Fact]
    public void Moderate_WrongPassword_FailsAndWritesNothing_AndIsCaseSensitive()
    {
        using var env = new TestEnv();
        var vault = VaultEngine.EncryptModerate(env.Write("a.bin", TestEnv.Random(300)), "Correct-Horse-Battery", Fast);

        Assert.False(VaultEngine.TryDecryptModerate(vault, "wrong", env.OutDir, out var p1));
        Assert.Null(p1);
        Assert.False(VaultEngine.TryDecryptModerate(vault, "correct-horse-battery", env.OutDir, out _));
        Assert.False(Directory.Exists(env.OutDir) && Directory.GetFiles(env.OutDir).Length > 0);
    }

    [Fact]
    public void Moderate_UnicodeCompositionDoesNotMatter_ButNothingElseIsForgiven()
    {
        using var env = new TestEnv();
        var vault = VaultEngine.EncryptModerate(env.Write("a.bin", TestEnv.Random(64)), "caf\u00e9-password-1", Fast);
        Assert.True(VaultEngine.TryDecryptModerate(vault, "cafe\u0301-password-1", env.OutDir, out _));
        Assert.False(VaultEngine.TryDecryptModerate(vault, " caf\u00e9-password-1", env.OutDir, out _));
    }

    [Fact]
    public void Moderate_TamperedCiphertext_IsRejected()
    {
        using var env = new TestEnv();
        var vault = VaultEngine.EncryptModerate(env.Write("a.bin", TestEnv.Random(2000)), Pw16, Fast);
        var bytes = File.ReadAllBytes(vault);
        bytes[bytes.Length / 2] ^= 0x01;
        File.WriteAllBytes(vault, bytes);

        Assert.False(VaultEngine.TryDecryptModerate(vault, Pw16, env.OutDir, out _));
    }

    [Fact]
    public void Moderate_TamperedMac_IsRejected()
    {
        using var env = new TestEnv();
        var vault = VaultEngine.EncryptModerate(env.Write("a.bin", TestEnv.Random(200)), Pw16, Fast);
        var bytes = File.ReadAllBytes(vault);
        bytes[^1] ^= 0x80;
        File.WriteAllBytes(vault, bytes);

        Assert.False(VaultEngine.TryDecryptModerate(vault, Pw16, env.OutDir, out _));
    }

    [Fact]
    public void Moderate_TamperedHeader_IsRejected()
    {
        using var env = new TestEnv();
        var vault = VaultEngine.EncryptModerate(env.Write("report.pdf", TestEnv.Random(200)), Pw16, Fast);
        var bytes = File.ReadAllBytes(vault);
        bytes[8] ^= 0x01;
        File.WriteAllBytes(vault, bytes);

        Assert.False(VaultEngine.TryDecryptModerate(vault, Pw16, env.OutDir, out _));
    }

    [Fact]
    public void Moderate_TruncatedFile_IsRejected()
    {
        using var env = new TestEnv();
        var vault = VaultEngine.EncryptModerate(env.Write("a.bin", TestEnv.Random(500)), Pw16, Fast);
        var bytes = File.ReadAllBytes(vault);
        File.WriteAllBytes(vault, bytes[..^10]);

        Assert.False(VaultEngine.TryDecryptModerate(vault, Pw16, env.OutDir, out _));
    }

    [Fact]
    public void Moderate_SameFileTwice_GivesDifferentVaults()
    {
        using var env = new TestEnv();
        var input = env.Write("a.bin", TestEnv.Random(500));

        var first = File.ReadAllBytes(VaultEngine.EncryptModerate(input, Pw16, Fast));
        var second = File.ReadAllBytes(VaultEngine.EncryptModerate(input, Pw16, Fast));
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Moderate_EmptyFile_RoundTrips()
    {
        using var env = new TestEnv();
        var vault = VaultEngine.EncryptModerate(env.Write("empty.bin", Array.Empty<byte>()), Pw16, Fast);
        Assert.True(VaultEngine.TryDecryptModerate(vault, Pw16, env.OutDir, out var restored));
        Assert.Empty(File.ReadAllBytes(restored!));
    }

    [Fact]
    public void Header_OldFormatVersion_IsRejectedClearly()
    {
        using var env = new TestEnv();
        var vault = VaultEngine.EncryptModerate(env.Write("a.bin", TestEnv.Random(100)), Pw16, Fast);
        var bytes = File.ReadAllBytes(vault);
        bytes[4] = 1;
        File.WriteAllBytes(vault, bytes);

        var ex = Assert.Throws<InvalidDataException>(() => VaultEngine.ReadHeader(vault));
        Assert.Contains("version", ex.Message);
    }

    [Fact]
    public void Header_AbsurdKdfCost_IsRejectedBeforeAnythingIsAllocated()
    {
        var h = new VaultHeader { Mode = VaultMode.Moderate, Kdf = new KdfParams(uint.MaxValue, 3, 4) };
        Assert.Throws<InvalidDataException>(() => VaultHeader.Deserialize(h.Serialize()));

        var h2 = new VaultHeader { Mode = VaultMode.Moderate, Kdf = new KdfParams(65536, 5000, 4) };
        Assert.Throws<InvalidDataException>(() => VaultHeader.Deserialize(h2.Serialize()));
    }

    [Fact]
    public void Header_GarbageAndShortInput_ThrowCleanly()
    {
        Assert.Throws<InvalidDataException>(() => VaultHeader.Deserialize(new byte[] { 1, 2, 3 }));
        Assert.Throws<InvalidDataException>(() => VaultHeader.Deserialize(TestEnv.Random(200)));
    }

    [Fact]
    public void Header_RoundTripsEveryField()
    {
        var h = new VaultHeader
        {
            Mode = VaultMode.Protected, OriginalFileName = "файл.txt", OriginalLength = 123456,
            Nonce = TestEnv.Random(16, 2), KeySalt = TestEnv.Random(32, 3),
            Tier = ProtectedTier.C, FactorsMask = Factors.Pattern | Factors.Password | Factors.Keyfile,
            PuzzleKind = PuzzleKind.Pattern, PuzzleHint = "hint", Kdf = KdfParams.TierC
        };
        var bytes = h.Serialize();
        var (back, read) = VaultHeader.Deserialize(bytes);

        Assert.Equal(bytes.Length, read);
        Assert.Equal(h.OriginalFileName, back.OriginalFileName);
        Assert.Equal(h.OriginalLength, back.OriginalLength);
        Assert.Equal(h.Nonce, back.Nonce);
        Assert.Equal(h.KeySalt, back.KeySalt);
        Assert.Equal(ProtectedTier.C, back.Tier);
        Assert.Equal(h.FactorsMask, back.FactorsMask);
        Assert.Equal("hint", back.PuzzleHint);
        Assert.Equal(KdfParams.TierC, back.Kdf);
    }

    [Fact]
    public void Simple_RoundTrip_NeedsNoSecret()
    {
        using var env = new TestEnv();
        var data = TestEnv.Random(777);
        var vault = VaultEngine.EncryptSimple(env.Write("s.bin", data));
        Assert.EndsWith(".sny", vault);
        Assert.Equal(VaultMode.Simple, VaultEngine.ReadHeader(vault).Mode);

        var restored = VaultEngine.DecryptSimple(vault, env.OutDir);
        Assert.Equal(data, File.ReadAllBytes(restored));
    }

    [Fact]
    public void Simple_HidesContent_ButUsesFreshKeysPerFile()
    {
        using var env = new TestEnv();
        var marker = System.Text.Encoding.ASCII.GetBytes("SIMPLE-TIER-MARKER-SIMPLE-TIER-MARKER");
        var input = env.Write("m.txt", marker);

        var first = File.ReadAllBytes(VaultEngine.EncryptSimple(input));
        var second = File.ReadAllBytes(VaultEngine.EncryptSimple(input));
        Assert.True(first.AsSpan().IndexOf(marker) < 0);
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Simple_Tampering_IsDetected()
    {
        using var env = new TestEnv();
        var vault = VaultEngine.EncryptSimple(env.Write("s.bin", TestEnv.Random(400)));
        var bytes = File.ReadAllBytes(vault);
        bytes[bytes.Length - 40] ^= 1;
        File.WriteAllBytes(vault, bytes);

        Assert.Throws<InvalidDataException>(() => VaultEngine.DecryptSimple(vault, env.OutDir));
    }

    [Fact]
    public void TierA_RoundTrip_NeedsBothFactors()
    {
        using var env = new TestEnv();
        var data = TestEnv.Random(3000);
        var vault = VaultEngine.EncryptProtected(env.Write("a.bin", data), A(), Fast);
        Assert.EndsWith(".snow", vault);

        var header = VaultEngine.ReadHeader(vault);
        Assert.Equal(ProtectedTier.A, header.Tier);
        Assert.Equal(Factors.Pattern | Factors.Password, header.FactorsMask);

        var ok = VaultEngine.DecryptProtected(vault, new ProtectedSecrets { Pattern = Pattern8, Password = "correct horse" }, env.OutDir);
        Assert.Equal(DecryptStatus.Success, ok.Status);
        Assert.Equal(data, File.ReadAllBytes(ok.OutputPath!));
    }

    [Fact]
    public void TierA_WrongOrMissingFactor_IsRefused()
    {
        using var env = new TestEnv();
        var vault = VaultEngine.EncryptProtected(env.Write("a.bin", TestEnv.Random(100)), A(), Fast);

        var wrongPattern = VaultEngine.DecryptProtected(vault, new ProtectedSecrets { Pattern = "0-1-2-3-4-5-6-8", Password = "correct horse" }, env.OutDir);
        Assert.Equal(DecryptStatus.WrongSecret, wrongPattern.Status);

        var wrongPassword = VaultEngine.DecryptProtected(vault, new ProtectedSecrets { Pattern = Pattern8, Password = "Correct horse" }, env.OutDir);
        Assert.Equal(DecryptStatus.WrongSecret, wrongPassword.Status);

        var noPassword = VaultEngine.DecryptProtected(vault, new ProtectedSecrets { Pattern = Pattern8 }, env.OutDir);
        Assert.Equal(DecryptStatus.MissingFactor, noPassword.Status);
        Assert.Equal(Factors.Password, noPassword.Missing);
    }

    [Fact]
    public void TierB_Keyfile_IsRequiredAndChecked()
    {
        using var env = new TestEnv();
        var keyfile = Path.Combine(env.Dir, "my.snokey");
        var otherKeyfile = Path.Combine(env.Dir, "other.snokey");
        Keyfile.Generate(keyfile);
        Keyfile.Generate(otherKeyfile);
        Assert.Equal(32, new FileInfo(keyfile).Length);

        var request = new ProtectedRequest { Tier = ProtectedTier.B, Password = "a-long-enough-password!", KeyfilePath = keyfile };
        var data = TestEnv.Random(1500);
        var vault = VaultEngine.EncryptProtected(env.Write("b.bin", data), request, Fast);

        var good = VaultEngine.DecryptProtected(vault, new ProtectedSecrets { Password = "a-long-enough-password!", KeyfilePath = keyfile }, env.OutDir);
        Assert.Equal(DecryptStatus.Success, good.Status);
        Assert.Equal(data, File.ReadAllBytes(good.OutputPath!));

        var wrongFile = VaultEngine.DecryptProtected(vault, new ProtectedSecrets { Password = "a-long-enough-password!", KeyfilePath = otherKeyfile }, env.OutDir);
        Assert.Equal(DecryptStatus.WrongSecret, wrongFile.Status);

        var noFile = VaultEngine.DecryptProtected(vault, new ProtectedSecrets { Password = "a-long-enough-password!" }, env.OutDir);
        Assert.Equal(DecryptStatus.MissingFactor, noFile.Status);
        Assert.Equal(Factors.Keyfile, noFile.Missing);
    }

    [Fact]
    public void UnreadableKeyfile_IsNotCountedAsAGuess()
    {
        using var env = new TestEnv();
        var keyfile = Path.Combine(env.Dir, "k.snokey");
        Keyfile.Generate(keyfile);
        var vault = VaultEngine.EncryptProtected(env.Write("b.bin", TestEnv.Random(100)),
            new ProtectedRequest { Tier = ProtectedTier.B, Password = "a-long-enough-password!", KeyfilePath = keyfile }, Fast);

        var header = VaultEngine.ReadHeader(vault);
        for (int i = 0; i < 5; i++)
        {
            var r = VaultEngine.DecryptProtected(vault,
                new ProtectedSecrets { Password = "a-long-enough-password!", KeyfilePath = Path.Combine(env.Dir, "nope.snokey") }, env.OutDir);
            Assert.Equal(DecryptStatus.KeyfileUnreadable, r.Status);
        }
        Assert.Equal(0, VaultEngine.Ledger.Peek(AttemptLedger.IdFor(header.Nonce, header.KeySalt)).Failures);
    }

    [Fact]
    public void TierC_NeedsAllThree()
    {
        using var env = new TestEnv();
        var keyfile = Path.Combine(env.Dir, "k.snokey");
        Keyfile.Generate(keyfile);
        var request = new ProtectedRequest
        {
            Tier = ProtectedTier.C, Pattern = Pattern8, Password = "sixteen+ characters!", KeyfilePath = keyfile
        };
        var data = TestEnv.Random(900);
        var vault = VaultEngine.EncryptProtected(env.Write("c.bin", data), request, Fast);

        var good = VaultEngine.DecryptProtected(vault,
            new ProtectedSecrets { Pattern = Pattern8, Password = "sixteen+ characters!", KeyfilePath = keyfile }, env.OutDir);
        Assert.Equal(DecryptStatus.Success, good.Status);

        var wrongPattern = VaultEngine.DecryptProtected(vault,
            new ProtectedSecrets { Pattern = "1-4-8-2-6-0-7-3", Password = "sixteen+ characters!", KeyfilePath = keyfile }, env.OutDir);
        Assert.Equal(DecryptStatus.WrongSecret, wrongPattern.Status);
    }

    [Fact]
    public void Protected_TamperedCiphertext_IsRejected()
    {
        using var env = new TestEnv();
        var vault = VaultEngine.EncryptProtected(env.Write("a.bin", TestEnv.Random(800)), A(), Fast);
        var bytes = File.ReadAllBytes(vault);
        bytes[bytes.Length - 100] ^= 4;
        File.WriteAllBytes(vault, bytes);

        var r = VaultEngine.DecryptProtected(vault, new ProtectedSecrets { Pattern = Pattern8, Password = "correct horse" }, env.OutDir);
        Assert.Equal(DecryptStatus.WrongSecret, r.Status);
    }

    [Fact]
    public void Protected_NoFastVerifierIsStoredInTheFile()
    {
        using var env = new TestEnv();
        var secret = "pattern-answer-xyz";
        var vault = File.ReadAllBytes(VaultEngine.EncryptProtected(env.Write("a.bin", TestEnv.Random(100)),
            new ProtectedRequest { Tier = ProtectedTier.Puzzle, PuzzleKind = PuzzleKind.Question, Hint = "q?", Answer = secret }, Fast));

        var header = VaultHeader.Deserialize(vault);
        var expectedHeader = new VaultHeader
        {
            Mode = VaultMode.Protected, OriginalFileName = "a.bin", OriginalLength = 100, Nonce = header.Header.Nonce,
            KeySalt = header.Header.KeySalt, Tier = ProtectedTier.Puzzle, FactorsMask = Factors.Question,
            PuzzleKind = PuzzleKind.Question, PuzzleHint = "q?", Kdf = Fast
        }.Serialize();
        Assert.Equal(expectedHeader.Length, header.BytesRead);
        Assert.Equal(expectedHeader, vault[..header.BytesRead]);
    }

    [Fact]
    public void LegacyPuzzle_Pattern_RoundTrips_ThroughTheOldApi()
    {
        using var env = new TestEnv();
        var data = TestEnv.Random(640);
        var vault = VaultEngine.EncryptProtected(env.Write("p.bin", data), PuzzleKind.Pattern, "draw it", "0-4-8-2-6", Fast);

        var header = VaultEngine.ReadHeader(vault);
        Assert.Equal(PuzzleKind.Pattern, header.PuzzleKind);
        Assert.Equal("draw it", header.PuzzleHint);
        Assert.True(VaultEngine.VerifyPuzzleAnswer(header, "anything"));

        Assert.False(VaultEngine.TryDecryptProtected(vault, "0-4-8-2-7", env.OutDir, out _));
        Assert.True(VaultEngine.TryDecryptProtected(vault, "0-4-8-2-6", env.OutDir, out var restored));
        Assert.Equal(data, File.ReadAllBytes(restored!));
    }

    [Fact]
    public void LegacyPuzzle_Question_IgnoresCaseAndSurroundingSpaces()
    {
        using var env = new TestEnv();
        var vault = VaultEngine.EncryptProtected(env.Write("q.bin", TestEnv.Random(64)), PuzzleKind.Question, "pet?", "Fluffy", Fast);

        Assert.True(VaultEngine.TryDecryptProtected(vault, "  fluffy ", env.OutDir, out _));
        Assert.False(VaultEngine.TryDecryptProtected(vault, "fluffy2", env.OutDir, out _));
    }

    [Fact]
    public void Policy_RejectsTooShortOrTrivialSecrets()
    {
        using var env = new TestEnv();
        var input = env.Write("x.bin", TestEnv.Random(10));

        PolicyViolationException Violation(ProtectedRequest r)
            => Assert.Throws<PolicyViolationException>(() => VaultEngine.EncryptProtected(input, r, Fast));

        Assert.Contains(PolicyIssue.PatternTooShort, Violation(A("0-4-8-2-6-1")).Issues);
        Assert.Contains(PolicyIssue.PatternTrivial, Violation(A("0-1-2-3-4-5-6-7")).Issues);
        Assert.Contains(PolicyIssue.PatternTrivial, Violation(A("8-7-6-5-4-3-2-1")).Issues);
        Assert.Contains(PolicyIssue.PatternInvalid, Violation(A("0-4-8-2-6-1-7-9")).Issues);
        Assert.Contains(PolicyIssue.PatternInvalid, Violation(A("0-4-8-2-6-1-7-7")).Issues);
        Assert.Contains(PolicyIssue.PasswordTooShort, Violation(A(pw: "short")).Issues);
        Assert.Contains(PolicyIssue.PasswordTooShort,
            Violation(new ProtectedRequest { Tier = ProtectedTier.B, Password = "fifteen-chars!!", KeyfilePath = "x" }).Issues);
    }

    [Fact]
    public void Policy_RequiresExactlyTheFactorsOfTheTier()
    {
        using var env = new TestEnv();
        var input = env.Write("x.bin", TestEnv.Random(10));

        foreach (var request in new[]
        {
            new ProtectedRequest { Tier = ProtectedTier.A, Password = "correct horse" },
            new ProtectedRequest { Tier = ProtectedTier.B, Password = "a-long-enough-password!" },
            new ProtectedRequest { Tier = ProtectedTier.A, Pattern = Pattern8, Password = "correct horse", KeyfilePath = "k" },
            new ProtectedRequest { Tier = ProtectedTier.Puzzle, Pattern = "0-1", Password = "extra-factor" }
        })
        {
            var ex = Assert.Throws<PolicyViolationException>(() => VaultEngine.EncryptProtected(input, request, Fast));
            Assert.Contains(PolicyIssue.WrongFactorSet, ex.Issues);
        }
    }

    [Fact]
    public void Policy_WeakButValidSecrets_AreAllowed()
    {
        using var env = new TestEnv();
        var vault = VaultEngine.EncryptProtected(env.Write("x.bin", TestEnv.Random(10)), A(pw: "aaaaaaaaaaaa"), Fast);
        Assert.True(File.Exists(vault));
    }

    private static DecryptResult Try(string vault, TestEnv env, string answer)
        => VaultEngine.DecryptProtected(vault, new ProtectedSecrets { Answer = answer }, env.OutDir);

    private static string QuestionVault(TestEnv env, string name = "r.bin")
        => VaultEngine.EncryptProtected(env.Write(name, TestEnv.Random(100)), PuzzleKind.Question, "q?", "right", Fast);

    [Fact]
    public void RateLimit_ThirdFailureLocks_AndLockedAttemptsAreRefusedWithoutCounting()
    {
        using var env = new TestEnv();
        var vault = QuestionVault(env);
        var h = VaultEngine.ReadHeader(vault);
        var id = AttemptLedger.IdFor(h.Nonce, h.KeySalt);

        Assert.Equal(TimeSpan.Zero, Try(vault, env, "bad1").RetryAfter);
        Assert.Equal(TimeSpan.Zero, Try(vault, env, "bad2").RetryAfter);

        var third = Try(vault, env, "bad3");
        Assert.Equal(DecryptStatus.WrongSecret, third.Status);
        Assert.Equal(TimeSpan.FromSeconds(5), third.RetryAfter);

        var locked = Try(vault, env, "right");
        Assert.Equal(DecryptStatus.Locked, locked.Status);
        Assert.InRange(locked.RetryAfter.TotalSeconds, 1.0, 5.0);
        Assert.Equal(3, VaultEngine.Ledger.Peek(id).Failures);

        env.Clock.Advance(TimeSpan.FromSeconds(6));
        var ok = Try(vault, env, "right");
        Assert.Equal(DecryptStatus.Success, ok.Status);
        Assert.Equal(0, VaultEngine.Ledger.Peek(id).Failures);
    }

    [Fact]
    public void RateLimit_Escalates()
    {
        Assert.Equal(0, AttemptLedger.DelaySeconds(2));
        Assert.Equal(5, AttemptLedger.DelaySeconds(3));
        Assert.Equal(5, AttemptLedger.DelaySeconds(4));
        Assert.Equal(30, AttemptLedger.DelaySeconds(5));
        Assert.Equal(300, AttemptLedger.DelaySeconds(10));
        Assert.Equal(86400, AttemptLedger.DelaySeconds(20));

        using var env = new TestEnv();
        var vault = QuestionVault(env);
        for (int i = 0; i < 4; i++)
        {
            Try(vault, env, "bad" + i);
            env.Clock.Advance(TimeSpan.FromSeconds(10));
        }
        var fifth = Try(vault, env, "bad4");
        Assert.Equal(TimeSpan.FromSeconds(30), fifth.RetryAfter);
    }

    [Fact]
    public void RateLimit_FollowsTheVault_NotItsPath()
    {
        using var env = new TestEnv();
        var vault = QuestionVault(env);
        var copy = Path.Combine(env.Dir, "renamed-copy.snow");
        File.Copy(vault, copy);

        for (int i = 0; i < 3; i++) Try(copy, env, "bad" + i);
        Assert.Equal(DecryptStatus.Locked, Try(vault, env, "right").Status);
    }

    [Fact]
    public void RateLimit_PersistsAcrossLedgerInstances()
    {
        using var env = new TestEnv();
        var vault = QuestionVault(env);
        for (int i = 0; i < 3; i++) Try(vault, env, "bad" + i);

        VaultEngine.Ledger = new AttemptLedger(Path.Combine(env.Dir, "attempts"), () => env.Clock.Now);
        Assert.Equal(DecryptStatus.Locked, Try(vault, env, "right").Status);
    }

    [Fact]
    public void RateLimit_DifferentVaultsAreIndependent()
    {
        using var env = new TestEnv();
        var one = QuestionVault(env, "one.bin");
        var two = QuestionVault(env, "two.bin");
        for (int i = 0; i < 3; i++) Try(one, env, "bad" + i);

        Assert.Equal(DecryptStatus.Locked, Try(one, env, "right").Status);
        Assert.Equal(DecryptStatus.Success, Try(two, env, "right").Status);
    }

    [Fact]
    public void Keyfile_Generate_RefusesToOverwrite_AndDigestIsStable()
    {
        using var env = new TestEnv();
        var path = Path.Combine(env.Dir, "k.snokey");
        Keyfile.Generate(path);
        Assert.Throws<IOException>(() => Keyfile.Generate(path));

        Assert.Equal(Keyfile.ReadDigest(path), SHA256.HashData(File.ReadAllBytes(path)));
        Assert.Equal(Keyfile.ReadDigest(path), Keyfile.ReadDigest(path));
    }
}
