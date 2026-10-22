using Snowy.Core;
using Xunit;

namespace Snowy.Tests;

public class MiscTests
{
    [Fact]
    public void FactorEncoding_IsUnambiguous()
    {
        var both = Factors.Pattern | Factors.Password;
        var a = FactorEncoder.Encode(both, "ab", null, "c", null);
        var b = FactorEncoder.Encode(both, "a", null, "bc", null);
        Assert.NotEqual(a, b);

        Assert.NotEqual(
            FactorEncoder.Encode(Factors.Password, null, null, "same", null),
            FactorEncoder.Encode(Factors.Question, null, "same", null, null));
    }

    [Fact]
    public void FactorEncoding_PasswordsAreCaseSensitive_AnswersAreNot()
    {
        Assert.NotEqual(
            FactorEncoder.Encode(Factors.Password, null, null, "Pass", null),
            FactorEncoder.Encode(Factors.Password, null, null, "pass", null));

        Assert.Equal(
            FactorEncoder.Encode(Factors.Question, null, "  Cat ", null, null),
            FactorEncoder.Encode(Factors.Question, null, "cat", null, null));
    }

    [Fact]
    public void FactorEncoding_MissingFactor_Throws()
    {
        Assert.Throws<ArgumentException>(() => FactorEncoder.Encode(Factors.Password, null, null, null, null));
    }

    [Fact]
    public void PatternEntropy_MatchesTheCombinatorics()
    {
        Assert.Equal(Math.Log2(3024), Entropy.PatternBits(new[] { 0, 1, 2, 3 }), 6);
        Assert.Equal(Math.Log2(362880), Entropy.PatternBits(new[] { 0, 1, 2, 3, 4, 5, 6, 7 }), 6);
        Assert.Equal(Entropy.PatternBits(new[] { 0, 1, 2, 3, 4, 5, 6, 7 }), Entropy.PatternBits(new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8 }), 6);
        Assert.Equal(0.0, Entropy.PatternBits(Array.Empty<int>()), 6);

        Assert.True(Entropy.PatternBits(new[] { 0, 1, 2, 3, 4, 5, 6, 7 }) < 19);
    }

    [Fact]
    public void PasswordEstimate_PunishesObviousChoices()
    {
        double random = Entropy.PasswordBits("k9#Vq!2zT@x7Lp4M");
        double repeated = Entropy.PasswordBits("aaaaaaaaaaaaaaaa");
        double sequence = Entropy.PasswordBits("abcdefghijklmnop");
        double common = Entropy.PasswordBits("password123456");

        Assert.True(random > 80);
        Assert.True(repeated < 30);
        Assert.True(sequence < 40);
        Assert.True(common < 40);
        Assert.Equal(Strength.VeryWeak, Entropy.LevelFor(repeated));
        Assert.Equal(Strength.Strong, Entropy.LevelFor(random));
        Assert.Equal(0.0, Entropy.PasswordBits(""), 6);
    }

    [Fact]
    public void GeneratedPassword_HasRequestedLengthAndAlphabet()
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#$%^&*-_=+";
        var p = CryptoUtils.GenerateStrongPassword(40);
        Assert.Equal(40, p.Length);
        Assert.True(p.All(alphabet.Contains));
        Assert.NotEqual(p, CryptoUtils.GenerateStrongPassword(40));
        Assert.Equal(12, CryptoUtils.GenerateStrongPassword(12).Length);
    }

    [Fact]
    public void GeneratedPassword_ReachesEveryCharacterOfTheAlphabet()
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#$%^&*-_=+";
        var seen = new HashSet<char>();
        for (int i = 0; i < 1000; i++) foreach (char c in CryptoUtils.GenerateStrongPassword(40)) seen.Add(c);
        Assert.Equal(alphabet.Length, seen.Count);
        Assert.Equal(69, alphabet.Length);
    }

    [Fact]
    public void KdfParams_ValidationBounds()
    {
        Assert.True(KdfParams.Moderate.IsValid(out _));
        Assert.True(KdfParams.TierC.IsValid(out _));
        Assert.False(new KdfParams(KdfParams.MaxMemoryKiB + 1, 3, 4).IsValid(out _));
        Assert.False(new KdfParams(65536, 0, 4).IsValid(out _));
        Assert.False(new KdfParams(65536, 3, 0).IsValid(out _));
        Assert.False(new KdfParams(8, 3, 4).IsValid(out _));
    }

    [Fact]
    public void Tiers_GetIncreasinglyExpensiveKdfs()
    {
        Assert.True(VaultEngine.KdfFor(ProtectedTier.A).MemoryKiB < VaultEngine.KdfFor(ProtectedTier.B).MemoryKiB);
        Assert.True(VaultEngine.KdfFor(ProtectedTier.B).MemoryKiB < VaultEngine.KdfFor(ProtectedTier.C).MemoryKiB);
        Assert.Equal(KdfParams.Moderate, VaultEngine.KdfFor(ProtectedTier.Puzzle));
    }
}
