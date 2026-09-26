param([switch]$PreviousDecision)
$ErrorActionPreference = 'Stop'
$rcRoot = Split-Path $PSScriptRoot -Parent
$vsDir = & "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" -latest -products '*' -requires Microsoft.Component.MSBuild -property installationPath
$compiler = Join-Path $vsDir 'MSBuild/Current/Bin/Roslyn/csc.exe'
$managed = 'F:/Steam/steamapps/common/RimWorld/RimWorldWin64_Data/Managed'
$harmonyDir = 'F:/Steam/steamapps/workshop/content/294100/2009463077/Current/Assemblies'
$outDir = Join-Path $env:TEMP ('aom-repair-native-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $outDir | Out-Null
try {
    Copy-Item -LiteralPath (Join-Path $harmonyDir '0Harmony.dll') -Destination (Join-Path $outDir '0Harmony.dll')
    $exe = Join-Path $outDir 'Probe.exe'
    & $compiler /nologo /target:exe /langversion:latest /warn:0 "/out:$exe" "/reference:$managed/Assembly-CSharp.dll" "/reference:$managed/UnityEngine.CoreModule.dll" "/reference:$managed/netstandard.dll" "/reference:$harmonyDir/0Harmony.dll" (Join-Path $PSScriptRoot 'RepairComponentNativeProbe.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Native repair probe compilation failed.' }
    $probeArgs = @($managed, (Join-Path $rcRoot '1.6/Assemblies'), $harmonyDir)
    if ($PreviousDecision) { $probeArgs += '--previous' }
    $ErrorActionPreference = 'Continue'
    $output = & $exe @probeArgs 2>&1
    $result = $LASTEXITCODE
    $ErrorActionPreference = 'Stop'
    $output | ForEach-Object { Write-Output "$_" }
    if ($PreviousDecision) {
        if ($result -eq 0 -or ($output -join "`n") -notmatch 'claimed-only repair is rejected before native target acceptance') {
            throw 'Previous search did not fail the expected acceptance regression.'
        }
        Write-Output 'PASS negative control: previous component search accepts the claimed-only repair.'
    } elseif ($result -ne 0) { throw 'Native repair probe failed.' }
} finally {
    foreach ($name in @('Probe.exe', '0Harmony.dll')) {
        $file = Join-Path $outDir $name
        if (Test-Path -LiteralPath $file) { Remove-Item -LiteralPath $file }
    }
    Remove-Item -LiteralPath $outDir
}
