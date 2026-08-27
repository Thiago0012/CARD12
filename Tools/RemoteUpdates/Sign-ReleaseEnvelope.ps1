[CmdletBinding()]
param(
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path,
    [string[]]$EnvelopePaths = @(
        'ContentStaging/production/v2/release-envelope.json',
        'Assets/Resources/RemoteUpdates/BundledReleaseEnvelope.json'
    )
)

$ErrorActionPreference = 'Stop'
$configurationPath = Join-Path $ProjectRoot '.release-secrets/release-secrets.json'
if (-not (Test-Path -LiteralPath $configurationPath)) {
    throw 'Segurança de publicação não inicializada.'
}
$configuration = Get-Content -LiteralPath $configurationPath -Raw |
    ConvertFrom-Json
$privateKeyPath = if ([IO.Path]::IsPathRooted($configuration.manifestPrivateKeyPath)) {
    $configuration.manifestPrivateKeyPath
} else {
    Join-Path $ProjectRoot $configuration.manifestPrivateKeyPath
}
$rsa = [Security.Cryptography.RSA]::Create()
if ([IO.Path]::GetExtension($privateKeyPath) -eq '.json') {
    $key = Get-Content -LiteralPath $privateKeyPath -Raw | ConvertFrom-Json
    $parameters = [Security.Cryptography.RSAParameters]::new()
    $parameters.Modulus = [Convert]::FromBase64String($key.modulus)
    $parameters.Exponent = [Convert]::FromBase64String($key.exponent)
    $parameters.D = [Convert]::FromBase64String($key.d)
    $parameters.P = [Convert]::FromBase64String($key.p)
    $parameters.Q = [Convert]::FromBase64String($key.q)
    $parameters.DP = [Convert]::FromBase64String($key.dp)
    $parameters.DQ = [Convert]::FromBase64String($key.dq)
    $parameters.InverseQ = [Convert]::FromBase64String($key.inverseQ)
    $rsa.ImportParameters($parameters)
} else {
    $rsa.ImportFromPem((Get-Content -LiteralPath $privateKeyPath -Raw))
}
try {
    foreach ($relativePath in $EnvelopePaths) {
        $path = if ([IO.Path]::IsPathRooted($relativePath)) {
            $relativePath
        } else {
            Join-Path $ProjectRoot $relativePath
        }
        $envelope = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
        if (-not $envelope.payload) { throw "Envelope sem payload: $path" }
        $payloadJson = $envelope.payload | ConvertTo-Json -Depth 20 -Compress
        $payloadBytes = [Text.Encoding]::UTF8.GetBytes($payloadJson)
        $signature = $rsa.SignData(
            $payloadBytes,
            [Security.Cryptography.HashAlgorithmName]::SHA256,
            [Security.Cryptography.RSASignaturePadding]::Pkcs1)
        $signed = [ordered]@{
            schemaVersion = [int]$envelope.schemaVersion
            keyId = [string]$configuration.manifestKeyId
            signatureBase64 = [Convert]::ToBase64String($signature)
            payload = $envelope.payload
        }
        $temporary = $path + '.tmp'
        [IO.File]::WriteAllText(
            $temporary,
            (($signed | ConvertTo-Json -Depth 20) + [Environment]::NewLine),
            [Text.UTF8Encoding]::new($false))
        Move-Item -LiteralPath $temporary -Destination $path -Force
        Write-Output "RELEASE_ENVELOPE_SIGNED path=$path"
    }
}
finally {
    $rsa.Dispose()
}
