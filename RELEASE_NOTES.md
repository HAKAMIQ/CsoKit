# CsoKit 0.6.0

Windows x64 release for PSP ISO and compressed disc-image workflows.

## Main functions

- Detect and inspect supported containers.
- Analyze PSP ISO structure.
- Verify CSO, ZSO, and DAX input.
- Compress ISO into CSO1.
- Decompress CSO into ISO.
- Rebuild readable input into verified CSO1 output.
- Produce JSON output for automation.

## Installation

Extract the complete release ZIP and keep `csokit.exe` beside `CsoKit.Native.dll`.

Run:

    .\csokit.exe native-info
    .\csokit.exe --help

## Important

- `game-safe` is the recommended compression profile.
- Output base names must contain 2 to 10 Unicode characters.
- Existing output is not overwritten without `--force`.
- Repair cannot reconstruct unreadable source data.
- Verification checks structure, not emulator compatibility.
