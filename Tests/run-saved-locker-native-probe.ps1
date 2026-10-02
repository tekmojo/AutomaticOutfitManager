param(
    [switch]$PreviousDecision,
    [switch]$ExpectedShelfRegression,
    [string]$CandidateAssemblyDirectory = '',
    [string]$RuntimeHarmonyPath = 'F:/Steam/steamapps/workshop/content/294100/2871933948/1.5/Source/VanillaAnimalsExpanded/packages/Lib.Harmony.2.3.1.1/lib/net8.0/0Harmony.dll'
)
$ErrorActionPreference = 'Stop'
$rcRoot = Split-Path $PSScriptRoot -Parent
if (!$CandidateAssemblyDirectory) { $CandidateAssemblyDirectory = Join-Path $rcRoot '1.6/Assemblies' }
$vsDir = & "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" -latest -products '*' -requires Microsoft.Component.MSBuild -property installationPath
$compiler = Join-Path $vsDir 'MSBuild/Current/Bin/Roslyn/csc.exe'
$managed = 'F:/Steam/steamapps/common/RimWorld/RimWorldWin64_Data/Managed'
$harmonyDir = 'F:/Steam/steamapps/workshop/content/294100/2009463077/Current/Assemblies'
$testDir = Join-Path $env:TEMP ('aom-saved-locker-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testDir | Out-Null
try {
    # Test host only: RimWorld's Mono Harmony build cannot run under CoreCLR.
    # Use an already installed net8 Harmony build; never replace game files.
    Copy-Item -LiteralPath $RuntimeHarmonyPath -Destination (Join-Path $testDir '0Harmony.dll')
    $exe=Join-Path $testDir 'Probe.exe'
    & $compiler /nologo /target:exe /langversion:latest /warn:0 "/out:$exe" "/reference:$managed/Assembly-CSharp.dll" "/reference:$managed/UnityEngine.CoreModule.dll" "/reference:$managed/netstandard.dll" "/reference:$harmonyDir/0Harmony.dll" "/reference:$CandidateAssemblyDirectory/AutomaticOutfitManager.dll" (Join-Path $PSScriptRoot 'SavedLockerNativeProbe.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Saved locker native probe compilation failed.' }
    # RimWorld storage interfaces contain default implementations, which need
    # a modern CLR rather than the .NET Framework used by older fixtures.
    '{"runtimeOptions":{"tfm":"net8.0","framework":{"name":"Microsoft.NETCore.App","version":"8.0.0"}}}' | Set-Content -LiteralPath (Join-Path $testDir 'Probe.runtimeconfig.json')
    $probeArgs=@($managed,$CandidateAssemblyDirectory,$harmonyDir)
    if ($PreviousDecision) { $probeArgs+='--previous' }
    $output=& dotnet $exe @probeArgs 2>&1
    $result=$LASTEXITCODE
    if ($PreviousDecision -or $ExpectedShelfRegression) {
        if ($result -ne 1 -or ($output -join "`n") -notmatch 'native hauling keeps saved apparel in its originating locker') { throw ('Unexpected negative control result: '+($output -join "`n")) }
        if ($ExpectedShelfRegression) {
            Write-Output 'PASS: supplied pre-fix candidate rejects pass-through locker storage (negative control).'
        } else {
            Write-Output 'PASS: previous native selection exports saved gear to the remote locker (negative control).'
        }
    } else {
        $output | Write-Output
        if ($result -ne 0) { throw 'Saved locker native checks failed.' }
    }
} finally {
    foreach ($name in @('Probe.exe','Probe.runtimeconfig.json','0Harmony.dll')) { $file=Join-Path $testDir $name; if (Test-Path -LiteralPath $file) { Remove-Item -LiteralPath $file } }
    Remove-Item -LiteralPath $testDir
}

