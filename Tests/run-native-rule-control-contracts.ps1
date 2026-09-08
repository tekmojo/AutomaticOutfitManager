param([string]$PreviousPathSource = '')
$ErrorActionPreference='Stop'
$rcRoot=Split-Path $PSScriptRoot -Parent
$vswhere=Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
$vsDir=& $vswhere -latest -products * -requires Microsoft.Component.MSBuild -property installationPath
$compiler=Join-Path $vsDir 'MSBuild/Current/Bin/Roslyn/csc.exe'
$testDir=Join-Path $env:TEMP ('aom-native-control-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testDir | Out-Null
function Read-Block([string]$source,[string]$signature) {
    $start=$source.IndexOf($signature); if($start -lt 0){throw "Missing production block $signature"}
    $brace=$source.IndexOf('{',$start);$end=$brace+1;$depth=1
    while($depth -gt 0){if($source[$end] -eq '{'){$depth++};if($source[$end] -eq '}'){$depth--};$end++}
    $source.Substring($start,$end-$start)
}
try {
    $start=Get-Content -Raw -LiteralPath (Join-Path $rcRoot 'Source/Patches/PawnJobTracker_StartJob_Patch.cs')
    $core=Get-Content -Raw -LiteralPath (Join-Path $rcRoot 'Source/Core/AutomaticOutfitManagerGameComponent.cs')
    $path=Get-Content -Raw -LiteralPath $(if($PreviousPathSource){$PreviousPathSource}else{Join-Path $rcRoot 'Source/Patches/PawnPathFollower_ProtectedArea_Patch.cs'})
    $guard=Read-Block $start 'if (IsNativeMentalActivity(pawn, newJob))'
    $fixture=@'
using System;using System.Collections.Generic;using System.Linq;using Verse;using Verse.AI;
using AutomaticOutfitManager.Core;using AutomaticOutfitManager.State;using AutomaticOutfitManager.Rules;using AutomaticOutfitManager.Detection;
namespace AutomaticOutfitManager.Patches {
public static partial class PawnJobTracker_StartJob_Patch {
public static void Admit(Pawn_JobTracker tracker,ref Job newJob,bool fromQueue){
var pawn=tracker.Pawn;var component=AutomaticOutfitManagerGameComponent.Current;var state=component.StateFor(pawn);
ThinkNode jobGiver=null;JobTag? tag=null;ThinkTreeDef thinkTree=null;
'@
    $fixture+=$guard+"`nif(component.UpdateNativeRuleSuspension(pawn,newJob))return;tracker.ManagedChecks++;}`n"
    $fixture+=(Read-Block $start 'internal static bool IsNativeMentalActivity(')+(Read-Block $start 'internal static bool IsNativeEmergencySafetyJob(')
    $fixture+='} public static partial class PawnPathFollower_ProtectedArea_Patch {'+(Read-Block $path 'public static bool Prefix(')+'}}'
    $fixture+='namespace AutomaticOutfitManager.Core {public partial class AutomaticOutfitManagerGameComponent {'+(Read-Block $core 'internal bool UpdateNativeRuleSuspension(')+'}}'
    $file=Join-Path $testDir 'Fixture.cs';Set-Content -LiteralPath $file -Value $fixture
    $exe=Join-Path $testDir 'Tests.exe'
    & $compiler /nologo /target:exe /langversion:latest /warn:0 "/out:$exe" $file (Join-Path $PSScriptRoot 'NativeRuleControlTests.cs') (Join-Path $rcRoot 'Source/Patches/NativeRuleControl.cs')
    if($LASTEXITCODE -ne 0){throw 'Native rule-control compilation failed'}
    $oldPreference=$ErrorActionPreference
    try{$ErrorActionPreference='Continue';$output=& $exe 2>&1;$result=$LASTEXITCODE}finally{$ErrorActionPreference=$oldPreference}
    $output | ForEach-Object { "$_" }
    if($PreviousPathSource) {
        if($result -eq 0 -or ($output -join "`n") -notmatch 'mental meal reaches next cell without a boundary interruption'){throw 'Previous path decision did not reproduce the intended mental meal loop'}
        'PASS: previous production path guard reproduces the mental meal boundary failure (negative control).'
    } elseif($result -ne 0){throw 'Native rule-control checks failed'}
    # The larger component methods are not simulated wholesale. Check guard
    # placement as integration wiring, separately from executable contracts.
    foreach($signature in @('private void ProcessPendingRecallInterrupts(','private void EnforceRuntimePawnRules(','private void RecoverIdleApparelWorkers(')) {
        $block=Read-Block $core $signature
        if(!$block.Contains('UpdateNativeRuleSuspension(pawn, pawn?.CurJob)')){throw "Missing periodic integration: $signature"}
    }
    foreach($fileName in @('Source/UI/MainRulesWindow.cs','Source/Patches/ProtectedPathAvoidance.cs')) {
        if((Get-Content -Raw -LiteralPath (Join-Path $rcRoot $fileName)) -notmatch 'NativeRuleControl.Suspends\(pawn, job\)'){throw "Missing projection/path-cost guard: $fileName"}
    }
    'PASS: periodic, activity-projection and path-cost wiring checks.'
} finally {
    foreach($name in @('Fixture.cs','Tests.exe')) { $file=Join-Path $testDir $name;if(Test-Path -LiteralPath $file){Remove-Item -LiteralPath $file} }
    Remove-Item -LiteralPath $testDir
}
exit 0
