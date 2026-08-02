# CsoKit 0.6.0

Windows x64 release focused on PSP ISO/CSO workflows, WPF usability, and release-grade verification.

## What's new

- Added WPF desktop app release path for Windows.
- Added localized plain-text reports for Arabic and English.
- Added deep forensic CSO verification details.
- Added Raw ISO deep verification through the block-container verifier.
- Added repair diagnostics for rebuild-only, corruption repair, and redump-required cases.
- Improved progress feedback, report opening, output naming, and language switching.
- Added the official release gate script.

## Supported workflows

- Detect supported container and image formats.
- Analyze PSP ISO structure.
- Verify CSO and Raw ISO.
- Compress ISO to CSO.
- Decompress CSO to ISO.
- Rebuild readable ISO/CSO input into normalized CSO1.
- Repair readable input when the source data is still recoverable.

## Release gate

Official portable gate:

    .\scripts\Run-OfficialReleaseGate.ps1 -Version 0.6.0 -Runtime win-x64

Optional local real-corpus smoke:

    .\scripts\Run-OfficialReleaseGate.ps1 -Version 0.6.0 -Runtime win-x64 -InputIso "D:\Games\PSP\game.iso"

The public release gate does not require a developer-specific game path. Use `-InputIso` only when you have a local PSP ISO available for extra smoke testing.

If NuGet vulnerability metadata times out with `NU1900`, retry later. If you must validate while the audit feed is unavailable, use `-SkipNuGetAudit` and mention that in the gate summary.

## Assets

The official GitHub release provides:

    csokit-0.6.0-win-x64.zip
    SHA256SUMS.txt

Download from the GitHub Releases page.