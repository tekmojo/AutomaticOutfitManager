param([ValidateSet('None','Candidate','Exit','Edit','Threshold','ExitCost','Occupied')][string]$PreviousDecision='None')
$ErrorActionPreference='Stop'
$rcRoot=Split-Path $PSScriptRoot -Parent
$vswhere=Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
$vsDir=& $vswhere -latest -products * -requires Microsoft.Component.MSBuild -property installationPath
$compiler=Join-Path $vsDir 'MSBuild/Current/Bin/Roslyn/csc.exe'
$stem=Join-Path $env:TEMP ('aom-unavailable-non-work-'+[guid]::NewGuid().ToString('N'))
function Method([string]$source,[string]$name) {
 $match=[regex]::Match($source,'(?m)^        (?:public|private|internal) static [^\r\n]*\b'+$name+'\(')
 if(!$match.Success){$match=[regex]::Match($source,'(?m)^        public void '+$name+'\(')}
 if(!$match.Success){throw "Missing method $name"}
 $start=$match.Index; $brace=$source.IndexOf('{',$start); $end=$brace+1; $depth=1
 while($depth -gt 0){if($source[$end] -eq '{'){$depth++};if($source[$end] -eq '}'){$depth--};$end++}
 return $source.Substring($start,$end-$start)
}
try {
 $start=Get-Content -Raw -LiteralPath (Join-Path $rcRoot 'Source/Patches/PawnJobTracker_StartJob_Patch.cs')
 $old=(& git -C $rcRoot show HEAD:Source/Patches/PawnJobTracker_StartJob_Patch.cs) -join "`n"
 $core=Get-Content -Raw -LiteralPath (Join-Path $rcRoot 'Source/Core/AutomaticOutfitManagerGameComponent.cs')
 $path=Get-Content -Raw -LiteralPath (Join-Path $rcRoot 'Source/Patches/ProtectedPathAvoidance.cs')
 $prefix='using System;using System.Linq;using System.Collections.Generic;using AutomaticOutfitManager.Core;using AutomaticOutfitManager.Detection;using AutomaticOutfitManager.Rules;using AutomaticOutfitManager.State;using Verse;using Verse.AI;'
 $body=$prefix+'namespace AutomaticOutfitManager.Patches { public static partial class PawnJobTracker_StartJob_Patch {'
 if($PreviousDecision -eq 'Candidate') {
  $body+='private static void BlockUnavailableGear(Pawn p,ApparelRule r,Job j,int ticks=1200){UnavailableWorkRegistry.Block(p,r,ticks);}'
 } else {$body+=Method $start 'BlockUnavailableGear'}
 $exitSource=if($PreviousDecision -eq 'Exit'){$old}else{$start}
 $cellMethod=Method $exitSource 'TryFindSafeTransitionCell'
 if($PreviousDecision -eq 'Threshold'){$cellMethod=$cellMethod.Replace('if (!cell.IsValid && requireExitRoute)','if (false)')}
 $body+=(Method $exitSource 'TryReplaceUnavailableGearWaitWithEgress')+$cellMethod
 $occupied=Method $start 'TryPrepareForOccupiedRules'
 if($PreviousDecision -eq 'Occupied'){$occupied=[regex]::Replace($occupied,'!TryRedirectIdleMissingGearWaitWithEgress\([\s\S]*?selectedNonWorkOnly: true\) &&\s*','')}
 $body+=(Method $start 'TryRedirectIdleMissingGearWaitWithEgress')+$occupied
 $segmentMethod=Method $path 'SegmentAvoidsRules'
 if($PreviousDecision -eq 'ExitCost'){$segmentMethod=$segmentMethod.Replace('costRules.Count == 0 ? null : GridFor(pawn.Map, costRules)','restrictedRules.Count == 0 ? null : GridFor(pawn.Map, restrictedRules)')}
 $body+='} public static partial class ProtectedPathAvoidance {'+$segmentMethod+(Method $path 'PathAvoidsRules')+'}}'
 $notify=Method $core 'NotifyRuleRequirementsChanged'
 if($PreviousDecision -eq 'Edit'){$notify=$notify.Replace('UnavailableWorkRegistry.ClearRule(ruleId);','')}
 $body+='namespace AutomaticOutfitManager.Core { public partial class AutomaticOutfitManagerGameComponent {'+$notify+'}}'
 Set-Content -LiteralPath ($stem+'.cs') -Value $body
 & $compiler /nologo /target:exe /langversion:latest /warn:0 "/out:$stem.exe" ($stem+'.cs') (Join-Path $PSScriptRoot 'UnavailableNonWorkTests.cs') (Join-Path $rcRoot 'Source/Detection/UnavailableWorkRegistry.cs')
 if($LASTEXITCODE -ne 0){throw 'Unavailable Non-Work fixture compilation failed'}
 $output=& ($stem+'.exe') 2>&1; $result=$LASTEXITCODE; $output | Write-Output
 if($PreviousDecision -eq 'None'){if($result -ne 0){throw 'Unavailable Non-Work contracts failed'}}
 else {
  $expected=@{Candidate='outside ingredient task yields';Exit='Non-Work shortage uses nearby exit';Edit='selection edit releases stateless pawns';Threshold='exit finishes beyond blocked threshold';ExitCost='initial occupied route avoids entry penalty';Occupied='unchecked occupant exits before available gear preparation'}[$PreviousDecision]
  if($result -eq 0 -or ($output -join "`n") -notmatch $expected){throw 'Previous decision did not fail the intended regression'}
  Write-Output "Previous $PreviousDecision decision fails the intended regression (negative control)."
 }
} finally {
 foreach($file in @($stem+'.cs',$stem+'.exe')){if(Test-Path -LiteralPath $file){Remove-Item -LiteralPath $file}}
}
exit 0
