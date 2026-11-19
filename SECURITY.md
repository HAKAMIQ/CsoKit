# Security Policy

## Supported versions

Security reports are accepted for:

- the latest public release
- the current `main` branch

Older releases and unofficial builds are not supported.

## Reporting a security issue

Do not open a public GitHub issue for security problems.

Use GitHub Security Advisories if available, or contact the maintainer privately before sharing details in public.

Include:

- affected version, release, or commit
- clear steps to reproduce the issue
- what you expected to happen
- what actually happened
- the command and `--json` output, with private data removed

Remove usernames, private paths, tokens, and unrelated logs before sending a report.

Do not attach games, BIOS files, disc images, keys, firmware, or copyrighted media. A small crafted file that reproduces the problem is enough.

## In scope

- crashes or hangs caused by crafted CSO, ZSO, DAX, or ISO files
- out-of-bounds reads or excessive memory use from malformed headers or indexes
- output written to an unexpected path, or an existing file overwritten without `--force`
- partial or temporary output left behind after a failure
- loading a native library from an unexpected location

## Out of scope

- requests for games, BIOS files, disc images, keys, or firmware
- problems caused by modified binaries not published by this repository
- unsupported operating systems

## Legal note

CsoKit does not provide games, BIOS files, disc images, keys, firmware, or copyrighted user files. Users are responsible for processing only files they have the legal right to use.
