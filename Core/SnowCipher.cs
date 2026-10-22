using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Security.Cryptography;

namespace Snowy.Core;

public static class SnowCipher
{
    public const int BlockSize = 32;
    public const int KeySize = 32;
    public const int NonceSize = 16;

    private const int Rounds = 18;
    private const int Columns = 8;
    private const int ParallelThreshold = 4 * 1024 * 1024;
    private const int ChunkBlocks = 32 * 1024;

    private static readonly byte[] SBoxTable = BuildSBox();
    private static readonly uint[] T0 = new uint[256];
    private static readonly uint[] T1 = new uint[256];
    private static readonly uint[] T2 = new uint[256];
    private static readonly uint[] T3 = new uint[256];

    static SnowCipher()
    {
        for (int x = 0; x < 256; x++)
        {
            uint s = SBoxTable[x];
            uint s2 = XTime((byte)s);
            uint s3 = s2 ^ s;
            T0[x] = s2 | (s << 8) | (s << 16) | (s3 << 24);
            T1[x] = s3 | (s2 << 8) | (s << 16) | (s << 24);
            T2[x] = s | (s3 << 8) | (s2 << 16) | (s << 24);
            T3[x] = s | (s << 8) | (s3 << 16) | (s2 << 24);
        }
    }

    internal static byte[] SBox => SBoxTable;

    public static byte[] Transform(byte[] input, byte[] key, byte[] nonce)
        => Transform(input, 0, input.Length, key, nonce, allowParallel: true);

    internal static byte[] Transform(byte[] input, int offset, int count, byte[] key, byte[] nonce, bool allowParallel)
    {
        if (key.Length != KeySize) throw new ArgumentException($"key must be {KeySize} bytes", nameof(key));
        if (nonce.Length != NonceSize) throw new ArgumentException($"nonce must be {NonceSize} bytes", nameof(nonce));
        if (offset < 0 || count < 0 || offset > input.Length - count) throw new ArgumentOutOfRangeException(nameof(count));

        uint[] rk = ExpandKey(key);
        var output = new byte[count];
        long blocks = ((long)count + BlockSize - 1) / BlockSize;

        if (!allowParallel || count < ParallelThreshold || Environment.ProcessorCount == 1)
        {
            Process(rk, nonce, input, offset, output, 0, blocks, count);
        }
        else
        {
            long chunks = (blocks + ChunkBlocks - 1) / ChunkBlocks;
            Parallel.For(0, chunks, ci =>
            {
                long first = ci * ChunkBlocks;
                long n = Math.Min(ChunkBlocks, blocks - first);
                Process(rk, nonce, input, offset, output, first, n, count);
            });
        }

        Array.Clear(rk);
        return output;
    }

    private static void Process(uint[] rk, byte[] nonce, byte[] input, int inOffset, byte[] output,
                                long firstBlock, long blockCount, int totalLength)
    {
        Span<byte> ctr = stackalloc byte[BlockSize];
        Span<byte> ks = stackalloc byte[BlockSize];
        ctr.Clear();
        nonce.AsSpan().CopyTo(ctr);

        for (long b = 0; b < blockCount; b++)
        {
            long index = firstBlock + b;
            BinaryPrimitives.WriteUInt64BigEndian(ctr[24..], (ulong)index);
            EncryptBlock(rk, ctr, ks);

            int pos = (int)(index * BlockSize);
            int n = Math.Min(BlockSize, totalLength - pos);
            if (n == BlockSize)
            {
                var x = Vector256.Create<byte>(new ReadOnlySpan<byte>(input, inOffset + pos, BlockSize)) ^ Vector256.Create<byte>(ks);
                x.CopyTo(new Span<byte>(output, pos, BlockSize));
            }
            else
            {
                for (int i = 0; i < n; i++)
                    output[pos + i] = (byte)(input[inOffset + pos + i] ^ ks[i]);
            }
        }
    }

    internal static uint[] ExpandKey(ReadOnlySpan<byte> key)
    {
        if (key.Length != KeySize) throw new ArgumentException($"key must be {KeySize} bytes", nameof(key));

        var rk = new uint[(Rounds + 1) * Columns];
        Span<byte> rkInput = stackalloc byte[9 + KeySize + 1];
        Span<byte> rcInput = stackalloc byte[9 + 1];
        Span<byte> h = stackalloc byte[32];
        Span<byte> rc = stackalloc byte[32];

        "SNOWII-RK"u8.CopyTo(rkInput);
        key.CopyTo(rkInput[9..]);
        "SNOWII-RC"u8.CopyTo(rcInput);

        for (int r = 0; r <= Rounds; r++)
        {
            rkInput[^1] = (byte)r;
            rcInput[^1] = (byte)r;
            SHA256.HashData(rkInput, h);
            SHA256.HashData(rcInput, rc);
            for (int c = 0; c < Columns; c++)
                rk[r * Columns + c] = BinaryPrimitives.ReadUInt32LittleEndian(h[(4 * c)..])
                                    ^ BinaryPrimitives.ReadUInt32LittleEndian(rc[(4 * c)..]);
        }

        CryptographicOperations.ZeroMemory(rkInput);
        CryptographicOperations.ZeroMemory(h);
        return rk;
    }

    internal static void EncryptBlock(uint[] rk, ReadOnlySpan<byte> input, Span<byte> output)
    {
        if (rk.Length < (Rounds + 1) * Columns) throw new ArgumentException("round key array is too short", nameof(rk));
        if (input.Length < BlockSize || output.Length < BlockSize) throw new ArgumentException("a block is 32 bytes");

        ref uint r = ref MemoryMarshal.GetArrayDataReference(rk);
        ref uint t0 = ref MemoryMarshal.GetArrayDataReference(T0);
        ref uint t1 = ref MemoryMarshal.GetArrayDataReference(T1);
        ref uint t2 = ref MemoryMarshal.GetArrayDataReference(T2);
        ref uint t3 = ref MemoryMarshal.GetArrayDataReference(T3);

        uint s0 = BinaryPrimitives.ReadUInt32LittleEndian(input) ^ r;
        uint s1 = BinaryPrimitives.ReadUInt32LittleEndian(input[4..]) ^ Unsafe.Add(ref r, 1);
        uint s2 = BinaryPrimitives.ReadUInt32LittleEndian(input[8..]) ^ Unsafe.Add(ref r, 2);
        uint s3 = BinaryPrimitives.ReadUInt32LittleEndian(input[12..]) ^ Unsafe.Add(ref r, 3);
        uint s4 = BinaryPrimitives.ReadUInt32LittleEndian(input[16..]) ^ Unsafe.Add(ref r, 4);
        uint s5 = BinaryPrimitives.ReadUInt32LittleEndian(input[20..]) ^ Unsafe.Add(ref r, 5);
        uint s6 = BinaryPrimitives.ReadUInt32LittleEndian(input[24..]) ^ Unsafe.Add(ref r, 6);
        uint s7 = BinaryPrimitives.ReadUInt32LittleEndian(input[28..]) ^ Unsafe.Add(ref r, 7);

        for (int round = 1; round < Rounds; round++)
        {
            r = ref Unsafe.Add(ref r, Columns);
            uint u0 = Unsafe.Add(ref t0, (int)(s0 & 0xFF)) ^ Unsafe.Add(ref t1, (int)((s1 >> 8) & 0xFF)) ^ Unsafe.Add(ref t2, (int)((s3 >> 16) & 0xFF)) ^ Unsafe.Add(ref t3, (int)(s4 >> 24)) ^ r;
            uint u1 = Unsafe.Add(ref t0, (int)(s1 & 0xFF)) ^ Unsafe.Add(ref t1, (int)((s2 >> 8) & 0xFF)) ^ Unsafe.Add(ref t2, (int)((s4 >> 16) & 0xFF)) ^ Unsafe.Add(ref t3, (int)(s5 >> 24)) ^ Unsafe.Add(ref r, 1);
            uint u2 = Unsafe.Add(ref t0, (int)(s2 & 0xFF)) ^ Unsafe.Add(ref t1, (int)((s3 >> 8) & 0xFF)) ^ Unsafe.Add(ref t2, (int)((s5 >> 16) & 0xFF)) ^ Unsafe.Add(ref t3, (int)(s6 >> 24)) ^ Unsafe.Add(ref r, 2);
            uint u3 = Unsafe.Add(ref t0, (int)(s3 & 0xFF)) ^ Unsafe.Add(ref t1, (int)((s4 >> 8) & 0xFF)) ^ Unsafe.Add(ref t2, (int)((s6 >> 16) & 0xFF)) ^ Unsafe.Add(ref t3, (int)(s7 >> 24)) ^ Unsafe.Add(ref r, 3);
            uint u4 = Unsafe.Add(ref t0, (int)(s4 & 0xFF)) ^ Unsafe.Add(ref t1, (int)((s5 >> 8) & 0xFF)) ^ Unsafe.Add(ref t2, (int)((s7 >> 16) & 0xFF)) ^ Unsafe.Add(ref t3, (int)(s0 >> 24)) ^ Unsafe.Add(ref r, 4);
            uint u5 = Unsafe.Add(ref t0, (int)(s5 & 0xFF)) ^ Unsafe.Add(ref t1, (int)((s6 >> 8) & 0xFF)) ^ Unsafe.Add(ref t2, (int)((s0 >> 16) & 0xFF)) ^ Unsafe.Add(ref t3, (int)(s1 >> 24)) ^ Unsafe.Add(ref r, 5);
            uint u6 = Unsafe.Add(ref t0, (int)(s6 & 0xFF)) ^ Unsafe.Add(ref t1, (int)((s7 >> 8) & 0xFF)) ^ Unsafe.Add(ref t2, (int)((s1 >> 16) & 0xFF)) ^ Unsafe.Add(ref t3, (int)(s2 >> 24)) ^ Unsafe.Add(ref r, 6);
            uint u7 = Unsafe.Add(ref t0, (int)(s7 & 0xFF)) ^ Unsafe.Add(ref t1, (int)((s0 >> 8) & 0xFF)) ^ Unsafe.Add(ref t2, (int)((s2 >> 16) & 0xFF)) ^ Unsafe.Add(ref t3, (int)(s3 >> 24)) ^ Unsafe.Add(ref r, 7);
            s0 = u0; s1 = u1; s2 = u2; s3 = u3; s4 = u4; s5 = u5; s6 = u6; s7 = u7;
        }

        r = ref Unsafe.Add(ref r, Columns);
        ref byte sb = ref MemoryMarshal.GetArrayDataReference(SBoxTable);
        BinaryPrimitives.WriteUInt32LittleEndian(output,       Last(ref sb, s0, s1, s3, s4) ^ r);
        BinaryPrimitives.WriteUInt32LittleEndian(output[4..],  Last(ref sb, s1, s2, s4, s5) ^ Unsafe.Add(ref r, 1));
        BinaryPrimitives.WriteUInt32LittleEndian(output[8..],  Last(ref sb, s2, s3, s5, s6) ^ Unsafe.Add(ref r, 2));
        BinaryPrimitives.WriteUInt32LittleEndian(output[12..], Last(ref sb, s3, s4, s6, s7) ^ Unsafe.Add(ref r, 3));
        BinaryPrimitives.WriteUInt32LittleEndian(output[16..], Last(ref sb, s4, s5, s7, s0) ^ Unsafe.Add(ref r, 4));
        BinaryPrimitives.WriteUInt32LittleEndian(output[20..], Last(ref sb, s5, s6, s0, s1) ^ Unsafe.Add(ref r, 5));
        BinaryPrimitives.WriteUInt32LittleEndian(output[24..], Last(ref sb, s6, s7, s1, s2) ^ Unsafe.Add(ref r, 6));
        BinaryPrimitives.WriteUInt32LittleEndian(output[28..], Last(ref sb, s7, s0, s2, s3) ^ Unsafe.Add(ref r, 7));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint Last(ref byte sb, uint row0From, uint row1From, uint row2From, uint row3From)
        => (uint)Unsafe.Add(ref sb, (int)(row0From & 0xFF))
         | ((uint)Unsafe.Add(ref sb, (int)((row1From >> 8) & 0xFF)) << 8)
         | ((uint)Unsafe.Add(ref sb, (int)((row2From >> 16) & 0xFF)) << 16)
         | ((uint)Unsafe.Add(ref sb, (int)(row3From >> 24)) << 24);

    private static byte XTime(byte x) => (byte)((x << 1) ^ ((x & 0x80) != 0 ? 0x1B : 0));

    private static byte Rotl8(byte x, int n) => (byte)((x << n) | (x >> (8 - n)));

    private static byte[] BuildSBox()
    {
        var sbox = new byte[256];
        byte p = 1, q = 1;
        do
        {
            p = (byte)(p ^ (p << 1) ^ ((p & 0x80) != 0 ? 0x1B : 0));
            q ^= (byte)(q << 1);
            q ^= (byte)(q << 2);
            q ^= (byte)(q << 4);
            if ((q & 0x80) != 0) q ^= 0x09;

            byte x = (byte)(q ^ Rotl8(q, 1) ^ Rotl8(q, 2) ^ Rotl8(q, 3) ^ Rotl8(q, 4));
            sbox[p] = (byte)(x ^ 0x63);
        } while (p != 1);

        sbox[0] = 0x63;
        return sbox;
    }
}
