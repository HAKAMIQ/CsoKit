# CsoKit

CsoKit is a Windows x64 toolkit for PSP ISO and CSO files. Use the desktop app for normal work, or the CLI when you want scripting and exact control.

## Download

Grab the latest Windows x64 package from the [Releases page](https://github.com/HAKAMIQ/CsoKit/releases/latest):

    csokit-*-win-x64.zip

Extract it anywhere. Keep `CsoKit.Native.dll` next to the executables.

## Quick start

Desktop app:

    .\CsoKit.App.exe

CLI help:

    .\csokit.exe --help

Version:

    .\csokit.exe --version

## Common commands

Show CSO information:

    .\csokit.exe info ".\game.cso"

Verify a CSO file:

    .\csokit.exe verify ".\game.cso"

Deep-verify and calculate SHA256:

    .\csokit.exe verify ".\game.cso" --deep --sha256

Detect an input format:

    .\csokit.exe detect ".\game.iso"

Analyze a PSP ISO:

    .\csokit.exe analyze ".\game.iso" --psp

Compress ISO to CSO:

    .\csokit.exe compress ".\game.iso"

Use the fast profile:

    .\csokit.exe compress ".\game.iso" --profile fast

Decompress CSO to ISO:

    .\csokit.exe decompress ".\game.cso"

Repair or normalize readable input into CSO1:

    .\csokit.exe repair ".\game.cso" -o ".\fixed.cso" --profile game-safe --deep-verify

Profiles: `game-safe` default, `compat`, `fast`, `smallest`, `archive-smallest`.

Output base names must be 2 to 10 characters; `.cso` and `.iso` extensions are not counted. Automatically suggested names follow this limit.

Full CLI reference: [docs/CLI.md](docs/CLI.md).

## Exit codes

| Code | Meaning |
| ---: | --- |
| 0 | Success |
| 1 | General failure |
| 2 | Invalid command or missing argument |
| 10 | Input file not found |
| 11 | Invalid CSO file header |
| 12 | Unsupported CSO file |
| 13 | Corrupt CSO index table |
| 14 | Output file already exists |
| 15 | Cannot write output file |
| 16 | Not enough disk space |
| 20 | Decompression failed |
| 21 | Compression failed |
| 130 | Operation canceled by user |

## Limitations

CsoKit focuses on PSP ISO/CSO workflows.
ZSO, DAX, and CSO2 are readable input containers; CSO1 is the default output format.
Verification checks file structure, not emulator or device compatibility.

## More documentation

- [CLI reference](docs/CLI.md)
- [Contributor scripts and release gates](CONTRIBUTING.md)
## Architecture and safety

The repository is split into `CsoKit.Core`, `CsoKit.Application`, CLI, and WPF adapters.
Production native loading is limited to the application directory and requires ABI 2.
Compression workers are bounded, and cancellation is propagated through verification,
repair, and final output promotion. See `docs/SECURITY_HARDENING.md`.
