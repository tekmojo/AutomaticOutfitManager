param([switch]$PreviousDecision, [switch]$PreviousHaulDecision)
$ErrorActionPreference = 'Stop'
$rcRoot = Split-Path $PSScriptRoot -Parent
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
$vsDir = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -property installationPath
$compiler = Join-Path $vsDir 'MSBuild/Current/Bin/Roslyn/csc.exe'
$testDir = Join-Path $env:TEMP ('aom-boundary-admission-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testDir | Out-Null
function Read-Method([string]$source, [string]$name) {
    $match = [regex]::Match($source, '(?m)^        (?:public|private|internal) static [^\r\n]*\b' + $name + '\(')
    if (!$match.Success) { throw "Missing production method: $name" }
    $start = $match.Index
    $brace = $source.IndexOf('{', $start)
    $depth = 1; $end = $brace + 1
    while ($depth -gt 0 -and $end -lt $source.Length) {
        if ($source[$end] -eq '{') { $depth++ }
        if ($source[$end] -eq '}') { $depth-- }
        $end++
    }
    if ($depth -ne 0) { throw "Unbalanced method: $name" }
    return $source.Substring($start, $end - $start)
}
try {
    $source = Get-Content -Raw -LiteralPath (Join-Path $rcRoot 'Source/Patches/PawnJobTracker_StartJob_Patch.cs')
    $decisionSource = $source
    if ($PreviousDecision) {
        $decisionSource = (& git -C $rcRoot show HEAD:Source/Patches/PawnJobTracker_StartJob_Patch.cs) -join "`n"
        if ($LASTEXITCODE -ne 0) { throw 'Cannot read previous production decision' }
    }
    $call = 'PreferBoundaryInterruptedJob(pawn, state, ref newJob, ref jobGiver, ref thinkTree, ref tag, out __state);'
    $waitStart = $source.IndexOf('            if (BoundaryJobAdmission.IsOpen(pawn)')
    $waitEnd = $source.IndexOf('                return;', $waitStart)
    if ($waitStart -lt 0 -or $waitEnd -lt $waitStart) { throw 'Production recovery-wait guard not found' }
    $nestedWait = $source.Substring($waitStart, $waitEnd + '                return;'.Length - $waitStart)
    if ($PreviousDecision) {
        $call = $call.Replace(', out __state', '')
        $nestedWait = ''
    }
    # Execute the production promotion/helper/finalizer through real Harmony
    # and a tracker body that performs native-style queueing, nested recovery,
    # cleanup/pooling and failed admissions. This is not an in-game path test.
    $fixture = @'
using System; using System.Collections.Generic; using System.Linq;
using AutomaticOutfitManager.Core; using AutomaticOutfitManager.Detection;
using AutomaticOutfitManager.Rules; using AutomaticOutfitManager.State;
using HarmonyLib; using RimWorld; using Verse; using Verse.AI;
namespace AutomaticOutfitManager.Patches {
[HarmonyPatch(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.StartJob))]
public static partial class PawnJobTracker_StartJob_Patch {
private static readonly HashSet<Pawn> BoundaryResumeAdmissions = new HashSet<Pawn>();
public static void Prefix(Pawn_JobTracker __instance, ref Job newJob, ref ThinkNode jobGiver,
    ref ThinkTreeDef thinkTree, ref JobTag? tag, out BoundaryJobAdmission __state) {
    __state=null; var pawn=__instance.Pawn; var state=AutomaticOutfitManagerGameComponent.Current.StateFor(pawn);
'@
    $fixture += $nestedWait + "`n" + $call + "`n}`n"
    $fixture += Read-Method $decisionSource 'PreferBoundaryInterruptedJob'
    $fixture += Read-Method $decisionSource 'TryResumeBoundaryInterruptedJob'
    $fixture += Read-Method $source 'Finalizer'
    $fixture += Read-Method $source 'TryFindRestorationCell'
    $fixture += Read-Method $source 'PawnInsideRestorationLocker'
    $fixture += Read-Method $source 'StateProtectedRules'
    if ($PreviousHaulDecision) { $fixture = $fixture -replace '\|\|\s*PausedAreaWorkFilter.IsPermittedHaulForRule\(pawn, interruptedJob, rule\)', '' }
    $fixture += '}}'
    $fixtureFile = Join-Path $testDir 'fixtures.cs'
    Set-Content -LiteralPath $fixtureFile -Value $fixture
    $harmony = Join-Path $testDir '0Harmony.dll'
    Copy-Item -LiteralPath 'F:/Steam/steamapps/workshop/content/294100/2009463077/Current/Assemblies/0Harmony.dll' -Destination $harmony
    $exe = Join-Path $testDir 'tests.exe'
    & $compiler /nologo /target:exe /langversion:latest /warn:0 "/out:$exe" "/reference:$harmony" $fixtureFile (Join-Path $PSScriptRoot 'BoundaryAdmissionTests.cs') (Join-Path $rcRoot 'Source/Detection/ProtectedBoundaryRetryRegistry.cs') (Join-Path $rcRoot 'Source/Detection/BoundaryJobAdmission.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Boundary admission fixture compilation failed' }
    $output = & $exe 2>&1
    $result = $LASTEXITCODE
    $output | Write-Output
    if ($PreviousDecision) {
        if ($result -eq 0 -or ($output -join "`n") -notmatch 'ASSERT: native recovery wait survives') {
            throw 'Previous decision did not fail the targeted native-recovery regression'
        }
        Write-Output 'Previous production decision fails the intended regression (negative control).'
    } elseif ($PreviousHaulDecision) {
        if ($result -eq 0 -or ($output -join "`n") -notmatch 'permitted paused haul resumes boundary preparation') { throw 'Paused haul boundary negative control did not fail as expected' }
        Write-Output 'Previous paused haul boundary decision fails regression (negative control).'
    } elseif ($result -ne 0) { throw 'Boundary admission contracts failed' }
} finally {
    foreach ($name in @('fixtures.cs','tests.exe','0Harmony.dll')) {
        $file = Join-Path $testDir $name
        if (Test-Path -LiteralPath $file) { Remove-Item -LiteralPath $file }
    }
    Remove-Item -LiteralPath $testDir
}
exit 0
