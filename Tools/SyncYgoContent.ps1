param(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$Python = "python",
    [string]$Sqlite3 = ""
)

$ErrorActionPreference = "Stop"
$catalog = Join-Path $ProjectRoot "Documentation\CoreCardCatalog.csv"
$database = Join-Path $ProjectRoot "ThirdParty\BabelCDB\cards.cdb"
$localization = Join-Path $ProjectRoot "Documentation\CardTextPtBr.json"
$compiler = Join-Path $ProjectRoot "Tools\CardDbCompiler\compile_cards.py"
$streaming = Join-Path $ProjectRoot "Assets\StreamingAssets\Ygo"
$scriptsSource = Join-Path $ProjectRoot "ThirdParty\CardScripts"
$officialSource = Join-Path $scriptsSource "official"
$officialTarget = Join-Path $streaming "Scripts\official"
$customTarget = Join-Path $streaming "CustomScripts"
$dataTarget = Join-Path $streaming "Data"

New-Item -ItemType Directory -Force -Path $officialTarget, $customTarget, $dataTarget | Out-Null

$compilerArguments = @(
    $compiler,
    "--catalog", $catalog,
    "--database", $database,
    "--output", $dataTarget,
    "--minimum-count", "200"
)
if (Test-Path -LiteralPath $localization) {
    $compilerArguments += @("--localization", $localization)
}
if (-not [string]::IsNullOrWhiteSpace($Sqlite3)) {
    $compilerArguments += @("--sqlite3-cli", $Sqlite3)
}
& $Python @compilerArguments
if ($LASTEXITCODE -ne 0) { throw "Card database compiler failed with exit code $LASTEXITCODE" }

Get-ChildItem -LiteralPath $scriptsSource -File -Filter "*.lua" |
    Copy-Item -Destination (Join-Path $streaming "Scripts") -Force

$unofficialProcedure = Join-Path $scriptsSource "unofficial\proc_unofficial.lua"
if (Test-Path -LiteralPath $unofficialProcedure) {
    Copy-Item -LiteralPath $unofficialProcedure `
        -Destination (Join-Path $streaming "Scripts\proc_unofficial.lua") `
        -Force
}

$rows = Import-Csv -LiteralPath $catalog
if ($rows.Count -lt 200) { throw "Expected at least 200 catalog rows; found $($rows.Count)." }
$uniqueCodes = @($rows.official_code | Sort-Object -Unique)
if ($uniqueCodes.Count -ne $rows.Count) {
    throw "Core catalog contains duplicate official codes."
}
foreach ($row in $rows) {
    if ([string]::IsNullOrWhiteSpace($row.script_code)) {
        continue
    }

    $sourceName = "c$([uint64]$row.script_code).lua"
    $source = Join-Path $officialSource $sourceName
    if (-not (Test-Path -LiteralPath $source)) {
        $source = Join-Path $scriptsSource $sourceName
    }
    if (-not (Test-Path -LiteralPath $source)) {
        throw "Catalog script is missing from pinned CardScripts: $sourceName"
    }
    Copy-Item -LiteralPath $source -Destination (Join-Path $officialTarget $sourceName) -Force

    if ([uint64]$row.script_code -ne [uint64]$row.official_code) {
        # ocgcore requests the printed/passcode ID. When the pinned repository
        # stores an alternate printing under its database alias, expose the
        # exact official Lua under the requested ID without changing rule logic.
        $aliasShimName = "c$([uint64]$row.official_code).lua"
        Copy-Item -LiteralPath $source -Destination (Join-Path $customTarget $aliasShimName) -Force
    }
}

# Alias/canonical records are runtime dependencies even when they are not
# separate authored catalog entries. Include their official scripts whenever
# the pinned CardScripts repository provides one.
$runtimeCatalogPath = Join-Path $dataTarget "card-texts.json"
$runtimeCatalog = Get-Content -LiteralPath $runtimeCatalogPath -Raw |
    ConvertFrom-Json
foreach ($card in $runtimeCatalog.cards) {
    $runtimeScriptName = "c$([uint64]$card.code).lua"
    $runtimeScriptSource = Join-Path $officialSource $runtimeScriptName
    if (Test-Path -LiteralPath $runtimeScriptSource) {
        Copy-Item -LiteralPath $runtimeScriptSource `
            -Destination (Join-Path $officialTarget $runtimeScriptName) `
            -Force
    }
}

$copied = (Get-ChildItem -LiteralPath $officialTarget -File -Filter "c*.lua").Count
$aliasShims = (Get-ChildItem -LiteralPath $customTarget -File -Filter "c*.lua").Count
Write-Output "ARCANE_YGO_CONTENT_OK cards=$($rows.Count) officialScripts=$copied aliasShims=$aliasShims"
