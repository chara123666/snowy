#!/usr/bin/env python3
"""Snow-II reference implementation — written from the spec, independent of the C# code.

Deliberately slow and obvious: bytes, lists, no tables. Use it to check the C# implementation, to
experiment (reduced rounds, differential/linear searches) or to cross-check a rewrite.
`python3 snow2_ref.py` runs the self-check against the published test vectors.
"""
import hashlib, hmac, struct

ROUNDS = 18
COLS = 8
SHIFTS = (0, 1, 3, 4)          # ShiftRows offsets for rows 0..3 on an 8-column state


def gmul(a, b):
    p = 0
    for _ in range(8):
        if b & 1:
            p ^= a
        hi = a & 0x80
        a = (a << 1) & 0xFF
        if hi:
            a ^= 0x1B
        b >>= 1
    return p


def _rotl8(x, n):
    return ((x << n) | (x >> (8 - n))) & 0xFF


def _make_sbox():
    # AES S-box by definition: multiplicative inverse in GF(2^8), then the affine map.
    # (The C# code generates it differently — walking the field with generator 3.)
    sbox = []
    for x in range(256):
        inv = 0 if x == 0 else next(y for y in range(1, 256) if gmul(x, y) == 1)
        s = inv ^ _rotl8(inv, 1) ^ _rotl8(inv, 2) ^ _rotl8(inv, 3) ^ _rotl8(inv, 4) ^ 0x63
        sbox.append(s)
    return sbox


SBOX = _make_sbox()


def round_keys(key: bytes):
    assert len(key) == 32
    keys = []
    for r in range(ROUNDS + 1):
        rk = hashlib.sha256(b"SNOWII-RK" + key + bytes([r])).digest()
        rc = hashlib.sha256(b"SNOWII-RC" + bytes([r])).digest()
        keys.append(bytes(a ^ b for a, b in zip(rk, rc)))
    return keys


def encrypt_block(rks, block: bytes) -> bytes:
    assert len(block) == 32
    s = [b ^ k for b, k in zip(block, rks[0])]            # state[row + 4*col]
    for rnd in range(1, ROUNDS + 1):
        s = [SBOX[b] for b in s]                          # SubBytes
        t = [0] * 32                                      # ShiftRows
        for r in range(4):
            for c in range(COLS):
                t[r + 4 * c] = s[r + 4 * ((c + SHIFTS[r]) % COLS)]
        s = t
        if rnd < ROUNDS:                                  # MixColumns (skipped in the last round)
            for c in range(COLS):
                a0, a1, a2, a3 = s[4 * c:4 * c + 4]
                s[4 * c + 0] = gmul(a0, 2) ^ gmul(a1, 3) ^ a2 ^ a3
                s[4 * c + 1] = a0 ^ gmul(a1, 2) ^ gmul(a2, 3) ^ a3
                s[4 * c + 2] = a0 ^ a1 ^ gmul(a2, 2) ^ gmul(a3, 3)
                s[4 * c + 3] = gmul(a0, 3) ^ a1 ^ a2 ^ gmul(a3, 2)
        s = [b ^ k for b, k in zip(s, rks[rnd])]          # AddRoundKey
    return bytes(s)


def ctr(key: bytes, nonce: bytes, data: bytes) -> bytes:
    assert len(nonce) == 16
    rks = round_keys(key)
    out = bytearray()
    for i in range(0, len(data), 32):
        counter_block = nonce + bytes(8) + struct.pack(">Q", i // 32)
        ks = encrypt_block(rks, counter_block)
        out += bytes(a ^ b for a, b in zip(data[i:i + 32], ks))
    return bytes(out)


def hkdf_expand_32(prk: bytes, info: bytes) -> bytes:
    return hmac.new(prk, info + b"\x01", hashlib.sha256).digest()   # one output block is all we need


def split_keys(root: bytes):
    return hkdf_expand_32(root, b"SNOWY-CIPH"), hkdf_expand_32(root, b"SNOWY-MAC")


def mac(mac_key: bytes, header: bytes, ciphertext: bytes) -> bytes:
    return hmac.new(mac_key, header + ciphertext, hashlib.sha256).digest()


VECTORS = {
    "key":       bytes(range(32)),
    "block_in":  bytes(255 - i for i in range(32)),
    "nonce":     bytes(0xA0 + i for i in range(16)),
    "plaintext": b"The quick brown fox jumps over the lazy dog",
    "rk0":   "2263c961e964c5ac6dacf5cb531c3dda8389acfd32db7d45a66afafe87b761b9",
    "rk18":  "78ab7f5ed2438d366d30febdcac9ba5ad44152534a979eeb2778f855ccdfa175",
    "block": "9118eef7176b10d9cf150ea51049269f2260b959074abeae709a26ff11cc16f2",
    "ctr":   "c4b358ffd1ededeb5778074fe4259402dca53517f2d05a0a38610c13adaa3e05e311a47d23abe2da528c8d",
    "hkdf_cipher": "202b0ff29fd86f6ae166c20bba5a726ec45d809723419e21dcbb2549afb08e09",
    "hkdf_mac":    "9ef18efdc95159358b3e8667a66708df62b636e51b2725e852f865b31c1373ca",
    "mac":   "79c80fd66adff2f16ceee16dcfb8a9fb8d4e841aef1eb371232051241950a05f",
}


def self_check():
    v = VECTORS
    assert SBOX[0x00] == 0x63 and SBOX[0x53] == 0xED and SBOX[0xFF] == 0x16
    rks = round_keys(v["key"])
    assert rks[0].hex() == v["rk0"], "round key 0"
    assert rks[18].hex() == v["rk18"], "round key 18"
    assert encrypt_block(rks, v["block_in"]).hex() == v["block"], "block"
    assert ctr(v["key"], v["nonce"], v["plaintext"]).hex() == v["ctr"], "ctr"
    c, m = split_keys(v["key"])
    assert c.hex() == v["hkdf_cipher"] and m.hex() == v["hkdf_mac"], "hkdf"
    assert mac(m, bytes([1, 2, 3]), bytes([4, 5, 6])).hex() == v["mac"], "mac"
    print("all Snow-II reference vectors match")


if __name__ == "__main__":
    self_check()
