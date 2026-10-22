# Snowy cryptographic design — container format v2

Status: **unaudited**. Snow-II is a new construction. This document is the reference for reviewers;
`snow2_ref.py` is an independent implementation of it, and the test vectors below are reproduced by it.

## 1. Goals and non-goals
* Protect a file at rest against someone who gets a copy of the vault file (offline attacker).
* Detect any modification of header or ciphertext before anything is decrypted or written.
* Non-goals: hiding the file name or size, protecting against malware running as the user, side-channel
  resistance (the cipher is table-driven, not constant-time).

## 2. Container
```
"SNOW" | version=2 | mode | name_len u16 | name (UTF-8, <=1024) | length i64 | nonce[16] | salt[32] | per-mode fields
then: ciphertext (length bytes) | tag[32]
```
All integers little-endian. Per-mode fields:
* Simple (0): none.
* Moderate (1): kdf = memKiB u32, iterations u32, parallelism u8.
* Protected (2): tier u8 (1 puzzle, 2 A, 3 B, 4 C) | factors u8 | puzzle_kind u8 | hint_len u16 + hint | kdf.
Factors bitmask: 1 pattern, 2 password, 4 keyfile, 8 totp (reserved), 16 question.
`tag = HMAC-SHA256(macKey, header_bytes || ciphertext)` — the whole header is authenticated.
**There is deliberately no stored verifier of any secret** (no hash of an answer or keyfile): it would let an
attacker test guesses without running the KDF. A wrong secret is detected only by the tag, after the KDF.
KDF parameters read from a header are bounded (mem <= 2 GiB, iterations <= 64, parallelism <= 16) before use.

## 3. Key derivation
* Moderate/Protected: `secret = encode(factors)`, `root = Argon2id(secret, salt, m, t, p) -> 32 bytes`.
  `encode`: for each present factor in ascending tag order: `tag(1) | len u32 big-endian | bytes`.
  Pattern = canonical string "n-n-n"; password = UTF-8 of NFC(password), no case folding, no trimming;
  question answer = trim + NFC + lowercase; keyfile = SHA-256 of the file's bytes.
  Presets: Moderate 64 MiB/t3/p4; A 256 MiB/t6/p4; B 1 GiB/t10/p4; C 2 GiB/t12/p4; legacy single-factor puzzle uses Moderate.
* Simple: `root = HKDF-Extract(salt = file salt, ikm = SHA-256("Snowy::SimpleTier::ObfuscationSeed::v2"))`.
  The seed is compiled into the program, so Simple is **obfuscation, not encryption** — anyone with Snowy can read it.
* Split: `cipherKey = HKDF-Expand(root, "SNOWY-CIPH", 32)`, `macKey = HKDF-Expand(root, "SNOWY-MAC", 32)` (SHA-256).

## 4. Snow-II block cipher (256-bit block, 256-bit key, 18 rounds)
* State: 32 bytes as 4 rows x 8 columns, byte index = row + 4*col.
* Round keys: `RK_r = SHA-256("SNOWII-RK" | key | byte r) XOR SHA-256("SNOWII-RC" | byte r)`, r = 0..18.
* `state ^= RK_0`; rounds 1..18: SubBytes (AES S-box) -> ShiftRows (row r shifted left by 0,1,3,4 columns)
  -> MixColumns (AES matrix on each column; **omitted in round 18**) -> `^= RK_r`.
* It is Rijndael-256 shaped (same S-box, 8-column ShiftRows offsets, MixColumns) with a different, hash-based
  key schedule and 18 instead of 14 rounds.

## 5. Mode
CTR: keystream block i = `E_K(nonce[16] | 0x00*8 | i as u64 big-endian)`; ciphertext = plaintext XOR keystream (last
block truncated). Fresh random 16-byte nonce and 32-byte salt per file, so (key, nonce) never repeats.

## 6. Decryption order
parse header (bounds-check) -> rate-limit gate (Protected only) -> KDF -> constant-time tag check -> only then
decrypt and write. Failed attempts are counted before the KDF and erased on success; lockouts 3->5 s, 5->30 s,
10->5 min, 20->24 h, keyed by hash(nonce|salt). This slows guessing through Snowy only — not offline cracking.

## 7. Known limitations / questions for reviewers
1. Is a SHA-256-derived key schedule with independent 256-bit round keys sound for this round function? Any
   structural or distinguishing attack on 18-round Snow-II beyond Rijndael-256's?
2. CTR with a 128-bit random nonce: birthday margin across many files under one password-derived key (a new
   salt per file already makes keys differ).
3. The graphical-key factor has at most log2(9!/1!) = 18.5 bits (8 or 9 nodes); real patterns far less. Strength
   of Protected A is therefore dominated by its password. Tiers B/C get their strength from the 256-bit keyfile.
4. The file name is stored in clear in the (authenticated) header.
5. Whole file in memory; no streaming yet. Not constant-time.

## 8. Test vectors (key = 00..1f)
```
RK_0   2263c961e964c5ac6dacf5cb531c3dda8389acfd32db7d45a66afafe87b761b9
RK_18  78ab7f5ed2438d366d30febdcac9ba5ad44152534a979eeb2778f855ccdfa175
block  in = ff fe fd ... e0 (255-i)  ->  9118eef7176b10d9cf150ea51049269f2260b959074abeae709a26ff11cc16f2
CTR    nonce = a0..af, "The quick brown fox jumps over the lazy dog" ->
       c4b358ffd1ededeb5778074fe4259402dca53517f2d05a0a38610c13adaa3e05e311a47d23abe2da528c8d
HKDF   cipher 202b0ff29fd86f6ae166c20bba5a726ec45d809723419e21dcbb2549afb08e09
       mac    9ef18efdc95159358b3e8667a66708df62b636e51b2725e852f865b31c1373ca
HMAC   (macKey, 010203 | 040506) = 79c80fd66adff2f16ceee16dcfb8a9fb8d4e841aef1eb371232051241950a05f
```
