param([switch]$PreviousDecision)
$ErrorActionPreference='Stop'
$rcRoot=Split-Path $PSScriptRoot -Parent
$vswhere=Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
$vsDir=& $vswhere -latest -products * -requires Microsoft.Component.MSBuild -property installationPath
$compiler=Join-Path $vsDir 'MSBuild/Current/Bin/Roslyn/csc.exe'
$testDir=Join-Path $env:TEMP ('aom-buffer-combat-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testDir | Out-Null
function Extract-Block([string]$source,[string]$signature) {
    $start=$source.IndexOf($signature)
    if($start -lt 0){throw "Missing production block: $signature"}
    $brace=$source.IndexOf('{',$start); $end=$brace+1; $depth=1
    while($depth -gt 0){if($source[$end] -eq '{'){$depth++};if($source[$end] -eq '}'){$depth--};$end++}
    $source.Substring($start,$end-$start)
}
try {
    $startSource=Get-Content -Raw -LiteralPath (Join-Path $rcRoot 'Source/Patches/PawnJobTracker_StartJob_Patch.cs')
    $endSource=Get-Content -Raw -LiteralPath (Join-Path $rcRoot 'Source/Patches/PawnJobTracker_EndCurrentJob_Patch.cs')
    $nonWork=Get-Content -Raw -LiteralPath (Join-Path $rcRoot 'Source/Patches/NonWorkBufferTracker.cs')
    $stateSource=Get-Content -Raw -LiteralPath (Join-Path $rcRoot 'Source/State/PawnApparelState.cs')
    $helpers=''
    foreach($name in @('IsBufferableJob','IsNativeMentalActivity','CanCountBufferedTask','IsNativeEmergencySafetyJob','IsMapDepartureJob')) {
        $helpers+=Extract-Block $startSource ('internal static bool '+$name+'(')
    }
    $guard=Extract-Block $startSource 'if (IsNativeMentalActivity(pawn, newJob))'
    $completion=(Extract-Block $endSource 'private static void CompleteOuterBufferCandidate(')+(Extract-Block $endSource 'private static void CompleteNestedBufferCandidates(')
    $nwEnd=Extract-Block $nonWork 'internal static void End('
    $nativeOverride=[regex]::Match($nonWork,'private static bool NativeOverride\([\s\S]*?;').Value
    if(!$nativeOverride){throw 'Missing Non-Work native override'}
    $clearing=(Extract-Block $stateSource 'public void ClearPendingBufferedTask(')+(Extract-Block $stateSource 'public void ClearPendingBufferCandidates(')
    # Compile actual completion decisions behind a real Harmony prefix. The
    # native-shaped body clears/pools the ending job before its next callback.
    $prefix=@'
using System;using System.Linq;using System.Collections.Generic;using HarmonyLib;using Verse;using Verse.AI;using RimWorld;using AutomaticOutfitManager.Core;using AutomaticOutfitManager.State;using AutomaticOutfitManager.Rules;using AutomaticOutfitManager.Detection;
namespace AutomaticOutfitManager.Patches {
'@
    $callsStart=$endSource.IndexOf('            CompleteOuterBufferCandidate(')
    $callsEnd=$endSource.IndexOf('        }',$callsStart)
    if($callsStart -lt 0 -or $callsEnd -le $callsStart){throw 'Missing completion calls'}
    $calls=$endSource.Substring($callsStart,$callsEnd-$callsStart)
    $expected=@{admission='social fight cannot become a buffer candidate';outer='outer social fight completion cannot spend a buffer';nested='nested social fight completion cannot spend a buffer';nonwork='non-work stale social fight candidate cannot count';native='native mental activity bypasses civilian outfit admission'}
    $harmony=Join-Path $testDir '0Harmony.dll'
    Copy-Item -LiteralPath 'F:/Steam/steamapps/workshop/content/294100/2009463077/Current/Assemblies/0Harmony.dll' -Destination $harmony
    foreach($case in $(if($PreviousDecision){@('admission','outer','nested','nonwork','native')}else{@('current')})) {
        $useHelpers=$helpers; $useCompletion=$completion; $useEnd=$nwEnd; $useGuard=$guard
        if($case -eq 'admission') {
            $useHelpers=$useHelpers.Replace('!IsNativeMentalActivity(null, job) &&','').Replace('!IsNativeMentalActivity(pawn, job) &&','')
        }
        if($case -eq 'outer' -or $case -eq 'nested') {
            $useCompletion=$useCompletion.Replace('PawnJobTracker_StartJob_Patch.CanCountBufferedTask(pawn, endingJob) &&','')
        }
        if($case -eq 'nonwork') {$useEnd=$useEnd -replace ' &&\s*PawnJobTracker_StartJob_Patch.CanCountBufferedTask\(pawn, job\)',''}
        if($case -eq 'native') {$useGuard=''}
        $generated=$prefix+'[HarmonyPatch(typeof(Pawn_JobTracker),"StartJob")] public static partial class PawnJobTracker_StartJob_Patch { static void Prefix(Pawn_JobTracker __instance,Job newJob){ var pawn=__instance.Pawn;var component=AutomaticOutfitManagerGameComponent.Current;var state=component.StateFor(pawn);bool fromQueue=false;ThinkNode jobGiver=null;JobTag? tag=null;ThinkTreeDef thinkTree=null;'+$useGuard+'__instance.ManagedChecks++;}'+$useHelpers+'}'
        $generated+='[HarmonyPatch(typeof(Pawn_JobTracker),"EndCurrentJob")]public static class BufferEndingPatch { static void Prefix(Pawn_JobTracker __instance,JobCondition condition){var pawn=__instance.Pawn;var endingJob=__instance.curJob;NonWorkBufferTracker.End(pawn,endingJob,condition);var component=AutomaticOutfitManagerGameComponent.Current;var state=component.StateFor(pawn);if(state==null)return;'+$calls+'}'+$useCompletion+'}'
        $generated+='public static partial class NonWorkBufferTracker{'+$nativeOverride+$useEnd+'}}namespace AutomaticOutfitManager.State{public partial class PawnApparelState{'+$clearing+'}}'
        $fixture=Join-Path $testDir 'Fixture.cs';Set-Content -LiteralPath $fixture -Value $generated
        $exe=Join-Path $testDir 'Tests.exe'
        & $compiler /nologo /target:exe /langversion:latest /warn:0 "/out:$exe" "/reference:$harmony" $fixture (Join-Path $PSScriptRoot 'BufferCombatTests.cs') (Join-Path $rcRoot 'Source/State/NonWorkOutfitBuffer.cs')
        if($LASTEXITCODE -ne 0){throw 'Combat buffer compilation failed'}
        $preference=$ErrorActionPreference
        try{$ErrorActionPreference='Continue';$output=& $exe $case 2>&1;$result=$LASTEXITCODE}finally{$ErrorActionPreference=$preference}
        if($PreviousDecision) {
            if($result -eq 0 -or ($output -join "`n") -notmatch $expected[$case]){throw "Negative control failed: $case : $output"}
            Write-Output "Previous $case decision fails targeted combat-buffer regression (negative control)."
        } else {$output;if($result -ne 0){throw 'Combat buffer checks failed'}}
    }
} finally {
    foreach($name in @('Fixture.cs','Tests.exe','0Harmony.dll')) {
        $file=Join-Path $testDir $name;if(Test-Path -LiteralPath $file){Remove-Item -LiteralPath $file}
    }
    Remove-Item -LiteralPath $testDir
}
exit 0
