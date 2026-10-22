using Snowy.Core;

namespace Snowy.Tests;

internal static class RefRijndael
{
    public static byte Mul(byte a, byte b)
    {
        byte p = 0;
        for (int i = 0; i < 8; i++)
        {
            if ((b & 1) != 0) p ^= a;
            bool hi = (a & 0x80) != 0;
            a <<= 1;
            if (hi) a ^= 0x1B;
            b >>= 1;
        }
        return p;
    }

    public static byte[] Encrypt(byte[] block, int nb, int[] shifts, byte[][] roundKeys)
    {
        byte[] sbox = SnowCipher.SBox;
        var s = (byte[])block.Clone();
        Xor(s, roundKeys[0]);

        int last = roundKeys.Length - 1;
        for (int round = 1; round <= last; round++)
        {
            for (int i = 0; i < s.Length; i++) s[i] = sbox[s[i]];

            var t = new byte[s.Length];
            for (int r = 0; r < 4; r++)
                for (int c = 0; c < nb; c++)
                    t[r + 4 * c] = s[r + 4 * ((c + shifts[r]) % nb)];
            s = t;

            if (round < last) MixColumns(s, nb);
            Xor(s, roundKeys[round]);
        }
        return s;
    }

    private static void MixColumns(byte[] s, int nb)
    {
        for (int c = 0; c < nb; c++)
        {
            byte a0 = s[4 * c], a1 = s[4 * c + 1], a2 = s[4 * c + 2], a3 = s[4 * c + 3];
            s[4 * c]     = (byte)(Mul(a0, 2) ^ Mul(a1, 3) ^ a2 ^ a3);
            s[4 * c + 1] = (byte)(a0 ^ Mul(a1, 2) ^ Mul(a2, 3) ^ a3);
            s[4 * c + 2] = (byte)(a0 ^ a1 ^ Mul(a2, 2) ^ Mul(a3, 3));
            s[4 * c + 3] = (byte)(Mul(a0, 3) ^ a1 ^ a2 ^ Mul(a3, 2));
        }
    }

    private static void Xor(byte[] s, byte[] k)
    {
        for (int i = 0; i < s.Length; i++) s[i] ^= k[i];
    }

    public static byte[][] Aes128RoundKeys(byte[] key)
    {
        byte[] sbox = SnowCipher.SBox;
        var w = new byte[44][];
        for (int i = 0; i < 4; i++) w[i] = key[(4 * i)..(4 * i + 4)];

        byte rcon = 1;
        for (int i = 4; i < 44; i++)
        {
            var t = (byte[])w[i - 1].Clone();
            if (i % 4 == 0)
            {
                t = new[] { sbox[t[1]], sbox[t[2]], sbox[t[3]], sbox[t[0]] };
                t[0] ^= rcon;
                rcon = Mul(rcon, 2);
            }
            w[i] = new byte[4];
            for (int j = 0; j < 4; j++) w[i][j] = (byte)(w[i - 4][j] ^ t[j]);
        }

        var keys = new byte[11][];
        for (int r = 0; r < 11; r++)
            keys[r] = [.. w[4 * r], .. w[4 * r + 1], .. w[4 * r + 2], .. w[4 * r + 3]];
        return keys;
    }
}
