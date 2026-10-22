# Security policy

## Read this first
Snowy is a hobby project built around a **custom, unaudited cipher (Snow-II)**. Do not rely on it for
secrets whose leak or loss would matter — use a vetted tool (for example VeraCrypt, `age`, or 7-Zip with AES-256).
What the design does and does not protect against is written down in [`docs/SPEC.md`](docs/SPEC.md).

## Reporting a vulnerability
Please **do not open a public issue** for a security problem. Use GitHub's private reporting:
*Security → Report a vulnerability* on this repository.

Useful reports include: a break or distinguisher for Snow-II, a flaw in the key derivation or container
format (`docs/SPEC.md`, section 7 lists open questions), a way to make a wrong secret look right, or a way to
get Snowy to write something other than a verified decryption. A proof-of-concept or a failing test helps.

This is a volunteer project: there is no bounty and no guaranteed response time, but reports are taken seriously.

## Supported versions
Only the latest release / `main`. The container format may change between 0.x versions.
