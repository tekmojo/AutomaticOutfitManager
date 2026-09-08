param([switch]$PreviousDecision)
$ErrorActionPreference = 'Stop'
$rcRoot = Split-Path $PSScriptRoot -Parent
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
$vsDir = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -property installationPath
$compiler = Join-Path $vsDir 'MSBuild/Current/Bin/Roslyn/csc.exe'
$stem = Join-Path $env:TEMP ('aom-departure-' + [Guid]::NewGuid().ToString('N'))
$fixtureFile = $stem + '.cs'
$exe = $stem + '.exe'
function Slice([string]$source, [string]$start, [string]$end) {
    $a = $source.IndexOf($start)
    if ($a -lt 0) { throw "Missing production fragment: $start" }
    $b = $source.IndexOf($end, $a + $start.Length)
    if ($a -lt 0 -or $b -le $a) { throw "Missing production fragment: $start" }
    return $source.Substring($a,$b-$a)
}
try {
    $startSource = Get-Content (Join-Path $rcRoot 'Source/Patches/PawnJobTracker_StartJob_Patch.cs') -Raw
    $componentSource = Get-Content (Join-Path $rcRoot 'Source/Core/AutomaticOutfitManagerGameComponent.cs') -Raw
    $exitStart = $startSource.IndexOf('        internal static bool IsMapDepartureJob(Job job)')
    $exitEnd = $startSource.IndexOf("`n        }", $exitStart)
    if ($exitStart -lt 0 -or $exitEnd -le $exitStart) { throw 'Native exit predicate not found.' }
    $nativeExit = $startSource.Substring($exitStart, $exitEnd + "`n        }".Length - $exitStart)
    if ($PreviousDecision) {
        $startSource = (& git -C $rcRoot show HEAD:Source/Patches/PawnJobTracker_StartJob_Patch.cs) -join "`n"
        if ($LASTEXITCODE -ne 0) { throw 'Cannot read previous StartJob decision.' }
        $componentSource = (& git -C $rcRoot show HEAD:Source/Core/AutomaticOutfitManagerGameComponent.cs) -join "`n"
        if ($LASTEXITCODE -ne 0) { throw 'Cannot read previous restoration decision.' }
    }
    $admission = Slice $startSource '            if (component?.TryCompleteSatisfiedRestoration(pawn, state) == true)' '            Job proposedBeforeMeal'
    # The previous source has no completion handoff; its state-removal statement
    # is compiled unchanged. Current source additionally records departure here.
    $completionStart = if ($PreviousDecision) { '            PawnStates.Remove(state);' } else { '            if (state.MapDepartureRequested)' }
    # Locate only the EndIntervention body, not other departure flag checks.
    $endBody = $componentSource.Substring($componentSource.IndexOf('        public void EndIntervention('))
    $completion = Slice $endBody $completionStart '            TransitionActivityDiagnostics.Cleared'
    $fixture = @'
using System;using AutomaticOutfitManager.Core;using AutomaticOutfitManager.Detection;using Verse;using Verse.AI;
namespace AutomaticOutfitManager.Patches { public static class PawnJobTracker_StartJob_Patch {
public static void Admit(Pawn pawn,ref Job newJob) {
var component=AutomaticOutfitManagerGameComponent.Current;var state=component.StateFor(pawn);
'@
    $fixture += $admission + @'
if(state==null && IsMapDepartureJob(newJob)) return;
if(state==null) component.Preparations++;
}
'@ + $nativeExit + '}}'
    $fixture += 'namespace AutomaticOutfitManager.Core { public partial class AutomaticOutfitManagerGameComponent { public void EndIntervention(Pawn pawn,PawnApparelState state) {' + $completion + '}}}'
    $fixture += 'static class DepartureRuntimeFixture { public static bool Allows(Pawn pawn,Job job) {'
    if (!$PreviousDecision) {
        $runtime = Slice $componentSource '                    if (StateFor(pawn) == null && NativeDepartureHandoff.Allows(pawn, job))' '                    PawnApparelState runtimeState'
        $runtime = $runtime.Replace('StateFor(pawn)', 'AutomaticOutfitManagerGameComponent.Current.StateFor(pawn)').Replace('occupiedGearRecoveryTicks.Remove(pawn);', '').Replace('continue;', 'return true;')
        $fixture += $runtime
    }
    $fixture += 'return AutomaticOutfitManagerGameComponent.Current.StateFor(pawn)==null && AutomaticOutfitManager.Patches.PawnJobTracker_StartJob_Patch.IsMapDepartureJob(job); }}'
    Set-Content -LiteralPath $fixtureFile -Value $fixture
    & $compiler /nologo /target:exe /langversion:latest /warn:0 "/out:$exe" $fixtureFile (Join-Path $PSScriptRoot 'NativeDepartureHandoffTests.cs') (Join-Path $rcRoot 'Source/Detection/NativeDepartureHandoff.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Departure handoff compilation failed.' }
    if ($PreviousDecision) {
        $ErrorActionPreference = 'Continue'
        $output = & $exe 2>&1
        $result = $LASTEXITCODE
        $ErrorActionPreference = 'Stop'
        if ($result -eq 0 -or ($output -join "`n") -notmatch 'restored departure wait must not re-equip work outfits') { throw 'Previous production decision did not fail the departure regression.' }
        Write-Output 'Previous production decision fails restored departure wait without re-equipping (negative control).'
    } else {
        & $exe
        if ($LASTEXITCODE -ne 0) { throw 'Departure handoff checks failed.' }
    }
} finally {
    foreach ($file in @($fixtureFile,$exe)) { if (Test-Path -LiteralPath $file) { Remove-Item -LiteralPath $file } }
}
exit 0
