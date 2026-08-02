# Contributor Guide

These notes are for contributors and maintainers. If you only want to use the tool, start with [README.md](README.md). For command-line details, see [docs/CLI.md](docs/CLI.md).

## Ground rules

Do not commit build output, release ZIPs, game images, local corpus files, bin or obj folders, or temporary repair output.

Run the right gate before changing compression, repair, verification, packaging, or release logic. Quick checks are fine while iterating, but do not ship from a quick check alone.

Do not recreate an existing release tag unless you are intentionally replacing that release. Usually, you do not want that.

## Roundtrip gate

Run this before touching compression. It does a full ISO to CSO to ISO cycle and compares SHA256 — a good way to catch regressions before they ship.

    .\scripts\Run-RoundtripGate.ps1 -InputIso "D:\Games\PSP\game.iso"

Add `-KeepArtifacts` only when something failed and you want to inspect the generated files.

## Profile matrix

This is the compression-profile sanity check. It runs roundtrips for the selected profiles and catches behavior drift between game-safe, compat, fast, and smallest.

    .\scripts\Run-ProfileRoundtripMatrix.ps1 -InputIso "D:\Games\PSP\game.iso"

To narrow the run:

    .\scripts\Run-ProfileRoundtripMatrix.ps1 -InputIso "D:\Games\PSP\game.iso" -Profiles game-safe,smallest,fast

Use `-KeepArtifacts` when you need the intermediate CSO or restored ISO. Most of the time, you will not.

## Release gate

Run this before release-oriented commits. It covers restore, build, tests, command smoke checks, and the real ISO gates when an input ISO is provided.

    .\scripts\Run-ReleaseGate.ps1 -InputIso "D:\Games\PSP\game.iso"

No game image handy?

    .\scripts\Run-ReleaseGate.ps1 -SkipRealIsoGates

That is fine for a quick pass. Not enough for final release confidence.

## Published EXE smoke

This tests the published executable instead of `dotnet run`. Useful when the code works locally but packaging or runtime layout might be wrong.

    .\scripts\Run-PublishedExeSmoke.ps1 -InputIso "D:\Games\PSP\game.iso"

Use `-SkipRealIsoGates` for a fast packaging smoke.

## Final release gate

The final gate is the heavier one. Run it after the normal release gate and published EXE smoke are stable.

    .\scripts\Run-FinalReleaseGate.ps1 -InputIso "D:\Games\PSP\game.iso"

For local script work, you can allow a dirty tree and skip package creation:

    .\scripts\Run-FinalReleaseGate.ps1 -InputIso "D:\Games\PSP\game.iso" -AllowDirty -SkipReleasePackage

Quick non-ISO check:

    .\scripts\Run-FinalReleaseGate.ps1 -SkipRealIsoGates -SkipReleasePackage

For a real final pass, keep the tree clean. Simple.

## Official release gate

Before publishing an official release, start here:

    .\scripts\Run-OfficialReleaseGate.ps1 -Version 0.6.0 -Runtime win-x64

If you have a real PSP ISO available, add the optional corpus smoke:

    .\scripts\Run-OfficialReleaseGate.ps1 -Version 0.6.0 -Runtime win-x64 -InputIso "D:\Games\PSP\game.iso"

For a future release, replace `0.6.0` with the version you are publishing.

If NuGet vulnerability metadata times out with `NU1900`, retry later. If you must validate while the audit feed is unavailable, use `-SkipNuGetAudit` and mention that in the release notes or gate summary.
## Native development search

Normal releases load `CsoKit.Native.dll` only beside the application executable. Local
repository artifact probing is disabled by default. Developers may opt in with
`CSOKIT_NATIVE_DEV_SEARCH=1`; set `CSOKIT_NATIVE_SHA256` when testing a specific build.

The release version comes from the root `VERSION` file. Update that file once; managed
project metadata, CMake native constants, and release scripts consume it.
