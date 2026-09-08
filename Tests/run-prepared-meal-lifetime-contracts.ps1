param([switch]$PreviousDecision)
$ErrorActionPreference = 'Stop'
$rcRoot = Split-Path $PSScriptRoot -Parent
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
$vsDir = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -property installationPath
$compiler = Join-Path $vsDir 'MSBuild/Current/Bin/Roslyn/csc.exe'
$testDir = Join-Path $env:TEMP ('aom-prepared-meal-' + [Guid]::NewGuid().ToString('N'))
$testOutput = Join-Path $testDir 'PreparedMealLifetimeTests.exe'
$harmonyCopy = Join-Path $testDir '0Harmony.dll'
$boundaryFile = Join-Path $testDir 'RejectionBoundaries.cs'
$registryFile = Join-Path $testDir 'PreparedIngestRetryRegistry.cs'
New-Item -ItemType Directory -Path $testDir | Out-Null
try {
    function Get-Block([string]$text, [string]$signature) {
        $start = $text.IndexOf($signature)
        if ($start -lt 0) { throw "Missing production boundary: $signature" }
        $end = $text.IndexOf('{', $start) + 1; $depth = 1
        while ($depth -gt 0) {
            if ($text[$end] -eq '{') { $depth++ }
            if ($text[$end] -eq '}') { $depth-- }
            $end++
        }
        return $text.Substring($start, $end - $start)
    }
    $source = Get-Content -LiteralPath (Join-Path $rcRoot 'Source/Patches/PawnJobTracker_StartJob_Patch.cs') -Raw
    $claim = Get-Block $source 'if (!assignedSavedRestorationJob &&'
    $reservation = Get-Block $source 'if (IngestReservationAdmission.Rejects(pawn, newJob))'
    $generated = 'using AutomaticOutfitManager.Core;using AutomaticOutfitManager.Detection;using Verse;using Verse.AI;namespace AutomaticOutfitManager.Patches {public static partial class PawnJobTracker_StartJob_Patch {'
    $generated += 'internal static void CheckClaim(Pawn_JobTracker __instance, ref Job newJob, ref ThinkNode jobGiver, ref ThinkTreeDef thinkTree, ref JobTag? tag) {var pawn=__instance.PawnForTests;bool assignedSavedRestorationJob=false;' + $claim + '}'
    $generated += 'internal static void CheckReservation(Pawn_JobTracker __instance, ref Job newJob, ref ThinkNode jobGiver, ref ThinkTreeDef thinkTree, ref JobTag? tag) {var pawn=__instance.PawnForTests;' + $reservation + '}}}'
    Set-Content -LiteralPath $boundaryFile -Value $generated
    $registry = Get-Content -LiteralPath (Join-Path $rcRoot 'Source/Detection/PreparedIngestRetryRegistry.cs') -Raw
    if ($PreviousDecision) {
        # Restore the two previous decisions in a temporary compiled copy:
        # a rejected retry is retained and unrelated hauling has no ownership gate.
        $reject = Get-Block $registry 'internal static void RejectAdmission('
        $registry = $registry.Replace($reject, 'internal static void RejectAdmission(Pawn pawn, Job rejectedJob) { }')
        $old = 'pending.RetryIssued && !HasLiveRetryOwner(pawn, pending, currentJob)'
        if (!$registry.Contains($old)) { throw 'Missing orphan gate for negative control' }
        $registry = $registry.Replace($old, 'false')
    }
    Set-Content -LiteralPath $registryFile -Value $registry
    Copy-Item -LiteralPath 'F:/Steam/steamapps/workshop/content/294100/2009463077/Current/Assemblies/0Harmony.dll' -Destination $harmonyCopy
    & $compiler /nologo /target:exe /langversion:latest /warn:0 "/out:$testOutput" "/reference:$harmonyCopy" (Join-Path $PSScriptRoot 'PreparedMealLifetimeTests.cs') $registryFile $boundaryFile (Join-Path $rcRoot 'Source/Patches/PreparationJobHandoff.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Prepared meal lifetime compilation failed.' }
    $preference = $ErrorActionPreference
    try { $ErrorActionPreference = 'Continue'; $output = & $testOutput 2>&1; $result = $LASTEXITCODE }
    finally { $ErrorActionPreference = $preference }
    if ($PreviousDecision) {
        if ($result -eq 0 -or ($output -join "`n") -notmatch 'claim: rejected meal must not strand queued cooking') {
            throw "Previous decision did not reproduce the diagnosed cooking stall: $output"
        }
        'Previous rejection/guard decisions strand queued cooking (negative control).'
    } else {
        $output
        if ($result -ne 0) { throw 'Prepared meal lifetime checks failed.' }
    }
} finally {
    foreach ($file in @($testOutput, $harmonyCopy, $boundaryFile, $registryFile)) {
        if (Test-Path -LiteralPath $file) { Remove-Item -LiteralPath $file }
    }
    Remove-Item -LiteralPath $testDir
}
