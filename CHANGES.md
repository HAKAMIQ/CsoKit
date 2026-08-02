# Changes

## 2026-08-02 — v7 published smoke gate repair

- Update the published CLI help smoke to the current verify contract: `csokit verify <input.cso|input.zso|input.dax>`.
- Build and stage `CsoKit.Native.dll` inside the standalone published EXE smoke directory before `native-info` and codec checks.
- Add the published EXE smoke to `Verify-Hardening.ps1` so the main merge gate covers it automatically.
- Add source guards preventing regression of the help contract, native staging, or main-gate integration.

## Release gate filename-policy alignment

- Move release and round-trip artifacts into isolated working directories.
- Use short fixed output names (`smoke.cso`, `out.cso`, and `back.iso`) that comply with the 2–10 Unicode text-element policy.
- Add a shared PowerShell output-name guard and enforce its presence from `Verify-Hardening.ps1`.
- Scan literal `.cso` and `.iso` names in release scripts so future policy regressions fail before build and publish.

## 0.6.0

CsoKit 0.6.0 adds the Windows desktop app, improves the CLI release path, and hardens ISO/CSO verification. This release is mainly about making the tool easier to use without weakening the repair and verification rules.

### Added

- WPF desktop app for common ISO/CSO workflows.
- Official release gate script for local release validation.
- Raw ISO deep verification through the block-container verifier.
- CLI and Core tests for Raw ISO verification behavior.
- Arabic and English plain-text report separation.
- Better repair diagnostics for rebuild-only, corruption repair, and redump-required cases.
- 0.6.0 release gate notes under docs/release.

### Fixed

- Raw ISO deep verify no longer reports UnsupportedContainer when the input can be read as an ISO.
- Raw ISO verification now checks sector alignment before doing a full deep read.
- ISO diagnostics stay separate from CSO-only concepts such as headers, index tables, and sentinels.
- WPF operation names are clearer: Compress to CSO, Decompress to ISO, Verify, and Repair.
- Arabic UI status and operation labels are separated from English text.
- Output-change UI sizing was adjusted to avoid clipped labels.

### Changed

- Release metadata was bumped to 0.6.0 across Core, CLI, App, CMake, and native version output.
- The official release gate is portable by default.
- Real ISO smoke testing is optional instead of requiring a developer-specific game path.
- Public release validation no longer depends on a local corpus path.

### Release gate

Before publishing 0.6.0, the release gate must pass:

- Release build.
- Release test run.
- Published CLI package.
- Published WPF App package.
- SHA256 manifest generation.
- Optional real ISO smoke when a local game image is available.

That is enough for the public release. Deeper benchmark and forensic notes live under docs/archive.
## Security and architecture hardening

- Restrict production native-library loading to the application directory; gate repository artifact probing behind `CSOKIT_NATIVE_DEV_SEARCH` and support an optional SHA-256 constraint.
- Require native ABI 2 before every native codec path.
- Propagate cancellation through deep verification, repair, CLI `Ctrl+C`, WPF Stop, and final output promotion.
- Add a centralized, bounded compression-worker policy and checked queue sizing.
- Add `CsoKit.Application`, typed operation metrics/detail lines, and a central container-reader factory.
- Split WPF operation execution and report generation out of the main ViewModel file and add WPF-facing tests.
- Build Native before tests in CI and require real native compression/decompression round trips.
- Centralize managed/native/release versioning in the root `VERSION` file.

### Hardening review follow-up

- Fix CLI thread-option definite assignment so Debug builds do not fail with CS0165.
- Qualify the WPF base class as `System.Windows.Application` to avoid the Application namespace collision.
- Remove the CLI project reference to Core and route compression, decompression, repair, and verification use cases through Application results and DTOs.
- Make typed operation detail records the source of rendered text and remove the legacy free-text detail parser.
- Add architecture source guards to the hardening verification gate.

## 2026-08-02 hardening v3 correction

- Fix WPF compilation after ViewModel/report extraction by adding explicit namespace imports and safe worker-count parsing.
- Use compressible native round-trip fixtures and separately verify fail-closed handling of native `OUTPUT_TOO_SMALL` candidates.
## 2026-08-02 output file-name length policy

- Require user-visible output base names to contain 2 to 10 characters, excluding the extension.
- Truncate long automatically suggested names and pad one-character names without changing the extension.
- Keep collision suffixes inside the same ten-character limit, for example `Game-2.cso`.
- Apply the same validation to CLI and WPF through the Application boundary.
- Add Arabic and English validation messages and boundary tests, including Unicode text elements.

## 2026-08-02 output-name CLI failure handling

- Handle output-name validation failures before requiring operation DTOs in `compress`, `decompress`, and `repair`.
- Preserve structured text errors and stable `CannotWriteOutput` exit codes for one-character and eleven-character output names.
- Emit JSON failure envelopes even when early validation returns no operation data; metrics are `null` in that case.
- Keep typed DTOs mandatory for successful operations, where missing data remains an internal contract failure.
- Add dedicated text and JSON tests for the lower and upper file-name boundaries across all three commands.
