$ErrorActionPreference = 'Stop'
$rcRoot = Split-Path $PSScriptRoot -Parent
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$vsDir = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -property installationPath
$compiler = Join-Path $vsDir 'MSBuild\Current\Bin\Roslyn\csc.exe'
$testOutput = Join-Path $env:TEMP ('aom-storage-tests-' + [Guid]::NewGuid().ToString('N') + '.exe')
$sources = @('Tests/StorageFilterContractTests.cs', 'Source/Storage/AutomaticOutfitStorageScope.cs', 'Source/Storage/ManagedApparelClassifier.cs', 'Source/Storage/ManagedWeaponClassifier.cs', 'Source/Storage/SpecialThingFilterWorkers.cs', 'Source/Patches/ThingFilter_SetAllow_Patch.cs') | ForEach-Object { Join-Path $rcRoot $_ }
try {
    & $compiler /nologo /target:exe /langversion:latest /warn:0 "/out:$testOutput" $sources
    if ($LASTEXITCODE -ne 0) { throw 'Storage test compilation failed.' }
    & $testOutput
    if ($LASTEXITCODE -ne 0) { throw 'Storage checks failed.' }
} finally {
    if (Test-Path -LiteralPath $testOutput) { Remove-Item -LiteralPath $testOutput }
}
exit 0
