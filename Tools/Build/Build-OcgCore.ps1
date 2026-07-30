[CmdletBinding()]
param(
    [string]$ProjectRoot = '',
    [string]$ToolRoot = 'D:\JOGO Y\Tools',
    [string]$BuildProjectAlias = 'D:\JOGO_Y_PROJECT',
    [string]$BuildToolAlias = 'D:\JOGO_Y_TOOLS',
    [string]$GitExe = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if ([string]::IsNullOrWhiteSpace($ProjectRoot)) {
    $scriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
    $ProjectRoot = (Resolve-Path (Join-Path $scriptDirectory '..\..')).Path
}

function Invoke-Checked {
    param(
        [Parameter(Mandatory)]
        [string]$FilePath,
        [Parameter(ValueFromRemainingArguments)]
        [string[]]$Arguments
    )

    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$FilePath failed with exit code $LASTEXITCODE."
    }
}

$coreRoot = Join-Path $ProjectRoot 'ThirdParty\ygopro-core'
$coreBuildRoot = Join-Path $BuildProjectAlias 'ThirdParty\ygopro-core'
$premake = Join-Path $BuildToolAlias 'premake-5.0.0-beta2\premake5.exe'
$toolBin = Join-Path $BuildToolAlias 'w64devkit\bin'
$make = Join-Path $toolBin 'make.exe'
$objdump = Join-Path $toolBin 'objdump.exe'
$strip = Join-Path $toolBin 'strip.exe'
$expectedDll = Join-Path $coreRoot 'bin\x64\release\ocgcore.dll'
$pluginRoot = Join-Path $ProjectRoot 'Assets\Plugins\Windows\x86_64'
$pluginDll = Join-Path $pluginRoot 'ocgcore.dll'
$manifestPath = Join-Path $ProjectRoot 'Documentation\Native\OcgCoreBuild.json'

if ([string]::IsNullOrWhiteSpace($GitExe)) {
    $gitCommand = Get-Command git.exe -ErrorAction SilentlyContinue
    if ($gitCommand) {
        $GitExe = $gitCommand.Source
    }
    else {
        $bundledGit = 'C:\Users\sousa\.cache\codex-runtimes\codex-primary-runtime\dependencies\native\git\cmd\git.exe'
        if (Test-Path -LiteralPath $bundledGit) {
            $GitExe = $bundledGit
        }
    }
}

foreach ($requiredPath in @(
    $coreRoot,
    $coreBuildRoot,
    $premake,
    $make,
    $objdump,
    $strip,
    $GitExe
)) {
    if (-not (Test-Path -LiteralPath $requiredPath)) {
        throw "Required build input was not found: $requiredPath"
    }
}

$previousPath = $env:PATH
$env:PATH = "$toolBin;$previousPath"

try {
    Push-Location $coreBuildRoot
    try {
        Invoke-Checked $premake 'gmake2'
        Invoke-Checked $make '-C' 'build' 'ocgcoreshared' 'config=release_x64'
    }
    finally {
        Pop-Location
    }
}
finally {
    $env:PATH = $previousPath
}

if (-not (Test-Path -LiteralPath $expectedDll)) {
    throw "The expected shared library was not produced: $expectedDll"
}

New-Item -ItemType Directory -Path $pluginRoot -Force | Out-Null
$unstrippedHash = (Get-FileHash -LiteralPath $expectedDll -Algorithm SHA256).Hash
$unstrippedBytes = (Get-Item -LiteralPath $expectedDll).Length
Copy-Item -LiteralPath $expectedDll -Destination $pluginDll -Force
Invoke-Checked $strip '--strip-unneeded' $pluginDll

$dependencyLines = & $objdump -p $pluginDll |
    Select-String -Pattern '^\s*DLL Name:\s*(.+)$'
$dependencies = @(
    $dependencyLines | ForEach-Object { $_.Matches[0].Groups[1].Value.Trim() }
)

$compilerVersion = (& (Join-Path $toolBin 'g++.exe') '--version' | Select-Object -First 1)
$makeVersion = (& $make '--version' | Select-Object -First 1)
$premakeVersion = (& $premake '--version' | Select-Object -First 1)
$coreCommit = (& $GitExe -C $coreRoot rev-parse HEAD).Trim()

$manifest = [ordered]@{
    schemaVersion = 1
    generatedUtc = [DateTime]::UtcNow.ToString('o')
    platform = 'Windows x64'
    configuration = 'Release'
    generator = 'gmake2'
    coreCommit = $coreCommit
    unstrippedBuildOutput = 'ThirdParty/ygopro-core/bin/x64/release/ocgcore.dll'
    unstrippedSha256 = $unstrippedHash
    unstrippedBytes = $unstrippedBytes
    output = 'Assets/Plugins/Windows/x86_64/ocgcore.dll'
    sha256 = (Get-FileHash -LiteralPath $pluginDll -Algorithm SHA256).Hash
    bytes = (Get-Item -LiteralPath $pluginDll).Length
    dependencies = $dependencies
    toolchain = [ordered]@{
        compiler = $compilerVersion
        make = $makeVersion
        premake = $premakeVersion
        root = $ToolRoot
        projectBuildAlias = $BuildProjectAlias
        toolBuildAlias = $BuildToolAlias
    }
}

New-Item -ItemType Directory -Path (Split-Path -Parent $manifestPath) -Force |
    Out-Null
$manifest | ConvertTo-Json -Depth 6 |
    Set-Content -LiteralPath $manifestPath -Encoding UTF8

$manifest | ConvertTo-Json -Depth 6
