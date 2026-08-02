# CsoKit 0.6.0 Official Release Gate

This gate validates the release candidate before publishing GitHub assets.

Required checks:

- Version metadata is consistent across Core, CLI, App, and native backend.
- Source tree has no tracked `bin`, `obj`, `.vs`, `artifacts`, `TestResults`, `.tmp`, or `.partial` files.
- Clean, restore, Release build, and test pass.
- CLI and WPF App publish to `win-x64`.
- CLI smoke checks `--version` and `--help`.
- Optional real ISO deep verify smoke validates the RawIso routing fixed in this release.
- Release ZIPs and SHA256 checksum files are produced.

Recommended command:

```powershell
.\scripts\Run-OfficialReleaseGate.ps1 -Version 0.6.0 -Runtime win-x64 -InputIso "G:\path\game.iso"
```

If NuGet vulnerability metadata times out with `NU1900`, rerun later or use:

```powershell
.\scripts\Run-OfficialReleaseGate.ps1 -Version 0.6.0 -Runtime win-x64 -InputIso "G:\path\game.iso" -SkipNuGetAudit
```

Using `-SkipNuGetAudit` means the release is not security-audited by NuGet vulnerability metadata in that run.


## Real-corpus policy

`Run-OfficialReleaseGate.ps1` does not require a developer-specific game path. The default gate is portable and runs build, tests, publish, CLI smoke, and packaging.

Use `-InputIso <path>` only as an optional local real-corpus smoke test. This path must never be documented as a user requirement and should not be uploaded in release summaries.
