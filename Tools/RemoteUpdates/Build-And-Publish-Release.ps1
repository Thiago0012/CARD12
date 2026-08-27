[CmdletBinding()]
param(
    [string]$Version,
    [int]$ProtocolVersion = 0,
    [string]$Notes = '',
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
)

$ErrorActionPreference = 'Stop'
$projectVersionPath = Join-Path $ProjectRoot 'ProjectSettings/ProjectVersion.txt'
$unityVersion = ((Get-Content -LiteralPath $projectVersionPath |
    Where-Object { $_ -like 'm_EditorVersion:*' }) -split ':', 2)[1].Trim()
$unityCandidates = @(
    "D:\Projeto do Game\$unityVersion\Editor\Unity.exe",
    (Join-Path $env:ProgramFiles "Unity\Hub\Editor\$unityVersion\Editor\Unity.exe")
)
$unity = $unityCandidates |
    Where-Object { Test-Path -LiteralPath $_ } |
    Select-Object -First 1
if (-not $unity) { throw "Unity $unityVersion não encontrada." }

if ($Version) { $env:MASTER_DUEL_RELEASE_VERSION = $Version }
if ($ProtocolVersion -gt 0) {
    $env:MASTER_DUEL_PROTOCOL_VERSION = [string]$ProtocolVersion
}
$env:MASTER_DUEL_RELEASE_NOTES = $Notes
$logPath = Join-Path $ProjectRoot 'Logs/remote-release-build.log'

$unityArguments = @(
    '-batchmode',
    '-nographics',
    '-buildTarget', 'Android',
    '-projectPath', ('"' + $ProjectRoot + '"'),
    '-executeMethod',
    'ArcaneDuel.Editor.RemoteUpdates.RemoteReleaseCommandLine.BuildSignedRelease',
    '-logFile', ('"' + $logPath + '"'),
    '-quit'
)
$unityProcess = Start-Process -FilePath $unity `
    -ArgumentList $unityArguments -Wait -PassThru -WindowStyle Hidden
if ($unityProcess.ExitCode -ne 0) {
    throw "A Unity não preparou o release. Consulte $logPath"
}

& (Join-Path $PSScriptRoot 'Publish-GitHubRelease.ps1') `
    -ProjectRoot $ProjectRoot
if ($LASTEXITCODE -ne 0) { throw 'A publicação no GitHub falhou.' }

Write-Output 'REMOTE_UPDATE_RELEASE_COMPLETE'
