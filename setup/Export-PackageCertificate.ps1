#Requires -Version 7.4
<#
.SYNOPSIS
Exports the primary signer certificate from a signed NuGet package as DER .cer.
#>
param(
    [Parameter(Mandatory)][string]$PackagePath,
    [string]$CertificatePath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Add-Type -AssemblyName System.Security.Cryptography.Pkcs

$PackagePath = (Resolve-Path -LiteralPath $PackagePath).Path
if (-not $CertificatePath) {
    $CertificatePath = [IO.Path]::ChangeExtension($PackagePath, '.cer')
}
$CertificatePath = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($CertificatePath)
if (Test-Path -LiteralPath $CertificatePath) {
    throw "Certificate output already exists: $CertificatePath"
}

$archive = [IO.Compression.ZipFile]::OpenRead($PackagePath)
try {
    $entry = $archive.GetEntry('.signature.p7s')
    if ($null -eq $entry) { throw "Package is unsigned: $PackagePath" }
    $stream = $entry.Open()
    $buffer = [IO.MemoryStream]::new()
    try {
        $stream.CopyTo($buffer)
        $cms = [Security.Cryptography.Pkcs.SignedCms]::new()
        $cms.Decode($buffer.ToArray())
    }
    finally {
        $stream.Dispose()
        $buffer.Dispose()
    }
}
finally { $archive.Dispose() }

if ($cms.SignerInfos.Count -ne 1) { throw 'Expected exactly one primary package signer.' }
# Select the signer, not the first certificate in the chain or timestamp signer.
$cms.CheckSignature($true)
$certificate = $cms.SignerInfos[0].Certificate
if ($null -eq $certificate) { throw 'The primary signer certificate is missing.' }
[IO.File]::WriteAllBytes($CertificatePath, $certificate.Export([Security.Cryptography.X509Certificates.X509ContentType]::Cert))
Write-Host "Certificate: $CertificatePath"
Write-Host "Subject: $($certificate.Subject)"
Write-Host "SHA256: $($certificate.GetCertHashString([Security.Cryptography.HashAlgorithmName]::SHA256))"
Write-Host "Valid until (UTC): $($certificate.NotAfter.ToUniversalTime().ToString('u'))"
