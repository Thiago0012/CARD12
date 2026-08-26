[CmdletBinding()]
param(
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path,
    [string]$Repository = 'Thiago0012/CARD12',
    [switch]$ManifestOnly
)

$ErrorActionPreference = 'Stop'
$envelopePath = Join-Path $ProjectRoot 'ContentStaging/production/v2/release-envelope.json'
$envelope = Get-Content -LiteralPath $envelopePath -Raw | ConvertFrom-Json
$version = [string]$envelope.payload.latestClientVersion
$tag = if ($ManifestOnly) {
    'updater-bootstrap-' + $version + '-s' +
        [string]$envelope.payload.sequenceNumber
} else {
    'v' + $version
}
$artifactRoot = Join-Path $ProjectRoot 'ContentStaging/production/artifacts'
$windowsPath = Join-Path $artifactRoot "MasterDuel2PlusUltra-Windows-v$version.zip"
$androidPath = Join-Path $artifactRoot "MasterDuel2PlusUltra-Android-v$version.apk"

function Assert-Artifact([string]$Path, $Descriptor) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Artefato ausente: $Path"
    }
    $file = Get-Item -LiteralPath $Path
    if ($file.Length -ne [long]$Descriptor.sizeBytes) {
        throw "Tamanho divergente: $Path"
    }
    $hash = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($hash -ne ([string]$Descriptor.sha256).ToLowerInvariant()) {
        throw "SHA-256 divergente: $Path"
    }
}

if (-not $ManifestOnly) {
    Assert-Artifact $windowsPath $envelope.payload.windows
    Assert-Artifact $androidPath $envelope.payload.android
}
if ([string]::IsNullOrWhiteSpace([string]$envelope.signatureBase64)) {
    throw 'O manifesto não está assinado.'
}

function Get-GitHubToken {
    if (-not [string]::IsNullOrWhiteSpace($env:GITHUB_TOKEN)) {
        return $env:GITHUB_TOKEN
    }
    $gh = Get-Command gh -ErrorAction SilentlyContinue
    if ($gh) {
        $token = & $gh.Source auth token 2>$null
        if ($LASTEXITCODE -eq 0 -and $token) { return [string]$token }
    }
    $credentialRequest = "protocol=https`nhost=github.com`n`n"
    $credentialOutput = $credentialRequest | git credential fill 2>$null
    $passwordLine = $credentialOutput |
        Where-Object { $_ -like 'password=*' } |
        Select-Object -First 1
    if ($passwordLine) { return $passwordLine.Substring('password='.Length) }
    throw 'Autenticação do GitHub não encontrada no computador.'
}

$token = Get-GitHubToken
$headers = @{
    Authorization = "Bearer $token"
    Accept = 'application/vnd.github+json'
    'X-GitHub-Api-Version' = '2022-11-28'
}
$api = "https://api.github.com/repos/$Repository"
$release = $null
try {
    $release = Invoke-RestMethod -Uri "$api/releases/tags/$tag" -Headers $headers
}
catch {
    if ($_.Exception.Response.StatusCode.value__ -ne 404) { throw }
}
if ($release -and -not $release.draft) {
    throw "O release $tag já está público; não será sobrescrito."
}
if (-not $release) {
    $body = @{
        tag_name = $tag
        target_commitish = 'main'
        name = if ($ManifestOnly) {
            "Infraestrutura de atualização $version"
        } else {
            "Master Duel 2 Plus Ultra $version"
        }
        body = ([string]$envelope.payload.summary)
        draft = $true
        prerelease = $false
    } | ConvertTo-Json
    $release = Invoke-RestMethod -Method Post -Uri "$api/releases" `
        -Headers $headers -ContentType 'application/json' -Body $body
}

function Send-ReleaseAsset([string]$Path, [string]$ContentType) {
    $name = [IO.Path]::GetFileName($Path)
    $existing = $release.assets | Where-Object { $_.name -eq $name }
    if ($existing) {
        Invoke-RestMethod -Method Delete `
            -Uri "$api/releases/assets/$($existing.id)" -Headers $headers | Out-Null
    }
    $escapedName = [Uri]::EscapeDataString($name)
    $uploadUri = "https://uploads.github.com/repos/$Repository/releases/$($release.id)/assets?name=$escapedName"
    Invoke-RestMethod -Method Post -Uri $uploadUri -Headers $headers `
        -ContentType $ContentType -InFile $Path | Out-Null
    Write-Output "RELEASE_ASSET_UPLOADED name=$name"
}

if (-not $ManifestOnly) {
    Send-ReleaseAsset $windowsPath 'application/zip'
    Send-ReleaseAsset $androidPath 'application/vnd.android.package-archive'
}
Send-ReleaseAsset $envelopePath 'application/json'

$publishBody = @{ draft = $false } | ConvertTo-Json
Invoke-RestMethod -Method Patch -Uri "$api/releases/$($release.id)" `
    -Headers $headers -ContentType 'application/json' -Body $publishBody | Out-Null
Write-Output "GITHUB_RELEASE_PUBLISHED tag=$tag"
