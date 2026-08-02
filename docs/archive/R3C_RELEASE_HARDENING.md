# CsoKit R3-C Release Hardening

R3-C turns the R3-B container work into a stricter intake, verify, repair, normalize, and diagnostics pass. The project remains self-contained: no release gate, test, or script depends on external comparison tools.

## Implemented in R3-C

- Golden container fixtures for CSO1, CSO2, ZSO, DAX, raw ISO9660, PSP ISO structure, and corrupt refusal cases.
- Streaming container repair path: readable CSO1, CSO2, ZSO, and DAX inputs are normalized to CSO1 without writing a temporary ISO.
- Atomic repair output: the final output path is moved into place only after the temporary CSO output is complete and, for game-safe/deep verify mode, verified.
- PSP ISO identity analysis: `UMD_DATA.BIN` DISC_ID and `PSP_GAME/PARAM.SFO` metadata are read with bounded file reads.
- PARAM.SFO failures are warnings, not crashes.
- Codec trial report model for explicit `--codec-report` diagnostics.
- Unified JSON fields added across the primary CLI output paths.
- Optional real-local corpus gate that uses developer-supplied files and does not copy games into the repository.
- UTF-8 BOM cleanup scripts and text-encoding gate.
- Native build documentation for online/offline expectations.

## Safety policy

R3-C keeps the conservative game-safe default. It does not enable unproven codecs by default and does not claim raw compression superiority without project-local measurements. Damaged compressed blocks, truncated payloads, non-monotonic indexes, and impossible decoded sizes must fail with a clear error instead of producing a questionable output.

## Not proven by R3-C

R3-C does not prove every game image, every malformed dump, or every emulator-specific edge case. It does not improve FPS, graphics settings, or انتر ريزلوشن. It does not make claims about raw compression leadership against external tools without a repeatable internal benchmark.

## Recommended local evidence

Run the normal release gate first:

```powershell
dotnet build CsoKit.slnx --no-restore
dotnet test CsoKit.slnx --no-build --no-restore
scripts/Run-ReleaseGate.ps1 -SkipRealIsoGates
scripts/Test-RepoTextEncoding.ps1
```

Then run the optional corpus gate against a private local folder:

```powershell
scripts/Run-RealLocalCorpusGate.ps1 -Root D:\LocalTestCorpus -Configuration Release
```

Use `-KeepArtifacts` only when you need to inspect temporary outputs. Do not commit local corpus files or generated artifacts.
