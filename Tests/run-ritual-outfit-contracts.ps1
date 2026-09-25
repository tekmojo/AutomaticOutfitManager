param([switch]$PreviousClaims, [switch]$PreviousReadiness, [switch]$PreviousIdle, [string]$PreviousSourceRoot)
$ErrorActionPreference='Stop'
$rcRoot=Split-Path $PSScriptRoot -Parent
$vswhere=Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
$vsDir=& $vswhere -latest -products * -requires Microsoft.Component.MSBuild -property installationPath
$compiler=Join-Path $vsDir 'MSBuild/Current/Bin/Roslyn/csc.exe'
$testDir=Join-Path $env:TEMP ('aom-ritual-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testDir | Out-Null
try {
 $harmony=Join-Path $testDir '0Harmony.dll'
 Copy-Item 'F:/Steam/steamapps/workshop/content/294100/2009463077/Current/Assemblies/0Harmony.dll' $harmony
 $claims=Join-Path $rcRoot 'Source/Detection/ManagedWorkClaimRegistry.cs'
 if(($PreviousClaims -or $PreviousReadiness) -and !$PreviousSourceRoot){throw 'Pass -PreviousSourceRoot with the saved first-iteration source directory for negative controls.'}
 $gate=Join-Path $rcRoot 'Source/Patches/RitualOutfitPreparation.cs'
 if($PreviousReadiness -or $PreviousIdle) {
  $gateSource=Get-Content $gate -Raw
  if($PreviousReadiness) {
   $old=Get-Content (Join-Path $PreviousSourceRoot 'RitualOutfitPreparation.before.cs') -Raw
   $start=$old.IndexOf('        internal static bool WaitsForPawn(')
   $end=$old.IndexOf('        private static IEnumerable<Job> TransitionJobs(', $start)
   $from=$gateSource.IndexOf('        internal static bool WaitsForPawn(')
   $to=$gateSource.IndexOf('        private static bool CanReachCeremony(', $from)
   $gateSource=$gateSource.Substring(0,$from)+$old.Substring($start,$end-$start)+$gateSource.Substring($to)
  } else {
   # Previous component behavior always reached the normal idle watchdog.
   $from=$gateSource.IndexOf('        internal static bool RetainReadyOutfit(')
   $to=$gateSource.IndexOf('        internal static bool WaitsForPawn(', $from)
   $gateSource=$gateSource.Substring(0,$from)+'        internal static bool RetainReadyOutfit(Pawn pawn, PawnApparelState state) => false;'+"`n"+$gateSource.Substring($to)
  }
  $gate=Join-Path $testDir 'PreviousGate.cs';Set-Content $gate $gateSource
 }
 if($PreviousClaims) {
  $claims=Join-Path $testDir 'PreviousClaims.cs'
  $source=Get-Content (Join-Path $PreviousSourceRoot 'ManagedWorkClaimRegistry.before.cs') -Raw
  Set-Content $claims $source
 }
 $exe=Join-Path $testDir 'Tests.exe'
 & $compiler /nologo /target:exe /langversion:latest /warn:0 "/out:$exe" "/reference:$harmony" (Join-Path $PSScriptRoot 'RitualOutfitPreparationTests.cs') (Join-Path $rcRoot 'Source/Detection/ChildAreaAccessPolicy.cs') $gate $claims
 if($LASTEXITCODE -ne 0){throw 'Ritual fixture compilation failed'}
 $arguments=if($PreviousClaims){@('--claims-only')}else{@()}
 $ErrorActionPreference='Continue';$output=& $exe @arguments 2>&1;$result=$LASTEXITCODE;$ErrorActionPreference='Stop'
 $output | ForEach-Object { "$_" }
 if($PreviousClaims){
  if($result -eq 0 -or ($output -join "`n") -notmatch 'shared ritual target does not block other spectator seats'){throw 'Previous claim decision did not reproduce spectator serialization'}
  'PASS: previous production claims block different spectator seats (negative control).'
 }elseif($PreviousReadiness -or $PreviousIdle) {
  $expected=if($PreviousReadiness){'eligible participant waits before outfit state exists'}else{'ready leader retains outfit during native gathering Wait'}
  if($result -eq 0 -or ($output -join "`n") -notmatch $expected){throw 'Previous readiness/idle decision did not reproduce the regression'}
  "PASS: previous decision fails $expected (negative control)."
 }elseif($result -ne 0){throw 'Ritual checks failed'}
} finally {
 foreach($name in @('Tests.exe','0Harmony.dll','PreviousClaims.cs','PreviousGate.cs')) { $p=Join-Path $testDir $name;if(Test-Path -LiteralPath $p){Remove-Item -LiteralPath $p} }
 Remove-Item -LiteralPath $testDir
}
exit 0
