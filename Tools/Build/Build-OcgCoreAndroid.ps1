[CmdletBinding()]
param(
    [string]$ProjectRoot = '',
    [string]$UnityEditorRoot = '',
    [ValidateSet('arm64-v8a')]
    [string]$Abi = 'arm64-v8a',
    [int]$AndroidApi = 26
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if ([string]::IsNullOrWhiteSpace($ProjectRoot)) {
    $scriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
    $ProjectRoot = (Resolve-Path (Join-Path $scriptDirectory '..\..')).Path
}

if ([string]::IsNullOrWhiteSpace($UnityEditorRoot)) {
    $projectVersionPath = Join-Path $ProjectRoot 'ProjectSettings\ProjectVersion.txt'
    $versionLine = Get-Content -LiteralPath $projectVersionPath |
        Where-Object { $_ -like 'm_EditorVersion:*' } |
        Select-Object -First 1
    $unityVersion = ($versionLine -split ':', 2)[1].Trim()
    $UnityEditorRoot = Join-Path -Path 'C:\Program Files\Unity\Hub\Editor' -ChildPath ($unityVersion + '-x86_64\Editor')
    if (-not (Test-Path -LiteralPath $UnityEditorRoot)) {
        $UnityEditorRoot = Join-Path -Path 'C:\Program Files\Unity\Hub\Editor' -ChildPath ($unityVersion + '\Editor')
    }
}

function Get-ShortPath {
    param([Parameter(Mandatory)][string]$Path)

    if (-not ('Arcane.NativeMethods' -as [type])) {
        Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
using System.Text;
namespace Arcane {
    public static class NativeMethods {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        public static extern uint GetShortPathName(
            string longPath,
            StringBuilder shortPath,
            uint bufferLength);
    }
}
'@
    }

    $buffer = New-Object System.Text.StringBuilder 4096
    $length = [Arcane.NativeMethods]::GetShortPathName(
        $Path,
        $buffer,
        [uint32]$buffer.Capacity)
    if ($length -eq 0) {
        throw "Could not create a short Windows path for $Path"
    }
    $buffer.ToString()
}

$coreRoot = Join-Path $ProjectRoot 'ThirdParty\ygopro-core'
$ndkRoot = Join-Path -Path $UnityEditorRoot -ChildPath 'Data\PlaybackEngines\AndroidPlayer\NDK'
$ndkBuild = Join-Path $ndkRoot 'ndk-build.cmd'
$builtLibrary = Join-Path $coreRoot "libs\$Abi\libocgcore.so"
$pluginDirectory = Join-Path -Path $ProjectRoot -ChildPath "Assets\Plugins\Android\$Abi"
$pluginLibrary = Join-Path $pluginDirectory 'libocgcore.so'
$manifestPath = Join-Path -Path $ProjectRoot -ChildPath 'Documentation\Native\OcgCoreAndroidBuild.json'

foreach ($required in @($coreRoot, $ndkBuild)) {
    if (-not (Test-Path -LiteralPath $required)) {
        throw "Required Android build input was not found: $required"
    }
}

$shortNdkBuild = Get-ShortPath $ndkBuild
& $shortNdkBuild -C $coreRoot 'NDK_PROJECT_PATH=.' "APP_ABI=$Abi" "APP_PLATFORM=android-$AndroidApi" 'APP_OPTIM=release'
if ($LASTEXITCODE -ne 0) {
    throw "Android NDK build failed with exit code $LASTEXITCODE."
}
if (-not (Test-Path -LiteralPath $builtLibrary)) {
    throw "The Android library was not produced: $builtLibrary"
}

New-Item -ItemType Directory -Force -Path $pluginDirectory | Out-Null
Copy-Item -LiteralPath $builtLibrary -Destination $pluginLibrary -Force

$sourceProperties = Get-Content -LiteralPath (Join-Path $ndkRoot 'source.properties')
$ndkRevision = ($sourceProperties |
    Where-Object { $_ -like 'Pkg.Revision*' } |
    Select-Object -First 1) -replace '^Pkg.Revision\s*=\s*', ''

$manifest = [ordered]@{
    schemaVersion = 1
    generatedUtc = [DateTime]::UtcNow.ToString('o')
    platform = 'Android'
    architecture = $Abi
    minimumApi = $AndroidApi
    configuration = 'Release'
    output = "Assets/Plugins/Android/$Abi/libocgcore.so"
    sha256 = (Get-FileHash -LiteralPath $pluginLibrary -Algorithm SHA256).Hash
    bytes = (Get-Item -LiteralPath $pluginLibrary).Length
    ndkRevision = $ndkRevision
}
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $manifestPath) | Out-Null
$manifest | ConvertTo-Json -Depth 5 |
    Set-Content -LiteralPath $manifestPath -Encoding UTF8
$manifest | ConvertTo-Json -Depth 5
