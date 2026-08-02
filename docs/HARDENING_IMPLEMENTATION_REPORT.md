# CsoKit hardening implementation report

Date: 2026-08-02
Version source: `VERSION` (`0.6.0`)

## Scope

This change set addresses the security and architecture findings covering native DLL loading, cancellation, compression worker bounds, ABI enforcement, native integration tests, application-layer boundaries, WPF decomposition, and centralized versioning.

## Implemented changes

### Native boundary

- Production native resolution is restricted to `AppContext.BaseDirectory`.
- Ancestor and `artifacts` probing requires the explicit `CSOKIT_NATIVE_DEV_SEARCH=1` development switch.
- Failure to find an approved library is fail-closed and does not fall back to the platform DLL search path.
- Optional SHA-256 enforcement is available through `CSOKIT_NATIVE_SHA256`.
- Managed/native ABI compatibility is fixed at ABI 2 and validated before every codec path can become available.

### Cancellation and final output promotion

- Cancellation tokens now flow through container deep verification, CSO deep verification, repair, CLI repair, CLI deep verify, and WPF operations.
- Block loops check cancellation between blocks.
- Compression and CSO writing check cancellation immediately before final `File.Move` promotion.
- WPF now exposes an active Stop button while an operation is running.
- `repair` and deep `verify` handle Ctrl+C and return exit code 130.

### Compression resource policy

- `CsoWorkerPolicy` centralizes default, configured, and absolute worker limits.
- The hard ceiling is 64 workers; the operational ceiling is processor-scaled or reduced with `CSOKIT_MAX_WORKERS`.
- Core, CLI, and WPF use the same validation.
- Queue capacity uses checked bounded arithmetic.

### Native CI and published-artifact validation

- CI builds the native backend before managed tests.
- Native integration tests fail when the DLL or mandatory capabilities are missing.
- Tests exercise zlib, libdeflate, and Zopfli raw-deflate/inflate round trips.
- Release publishing stages the actual DLL beside the published executable and performs a real published CLI ISO → CSO → ISO SHA-256 round trip.

### Application boundary and WPF decomposition

- Added `CsoKit.Application` as the use-case boundary between CLI/WPF and Core.
- Centralized container reader creation in `BlockContainerReaderFactory`.
- CLI no longer references `CsoKit.Core` as a project and no longer instantiates compressor, repair, shallow verifier, or deep verifier implementations directly.
- Application results expose typed paths, format, byte metrics, error code, operation DTOs, and structured detail records.
- Typed detail records are now the source of rendered `Details`; the former free-text detail parser was removed.
- WPF no longer parses English result text to recover sizes, savings, or report structure.
- Operation report writing was extracted from the ViewModel.
- MainWindowViewModel operation execution was split into a separate partial file.
- Added a Windows test project for report rendering outside a live WPF window.

### Version source

- Added root `VERSION` and `Directory.Build.props`.
- .NET projects read one managed version source.
- CMake reads the same file and generates native version/ABI macros.
- Publish and verification scripts default to the root version instead of duplicated literals.

## Tests added or strengthened

- Worker policy minimum, maximum, environment override, and compressor rejection.
- Deep verification cancellation before input and between blocks.
- Production native search policy.
- Mandatory native ABI/capability checks.
- zlib/libdeflate/Zopfli native round trips.
- Typed operation result rendering without reverse-parsing free text.
- Arabic WPF report rendering from structured operation details.

There are 150 `[Fact]`/`[Theory]` declarations in the source tree after this change. Runtime case counts can be higher because theories expand into multiple cases.

## Validation performed in the editing environment

- XML/XAML/CSProj/Solution parsing: PASS.
- GitHub Actions YAML parsing: PASS.
- C# delimiter and lexical structural scan across 168 files: PASS.
- Project and solution reference existence: PASS.
- WPF event handler linkage scan: PASS.
- Stale WPF service symbol scan: PASS.
- Dependency direction scan: PASS.
- `git diff --check`: PASS.
- UI direction attributes: unchanged.

## Follow-up correction after external build review

- Fixed `CS0165` in CLI thread parsing by separating integer parsing from worker-policy validation.
- Fixed the WPF namespace collision by deriving `App` explicitly from `System.Windows.Application`.
- Removed the CLI project reference to Core and routed long-running/state-changing commands through Application DTOs.
- Removed `CsoOperationDetailParser`; structured detail records now generate the compatibility text view.
- Added architecture source guards to `Verify-Hardening.ps1`.


## Follow-up correction after v2 Windows build review

- Added the explicit `System.IO` imports required by the WPF report and operation partial files.
- Added the explicit `CsoKit.App.Localization` import required by the split ViewModel partial.
- Separated WPF worker-count parsing from policy validation to eliminate the same definite-assignment failure already fixed in CLI.
- Changed native round-trip fixtures to use deliberately compressible data for every zlib strategy.
- Added a separate native integration theory proving that expansion-prone Huffman-only and RLE candidates are rejected cleanly when the native layer reports `OUTPUT_TOO_SMALL`, with no partial output exposed.

## Follow-up correction after v4 Windows CLI review

- Reordered `compress`, `decompress`, and `repair` handling so early validation failures are processed before typed operation data is required.
- Successful operations still require their typed DTO and fail as an internal contract error if it is missing.
- JSON output now preserves the common failure envelope when operation data is absent; command metrics are serialized as `null`.
- Changed the general JSON failure test to use a valid short output name so it continues testing the intended missing-input path.
- Added text and JSON boundary coverage for one-character and eleven-character output names across all three state-changing commands.
- Replaced the repair suffix assertion with `Assert.EndsWith`.

## Runtime validation limitation

The editing environment did not contain the .NET SDK or PowerShell and could not download them. Managed builds, WPF compilation, native Windows compilation, and the runtime test suite were therefore not executed independently here.

Run the following on Windows before merge or release:

```powershell
.\scripts\Verify-Hardening.ps1 -Configuration Release
```

The script performs restore, native build, Debug/Release managed builds, native-backed tests, release publishing, and release verification.

## Commit suggestion

`fix(security): harden native loading, cancellation, and application boundaries`
## Output file-name length policy

- Output base names are validated at the Application boundary and must contain 2 to 10 user-visible characters; the extension is not counted.
- Automatic compression/decompression names are normalized into that range.
- Repair suggestions use a short `-r` suffix and numbered collision suffixes remain within ten characters.
- Input file names are not restricted, so existing game images with long names remain usable.
- Unicode text elements are counted instead of raw UTF-16 code units.


## Release-script filename-policy hardening

The release gates now create artifacts in isolated work directories and use short fixed filenames. `scripts/OutputFileNamePolicy.ps1` counts Unicode text elements using `System.Globalization.StringInfo`, matching the Core filename policy. The main hardening gate verifies that all release scripts load and invoke this guard, and checks literal `.cso`/`.iso` names for the 2–10 rule.


## v7 published executable smoke integration

- Updated the exact help contract expected by `Run-PublishedExeSmoke.ps1` to include CSO, ZSO, and DAX verification inputs.
- The smoke gate now builds the Release native backend and copies `CsoKit.Native.dll` beside the published CLI before `native-info`.
- `Verify-Hardening.ps1` now executes the standalone smoke with real-ISO gates skipped; full real-profile roundtrips remain available through the dedicated invocation.
- Source guards require the current help contract, native build/staging, and main-gate integration.
