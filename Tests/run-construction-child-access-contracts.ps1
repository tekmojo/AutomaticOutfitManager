param([switch]$PreviousDecision,[switch]$PreviousAdmissionDecision)
$ErrorActionPreference='Stop'
$rcRoot=Split-Path $PSScriptRoot -Parent
$vsDir=& "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" -latest -products '*' -requires Microsoft.Component.MSBuild -property installationPath
$compiler=Join-Path $vsDir 'MSBuild/Current/Bin/Roslyn/csc.exe'
$testDir=Join-Path $env:TEMP ('aom-child-construction-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testDir | Out-Null
try {
 $filter=Get-Content (Join-Path $rcRoot 'Source/Patches/WorkGiverPausedArea_Patches.cs') -Raw
 $start=$filter.IndexOf('        public static ApparelRule DeniedActivityRule(')
 $end=$filter.IndexOf('        public static ApparelRule DeniedHaulingRule(', $start)
 $decision=$filter.Substring($start,$end-$start)
 if($PreviousAdmissionDecision){$decision=$decision -replace 'if \(ConstructionChildAccess.TryGetRouteRestriction\(pawn, job, out ApparelRule childDenied\)\)\s*return childDenied;', ''}
 $fixture='using System.Collections.Generic;using System.Linq;using Verse;using Verse.AI;using AutomaticOutfitManager.Core;using AutomaticOutfitManager.Rules;using AutomaticOutfitManager.State;using AutomaticOutfitManager.Detection;namespace AutomaticOutfitManager.Patches { public static partial class PausedAreaWorkFilter {'+$decision+'}}'
 $decisionFile=Join-Path $testDir 'ActivityDecision.cs'
 Set-Content -LiteralPath $decisionFile -Value $fixture
 $exe=Join-Path $testDir 'Tests.exe'
 $harmony='F:/Steam/steamapps/workshop/content/294100/2009463077/Current/Assemblies/0Harmony.dll'
 Copy-Item -LiteralPath $harmony -Destination (Join-Path $testDir '0Harmony.dll')
 & $compiler /nologo /target:exe /langversion:latest /warn:0 "/out:$exe" "/reference:$harmony" $decisionFile (Join-Path $PSScriptRoot 'ConstructionChildAccessTests.cs') (Join-Path $rcRoot 'Source/Patches/ConstructionChildAccess.cs') (Join-Path $rcRoot 'Source/Detection/ChildAreaAccessPolicy.cs')
 if($LASTEXITCODE -ne 0){throw 'Construction child access fixture compilation failed'}
 if($PreviousDecision){
  $ErrorActionPreference='Continue'
  $output=& $exe --previous 2>&1
  $result=$LASTEXITCODE
  $ErrorActionPreference='Stop'
  if($result -ne 1 -or ($output -join "`n") -notmatch 'restricted child does not accept impossible boundary delivery'){throw "Negative control did not fail at the observed defect: $output"}
  'PASS negative control: previous unfiltered scanner accepts impossible boundary delivery.'
 }elseif($PreviousAdmissionDecision){
  $ErrorActionPreference='Continue'
  $output=& $exe 2>&1
  $result=$LASTEXITCODE
  $ErrorActionPreference='Stop'
  if($result -ne 1 -or ($output -join "`n") -notmatch 'carried-phase runtime retains legal exterior delivery'){throw "Admission negative control failed unexpectedly: $output"}
  'PASS negative control: previous activity decision interrupts the legal carried-phase delivery.'
 }else{
  & $exe
  if($LASTEXITCODE -ne 0){throw 'Construction child access checks failed'}
 }
}finally{
 foreach($name in @('Tests.exe','0Harmony.dll','ActivityDecision.cs')){ $file=Join-Path $testDir $name;if(Test-Path -LiteralPath $file){Remove-Item -LiteralPath $file} }
 Remove-Item -LiteralPath $testDir
}
exit 0
