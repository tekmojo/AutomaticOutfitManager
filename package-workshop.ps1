param(
    [string]$RimWorldDir = "",
    [string]$HarmonyDll = "",
    [string]$OutputRoot = "",
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"

if (-not $OutputRoot) {
    $OutputRoot = Join-Path $PSScriptRoot "work\workshop-staging"
}

$resolvedRepository = [System.IO.Path]::GetFullPath($PSScriptRoot)
$resolvedOutputRoot = [System.IO.Path]::GetFullPath($OutputRoot)
$stageDirectory = Join-Path $resolvedOutputRoot "AutomaticOutfitManager"
$resolvedStageDirectory = [System.IO.Path]::GetFullPath($stageDirectory)

if ($resolvedStageDirectory -eq $resolvedRepository -or
    -not $resolvedStageDirectory.StartsWith($resolvedOutputRoot + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "The staging directory must be a child of OutputRoot and cannot be the repository root."
}

if (-not $SkipBuild) {
    $buildArguments = @{}
    if ($RimWorldDir) {
        $buildArguments.RimWorldDir = $RimWorldDir
    }
    if ($HarmonyDll) {
        $buildArguments.HarmonyDll = $HarmonyDll
    }

    & (Join-Path $PSScriptRoot "build.ps1") @buildArguments
}

$assembly = Join-Path $PSScriptRoot "1.6\Assemblies\AutomaticOutfitManager.dll"
if (-not (Test-Path -LiteralPath $assembly -PathType Leaf)) {
    throw "Release assembly not found at $assembly. Build first or omit -SkipBuild."
}

$requiredFiles = @(
    "About\About.xml",
    "About\ModIcon.png",
    "About\Preview.png"
)

$requiredRootFiles = @(
    "LICENSE",
    "NOTICE.md"
)

foreach ($relativePath in @($requiredFiles) + @($requiredRootFiles)) {
    $sourcePath = Join-Path $PSScriptRoot $relativePath
    if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) {
        throw "Required Workshop file is missing: $relativePath"
    }
}

if (Test-Path -LiteralPath $resolvedStageDirectory) {
    Remove-Item -LiteralPath $resolvedStageDirectory -Recurse -Force
}

New-Item -ItemType Directory -Path $resolvedStageDirectory -Force | Out-Null

$stagedAboutDirectory = Join-Path $resolvedStageDirectory "About"
New-Item -ItemType Directory -Path $stagedAboutDirectory -Force | Out-Null
foreach ($relativePath in $requiredFiles) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $relativePath) -Destination $stagedAboutDirectory
}

foreach ($relativePath in $requiredRootFiles) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $relativePath) -Destination $resolvedStageDirectory
}

foreach ($directory in @("Defs", "Textures")) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $directory) -Destination $resolvedStageDirectory -Recurse
}

$stagedAssemblyDirectory = Join-Path $resolvedStageDirectory "1.6\Assemblies"
New-Item -ItemType Directory -Path $stagedAssemblyDirectory -Force | Out-Null
Copy-Item -LiteralPath $assembly -Destination $stagedAssemblyDirectory

$publishedFileId = Join-Path $PSScriptRoot "About\PublishedFileId.txt"
if (Test-Path -LiteralPath $publishedFileId -PathType Leaf) {
    Copy-Item -LiteralPath $publishedFileId -Destination $stagedAboutDirectory
}

$unexpectedFiles = Get-ChildItem -LiteralPath $resolvedStageDirectory -Recurse -File | Where-Object {
    $_.Extension -in @(".cs", ".csproj", ".pdb", ".ps1") -or $_.Name -eq ".git"
}
if ($unexpectedFiles) {
    throw "Unexpected development files entered the Workshop package: $($unexpectedFiles.FullName -join ', ')"
}

$xmlFiles = Get-ChildItem -LiteralPath $resolvedStageDirectory -Recurse -File -Filter "*.xml"
foreach ($xmlFile in $xmlFiles) {
    try {
        [void][xml](Get-Content -LiteralPath $xmlFile.FullName -Raw)
    }
    catch {
        throw "Invalid XML in $($xmlFile.FullName): $($_.Exception.Message)"
    }
}

$expectedVersion = "0.3.7"
$aboutXml = [xml](Get-Content -LiteralPath (Join-Path $stagedAboutDirectory "About.xml") -Raw)
if ($aboutXml.ModMetaData.modVersion -ne $expectedVersion) {
    throw "About.xml modVersion must be $expectedVersion. Current value: $($aboutXml.ModMetaData.modVersion)"
}

$stagedAssembly = Join-Path $stagedAssemblyDirectory "AutomaticOutfitManager.dll"
$assemblyVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($stagedAssembly).ProductVersion
if ($assemblyVersion -ne $expectedVersion) {
    throw "Release assembly ProductVersion must be $expectedVersion. Current value: $assemblyVersion"
}

Add-Type -AssemblyName System.Drawing
$previewPath = Join-Path $stagedAboutDirectory "Preview.png"
$previewImage = [System.Drawing.Image]::FromFile($previewPath)
try {
    if ($previewImage.Width * 9 -ne $previewImage.Height * 16) {
        throw "About/Preview.png must use a 16:9 aspect ratio. Current dimensions: $($previewImage.Width)x$($previewImage.Height)."
    }
}
finally {
    $previewImage.Dispose()
}

$iconPath = Join-Path $stagedAboutDirectory "ModIcon.png"
$iconImage = [System.Drawing.Bitmap]::new($iconPath)
try {
    if ($iconImage.Width -ne 64 -or $iconImage.Height -ne 64) {
        throw "About/ModIcon.png must be 64x64. Current dimensions: $($iconImage.Width)x$($iconImage.Height)."
    }
    if (-not [System.Drawing.Image]::IsAlphaPixelFormat($iconImage.PixelFormat) -or $iconImage.GetPixel(0, 0).A -ne 0) {
        throw "About/ModIcon.png must retain a transparent background."
    }
}
finally {
    $iconImage.Dispose()
}

$manifestPath = Join-Path $resolvedOutputRoot "AutomaticOutfitManager-0.3.7-SHA256.txt"
$stagePathPrefix = $resolvedStageDirectory.TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
$manifestLines = Get-ChildItem -LiteralPath $resolvedStageDirectory -Recurse -File |
    Sort-Object FullName |
    ForEach-Object {
        $relativePath = $_.FullName.Substring($stagePathPrefix.Length).Replace("\", "/")
        $hash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
        "$hash  $relativePath"
    }
$manifestLines | Set-Content -LiteralPath $manifestPath -Encoding utf8

$preview = Get-Item -LiteralPath $previewPath
if ($preview.Length -ge 1MB) {
    throw "About/Preview.png must remain under 1 MB. Current size: $($preview.Length) bytes."
}

$assemblyHash = (Get-FileHash -LiteralPath $stagedAssembly -Algorithm SHA256).Hash
Write-Host "Workshop package ready: $resolvedStageDirectory"
Write-Host "Manifest: $manifestPath"
Write-Host "Assembly SHA-256: $assemblyHash"
