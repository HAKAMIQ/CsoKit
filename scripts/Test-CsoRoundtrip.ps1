<#
  CsoKit roundtrip audit (synthetic data only - no game files).
  Every case: ISO -> CSO -> ISO, SHA1 must match source. Any mismatch = FAIL.
  Exits with an error if any case is FAIL (SKIP and INFO rows are not failures).
  Usage:
    .\Test-CsoRoundtrip.ps1 -Exe "C:\...\csokit.exe" [-MaxCso "C:\...\maxcso.exe"] [-IncludeLarge]
#>
param(
    [string]$Exe = "$PSScriptRoot\..\tests\CsoKit.Tests\bin\Release\net10.0\csokit.exe",
    [string]$MaxCso = "",
    [switch]$IncludeLarge
)

$ErrorActionPreference = 'Stop'
$Exe = (Resolve-Path $Exe).Path
$work = Join-Path $env:TEMP ("csokit-audit-" + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $work | Out-Null
$results = New-Object System.Collections.Generic.List[object]

function Sha1($p) { (Get-FileHash -Algorithm SHA1 -LiteralPath $p).Hash }
function Run([string[]]$a) { & $Exe @a *> $null; return $LASTEXITCODE }
function TempLeft { @(Get-ChildItem -LiteralPath $work -Force -Filter '.*.tmp').Count + @(Get-ChildItem -LiteralPath $work -Force -Filter '.cso-*.tmp').Count }
function Add($case, $src, $out, $ok, $note) {
    $results.Add([pscustomobject]@{ Case=$case; SourceSHA1=$src; OutputSHA1=$out; Result=$(if($ok -is [string]){$ok}elseif($ok){'PASS'}else{'FAIL'}); Note=$note })
}

function New-Data([string]$name, [long]$size, [string]$kind) {
    $path = Join-Path $work $name
    $rng = New-Object System.Random 12345
    $fs = [System.IO.File]::Create($path)
    try {
        $buf = New-Object byte[] 65536
        $text = [System.Text.Encoding]::ASCII.GetBytes("CSOKIT-SYNTHETIC-PATTERN-0123456789-")
        [long]$left = $size; [long]$pos = 0
        while ($left -gt 0) {
            $n = [int][Math]::Min($buf.Length, $left)
            switch ($kind) {
                'zero'   { [Array]::Clear($buf, 0, $n) }
                'random' { $rng.NextBytes($buf) }
                'text'   { for ($i=0; $i -lt $n; $i++) { $buf[$i] = $text[($pos + $i) % $text.Length] } }
                'mixed'  {
                    $sel = [int](($pos / 65536) % 3)
                    if ($sel -eq 0) { $rng.NextBytes($buf) }
                    elseif ($sel -eq 1) { [Array]::Clear($buf, 0, $n) }
                    else { for ($i=0; $i -lt $n; $i++) { $buf[$i] = $text[($pos + $i) % $text.Length] } }
                }
            }
            $fs.Write($buf, 0, $n); $left -= $n; $pos += $n
        }
    } finally { $fs.Dispose() }
    return $path
}

# --- 1) Roundtrip matrix ---
$cases = @(
    @{ n='s1.iso'; size=1000;                 k='text'   },  # smaller than one block
    @{ n='s2.iso'; size=2048;                 k='random' },  # exactly one block, incompressible
    @{ n='s3.iso'; size=1MB;                  k='text'   },  # exact multiple, compressible
    @{ n='s4.iso'; size=(1MB + 1234);         k='mixed'  },  # not a multiple of block size
    @{ n='s5.iso'; size=8MB;                  k='random' },  # incompressible -> stored blocks
    @{ n='s6.iso'; size=8MB;                  k='zero'   },  # all zero
    @{ n='s7.iso'; size=(48MB + 2048 * 7);    k='mixed'  }   # larger mixed, parallel path
)
$profiles = @(
    @{ p='game-safe'; t=@() },
    @{ p='fast';      t=@() },
    @{ p='game-safe'; t=@('--threads','1') }
)

foreach ($c in $cases) {
    $iso = New-Data $c.n $c.size $c.k
    $src = Sha1 $iso
    $base = [IO.Path]::GetFileNameWithoutExtension($c.n)
    $pi = 0
    foreach ($pr in $profiles) {
        $pi++
        $cso = Join-Path $work "$base$pi.cso"
        $back = Join-Path $work "$base${pi}r.iso"
        $case = "$($c.n) [$($pr.p) $($pr.t -join ' ')]"
        $e1 = Run (@('compress', $iso, '-o', $cso, '--profile', $pr.p, '--force', '--quiet') + $pr.t)
        if ($e1 -ne 0) { Add $case $src '' $false "compress exit $e1"; continue }
        $e2 = Run @('verify', $cso, '--deep')
        $e3 = Run @('decompress', $cso, '-o', $back, '--force', '--quiet')
        if ($e3 -ne 0) { Add $case $src '' $false "decompress exit $e3"; continue }
        $out = Sha1 $back
        Add $case $src $out (($out -eq $src) -and ($e2 -eq 0)) "verify --deep exit $e2"
        Remove-Item -LiteralPath $back -Force
    }
}

# --- 2) Cross-check with maxcso (optional) ---
if ($MaxCso -and (Test-Path $MaxCso)) {
    $iso = Join-Path $work 's7.iso'; $src = Sha1 $iso
    $mx = Join-Path $work 'mx.cso'; $kitBack = Join-Path $work 'mxr.iso'
    & $MaxCso $iso -o $mx *> $null
    $e = Run @('decompress', $mx, '-o', $kitBack, '--force', '--quiet')
    $h = if ($e -eq 0) { Sha1 $kitBack } else { '' }
    Add 'maxcso CSO -> CsoKit decompress' $src $h ($h -eq $src) "exit $e"
    $kitCso = Join-Path $work 's71.cso'; $mxBack = Join-Path $work 'kmx.iso'
    & $MaxCso --decompress $kitCso -o $mxBack *> $null
    $h2 = if (Test-Path $mxBack) { Sha1 $mxBack } else { '' }
    Add 'CsoKit CSO -> maxcso decompress' $src $h2 ($h2 -eq $src) "maxcso exit $LASTEXITCODE"
} else {
    Add 'maxcso cross-check' '' '' 'SKIP' 'SKIPPED: maxcso not provided'
}

# --- 3) Failure handling: must fail and leave no output ---
# 3a) truncated CSO
$good = Join-Path $work 's41.cso'
$trunc = Join-Path $work 'tr.cso'
$bytes = [IO.File]::ReadAllBytes($good); [IO.File]::WriteAllBytes($trunc, $bytes[0..($bytes.Length - 101)])
$o = Join-Path $work 'tro.iso'
$e = Run @('decompress', $trunc, '-o', $o, '--quiet')
Add 'truncated CSO must fail' '' '' (($e -ne 0) -and -not (Test-Path $o) -and (TempLeft) -eq 0) "exit $e, output exists=$(Test-Path $o), temp left=$(TempLeft)"

# 3b) stored block whose index span is too small (decompress vs verify --deep)
$rnd = Join-Path $work 's51.cso'   # random data -> all stored blocks
$bad = Join-Path $work 'sb.cso'
$b = [IO.File]::ReadAllBytes($rnd)
$i0 = [BitConverter]::ToUInt32($b, 24) -band 0x7FFFFFFF
$new = [BitConverter]::GetBytes([uint32]([int64]$i0 + 100 + [int64]2147483648))
[Array]::Copy($new, 0, $b, 28, 4); [IO.File]::WriteAllBytes($bad, $b)
$o = Join-Path $work 'sbo.iso'
$ev = Run @('verify', $bad, '--deep')
$ed = Run @('decompress', $bad, '-o', $o, '--quiet')
Add 'stored block span < block size' '' '' (($ed -ne 0) -and -not (Test-Path $o)) "verify --deep exit $ev, decompress exit $ed, output exists=$(Test-Path $o)"

# 3c) corrupted compressed byte (CSO has no checksum: shows what is/isn't detectable)
$txt = Join-Path $work 's31.cso'
$cor = Join-Path $work 'cb.cso'
$b = [IO.File]::ReadAllBytes($txt)
$d0 = [BitConverter]::ToUInt32($b, 24) -band 0x7FFFFFFF
$b[$d0 + 5] = $b[$d0 + 5] -bxor 0xFF; [IO.File]::WriteAllBytes($cor, $b)
$o = Join-Path $work 'cbo.iso'
$ev = Run @('verify', $cor, '--deep')
$ed = Run @('decompress', $cor, '-o', $o, '--quiet')
$silent = ($ed -eq 0) -and (Test-Path $o) -and ((Sha1 $o) -ne (Sha1 (Join-Path $work 's3.iso')))
Add 'corrupted compressed byte (format has no checksum)' '' '' 'INFO' "verify --deep exit $ev, decompress exit $ed, silent wrong output=$silent"

# 3d) >2 GiB input must be refused clearly (no output, no temp)
if ($IncludeLarge) {
    foreach ($sz in @(2.5GB, 4.5GB)) {
        $big = Join-Path $work 'bg.iso'
        $fs = [IO.File]::Create($big); $fs.SetLength([long]$sz); $fs.Dispose()
        $o = Join-Path $work 'bg.cso'
        $e = Run @('compress', $big, '-o', $o, '--profile', 'game-safe', '--quiet')
        Add "compress $([math]::Round($sz/1GB,1)) GiB" '' '' (($e -ne 0) -and -not (Test-Path $o) -and (TempLeft) -eq 0) "exit $e (expected refusal: IndexShiftRequired)"
        Remove-Item -LiteralPath $big -Force
    }
} else {
    Add '>2 GiB input' '' '' 'SKIP' 'SKIPPED: run with -IncludeLarge'
}

$results | Format-Table -AutoSize -Wrap
$csv = Join-Path $work 'results.csv'
$results | Export-Csv -NoTypeInformation -Encoding UTF8 $csv
Write-Host "Results: $csv"
Write-Host "Work dir (synthetic files only, safe to delete): $work"

$failed = @($results | Where-Object { $_.Result -eq 'FAIL' })
if ($failed.Count -gt 0) {
    throw "$($failed.Count) audit case(s) failed: $($failed.Case -join '; ')"
}
