using System.Buffers.Binary;
using System.Numerics;
using System.Security.Cryptography;
using Snowy.Core;
using Xunit;

namespace Snowy.Tests;

public class CipherTests
{
    private static readonly int[] SnowShifts = { 0, 1, 3, 4 };

    private static byte[] Hex(string s) => Convert.FromHexString(s);

    private static byte[][] SnowRoundKeys(byte[] key)
    {
        uint[] words = SnowCipher.ExpandKey(key);
        var keys = new byte[words.Length / 8][];
        for (int r = 0; r < keys.Length; r++)
        {
            keys[r] = new byte[32];
            for (int c = 0; c < 8; c++)
                BinaryPrimitives.WriteUInt32LittleEndian(keys[r].AsSpan(4 * c), words[r * 8 + c]);
        }
        return keys;
    }

    private static int BitDiff(byte[] a, byte[] b)
    {
        int n = 0;
        for (int i = 0; i < a.Length; i++) n += BitOperations.PopCount((uint)(a[i] ^ b[i]));
        return n;
    }

    [Fact]
    public void SBox_FirstRowAndLastEntry_MatchAes()
    {
        byte[] firstRow = { 0x63, 0x7c, 0x77, 0x7b, 0xf2, 0x6b, 0x6f, 0xc5, 0x30, 0x01, 0x67, 0x2b, 0xfe, 0xd7, 0xab, 0x76 };
        for (int i = 0; i < 16; i++) Assert.Equal(firstRow[i], SnowCipher.SBox[i]);
        Assert.Equal((byte)0xED, SnowCipher.SBox[0x53]);
        Assert.Equal((byte)0x16, SnowCipher.SBox[0xFF]);
    }

    [Fact]
    public void Reference_Aes128_MatchesFips197Vector()
    {
        var key = Hex("000102030405060708090a0b0c0d0e0f");
        var plain = Hex("00112233445566778899aabbccddeeff");
        var cipher = RefRijndael.Encrypt(plain, 4, new[] { 0, 1, 2, 3 }, RefRijndael.Aes128RoundKeys(key));
        Assert.Equal(Hex("69c4e0d86a7b0430d8cdb78070b4c55a"), cipher);
    }

    [Fact]
    public void Reference_Aes128_MatchesDotNetAes()
    {
        var rng = new Random(7);
        for (int i = 0; i < 50; i++)
        {
            var key = new byte[16]; rng.NextBytes(key);
            var block = new byte[16]; rng.NextBytes(block);

            using var aes = Aes.Create();
            aes.Key = key;
            var expected = aes.EncryptEcb(block, PaddingMode.None);
            var actual = RefRijndael.Encrypt(block, 4, new[] { 0, 1, 2, 3 }, RefRijndael.Aes128RoundKeys(key));
            Assert.Equal(expected, actual);
        }
    }

    [Fact]
    public void KeySchedule_FollowsTheSpec()
    {
        var key = new byte[32]; new Random(1).NextBytes(key);
        var keys = SnowRoundKeys(key);
        Assert.Equal(19, keys.Length);

        for (int r = 0; r < keys.Length; r++)
        {
            var rk = SHA256.HashData([.. "SNOWII-RK"u8.ToArray(), .. key, (byte)r]);
            var rc = SHA256.HashData([.. "SNOWII-RC"u8.ToArray(), (byte)r]);
            var expected = new byte[32];
            for (int i = 0; i < 32; i++) expected[i] = (byte)(rk[i] ^ rc[i]);
            Assert.Equal(expected, keys[r]);
        }
    }

    [Fact]
    public void TableImplementation_MatchesNaiveReference()
    {
        var rng = new Random(2024);
        for (int i = 0; i < 200; i++)
        {
            var key = new byte[32]; rng.NextBytes(key);
            var block = new byte[32]; rng.NextBytes(block);

            var expected = RefRijndael.Encrypt(block, 8, SnowShifts, SnowRoundKeys(key));
            var actual = new byte[32];
            SnowCipher.EncryptBlock(SnowCipher.ExpandKey(key), block, actual);
            Assert.Equal(expected, actual);
        }
    }

    [Fact]
    public void FlippingOneInputBit_ChangesAboutHalfTheOutputBits()
    {
        var rng = new Random(11);
        var key = new byte[32]; rng.NextBytes(key);
        var rk = SnowCipher.ExpandKey(key);

        double total = 0;
        const int samples = 300;
        for (int i = 0; i < samples; i++)
        {
            var a = new byte[32]; rng.NextBytes(a);
            var b = (byte[])a.Clone();
            int bit = rng.Next(256);
            b[bit / 8] ^= (byte)(1 << (bit % 8));

            var ea = new byte[32]; var eb = new byte[32];
            SnowCipher.EncryptBlock(rk, a, ea);
            SnowCipher.EncryptBlock(rk, b, eb);
            total += BitDiff(ea, eb);
        }
        Assert.InRange(total / samples, 124.0, 132.0);
    }

    [Fact]
    public void FlippingOneKeyBit_ChangesAboutHalfTheOutputBits()
    {
        var rng = new Random(12);
        var block = new byte[32]; rng.NextBytes(block);

        double total = 0;
        const int samples = 300;
        for (int i = 0; i < samples; i++)
        {
            var k1 = new byte[32]; rng.NextBytes(k1);
            var k2 = (byte[])k1.Clone();
            int bit = rng.Next(256);
            k2[bit / 8] ^= (byte)(1 << (bit % 8));

            var e1 = new byte[32]; var e2 = new byte[32];
            SnowCipher.EncryptBlock(SnowCipher.ExpandKey(k1), block, e1);
            SnowCipher.EncryptBlock(SnowCipher.ExpandKey(k2), block, e2);
            total += BitDiff(e1, e2);
        }
        Assert.InRange(total / samples, 124.0, 132.0);
    }

    [Fact]
    public void Ctr_KeystreamFollowsCounterBlockLayout()
    {
        var key = new byte[32]; new Random(3).NextBytes(key);
        var nonce = new byte[16]; new Random(4).NextBytes(nonce);
        var rk = SnowCipher.ExpandKey(key);

        var stream = SnowCipher.Transform(new byte[96], key, nonce);
        for (int block = 0; block < 3; block++)
        {
            var ctr = new byte[32];
            nonce.CopyTo(ctr, 0);
            BinaryPrimitives.WriteUInt64BigEndian(ctr.AsSpan(24), (ulong)block);
            var expected = new byte[32];
            SnowCipher.EncryptBlock(rk, ctr, expected);
            Assert.Equal(expected, stream[(block * 32)..(block * 32 + 32)]);
        }
    }

    [Fact]
    public void Ctr_RoundTrips_AtAwkwardLengths()
    {
        var rng = new Random(5);
        var key = new byte[32]; rng.NextBytes(key);
        var nonce = new byte[16]; rng.NextBytes(nonce);

        foreach (int len in new[] { 0, 1, 15, 31, 32, 33, 63, 64, 65, 1000, 100_000 })
        {
            var plain = new byte[len]; rng.NextBytes(plain);
            var cipher = SnowCipher.Transform(plain, key, nonce);
            Assert.Equal(len, cipher.Length);
            if (len >= 16) Assert.NotEqual(plain, cipher);
            Assert.Equal(plain, SnowCipher.Transform(cipher, key, nonce));
        }
    }

    [Fact]
    public void Ctr_DifferentNonceOrKey_GivesDifferentStream()
    {
        var key = new byte[32]; new Random(6).NextBytes(key);
        var key2 = (byte[])key.Clone(); key2[31] ^= 1;
        var nonce = new byte[16]; new Random(8).NextBytes(nonce);
        var nonce2 = (byte[])nonce.Clone(); nonce2[0] ^= 1;
        var zeros = new byte[128];

        var baseStream = SnowCipher.Transform(zeros, key, nonce);
        Assert.NotEqual(baseStream, SnowCipher.Transform(zeros, key, nonce2));
        Assert.NotEqual(baseStream, SnowCipher.Transform(zeros, key2, nonce));
    }

    [Fact]
    public void Ctr_NoRepeatedKeystreamBlocks()
    {
        var key = new byte[32]; new Random(9).NextBytes(key);
        var nonce = new byte[16];
        var stream = SnowCipher.Transform(new byte[32 * 2000], key, nonce);

        var seen = new HashSet<string>();
        for (int i = 0; i < 2000; i++)
            Assert.True(seen.Add(Convert.ToHexString(stream, i * 32, 32)));
    }

    [Fact]
    public void Ctr_ParallelAndSequentialAgree_AndOffsetsWork()
    {
        var rng = new Random(10);
        var key = new byte[32]; rng.NextBytes(key);
        var nonce = new byte[16]; rng.NextBytes(nonce);

        var data = new byte[9 * 1024 * 1024 + 123];
        rng.NextBytes(data);

        var seq = SnowCipher.Transform(data, 0, data.Length, key, nonce, allowParallel: false);
        var par = SnowCipher.Transform(data, 0, data.Length, key, nonce, allowParallel: true);
        Assert.True(seq.AsSpan().SequenceEqual(par));

        var slice = data[1000..5000];
        var viaOffset = SnowCipher.Transform(data, 1000, 4000, key, nonce, allowParallel: false);
        Assert.Equal(SnowCipher.Transform(slice, key, nonce), viaOffset);
    }

    [Fact]
    public void Transform_RejectsWrongKeyOrNonceLength()
    {
        Assert.Throws<ArgumentException>(() => SnowCipher.Transform(new byte[10], new byte[16], new byte[16]));
        Assert.Throws<ArgumentException>(() => SnowCipher.Transform(new byte[10], new byte[32], new byte[12]));
    }
}
