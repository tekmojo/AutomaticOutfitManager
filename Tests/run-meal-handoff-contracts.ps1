$ErrorActionPreference = 'Stop'
$rcRoot = Split-Path $PSScriptRoot -Parent
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$vsDir = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -property installationPath
$compiler = Join-Path $vsDir 'MSBuild\Current\Bin\Roslyn\csc.exe'
$testOutput = Join-Path $env:TEMP ('aom-meal-handoff-tests-' + [Guid]::NewGuid().ToString('N') + '.exe')
try {
    & $compiler /nologo /target:exe /langversion:latest /warn:0 "/out:$testOutput" (Join-Path $PSScriptRoot 'MealHandoffContractTests.cs') (Join-Path $PSScriptRoot 'ChildcareContractCases.cs') (Join-Path $rcRoot 'Source/State/NonWorkMealTrip.cs') (Join-Path $rcRoot 'Source/Patches/NonWorkMealHandoff.cs') (Join-Path $rcRoot 'Source/Patches/AccessExitJobs.cs') (Join-Path $rcRoot 'Source/State/NonWorkOutfitBuffer.cs') (Join-Path $rcRoot 'Source/Patches/NonWorkBufferTracker.cs') (Join-Path $rcRoot 'Source/Patches/ChildcareContinuation.cs') (Join-Path $rcRoot 'Source/Patches/BufferedTransitGuard.cs') (Join-Path $rcRoot 'Source/UI/RuleBufferProgress.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Meal handoff contract test compilation failed.' }
    & $testOutput
    if ($LASTEXITCODE -ne 0) { throw 'Meal handoff controller checks failed.' }
} finally {
    if (Test-Path -LiteralPath $testOutput) { Remove-Item -LiteralPath $testOutput }
}
exit 0
