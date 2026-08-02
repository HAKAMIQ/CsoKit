# R1 Architecture Repair: Native Backend Policy

> Historical note: this document records the R1 decision before the threaded/native codec phase. Current builds include a bounded threaded compression pipeline, native zlib/libdeflate raw-Deflate candidates, and an explicit optional native Zopfli candidate path.

R1 resolves the native-backend work as an architecture boundary, not a compression-speed feature.

## Decision

The production compression path remains the managed CSO compressor unless native candidates are available and selected safely. The native DLL is release-gated only for runtime availability, ABI compatibility, `native-info`, and fallback behavior.

## Rationale

Measured real-ISO evidence did not prove that native participation made compression faster. The managed fast profile was faster and slightly smaller in the manual native-versus-managed proof. Keeping an unproven native hint in the compression hot path would make the release harder to reason about without delivering a measured benefit.

## Accepted R1 scope

- Native DLL build and final publish inclusion.
- `native-info` runtime reporting.
- Managed fallback via `CSOKIT_DISABLE_NATIVE` for diagnostics and benchmarks.
- Final release gate verification that the release package loads the native backend.
- Benchmark support for comparing native-runtime and managed-runtime modes.

## Rejected R1 scope

- No native compression acceleration claim.
- No native block store hint in the production compression path.
- No threaded compression pipeline.
- No producer/consumer compression engine.
- No change to `smallest`, `compat`, or `fast` profile output policy.

## Future phase boundary

Parallel compression, pipelined I/O, and real native acceleration belong in a later phase. That phase must introduce its own gates and prove:

- Same CSO output validity.
- SHA256 roundtrip correctness.
- Repeatable speed improvement on real ISOs.
- No regression in `smallest`/`compat` size behavior unless explicitly approved.
