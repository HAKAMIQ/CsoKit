# CsoKit

CsoKit is a Windows x64 command-line tool for PSP ISO and compressed disc images.

It supports detection, inspection, verification, compression, decompression, and safe rebuilding of supported containers.

## Download

Download the latest Windows x64 release:

https://github.com/HAKAMIQ/CsoKit/releases/latest

Extract the complete ZIP into one folder and keep:

```text
csokit.exe
CsoKit.Native.dll
```

together.

No installer is required.

## Main features

- Detect ISO, CSO, ZSO, DAX, and supported CSO2 input.
- Inspect CSO1 and supported CSO2 images.
- Analyze PSP ISO structure.
- Deep verification with SHA-256.
- Compress ISO to CSO1.
- Decompress CSO to ISO.
- Rebuild readable images into verified CSO1 output.
- Structured JSON output for scripts and automation.

## Quick start

Check the installation:

```powershell
.\csokit.exe --version
.\csokit.exe native-info
.\csokit.exe --help
```

Compress an ISO:

```powershell
.\csokit.exe compress ".\game.iso" --profile game-safe
```

Verify a CSO:

```powershell
.\csokit.exe verify ".\game.cso" --deep --sha256
```

Decompress a CSO:

```powershell
.\csokit.exe decompress ".\game.cso" -o ".\game.iso"
```

## Compression profiles

- `game-safe` — recommended default
- `compat` — compatibility-focused
- `fast` — faster compression
- `smallest` — additional compression trials
- `archive-smallest` — size-focused experimental profile

Use `game-safe` unless you have a specific reason to choose another profile.

## Safety

- Keep the original image until the output is verified.
- Existing output files are not overwritten unless `--force` is used.
- Repair cannot recover missing or unreadable source data.
- Structural verification does not guarantee emulator or physical-device compatibility.
- Keep `CsoKit.Native.dll` beside `csokit.exe`.

## Release package

The release ZIP contains the executable, native runtime, license, notices, release notes, and SHA-256 manifest.

See the latest release for downloads and version-specific changes.
