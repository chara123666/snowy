using Snowy.Core;
using Xunit;

namespace Snowy.Tests;

public class VectorTests
{
    private static readonly byte[] Key = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();

    private static string Hex(byte[] b) => Convert.ToHexString(b).ToLowerInvariant();

    [Fact]
    public void RoundKeys_0_And_18()
    {
        uint[] rk = SnowCipher.ExpandKey(Key);
        byte[] RoundKey(int r)
        {
            var b = new byte[32];
            for (int c = 0; c < 8; c++)
                System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(4 * c), rk[r * 8 + c]);
            return b;
        }
        Assert.Equal("2263c961e964c5ac6dacf5cb531c3dda8389acfd32db7d45a66afafe87b761b9", Hex(RoundKey(0)));
        Assert.Equal("78ab7f5ed2438d366d30febdcac9ba5ad44152534a979eeb2778f855ccdfa175", Hex(RoundKey(18)));
    }

    [Fact]
    public void SingleBlock()
    {
        var block = Enumerable.Range(0, 32).Select(i => (byte)(255 - i)).ToArray();
        var output = new byte[32];
        SnowCipher.EncryptBlock(SnowCipher.ExpandKey(Key), block, output);
        Assert.Equal("9118eef7176b10d9cf150ea51049269f2260b959074abeae709a26ff11cc16f2", Hex(output));
    }

    [Fact]
    public void CtrMessage_SpanningTwoBlocks()
    {
        var nonce = Enumerable.Range(0, 16).Select(i => (byte)(0xA0 + i)).ToArray();
        var plain = System.Text.Encoding.ASCII.GetBytes("The quick brown fox jumps over the lazy dog");
        Assert.Equal(43, plain.Length);
        Assert.Equal(
            "c4b358ffd1ededeb5778074fe4259402dca53517f2d05a0a38610c13adaa3e05e311a47d23abe2da528c8d",
            Hex(SnowCipher.Transform(plain, Key, nonce)));
    }

    [Fact]
    public void HkdfSplit_And_Mac()
    {
        var (cipher, mac) = CryptoUtils.SplitKeys(Key);
        Assert.Equal("202b0ff29fd86f6ae166c20bba5a726ec45d809723419e21dcbb2549afb08e09", Hex(cipher));
        Assert.Equal("9ef18efdc95159358b3e8667a66708df62b636e51b2725e852f865b31c1373ca", Hex(mac));
        Assert.Equal(
            "79c80fd66adff2f16ceee16dcfb8a9fb8d4e841aef1eb371232051241950a05f",
            Hex(CryptoUtils.ComputeMac(mac, new byte[] { 1, 2, 3 }, new byte[] { 4, 5, 6 })));
    }
}
