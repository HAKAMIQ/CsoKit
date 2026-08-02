# CsoKit stability fix notes

## Release-readiness hardening

- Add final publish native backend verification to the final release gate.
- Make release workflow tag-only to preserve signed-tag release policy.
- Add independent CsoKit compression benchmark coverage.
- Add 0.5.0 release scorecard with explicit blockers and required evidence.

## P2-I stable release promotion

- Promote release defaults and package metadata to the 0.5.0 stable release.
- Update GitHub Actions manual release defaults, release scripts, project versions, README, and release notes.
- Keep compression behavior unchanged.




## P2-G release notes and finalization

- Add `RELEASE_NOTES.md` for the 0.5.0 win-x64 stable release.
- Add `scripts/Run-FinalReleaseGate.ps1` to chain release validation, published EXE smoke, release package publishing, release verification, and source package publishing.
- Include `RELEASE_NOTES.md` in the release package and SHA256 manifest.
- Make release verification require release notes and profile help text in the packaged executable.
- Use `RELEASE_NOTES.md` as the GitHub Release notes source.
- Keep compression behavior unchanged.

## P2-F published EXE smoke

- Add `scripts/Run-PublishedExeSmoke.ps1` to publish and test the actual `csokit.exe` output.
- Smoke-test help, version, native-info, JSON argument output, measure output, verify, compress, and decompress through the published executable.
- Run real ISO -> CSO -> ISO SHA256 checks for `smallest`, `compat`, and `fast` using the published executable.
- Keep generated smoke artifacts under `artifacts/published-exe-smoke-work` only when `-KeepArtifacts` is supplied.
- Keep compression behavior unchanged.

This source package applies the stability fixes requested after the CSO readiness review.




## P2-E release gate consolidation

- Add `scripts/Run-ReleaseGate.ps1` as a single local gate for pre-release validation.
- Run restore, Debug build, tests, forbidden-term scan, help smoke, and JSON argument smoke from one script.
- Chain the real roundtrip gate and profile roundtrip matrix when an input ISO is supplied.
- Add `-SkipRealIsoGates` for quick non-ISO checks, plus `-KeepArtifacts` and `-Quiet` forwarding for real ISO gates.
- Keep compression behavior unchanged.


## P2-D real profile roundtrip matrix

- Add `scripts/Run-ProfileRoundtripMatrix.ps1` for real ISO -> CSO -> ISO verification across compression profiles.
- Check `smallest`, `compat`, and `fast` by default, with `-Profiles` for targeted profile runs.
- Compare restored ISO SHA256 and byte size against the original ISO for every selected profile.
- Print a final matrix with profile name, CSO size, ratio, restored ISO size, and status.
- Remove successful test artifacts by default and support `-KeepArtifacts` for manual inspection.
- Keep compression behavior unchanged.

## P2-C profile conflict and CLI contract tests

- Add CLI contract tests for profile help text, invalid profile values, and `--fast` conflict handling.
- Verify text and JSON argument errors use stable exit code and error fields.
- Allow `--profile fast --fast` while rejecting `--fast` with `compat` or `smallest`.
- Add a profile-name helper so conflict messages can name the resolved public profile.
- Keep compression behavior unchanged.

## P2-B profile output polish and JSON contract

- Add a stable JSON contract for compress write and measure output with `schemaVersion`, `options.profile`, `metrics`, and structured error fields.
- Report resolved profile information through a single profile output model: name, fast flag, and logical level.
- Improve invalid profile and conflicting profile argument messages.
- Make help and usage text read supported public profile names from one policy source.
- Add focused tests for profile JSON contract shape and supported profile text.

## P2-A profiles foundation

- Add first-class compression profiles for end-user workflows: `compat`, `fast`, and `smallest`.
- Keep `smallest` as the default profile and preserve the current safe CSO behavior.
- Add `--profile <compat|fast|smallest>` to compress and measure commands.
- Add `--fast` as a short alias for `--profile fast`.
- Route profile settings through `CsoCompressionWorker`, `CsoCompressor`, and `CsoMeasureEstimator` so write and measure paths use the same compression policy.
- Report profile, fast mode, and logical level in text and JSON output.
- Add focused tests for profile parsing and worker profile settings.

## P1-E real roundtrip gate

- Add `scripts/Run-RoundtripGate.ps1` for real ISO -> CSO -> ISO verification.
- Compare original and restored ISO SHA256 hashes after the roundtrip.
- Write uniquely named gate artifacts beside the input ISO and remove them after successful verification by default.
- Add `-KeepArtifacts` for manual inspection of generated CSO and restored ISO.
- Avoid overwriting existing files and avoid automatic output-folder creation in the gate script.

## P1-D output path policy

- Add same-folder default output naming for end-user conversion commands.
- Allow `csokit compress <input.iso>` to write `<input>.cso` beside the source file.
- Allow `csokit decompress <input.cso>` to write `<input>.iso` beside the source file.
- Avoid automatic output-folder creation. Explicit `-o` paths must point to an existing folder.
- Avoid overwriting existing files when output is auto-named. Existing targets receive ` - CsoKit Converted`, then numbered suffixes.
- Keep `--measure` write-free and output-path-free.

## P1-C measure layer

- Add `CsoMeasureEstimator`, `CsoMeasureOptions`, and `CsoMeasureResult` for estimating CSO output size without writing a CSO file.
- Add `csokit compress <input.iso> --measure` for measure-only CLI usage.
- Report original size, estimated CSO size, estimated ratio, saved/growth bytes, total blocks, compressed blocks, stored blocks, profile, fast mode, and level.
- Reuse the same sector compression worker and stored-versus-compressed selector used by the writer path so estimates track current compression behavior.
- Add focused tests to compare measure results with actual compression output.

## P1-B compression decision split

- Add `CsoCompressionWorker` to own per-sector raw-deflate candidate creation.
- Add `CsoBestCandidateSelector` to own the stored-versus-compressed decision.
- Keep the current compression behavior unchanged: raw-deflate is selected only when it is smaller than the original sector; otherwise the original sector is stored.
- Add focused tests for candidate selection and worker output decisions.

## P1-A sector engine foundation

- Add `CompressionMethod`, `SectorJob`, and `SectorResult` as the first block-pipeline data contracts.
- Add `CsoBlockReader`, `CsoIndexBuilder`, and `CsoOrderedOutputWriter` to split block reading, index construction, and ordered output writing from the compressor loop.
- Keep the current compression behavior unchanged: zlib/raw-deflate path, same store-raw decision, same ordered output flow.
- Clean remaining user-facing unsupported CSO wording to `Unsupported CSO file.`
- Add focused tests for the new sector/index foundation contracts.

## Fixed

- Treat legacy CSO header `version = 0` as supported alongside `version = 1`.
- Add `EffectiveHeaderSize` and use it for legacy index reading/verification so unreliable CSO `header_size` does not break valid legacy files.
- Add upper-bound validation for dangerous `block_size` and `index_shift` values.
- Make decompression output temp files unique instead of using `<output>.tmp`, preventing accidental deletion of a user-owned sibling temp file.
- Replace output via `File.Move(..., overwrite: true)` after successful temp write instead of deleting the destination first.
- Make CLI `--version` read `AssemblyInformationalVersion` instead of hardcoding `0.4.0-beta.1`.
- Fix CLI exit-code mapping for `UnsupportedVersion`, `BlockSizeTooLarge`, `InvalidIndexShift`, and additional corrupt-index verification codes.
- Change GitHub Actions artifact upload from `actions/upload-artifact@v7` to `actions/upload-artifact@v6`.
- Add source packaging script that excludes `.git`, `.vs`, `bin`, `obj`, `artifacts`, and `TestResults`.

## Added tests

- CSO raw-deflate decompression roundtrip.
- Legacy CSO `version = 0` decompression roundtrip.
- High-bit uncompressed block decompression roundtrip.
- Forced overwrite decompression behavior.
- Existing `<output>.tmp` sibling preservation.
- Legacy/unreliable CSO `header_size` behavior in header and index readers.

## Validation note

The editing environment used to prepare this package does not include the .NET SDK, so `dotnet build` / `dotnet test` could not be executed here. Run the normal local gates on Windows before merging:

```powershell
dotnet restore .\CsoKit.slnx -r win-x64 -p:NuGetAudit=false
dotnet build .\CsoKit.slnx -c Debug --no-restore -p:NuGetAudit=false
dotnet test .\CsoKit.slnx -c Debug --no-build
.\scripts\Publish-Release.ps1
.\scripts\Verify-Release.ps1
```

## R1 - Architecture repair: native backend policy

- Treat native backend support as runtime availability, ABI verification, `native-info`, and fallback infrastructure only.
- Reject the unproven native compression performance claim for 0.5.0 after measured native-runtime fast was slower and slightly larger than managed-runtime fast on the real PSP ISO.
- Do not retain native block store hints or any native compression hot-path change in production.
- Keep `CSOKIT_DISABLE_NATIVE` as an explicit diagnostic and benchmark fallback switch.
- Add `docs/R1_ARCHITECTURE_REPAIR.md` and refresh the release scorecard to separate native runtime proof from compression performance claims.


- Remove stale external benchmark notes so CsoKit remains fully independent.
