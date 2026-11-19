# CsoKit

CsoKit is a Windows x64 command-line tool for PSP ISO and compressed disc images.

It supports detection, inspection, verification, compression, decompression, and conservative rebuilding of readable containers.

## Installation

1. Download the latest Windows x64 ZIP from:
   https://github.com/HAKAMIQ/CsoKit/releases/latest
2. Extract the complete ZIP into one folder.
3. Keep `csokit.exe` and `CsoKit.Native.dll` together.
4. No installer is required.

## First check

    .\csokit.exe --version
    .\csokit.exe native-info
    .\csokit.exe --help

`native-info` should report that the native backend and codecs are available.

## Detect the input format

    .\csokit.exe detect ".\game.iso"
    .\csokit.exe detect ".\game.cso"

## Inspect a compressed image

    .\csokit.exe info ".\game.cso"

## Analyze a PSP ISO

    .\csokit.exe analyze ".\game.iso" --psp

This checks the PSP ISO structure without changing the file.

## Verify a compressed image

Basic verification:

    .\csokit.exe verify ".\game.cso"

Deep verification with SHA-256:

    .\csokit.exe verify ".\game.cso" --deep --sha256

Verification supports CSO, ZSO, and DAX where indicated by the command help.

## Compress ISO to CSO

Recommended:

    .\csokit.exe compress ".\game.iso" --profile game-safe

Choose the output file:

    .\csokit.exe compress ".\game.iso" -o ".\game.cso" --profile game-safe

Faster compression:

    .\csokit.exe compress ".\game.iso" --profile fast

Estimate size without creating a file:

    .\csokit.exe compress ".\game.iso" --measure

## Decompress CSO or ZSO to ISO

    .\csokit.exe decompress ".\game.cso" -o ".\game.iso"
    .\csokit.exe decompress ".\game.zso" -o ".\game.iso"

ZSO input is read in the layouts written by maxcso and ziso.py, including index-aligned images larger than 2 GB.

## Repair or normalize

    .\csokit.exe repair ".\game.cso" -o ".\fixed.cso" --profile game-safe --deep-verify

Repair rebuilds readable data into CSO1. It cannot recreate missing or unreadable source data.

## Compression profiles

| Profile | Purpose |
| --- | --- |
| `game-safe` | Recommended default |
| `compat` | Compatibility-focused |
| `fast` | Faster compression |
| `smallest` | More compression trials |
| `archive-smallest` | Size-focused experimental profile |

Use `game-safe` unless you have a specific reason to choose another profile.

## Output names

The output base name must contain 2 to 10 Unicode characters. The extension is not counted.

Valid:

    game.cso
    game-2.cso
    back.iso

Invalid:

    x.cso
    verylongname.cso

## Existing files

CsoKit does not overwrite an output file unless `--force` is supplied.

    .\csokit.exe decompress ".\game.cso" -o ".\game.iso" --force

## JSON output

    .\csokit.exe verify ".\game.cso" --deep --json

## Exit codes

| Code | Meaning |
| ---: | --- |
| 0 | Success |
| 1 | General failure |
| 2 | Invalid command or argument |
| 10 | Input file not found |
| 11 | Invalid container header |
| 12 | Unsupported container |
| 13 | Corrupt index |
| 14 | Output already exists |
| 15 | Output cannot be written |
| 16 | Insufficient disk space |
| 20 | Decompression failed |
| 21 | Compression failed |
| 130 | Operation canceled |

## Desktop app (preview)

The source tree also contains `CsoKit.App`, a Windows desktop interface with Arabic and English text. It runs the same operations as the CLI and writes a plain-text report beside the output after each operation.

The desktop app is not included in the release ZIP. It is built from source by `scripts\Run-OfficialReleaseGate.ps1`, which produces `artifacts\release\csokit-<version>-app-win-x64.zip`. Keep `CsoKit.App.exe` and `CsoKit.Native.dll` together, as with the CLI.

## Safety

- Keep the original image until the output is verified.
- Use `--deep` before archiving or transferring an output.
- Repair does not invent missing sectors or corrupted blocks.
- Structural verification does not guarantee emulator or device compatibility.
- Do not separate `CsoKit.Native.dll` from `csokit.exe`.

## Release ZIP contents

    csokit.exe
    CsoKit.Native.dll
    README.md
    RELEASE_NOTES.md
    LICENSE.txt
    THIRD_PARTY_NOTICES.md
    SHA256SUMS.txt

Use `SHA256SUMS.txt` to verify that release files were not modified.
