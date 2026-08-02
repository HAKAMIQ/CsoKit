# CsoKit R3-E Real Corpus Evidence and Deflate Backend Calibration

R3-E keeps CsoKit scoped to PSP CSO workflows. It does not add Zstandard, LZ4, FLAC, CHD logic, emulator settings, or external executable dependencies. CSO1 remains the safe output target and the game-safe repair/normalization path remains conservative.

## Benchmark truth corrections

`Run-BenchmarkTruthLayer.ps1` now separates two different workflows:

- ISO input: profile comparison is valid. The script runs `compress` for each selected profile, then verifies, decompresses, and compares SHA256.
- CSO/ZSO/DAX/CSO2 input: profile comparison is not valid unless the container is first expanded to ISO. The supported normalization path is `repair --profile game-safe`. Non-game-safe profiles are reported as `SKIP`, not `FAIL`.

The JSON and Markdown reports now include failure-stage and skip details so a report does not hide the reason a case did not produce trusted output. Relevant fields include:

- `stage`
- `skipped`
- `skippedReason`
- `failures[].Stage`
- `failures[].Code`
- `failures[].Message`
- `compressedBlocks`
- `storedBlocks`
- `zeroBlocks`

A skipped case is not evidence of compression failure. It means that the requested benchmark path is not supported by the current CSO workflow.

## PowerShell compatibility

The benchmark process launcher no longer depends only on `ProcessStartInfo.ArgumentList`. Windows PowerShell 5.1 can run the script by falling back to a quoted `Arguments` string when `ArgumentList` is unavailable. PowerShell 7 remains supported.

## Deflate backend calibration boundary

R3-E does not introduce new CSO1 codecs. The supported CSO1 meaning remains raw Deflate or stored blocks according to existing CSO semantics.

Allowed backend and decision concepts:

- zlib/managed Deflate remains the safe baseline.
- native libdeflate remains a raw-Deflate backend candidate when available.
- Zopfli remains explicitly opt-in for slow profiles.
- stored block selection is an internal decision when compressed bytes do not beat stored bytes.
- zero-block detection is telemetry and fast-path evidence only; it is not a CSO codec tag.

Explicitly not added:

- no Zstandard inside CSO1
- no LZ4 inside CSO1
- no FLAC
- no CHD/CD/DVD/PS3 logic
- no external comparison-tool dependency
- no external executable comparison gate

## Zero-block evidence

Compression and repair metrics now report `zeroBlocks`. This count is based on a full scan of the block source bytes, not sampling. It is evidence for corpus analysis and future policy calibration, while preserving the existing CSO1 output semantics.

## Correct benchmark usage

For CSO normalization evidence:

```powershell
scripts/Run-BenchmarkTruthLayer.ps1 `
  -Root "G:\All Console Games\PLAYSTATION Consoles\PlayStation Portable (2004)\1" `
  -Profiles game-safe,fast,smallest `
  -Configuration Release `
  -KeepArtifacts
```

Expected behavior:

- `game-safe` cases run repair/normalize and should PASS when the inputs are readable.
- `fast` and `smallest` cases on CSO/ZSO/DAX/CSO2 inputs are SKIP with a reason.

For actual profile comparison, use ISO input:

```powershell
scripts/Run-BenchmarkTruthLayer.ps1 `
  -InputPath D:\Corpus\game.iso `
  -Profiles game-safe,fast,smallest `
  -Configuration Release `
  -KeepArtifacts
```

Only ISO input proves profile ratio/time differences.

## Acceptance statement

R3-E is successful when:

- ReleaseGate passes.
- tests pass.
- CSO inputs produce game-safe normalization evidence and non-game-safe profile skips.
- ISO inputs produce real profile comparison evidence.
- no unsupported compression algorithm is introduced into CSO1.
- game-safe defaults remain unchanged unless later corpus evidence proves a safe improvement.
