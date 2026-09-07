$ErrorActionPreference = 'Stop'
$rcRoot = Split-Path $PSScriptRoot -Parent
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
$vsDir = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -property installationPath
$compiler = Join-Path $vsDir 'MSBuild/Current/Bin/Roslyn/csc.exe'
$testOutput = Join-Path $env:TEMP ('aom-work-candidate-' + [Guid]::NewGuid().ToString('N') + '.exe')
try {
    & $compiler /nologo /target:exe /langversion:latest /warn:0 "/out:$testOutput" (Join-Path $PSScriptRoot 'ManagedWorkCandidateTests.cs') (Join-Path $rcRoot 'Source/Detection/ManagedWorkClaimRegistry.cs') (Join-Path $rcRoot 'Source/Detection/ManagedWorkCandidateFilter.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Managed work candidate compilation failed.' }
    & $testOutput
    if ($LASTEXITCODE -ne 0) { throw 'Managed work candidate checks failed.' }
} finally {
    if (Test-Path -LiteralPath $testOutput) { Remove-Item -LiteralPath $testOutput }
}
