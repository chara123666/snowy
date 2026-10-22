# Snowy

A Windows desktop app for encrypting files locally, with a custom cipher (Snow-II), a graphical
pattern and optional keyfiles. Written in C# with WinForms on .NET 8.

> **Status: experimental and unaudited.** Snow-II is a new construction that nobody outside this project
> has reviewed. Do not use Snowy to protect data whose loss or exposure would matter. For that, use a vetted
> tool such as [VeraCrypt](https://www.veracrypt.fr/), [age](https://age-encryption.org/) or 7-Zip with AES-256.

## Protection levels

| Level | Extension | What it needs | Notes |
|-------|-----------|---------------|-------|
| **Simple** | `.sny` | nothing | Obfuscation only. The key is compiled into the program, so anyone with Snowy can open these files. |
| **Moderate** | `.sny` | a password | Argon2id (64 MiB, 3 passes). |
| **Protected** | `.snow` | a question or a pattern on a 3×3 grid | Single-factor puzzle, Argon2id (64 MiB, 3 passes). Wrong answers are rate-limited. |

The core also implements the multi-factor tiers **A** (pattern + password), **B** (password + keyfile) and
**C** (pattern + password + keyfile). Their key derivation is heavier (256 MiB, 1 GiB and 2 GiB of memory),
but **the UI for them is not finished yet**.

## How it works

```
secrets ──► Argon2id (salt, cost from header) ──► 32-byte root key
root key ──► HKDF-SHA256 ──► cipher key, MAC key
plaintext ──► Snow-II in CTR mode ──► ciphertext
header || ciphertext ──► HMAC-SHA256 ──► tag
file = header || ciphertext || tag
```

* **Encrypt-then-MAC.** The tag covers the whole header, so every field (mode, factors, KDF cost,
  original name and length) is authenticated.
* **Verify before write.** On decryption the tag is checked in constant time. Nothing is decrypted or
  written to disk until it matches.
* **No fast verifier.** The file contains nothing derived from a secret that would let someone test a guess
  without running the full key derivation. A wrong secret is only detected by the tag, after Argon2.
* **Rate limiting.** Failed attempts on a Protected file are counted per file (keyed by its nonce and salt,
  so renaming or copying it does not reset the count). After 3 failures the file locks for 5 s; the delay grows
  to 30 s at 5, 5 min at 10, and 24 h at 20. Successful decryption clears the count. This slows typing guesses
  into Snowy only. It does not stop someone who copies the file and cracks it elsewhere.
* **Header limits.** KDF parameters read from a file are bounded (memory ≤ 2 GiB, iterations ≤ 64,
  parallelism ≤ 16) before anything is allocated.

The full design, including the container layout and the open questions, is in
[`docs/SPEC.md`](docs/SPEC.md).

### Snow-II

A 256-bit block cipher with 18 rounds, built on the structure of Rijndael-256:

* S-box, ShiftRows (offsets 0, 1, 3, 4 on 8 columns) and MixColumns are the standard ones.
* Round keys come from SHA-256 with domain tags `SNOWII-RK` and `SNOWII-RC`, XORed together.
* The cipher is used in CTR mode with a 16-byte random nonce per file.

The key schedule and the round composition are this project's own design. The implementation is table-driven,
so it is not constant-time. It is not AES.

## Features

* Drag and drop files onto the window, or pick them with the dialog.
* Three themes (Snowy, Fire, Nature) with animated transitions.
* Interface in English, Russian and German.
* A history tab with links that open the result in Explorer.
* An optional falling-snow effect.
* Two short UI sounds, if you supply the audio files (see below).

## Building

Requirements: Windows 10 or 11, the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0), and
Visual Studio 2022 17.13 or newer (optional).

```
git clone https://github.com/<your-user>/snowy.git
cd snowy
dotnet build Snowy.csproj -c Release
dotnet run --project Snowy.csproj
```

The NuGet dependency [Konscious.Security.Cryptography.Argon2](https://www.nuget.org/packages/Konscious.Security.Cryptography.Argon2)
is restored automatically.

### Optional assets

* `icon.ico` in the project root is used as the application icon when present.
* `Sounds/ui.mp3` and `Sounds/theme.mp3` are played when present. They are not in the repository because
  their licenses are not included here. Use your own recordings or public-domain sounds. Without them the app is silent.

## Tests

```
dotnet test Snowy.Tests/Snowy.Tests.csproj
```

The suite has 61 tests and covers:

* the AES building blocks, checked against FIPS-197 and .NET's own AES;
* the table-driven Snow-II against a naive reference implementation;
* frozen test vectors (also reproduced by `docs/snow2_ref.py`, an independent Python implementation);
* round-trips, tampering, truncation, wrong secrets, keyfiles and rate limiting.

Run the Python reference on its own with `python docs/snow2_ref.py`.

## Project layout

```
Core/          cipher, vault format, key derivation, keyfiles, rate limiting, strength estimates
UI/            WinForms: main window, dialogs, themes, animation, localisation (I18n.cs)
Snowy.Tests/   unit tests
docs/          SPEC.md (design), snow2_ref.py (reference implementation)
Fonts/         Lancelot-Regular.ttf (SIL Open Font License, see Fonts/OFL.txt)
Sounds/        place optional UI sounds here (not tracked)
```

## Security

Found a problem? Please report it privately, not in a public issue. See [SECURITY.md](SECURITY.md).

Cryptanalysis is especially welcome. The open questions are listed in `docs/SPEC.md` section 7.

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md). Changes to the file format or the cipher need a matching update to
the spec, the test vectors and the Python reference.

## Known limitations

* Snow-II is unaudited and not constant-time.
* Protected tiers A, B and C are not yet selectable in the interface.
* Files are read entirely into memory. There is no streaming, so very large files need a lot of RAM.
* The original file name is stored in the header in plain text. The file size is visible too.
* Old `.sny` and `.snow` files from earlier builds do not open (container version 2 only).
* Passwords are ordinary .NET strings and cannot be wiped from memory.

## License

MIT, see [LICENSE](LICENSE). Third-party components are listed in
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
