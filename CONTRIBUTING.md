# Contributing

Thanks for taking a look! Bug reports, ideas and pull requests are welcome.

## Set up
Windows 10/11 and the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).
```
git clone <your fork>
cd snowy
dotnet build
dotnet test Snowy.Tests
```
Open `Snowy.slnx` in Visual Studio 2022 (17.13+) or just run `dotnet run`.

## Ground rules
* **Tests must pass.** Add a test for any behavior you change.
* **Crypto changes are special.** If you touch `Core/` in a way that changes bytes on disk, update
  `docs/SPEC.md`, the vectors in `Snowy.Tests/VectorTests.cs` **and** `docs/snow2_ref.py`, and keep
  `python3 docs/snow2_ref.py` passing — the Python file is an independent implementation, which is the point.
  Bump the container version if old files would stop opening.
* **No fast verifiers.** Never store anything derived from a secret in the file that lets someone check a guess
  without the full key derivation.
* **UI text goes through `I18n`** with English, Russian and German entries.
* **Only add assets you may redistribute** (fonts, sounds, icons) and list them in `THIRD-PARTY-NOTICES.md`.
* New dependencies: please open an issue first.

## Pull requests
Keep them focused, describe what and why, and mention anything you could not test (for example the WinForms
side if you only built the core).
