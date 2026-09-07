$ErrorActionPreference = 'Stop'
$rcRoot = Split-Path $PSScriptRoot -Parent
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
$vsDir = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -property installationPath
$compiler = Join-Path $vsDir 'MSBuild/Current/Bin/Roslyn/csc.exe'
$stem = Join-Path $env:TEMP ('aom-session-audit-' + [Guid]::NewGuid().ToString('N'))
$testOutput = $stem + '.exe'
$policyFile = $stem + '.cs'
try {
    # Compile the production restriction selection verbatim, excluding native
    # grid allocation/pathfinding plumbing; test the complete selected rule set.
    $source = Get-Content (Join-Path $rcRoot 'Source/Patches/ProtectedPathAvoidance.cs') -Raw
    $start = $source.IndexOf('        internal static IReadOnlyList<ApparelRule> RestrictedTransitRules(')
    $end = $source.IndexOf('        private static bool RouteExistsAvoiding(', $start)
    if ($start -lt 0 -or $end -le $start) { throw 'Restriction policy not found' }
    $prefix = @'
using System;using System.Collections.Generic;using AutomaticOutfitManager.Core;using AutomaticOutfitManager.Detection;using AutomaticOutfitManager.Rules;using AutomaticOutfitManager.State;using Verse;using Verse.AI;
namespace AutomaticOutfitManager.Patches { internal static class ProtectedPathAvoidance {
private static readonly IReadOnlyList<ApparelRule> EmptyRules=Array.Empty<ApparelRule>();
'@
    Set-Content -LiteralPath $policyFile -Value ($prefix + "`n" + $source.Substring($start,$end-$start) + "}}");
    & $compiler /nologo /target:exe /langversion:latest /warn:0 "/out:$testOutput" $policyFile (Join-Path $PSScriptRoot 'SessionAuditFixTests.cs') (Join-Path $rcRoot 'Source/Patches/EatingDestination.cs') (Join-Path $rcRoot 'Source/Patches/ReadingDestination.cs') (Join-Path $rcRoot 'Source/UI/PawnActivityPresentation.cs') (Join-Path $rcRoot 'Source/Detection/ActivityJobClassifier.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Session audit fix test compilation failed.' }
    & $testOutput
    if ($LASTEXITCODE -ne 0) { throw 'Session audit fix contracts failed.' }
} finally {
    foreach ($file in @($testOutput,$policyFile)) { if (Test-Path -LiteralPath $file) { Remove-Item -LiteralPath $file } }
}
exit 0
