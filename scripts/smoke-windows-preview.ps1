param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string]$Executable
)

$ErrorActionPreference = 'Stop'
$resolved = (Resolve-Path -LiteralPath $Executable).Path
if ([System.IO.Path]::GetExtension($resolved) -ne '.exe') {
    throw "Windows Preview smoke target is not an EXE: $resolved"
}
$license = Join-Path (Split-Path -Parent $resolved) 'LICENSE.txt'
if (-not (Test-Path -LiteralPath $license -PathType Leaf) -or
    (Get-Content -Raw -LiteralPath $license).IndexOf('MIT License', [StringComparison]::Ordinal) -lt 0) {
    throw "The Windows release evidence is missing the readable MIT LICENSE beside the EXE."
}

$process = $null
try {
    $process = Start-Process -FilePath $resolved -PassThru
    Start-Sleep -Seconds 6
    $process.Refresh()
    if ($process.HasExited) {
        throw "The final Windows EXE exited before the six-second launch-smoke window (exit code $($process.ExitCode))."
    }
    Write-Host 'PASS: the final complete Windows EXE launched and remained alive for six seconds.'
}
finally {
    if ($null -ne $process) {
        $process.Refresh()
        if (-not $process.HasExited) {
            Stop-Process -Id $process.Id -Force
            $process.WaitForExit()
        }
        $process.Dispose()
    }
}
