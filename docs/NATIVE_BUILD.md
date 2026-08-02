# Native Build Notes

CsoKit does not require the native backend to function.

The managed path stays available for detect, analyze, verify, repair, decompress, and compress. Native code only adds extra raw-Deflate candidates such as zlib, libdeflate, and optional Zopfli.

If the native DLL is missing, the CLI should say so clearly and keep working. No fake availability. No silent fallback that pretends native compression is active.

## Build the native backend

Run this when you want the full release layout or when you are testing native codec behavior:

    .\scripts\Build-Native.ps1 -Configuration Release -Platform x64

Then check what the CLI can actually see:

    .\csokit.exe native-info

The report should be honest about zlib, libdeflate, Zopfli, and anything unavailable.

## Online dependency resolution

The first native configure may need network access. CMake can resolve dependencies such as zlib or libdeflate through FetchContent or an equivalent setup.

If that fails because the machine is offline, do not patch around it by hardcoding success. Use the managed-only path instead. Simple.

## Managed-only workflow

This is enough for normal .NET validation when native dependencies are not available:

    dotnet build CsoKit.slnx --no-restore
    dotnet test CsoKit.slnx --no-build --no-restore
    .\csokit.exe native-info

Managed Deflate, container decoding, PSP ISO analysis, JSON diagnostics, and repair safety should still work without CsoKit.Native.dll.

## Release expectation

Release packages should include:

    CsoKit.Native.dll

Keep it next to:

    csokit.exe
    CsoKit.App.exe

If native-info says unavailable in a release package, check the published ZIP layout first. Most native problems are packaging problems, not compression bugs.
## Runtime loading policy

Release builds must place `CsoKit.Native.dll` beside the executable. Production never
searches the current directory or repository `artifacts` paths. Repository probing is a
developer-only opt-in through `CSOKIT_NATIVE_DEV_SEARCH=1`; optionally constrain the
accepted DLL with `CSOKIT_NATIVE_SHA256`.

The managed runtime rejects any library whose reported ABI is not exactly `2` before a
codec entry point is used. CI and release gates must perform native round-trip tests, not
capability discovery alone.
