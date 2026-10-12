param(
    [Parameter(Mandatory = $true)]
    [string]$RimWorldDir,
    [string]$ManagedDir = '',
    [string]$HarmonyDll = ''
)

# Read-only inventory. Output is evidence of files, never a compatibility pass.
$ErrorActionPreference = 'Stop'
$gameRoot = (Resolve-Path -LiteralPath $RimWorldDir -ErrorAction Stop).Path
if (-not (Test-Path -LiteralPath $gameRoot -PathType Container)) {
    throw 'RimWorldDir must be a directory.'
}
$rcRoot = Split-Path $PSScriptRoot -Parent
if (-not $ManagedDir) {
    $ManagedDir = Join-Path $gameRoot 'RimWorldWin64_Data/Managed'
}
if (-not $HarmonyDll) {
    $HarmonyDll = Join-Path $gameRoot '../../workshop/content/294100/2009463077/Current/Assemblies/0Harmony.dll'
}

function Get-FileEvidence([string]$Path) {
    $fullPath = [IO.Path]::GetFullPath($Path)
    if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
        return [ordered]@{ Path = $fullPath; Exists = $false }
    }
    $file = Get-Item -LiteralPath $fullPath
    return [ordered]@{
        Path = $fullPath
        Exists = $true
        Bytes = $file.Length
        SHA256 = (Get-FileHash -LiteralPath $fullPath -Algorithm SHA256).Hash
        FileVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo($fullPath).FileVersion
    }
}

$versionPath = Join-Path $gameRoot 'Version.txt'
$aboutPath = Join-Path $rcRoot 'About/About.xml'
$about = [xml](Get-Content -LiteralPath $aboutPath -Raw)
$references = @('netstandard.dll', 'Assembly-CSharp.dll', 'UnityEngine.CoreModule.dll',
    'UnityEngine.IMGUIModule.dll', 'UnityEngine.TextRenderingModule.dll', 'Unity.Collections.dll')
$manifestPath = [IO.Path]::GetFullPath((Join-Path $gameRoot '../../appmanifest_294100.acf'))
$buildId = $null
if (Test-Path -LiteralPath $manifestPath -PathType Leaf) {
    $manifest = Get-Content -LiteralPath $manifestPath -Raw
    if ($manifest -match '"buildid"\s+"(\d+)"') { $buildId = $Matches[1] }
}

[ordered]@{
    CapturedUtc = [DateTime]::UtcNow.ToString('o')
    Scope = 'File inventory only; no build, deployment, patch execution or gameplay validation.'
    RimWorldDir = $gameRoot
    GameVersion = if (Test-Path -LiteralPath $versionPath) { (Get-Content -LiteralPath $versionPath -Raw).Trim() } else { $null }
    SteamBuildId = $buildId
    GameRootEntries = @(Get-ChildItem -LiteralPath $gameRoot | Sort-Object Name | Select-Object -ExpandProperty Name)
    ManagedDir = [IO.Path]::GetFullPath($ManagedDir)
    ReferenceLayoutNote = 'Default paths are the existing 1.6 build inputs. Supply explicit paths after inspecting a newer installation; missing files do not prove incompatibility.'
    References = @($references | ForEach-Object { Get-FileEvidence (Join-Path $ManagedDir $_) })
    Harmony = Get-FileEvidence $HarmonyDll
    AomVersion = [string]$about.ModMetaData.modVersion
    DeclaredGameVersions = @($about.ModMetaData.supportedVersions.li | ForEach-Object { [string]$_ })
    AomAssembly = Get-FileEvidence (Join-Path $rcRoot '1.6/Assemblies/AutomaticOutfitManager.dll')
    About = Get-FileEvidence $aboutPath
    LoadFolders = Get-FileEvidence (Join-Path $rcRoot 'LoadFolders.xml')
} | ConvertTo-Json -Depth 6
