#Requires -Version 7.4
<#
.SYNOPSIS
Builds, tests, packs and signs Catfood.Shapefile, then exports its signing certificate.
.EXAMPLE
pwsh -File .\setup\build.ps1 -Version 3.0.0
.NOTES
Requires Windows x64, .NET 10 SDK, Azure CLI (az login), and permission to sign
with the Artifact Signing account/profile in metadata.json. Does not publish.
#>
param(
    [ValidatePattern('^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$')]
    [string]$Version,
    [string]$MetadataPath = (Join-Path $PSScriptRoot 'metadata.json')
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Assert-ExitCode([string]$Operation) {
    if ($LASTEXITCODE -ne 0) { throw "$Operation failed (exit code $LASTEXITCODE)." }
}

if (-not $IsWindows) { throw 'Run this release script on Windows.' }
Get-Command dotnet, az -ErrorAction Stop | Out-Null
$metadata = Get-Content -LiteralPath $MetadataPath -Raw | ConvertFrom-Json
foreach ($property in 'Endpoint', 'CodeSigningAccountName', 'CertificateProfileName') {
    if ([string]::IsNullOrWhiteSpace($metadata.$property)) { throw "Missing $property in $MetadataPath" }
}
& az account get-access-token --resource 'https://codesigning.azure.net' --output none
Assert-ExitCode 'Azure signing token check; run az login --scope https://codesigning.azure.net/.default if needed'

$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'Catfood.Shapefile/Catfood.Shapefile.csproj'
# Unique output prevents stale or previously signed packages entering a release.
$release = Join-Path $root ("artifacts/releases/" + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N').Substring(0, 8))
$staging = Join-Path $release 'staging'
$unsigned = Join-Path $staging 'unsigned'
$signed = Join-Path $staging 'signed'
$signVersion = '0.9.1-beta.26371.2'
$toolDirectory = Join-Path $root "artifacts/tools/sign-$signVersion"
$sign = Join-Path $toolDirectory 'sign.exe'

Push-Location $root
try {
    if (-not (Test-Path -LiteralPath $sign)) {
        & dotnet tool install sign --version $signVersion --tool-path $toolDirectory
        Assert-ExitCode 'Install Sign CLI'
    }
    $versionArguments = @()
    if ($Version) { $versionArguments += "-p:Version=$Version" }
    & dotnet test Catfood.Shapefile.sln --configuration Release --settings test.runsettings '-p:GeneratePackageOnBuild=false' @versionArguments
    Assert-ExitCode 'Release tests'

    New-Item -ItemType Directory -Path $unsigned, $signed -Force | Out-Null
    & dotnet pack $project --configuration Release --output $unsigned '-p:GeneratePackageOnBuild=false' @versionArguments
    Assert-ExitCode 'NuGet pack'
    $packages = @(Get-ChildItem -LiteralPath $unsigned -Filter '*.nupkg' -File)
    if ($packages.Count -ne 1) { throw "Expected one NuGet package, found $($packages.Count)." }
    $package = Join-Path $signed $packages[0].Name
    Copy-Item -LiteralPath $packages[0].FullName -Destination $package

    # No --recurse-containers: sign the NuGet package itself.
    & $sign code artifact-signing $package `
        --artifact-signing-endpoint $metadata.Endpoint `
        --artifact-signing-account $metadata.CodeSigningAccountName `
        --artifact-signing-certificate-profile $metadata.CertificateProfileName `
        --azure-credential-type azure-cli `
        --file-digest sha256 --timestamp-digest sha256 `
        --timestamp-url 'http://timestamp.acs.microsoft.com' --verbosity Information
    Assert-ExitCode 'Artifact Signing'
    & dotnet nuget verify $package --all
    Assert-ExitCode 'NuGet signature verification'

    $certificate = [IO.Path]::ChangeExtension($package, '.cer')
    & (Join-Path $PSScriptRoot 'Export-PackageCertificate.ps1') -PackagePath $package -CertificatePath $certificate
    # Only fully verified output is placed in the ready directory.
    $ready = Join-Path $release 'ready'
    New-Item -ItemType Directory -Path $ready | Out-Null
    Copy-Item -LiteralPath $package, $certificate -Destination $ready
    Write-Host "Release ready: $ready" -ForegroundColor Green
    Write-Host 'Upload the .cer to your NuGet.org account before uploading this .nupkg.'
}
finally { Pop-Location }
