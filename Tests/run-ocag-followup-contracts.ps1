$ErrorActionPreference = 'Stop'
$rcRoot = Split-Path $PSScriptRoot -Parent
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
$vsDir = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -property installationPath
$compiler = Join-Path $vsDir 'MSBuild/Current/Bin/Roslyn/csc.exe'
$testDir = Join-Path $env:TEMP ('aom-ocag-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testDir | Out-Null
try {
    $source = Get-Content (Join-Path $rcRoot 'Source/Patches/WorkGiverPausedArea_Patches.cs') -Raw
    $fixtures = 'using System;using System.Reflection;using System.Collections.Generic;using System.Linq;using HarmonyLib;namespace AutomaticOutfitManager.Patches {'
    $names = @('HasJobThing','HasJobCell','HasJobFallback','JobOnThing','JobOnCell','JobOnFallback') | ForEach-Object { 'WorkGiverPausedArea_' + $_ + '_Patch' }
    $names += 'ThinkNodeJobGiver_ProtectedArea_Patch'
    foreach ($name in $names) {
        $start = $source.IndexOf('internal static class ' + $name)
        if ($start -lt 0) { throw "Missing production patch $name" }
        $prepare = [regex]::Match($source.Substring($start), 'private static bool Prepare\(\) => [^;]+;').Value
        if (!$prepare) { throw "Missing production gate $name" }
        $fixtures += '[HarmonyPatch] internal static class ' + $name + ' {' + $prepare + ' private static IEnumerable<MethodBase> TargetMethods(){return new[]{typeof(StartupTarget).GetMethod("Run")};} private static void Postfix(ref bool __result){ StartupTests.Postfixes++; __result=false;} }'
    }
    $fixtures += '}'
    $fixtureFile=Join-Path $testDir 'fixtures.cs'
    Set-Content $fixtureFile $fixtures
    $harmonyCopy=Join-Path $testDir '0Harmony.dll'
    Copy-Item -LiteralPath 'F:/Steam/steamapps/workshop/content/294100/2009463077/Current/Assemblies/0Harmony.dll' -Destination $harmonyCopy
    $testOutput=Join-Path $testDir 'tests.exe'
    & $compiler /nologo /target:exe /langversion:latest /warn:0 "/out:$testOutput" "/reference:$harmonyCopy" $fixtureFile (Join-Path $PSScriptRoot 'OcagFollowupTests.cs') (Join-Path $rcRoot 'Source/Patches/DeferredWorkScannerPatches.cs') (Join-Path $rcRoot 'Source/Detection/RestorationWaitDiagnostics.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Follow-up test compilation failed.' }
    & $testOutput
    if ($LASTEXITCODE -ne 0) { throw 'Follow-up contracts failed.' }
} finally {
    # Only explicitly named files inside the freshly created test directory.
    foreach($name in @('fixtures.cs','tests.exe','0Harmony.dll')) {
        $file=Join-Path $testDir $name
        if(Test-Path -LiteralPath $file){Remove-Item -LiteralPath $file}
    }
    Remove-Item -LiteralPath $testDir
}
exit 0
