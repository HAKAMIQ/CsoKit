function Get-CsoKitOutputFileNameLength {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $baseName = [System.IO.Path]::GetFileNameWithoutExtension($Path)

    if ([string]::IsNullOrWhiteSpace($baseName)) {
        return 0
    }

    return [System.Globalization.StringInfo]::ParseCombiningCharacters($baseName).Length
}

function Assert-CsoKitOutputFileName {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [string]$Context = "Generated output"
    )

    $baseName = [System.IO.Path]::GetFileNameWithoutExtension($Path)
    $length = Get-CsoKitOutputFileNameLength -Path $Path

    if ($length -lt 2 -or $length -gt 10) {
        throw "$Context file name '$baseName' contains $length Unicode text elements; expected 2 to 10. Path: $Path"
    }
}
