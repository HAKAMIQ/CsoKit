# Security and reliability hardening

This document records the hardening applied after the 0.6.0 architecture review.

## Native runtime boundary

Production resolves the native library only from `AppContext.BaseDirectory`.
Repository `artifacts` directories are ignored unless the developer explicitly sets:

```text
CSOKIT_NATIVE_DEV_SEARCH=1
```

Development search should be combined with a pinned SHA-256 value:

```text
CSOKIT_NATIVE_SHA256=<64 hexadecimal characters>
```

The managed runtime requires native ABI `2` before any codec call. An absent, invalid,
hash-mismatched, or ABI-incompatible library falls back to the managed backend.

## Cancellation

Cancellation now crosses CLI, WPF, compression, repair, and deep-verification layers.
Long block loops check the token between blocks. Writers check cancellation again after
verification and immediately before promoting a temporary file to its final path.

The WPF Stop action cancels the active operation. The CLI handles `Ctrl+C` for repair
and deep verification and returns exit code `130` for canceled operations.

## Compression parallelism

`CsoWorkerPolicy` is the single authority for worker-count validation and queue capacity.
The default is bounded by processor count, the absolute hard limit is 64, and an operator
may lower the effective maximum with `CSOKIT_MAX_WORKERS`. Queue-capacity arithmetic is checked.

## Application boundary

`CsoKit.Application` owns the long-running and state-changing use cases shared by CLI and WPF.
The CLI project no longer references Core directly and cannot instantiate the compressor, repairer,
or verification implementations without tripping the hardening gate. Container-reader creation is
centralized in Core. Results expose typed metrics, operation DTOs, and typed detail lines. Rendered
text is derived from those records; WPF no longer parses English result text to recover data.

## Native release verification

CI builds the native backend before managed tests and copies it to the test output.
Native integration tests fail when the DLL or ABI is unavailable and perform actual
raw-deflate/inflate round trips. The release publisher repeats a round trip through the
published CLI before producing the SHA-256 manifest and ZIP.

## Version source

The product version is stored once in the root `VERSION` file. MSBuild reads it through
`Directory.Build.props`; CMake generates native version constants from the same value;
release scripts use the same file when no explicit version is supplied.
