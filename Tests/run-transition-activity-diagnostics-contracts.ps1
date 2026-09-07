$ErrorActionPreference = 'Stop'
$rcRoot = Split-Path $PSScriptRoot -Parent
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
$vsDir = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -property installationPath
$compiler = Join-Path $vsDir 'MSBuild/Current/Bin/Roslyn/csc.exe'
$testDir = Join-Path $env:TEMP ('aom-transition-diagnostics-' + [Guid]::NewGuid().ToString('N'))
$testOutput = Join-Path $testDir 'TransitionActivityDiagnosticsTests.exe'
$harmonyCopy = Join-Path $testDir '0Harmony.dll'
New-Item -ItemType Directory -Path $testDir | Out-Null
try {
    Copy-Item -LiteralPath 'F:/Steam/steamapps/workshop/content/294100/2009463077/Current/Assemblies/0Harmony.dll' -Destination $harmonyCopy
    & $compiler /nologo /target:exe /langversion:latest /warn:0 "/out:$testOutput" "/reference:$harmonyCopy" (Join-Path $PSScriptRoot 'TransitionActivityDiagnosticsTests.cs') (Join-Path $rcRoot 'Source/Detection/TransitionActivityDiagnostics.cs') (Join-Path $rcRoot 'Source/Patches/TransitionActivityDiagnostics_Patches.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Transition diagnostics compilation failed.' }
    & $testOutput
    if ($LASTEXITCODE -ne 0) { throw 'Transition diagnostics checks failed.' }
} finally {
    if (Test-Path -LiteralPath $testOutput) { Remove-Item -LiteralPath $testOutput }
    if (Test-Path -LiteralPath $harmonyCopy) { Remove-Item -LiteralPath $harmonyCopy }
    Remove-Item -LiteralPath $testDir
}
exit 0
