param([switch]$PreviousDecision)
$ErrorActionPreference = 'Stop'
$rcRoot = Split-Path $PSScriptRoot -Parent
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
$vsDir = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -property installationPath
$compiler = Join-Path $vsDir 'MSBuild/Current/Bin/Roslyn/csc.exe'
$testOutput = Join-Path $env:TEMP ('aom-work-candidate-' + [Guid]::NewGuid().ToString('N') + '.exe')
$admissionFile = $testOutput + '.cs'
$oldRegistry = $testOutput + '.registry.cs'
try {
    $source = Get-Content (Join-Path $rcRoot 'Source/Patches/PawnJobTracker_StartJob_Patch.cs') -Raw
    $start = $source.IndexOf('            if (!assignedSavedRestorationJob &&')
    $end = $source.IndexOf('            // Some modded robot thinkers', $start)
    if ($start -lt 0 -or $end -le $start) { throw 'Production claim fallback not found.' }
    $prefix = @'
using AutomaticOutfitManager.Core;using AutomaticOutfitManager.Detection;using Verse;using Verse.AI;
static class ClaimAdmissionFixture {
static void ReplaceWithWait(Pawn pawn,int ticks,ref Job job,ref ThinkNode giver,ref JobTag? tag) { job=new Job { def=new JobDef { defName="Wait" } }; }
public static void Start(QueueTracker __instance,Pawn pawn,ref Job newJob) { bool assignedSavedRestorationJob=false;ThinkNode jobGiver=null;JobTag? tag=null;
'@
    Set-Content -LiteralPath $admissionFile -Value ($prefix + $source.Substring($start,$end-$start) + '}}')
    $registry = Join-Path $rcRoot 'Source/Detection/ManagedWorkClaimRegistry.cs'
    if ($PreviousDecision) {
        $old = (& git -C $rcRoot show HEAD:Source/Detection/ManagedWorkClaimRegistry.cs) -join "`n"
        if ($LASTEXITCODE -ne 0) { throw 'Cannot read previous claim registry.' }
        Set-Content -LiteralPath $oldRegistry -Value $old
        $registry = $oldRegistry
    }
    & $compiler /nologo /target:exe /langversion:latest /warn:0 "/out:$testOutput" $admissionFile (Join-Path $PSScriptRoot 'ManagedWorkCandidateTests.cs') $registry (Join-Path $rcRoot 'Source/Detection/ManagedWorkCandidateFilter.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Managed work candidate compilation failed.' }
    if ($PreviousDecision) {
        foreach ($case in @('', '--queue-first')) {
            $ErrorActionPreference = 'Continue'
            $output = & $testOutput $case 2>&1
            $result = $LASTEXITCODE
            $ErrorActionPreference = 'Stop'
            $expected = if ($case) { 'connective wait preserves queued saved Wear' } else { 'targetless preparation creates no origin claim' }
            if ($result -eq 0 -or ($output -join "`n") -notmatch $expected) { throw "Previous decision did not fail expected regression: $expected" }
            Write-Output "Previous production registry fails: $expected (negative control)."
        }
    } else {
        & $testOutput
        if ($LASTEXITCODE -ne 0) { throw 'Managed work candidate checks failed.' }
    }
} finally {
    foreach ($file in @($testOutput,$admissionFile,$oldRegistry)) { if (Test-Path -LiteralPath $file) { Remove-Item -LiteralPath $file } }
}
exit 0
