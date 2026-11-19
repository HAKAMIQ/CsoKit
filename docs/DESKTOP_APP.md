# Desktop app (preview)

`src/CsoKit.App` is a Windows WPF interface with Arabic and English text. It runs the same operations as the CLI through `CsoKit.Application` and writes a plain-text report beside the output after each operation.

The desktop app is not included in the release ZIP. `scripts/Run-OfficialReleaseGate.ps1` builds it into `artifacts/release/csokit-<version>-app-win-x64.zip`. Keep `CsoKit.App.exe` and `CsoKit.Native.dll` together.

Tests: `tests/CsoKit.App.Tests`.
