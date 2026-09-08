param([switch]$PreviousPauseDecision)
$ErrorActionPreference = 'Stop'
$rcRoot = Split-Path $PSScriptRoot -Parent
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
$vsDir = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -property installationPath
$compiler = Join-Path $vsDir 'MSBuild/Current/Bin/Roslyn/csc.exe'
$testDir = Join-Path $env:TEMP ('aom-transition-diagnostics-' + [Guid]::NewGuid().ToString('N'))
$testOutput = Join-Path $testDir 'TransitionActivityDiagnosticsTests.exe'
$harmonyCopy = Join-Path $testDir '0Harmony.dll'
$diagnosticSource = Join-Path $testDir 'Diagnostics.cs'
New-Item -ItemType Directory -Path $testDir | Out-Null
try {
    Copy-Item -LiteralPath 'F:/Steam/steamapps/workshop/content/294100/2009463077/Current/Assemblies/0Harmony.dll' -Destination $harmonyCopy
    $diagnostics = Get-Content (Join-Path $rcRoot 'Source/Detection/TransitionActivityDiagnostics.cs') -Raw
    if ($PreviousPauseDecision) {
        # Previous rejection hooks observed existing watches only.
        $diagnostics = $diagnostics.Replace('Watch watch = Get(pawn, true);' + "`n" + '            if (watch.PausedRule == null)', 'Watch watch = Get(pawn, false); if (watch == null) return;' + "`n" + '            if (watch.PausedRule == null)')
    }
    Set-Content -LiteralPath $diagnosticSource -Value $diagnostics
    & $compiler /nologo /target:exe /langversion:latest /warn:0 "/out:$testOutput" "/reference:$harmonyCopy" (Join-Path $PSScriptRoot 'TransitionActivityDiagnosticsTests.cs') $diagnosticSource (Join-Path $rcRoot 'Source/Patches/TransitionActivityDiagnostics_Patches.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Transition diagnostics compilation failed.' }
    $pref = $ErrorActionPreference
    try { $ErrorActionPreference = 'Continue'; $output = & $testOutput 2>&1; $result = $LASTEXITCODE }
    finally { $ErrorActionPreference = $pref }
    if ($PreviousPauseDecision) {
        if ($result -eq 0 -or ($output -join "`n") -notmatch 'pause denial opens evidence for untracked pawn') { throw "Previous pause diagnostic control failed: $output" }
        'Previous session-only rejection observation fails untracked-pawn regression (negative control).'
    } else {
        $output
        if ($result -ne 0) { throw 'Transition diagnostics checks failed.' }
    }
} finally {
    if (Test-Path -LiteralPath $testOutput) { Remove-Item -LiteralPath $testOutput }
    if (Test-Path -LiteralPath $harmonyCopy) { Remove-Item -LiteralPath $harmonyCopy }
    if (Test-Path -LiteralPath $diagnosticSource) { Remove-Item -LiteralPath $diagnosticSource }
    Remove-Item -LiteralPath $testDir
}
exit 0
