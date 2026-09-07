$ErrorActionPreference = 'Stop'
$rcRoot = Split-Path $PSScriptRoot -Parent
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
$vsDir = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -property installationPath
$compiler = Join-Path $vsDir 'MSBuild/Current/Bin/Roslyn/csc.exe'
$stem = Join-Path $env:TEMP ('aom-weapon-preparation-' + [Guid]::NewGuid().ToString('N'))
$testOutput = $stem + '.exe'
$policyFile = $stem + '.cs'
function Slice-Production($path, $start, $end) {
    $source = Get-Content -LiteralPath (Join-Path $rcRoot $path) -Raw
    $a = $source.IndexOf($start)
    $b = $source.IndexOf($end, $a)
    if ($a -lt 0 -or $b -le $a) { throw "Production method not found: $start" }
    return $source.Substring($a, $b - $a)
}
try {
    $prefix = @'
using System;using System.Linq;using System.Collections.Generic;using AutomaticOutfitManager.Core;using AutomaticOutfitManager.Detection;using AutomaticOutfitManager.State;using AutomaticOutfitManager.Rules;using AutomaticOutfitManager.Patches;using RimWorld;using Verse;using Verse.AI;
'@
    $state = Slice-Production 'Source/State/PawnApparelState.cs' '        public void BeginManagedWeapon(' '        public void ClearPendingBufferedTask('
    $restore = Slice-Production 'Source/State/PawnApparelState.cs' '        public void CompleteWeaponRestoration(' '        public void MarkWeaponPlayerOverride('
    $finder = Slice-Production 'Source/Detection/WeaponFinder.cs' '        private static ThingWithComps FindClosest(' '        private static ThingDef PreferredExactDefinition('
    $abort = Slice-Production 'Source/Patches/PawnJobTracker_StartJob_Patch.cs' '        private static bool TryAbortExhaustedWeaponPreparation(' '        private static bool HasManagedWorkContext('
    $status = Slice-Production 'Source/UI/PawnAutomaticOutfitStatus.cs' '        private static bool IsManagedWorkStatusJob(' '        private static string BufferStatus('
    $essential = Slice-Production 'Source/Patches/WorkGiverPausedArea_Patches.cs' '        public static bool IsEssentialPersonalJob(' '        public static bool IsHaulingJob('
    $body = $prefix + "`nnamespace AutomaticOutfitManager.State { public partial class PawnApparelState {" + $state + $restore + '}}' +
        "`nnamespace AutomaticOutfitManager.Detection { public partial class WeaponFinder {" + $finder.Replace('private static','public static') + '}}' +
        "`nnamespace AutomaticOutfitManager.Patches { public partial class PawnJobTracker_StartJob_Patch {" + $abort.Replace('private static','public static') + '}' +
        'public partial class PausedAreaWorkFilter {' + $essential + '}}' +
        "`nnamespace AutomaticOutfitManager.UI { public partial class PawnAutomaticOutfitStatus {" + $status.Replace('private static','public static') + '}}'
    Set-Content -LiteralPath $policyFile -Value $body -Encoding UTF8
    & $compiler /nologo /target:exe /langversion:latest /warn:0 "/out:$testOutput" $policyFile (Join-Path $PSScriptRoot 'WeaponPreparationContractTests.cs') (Join-Path $rcRoot 'Source/Detection/WeaponPreparationRetryRegistry.cs') (Join-Path $rcRoot 'Source/Detection/WeaponPreparationDiagnostics.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Weapon preparation compilation failed.' }
    & $testOutput
    if ($LASTEXITCODE -ne 0) { throw 'Weapon preparation contracts failed.' }
} finally {
    foreach ($file in @($testOutput,$policyFile)) { if (Test-Path -LiteralPath $file) { Remove-Item -LiteralPath $file } }
}
exit 0
