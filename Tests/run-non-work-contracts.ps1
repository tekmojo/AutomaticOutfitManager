param([switch]$VerifyFailureReporting)

$ErrorActionPreference = 'Stop'
$rcRoot = Split-Path $PSScriptRoot -Parent
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$vsDir = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -property installationPath
$compiler = Join-Path $vsDir 'MSBuild\Current\Bin\Roslyn\csc.exe'
$testOutput = Join-Path $env:TEMP ('aom-non-work-tests-' + [Guid]::NewGuid().ToString('N') + '.exe')
$sources = @(
    (Join-Path $PSScriptRoot 'NonWorkOutfitContractTests.cs'),
    (Join-Path $rcRoot 'Source\Rules\ApparelRule.cs'),
    (Join-Path $rcRoot 'Source\State\SavedNonWorkOutfit.cs'),
    (Join-Path $rcRoot 'Source\State\NonWorkOutfitBuffer.cs'),
    (Join-Path $rcRoot 'Source\Detection\RuleEvaluator.cs'),
    (Join-Path $rcRoot 'Source\Detection\NonWorkFallbackPolicy.cs'),
    (Join-Path $rcRoot 'Source\Detection\GearSelectionPolicy.cs'),
    (Join-Path $rcRoot 'Source\Detection\WorkGearSnapshotPolicy.cs'),
    (Join-Path $rcRoot 'Source\Detection\RepairMaterialStage.cs'),
    (Join-Path $rcRoot 'Source\Detection\GearRetrievalRoute.cs')
)
try {
    & $compiler /nologo /target:exe /langversion:latest /warn:0 "/out:$testOutput" $sources
    if ($LASTEXITCODE -ne 0) { throw 'Contract test compilation failed.' }
    & $testOutput
    if ($LASTEXITCODE -ne 0) { throw 'Contract checks failed.' }
    if ($VerifyFailureReporting) {
        $failureOutput = & $testOutput --verify-failure-reporting 2>&1
        $failureExitCode = $LASTEXITCODE
        if ($failureExitCode -ne 1 -or
            ($failureOutput -join "`n") -notmatch 'FAIL System.InvalidOperationException: Intentional failure-reporting check') {
            throw 'Failure-reporting verification did not return the expected diagnostic and exit code.'
        }
        Write-Host 'PASS intentional failure reports its diagnostic and exits with code 1 without an unhandled exception.'
    }
} finally {
    if (Test-Path -LiteralPath $testOutput) { Remove-Item -LiteralPath $testOutput }
}

exit 0
