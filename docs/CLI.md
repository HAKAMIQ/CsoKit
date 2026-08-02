# CsoKit CLI Reference

This is the command-line reference for `csokit.exe`.

The desktop app is the easier path for normal use. The CLI is for scripting, automation, diagnostics, and cases where you want exact control over compression, repair, or verification.

## Start here

Print help:

    .\csokit.exe --help

Check the installed version:

    .\csokit.exe --version

Check whether the native backend is available:

    .\csokit.exe native-info

List available codecs:

    .\csokit.exe codecs

## Inspect a file

Show CSO metadata:

    .\csokit.exe info ".\game.cso"

Detect the input format before doing anything else:

    .\csokit.exe detect ".\game.iso"

JSON is useful when another script needs to read the result:

    .\csokit.exe detect ".\game.cso" --json

Detected formats include ISO, CSO1, CSO2, ZSO, DAX, and unknown input.

## Analyze a PSP ISO

Analyze checks PSP ISO structure without changing the file.

    .\csokit.exe analyze ".\game.iso" --psp

For scripts:

    .\csokit.exe analyze ".\game.iso" --psp --json

Run this before compression if you want a quick sanity check.

## Verify CSO files

Basic verification checks the container structure:

    .\csokit.exe verify ".\game.cso"

Deep verification reads compressed blocks and validates more of the file:

    .\csokit.exe verify ".\game.cso" --deep

Add SHA256 when you need a stable hash for records or comparison:

    .\csokit.exe verify ".\game.cso" --deep --sha256

Machine-readable result:

    .\csokit.exe verify ".\game.cso" --deep --sha256 --json

## Compress ISO to CSO

Default compression writes a CSO next to the ISO:

    .\csokit.exe compress ".\game.iso"

Set the output path when you need a specific name:

    .\csokit.exe compress ".\game.iso" -o ".\game.cso"

Overwrite only when you mean it:

    .\csokit.exe compress ".\game.iso" -o ".\game.cso" --force

Estimate output size without writing a CSO:

    .\csokit.exe compress ".\game.iso" --measure

For automation:

    .\csokit.exe compress ".\game.iso" --profile fast --json

## Profiles

Available profiles:

    game-safe
    compat
    fast
    smallest
    archive-smallest

`game-safe` is the default. It writes CSO1, keeps the default 2048-byte block size, uses raw Deflate candidates, and deep-verifies the output.

`fast` is the quick path.

`smallest` tries harder candidates. It still does not enable Zopfli unless you ask for it.

`archive-smallest` is for experiments where size matters more than broad compatibility.

Pick a profile:

    .\csokit.exe compress ".\game.iso" --profile fast

Shortcut:

    .\csokit.exe compress ".\game.iso" --fast

Do not combine `--fast` with another explicit profile. Pick one.

## Compression tuning

Threads:

    .\csokit.exe compress ".\game.iso" --threads 8

Block size:

    .\csokit.exe compress ".\game.iso" --block 16K

Optional Zopfli trials:

    .\csokit.exe compress ".\game.iso" --zopfli

Codec winner report:

    .\csokit.exe compress ".\game.iso" --codec-report

Block size accepts raw bytes, `K`, or `M`. It must be at least 2048 and a power of two. Larger blocks can improve compression, but they may hurt compatibility or random-read behavior. For PSP safety, 2048 is still the sensible default.

## Decompress CSO to ISO

Default output goes next to the CSO:

    .\csokit.exe decompress ".\game.cso"

Choose an output path:

    .\csokit.exe decompress ".\game.cso" -o ".\game.iso"

Overwrite intentionally:

    .\csokit.exe decompress ".\game.cso" -o ".\game.iso" --force

## Repair and normalize

Repair is conservative. It rebuilds readable input into game-safe CSO1, but it does not invent missing data.

    .\csokit.exe repair ".\game.cso" -o ".\fixed.cso" --profile game-safe --deep-verify

Readable input can include ISO, CSO1, ZSO, DAX, and supported CSO2. Output is CSO1 by default.

If a compressed block is corrupt or the source is incomplete, the command fails with a diagnosis such as `ReDumpRequired`. Good. A broken game should not become a fake "fixed" file.

Padding a non-2048-aligned ISO only happens when explicit repair behavior is requested.

## Output naming

The output base name must contain 2 to 10 characters; the extension is not counted. If the default output already exists, CsoKit uses a short numbered suffix while preserving the limit:

    game.cso
    game-2.cso
    game-3.cso

Long automatic names are shortened to ten characters, and one-character names are padded to two characters. With `-o`, the destination folder must already exist and the same length rule applies.

## Native backend

Release packages include:

    CsoKit.Native.dll

Keep it next to the executables.

The native backend adds zlib and libdeflate raw-Deflate candidates. Managed Deflate remains the fallback. Zopfli is native-only and opt-in through `--zopfli`.

Quick check:

    .\csokit.exe native-info

## JSON output

Add `--json` when another program needs structured output.

Examples:

    .\csokit.exe verify ".\game.cso" --json
    .\csokit.exe verify ".\game.cso" --deep --sha256 --json
    .\csokit.exe compress ".\game.iso" --measure --profile smallest --json

Compression and measure JSON include `schemaVersion`, `command`, `mode`, `success`, `options`, `metrics`, and `error` when the command fails.

Profile object example:

    {
      "profile": {
        "name": "smallest",
        "fast": false,
        "level": 9
      }
    }

Invalid profile values return a clear argument error. Conflicting profile options use the same contract in JSON mode and a short message in text mode.

## Checksums

Release packages include `SHA256SUMS.txt`.

Use it when you want to confirm the downloaded files are unchanged.

## Third-party notices

`THIRD_PARTY_NOTICES.md` lists native compression components and licenses, including Zopfli, zlib, and libdeflate.