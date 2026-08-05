param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string]$Version,
    [Parameter(Mandatory = $true, Position = 1)]
    [string]$BuildDrop,
    [Parameter(Mandatory = $true, Position = 2)]
    [string]$ArtifactDirectory,
    [Parameter(Mandatory = $true, Position = 3)]
    [string]$SbomTool,
    [Parameter(Mandatory = $true, Position = 4)]
    [string]$Platform
)

$ErrorActionPreference = 'Stop'
$buildDropPath = (Resolve-Path -LiteralPath $BuildDrop).Path
$artifactPath = (Resolve-Path -LiteralPath $ArtifactDirectory).Path
$toolPath = (Resolve-Path -LiteralPath $SbomTool).Path
$manifestRoot = Join-Path $buildDropPath '_manifest'
if (Test-Path -LiteralPath $manifestRoot) {
    Remove-Item -LiteralPath $manifestRoot -Recurse -Force
}

& $toolPath generate `
    -b $buildDropPath `
    -bc $env:GITHUB_WORKSPACE `
    -pn "FB2WordPress $Platform Preview" `
    -pv $Version `
    -ps 'Flameblade Studio' `
    -nsb "https://github.com/hitoshic1982/FB2WordPress/blob/$($env:GITHUB_SHA)/" `
    -V Information
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$manifest = Join-Path $manifestRoot 'spdx_2.2/manifest.spdx.json'
if (-not (Test-Path -LiteralPath $manifest -PathType Leaf) -or (Get-Item -LiteralPath $manifest).Length -eq 0) {
    throw 'Microsoft SBOM Tool did not produce an SPDX manifest.'
}
Copy-Item -LiteralPath $manifest -Destination (Join-Path $artifactPath "FB2WordPress-v$Version-$Platform-Preview.spdx.json")
Remove-Item -LiteralPath $manifestRoot -Recurse -Force
