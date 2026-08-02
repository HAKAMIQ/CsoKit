# CsoKit R3-D Benchmark Truth

R3-D adds a local evidence layer for compression and normalization decisions. It does not add external comparison dependencies, does not store a corpus in Git, and does not change the default game-safe CSO1 output target.

## Engineering decision

The project already had safe CSO1 output, deep verification, streaming normalization from readable CSO1, CSO2, ZSO, and DAX inputs, and native codec probing. The missing piece was repeatable local measurement. R3-D therefore focuses on bounded diagnostics and local reports instead of adding new output writers.

## Benchmark Truth Layer

Use the optional local script with developer-owned files:

```powershell
scripts/Run-BenchmarkTruthLayer.ps1 -Root D:\LocalCsoCorpus -Profiles game-safe,fast,smallest -Configuration Release
```

Or run it against explicit files:

```powershell
scripts/Run-BenchmarkTruthLayer.ps1 -InputPath D:\Corpus\game.iso,D:\Corpus\old.zso -Profiles game-safe -Configuration Release
```

The script writes JSON and Markdown reports to a temp folder by default. It never copies the corpus into the repository. Use `-OutputDirectory <path>` only when you intentionally want report files in a specific folder. Use `-KeepArtifacts` only when you need to inspect generated CSO/ISO artifacts.

Each case reports:

- physical input bytes and logical bytes
- CSO1 output bytes and saved percent against logical bytes
- encode or normalize milliseconds
- output deep-verify milliseconds
- decode milliseconds
- peak working-set bytes sampled from the CLI process
- profile and codec candidates
- selected codec wins and rejected reasons
- input/output logical SHA256 match
- warnings and failures

## Codec Optimizer

R3-D keeps game-safe conservative. The game-safe selector still prefers the smallest valid raw-deflate candidate and falls back to stored blocks when compression does not beat stored bytes.

The fast profile now has a near-tie decision rule: when two valid candidates are very close in size, it may prefer the lower measured or estimated encode/decode cost. This is deliberately scoped to fast intent and is covered by tests. The smallest and archive-smallest profiles still prioritize size.

Codec trial reports are now bounded:

- `--codec-report` gathers summary counts for every block.
- `--codec-report-block-limit <n>` controls how many per-block reports are retained.
- `0` keeps only aggregate evidence, which is what the benchmark script uses for large local corpora.

## Repair Diagnostics

Repair/normalize can now emit the same codec report summary as direct ISO compression when requested. This matters because readable CSO1, CSO2, ZSO, and DAX inputs are normalized to CSO1 through the streaming writer.

## Compatibility Evidence

Proven in-tree:

- deterministic fixtures for CSO1, CSO2, ZSO, DAX, raw ISO, and PSP ISO analysis
- corrupt/truncated payload refusal without trusted final output
- streaming repair to CSO1 for readable container fixtures
- deep verify and SHA256 reconstruction for supported readable containers
- bounded codec report summaries
- fast near-tie selector behavior without changing game-safe selection

Proven only when the local benchmark script is run:

- compression ratio and timing for a developer-supplied corpus
- profile-specific codec wins and rejected reasons on real files
- local native backend impact
- logical SHA256 roundtrip on those exact files

Not proven:

- every game image
- every malformed dump
- emulator-specific performance or graphics behavior
- compression leadership against other software
- new output container compatibility

Correct support statement:

CsoKit reads supported readable containers, rejects corruption clearly, normalizes to CSO1 game-safe output by default, verifies output when requested or required by policy, and can produce local benchmark evidence for developer-owned files.
## R3-E clarification

After R3-E, the benchmark truth layer treats container inputs and ISO inputs differently. CSO1, CSO2, ZSO, and DAX inputs prove game-safe repair/normalization only; non-game-safe profiles on those inputs are reported as skipped because `repair` supports game-safe normalization only. Use raw ISO input when the goal is profile comparison.

