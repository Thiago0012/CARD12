[CmdletBinding()]
param(
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
)

$ErrorActionPreference = 'Stop'
$secretDirectory = Join-Path $ProjectRoot '.release-secrets'
$configurationPath = Join-Path $secretDirectory 'release-secrets.json'
$manifestPrivatePath = Join-Path $secretDirectory 'manifest-private-key.json'
$manifestPrivatePemPath = Join-Path $secretDirectory 'manifest-private-key.pem'
$manifestPublicPath = Join-Path $secretDirectory 'manifest-public-key.pem'
$keystorePath = Join-Path $secretDirectory 'master-duel-2-plus-ultra.p12'
$alias = 'masterduel2plusultra'

if (Test-Path -LiteralPath $configurationPath) {
    Write-Output "RELEASE_SECURITY_ALREADY_INITIALIZED path=$configurationPath"
    exit 0
}

New-Item -ItemType Directory -Path $secretDirectory -Force | Out-Null

function Convert-ToPem([string]$Label, [byte[]]$Bytes) {
    $base64 = [Convert]::ToBase64String($Bytes)
    $lines = for ($offset = 0; $offset -lt $base64.Length; $offset += 64) {
        $base64.Substring($offset, [Math]::Min(64, $base64.Length - $offset))
    }
    return "-----BEGIN $Label-----`n$($lines -join "`n")`n-----END $Label-----`n"
}

function New-SecureBuildPassword {
    $bytes = [byte[]]::new(32)
    [Security.Cryptography.RandomNumberGenerator]::Fill($bytes)
    return [Convert]::ToHexString($bytes).ToLowerInvariant()
}

$rsa = [Security.Cryptography.RSA]::Create(3072)
try {
    $privateParameters = $rsa.ExportParameters($true)
    $portablePrivateKey = [ordered]@{
        modulus = [Convert]::ToBase64String($privateParameters.Modulus)
        exponent = [Convert]::ToBase64String($privateParameters.Exponent)
        d = [Convert]::ToBase64String($privateParameters.D)
        p = [Convert]::ToBase64String($privateParameters.P)
        q = [Convert]::ToBase64String($privateParameters.Q)
        dp = [Convert]::ToBase64String($privateParameters.DP)
        dq = [Convert]::ToBase64String($privateParameters.DQ)
        inverseQ = [Convert]::ToBase64String($privateParameters.InverseQ)
    }
    $privatePem = Convert-ToPem 'PRIVATE KEY' $rsa.ExportPkcs8PrivateKey()
    $publicPem = Convert-ToPem 'PUBLIC KEY' $rsa.ExportSubjectPublicKeyInfo()
    [IO.File]::WriteAllText(
        $manifestPrivatePemPath,
        $privatePem,
        [Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText(
        $manifestPrivatePath,
        ($portablePrivateKey | ConvertTo-Json),
        [Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText(
        $manifestPublicPath,
        $publicPem,
        [Text.UTF8Encoding]::new($false))
}
finally {
    $rsa.Dispose()
}

$keytoolCandidates = @(
    'D:\Projeto do Game\6000.5.0f1\Editor\Data\PlaybackEngines\AndroidPlayer\OpenJDK\bin\keytool.exe',
    (Join-Path $env:ProgramFiles 'Unity\Hub\Editor\6000.5.0f1\Editor\Data\PlaybackEngines\AndroidPlayer\OpenJDK\bin\keytool.exe'),
    (Join-Path $env:ProgramFiles 'Unity\Hub\Editor\6000.0.38f1\Editor\Data\PlaybackEngines\AndroidPlayer\OpenJDK\bin\keytool.exe'),
    (Join-Path $env:ProgramFiles 'Android\Android Studio\jbr\bin\keytool.exe'),
    (Join-Path ${env:ProgramFiles(x86)} 'Common Files\Oracle\Java\javapath\keytool.exe'),
    (Join-Path $env:ProgramFiles 'Java\jre1.8.0_491\bin\keytool.exe')
)
$keytool = $keytoolCandidates |
    Where-Object { $_ -and (Test-Path -LiteralPath $_) } |
    Select-Object -First 1
if (-not $keytool) {
    $command = Get-Command keytool -ErrorAction SilentlyContinue
    if ($command) { $keytool = $command.Source }
}
if (-not $keytool) {
    throw 'keytool não foi encontrado. Instale o módulo Android da Unity.'
}

$password = New-SecureBuildPassword
& $keytool -genkeypair -noprompt `
    -keystore $keystorePath `
    -storetype PKCS12 `
    -storepass $password `
    -keypass $password `
    -alias $alias `
    -keyalg RSA `
    -keysize 4096 `
    -validity 36500 `
    -dname 'CN=Master Duel 2 Plus Ultra, OU=Release, O=Arcane Duel Team, C=BR'
if ($LASTEXITCODE -ne 0) { throw "keytool falhou com código $LASTEXITCODE." }

$certificatePath = Join-Path $secretDirectory 'android-release-certificate.der'
& $keytool -exportcert `
    -keystore $keystorePath `
    -storepass $password `
    -alias $alias `
    -file $certificatePath
if ($LASTEXITCODE -ne 0) { throw "A exportação do certificado falhou: $LASTEXITCODE." }
try {
    $certificateBytes = [IO.File]::ReadAllBytes($certificatePath)
    $sha = [Security.Cryptography.SHA256]::Create()
    try {
        $certificateSha256 = [Convert]::ToHexString(
            $sha.ComputeHash($certificateBytes)).ToLowerInvariant()
    }
    finally {
        $sha.Dispose()
    }
}
finally {
    Remove-Item -LiteralPath $certificatePath -Force -ErrorAction SilentlyContinue
}

$configuration = [ordered]@{
    schemaVersion = 1
    manifestKeyId = 'production-2026'
    # Unity/Mono signs through RSAParameters; keep the portable JSON as the
    # configured source. The PEM remains an additional offline backup/export.
    manifestPrivateKeyPath = '.release-secrets/manifest-private-key.json'
    androidKeystorePath = '.release-secrets/master-duel-2-plus-ultra.p12'
    androidKeystorePassword = $password
    androidAlias = $alias
    androidAliasPassword = $password
    androidCertificateSha256 = $certificateSha256
}
[IO.File]::WriteAllText(
    $configurationPath,
    ($configuration | ConvertTo-Json -Depth 4),
    [Text.UTF8Encoding]::new($false))

Write-Output "RELEASE_SECURITY_INITIALIZED directory=$secretDirectory keyId=production-2026"
Write-Output 'PRIVATE_KEYS_ARE_GIT_IGNORED_BACKUP_REQUIRED'
