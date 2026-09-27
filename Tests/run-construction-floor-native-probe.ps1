param([switch]$PreviousDecision, [switch]$PreviousRefreshDecision)
$ErrorActionPreference = 'Stop'
$rcRoot = Split-Path $PSScriptRoot -Parent
$vsDir = & "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" -latest -products '*' -requires Microsoft.Component.MSBuild -property installationPath
$compiler = Join-Path $vsDir 'MSBuild/Current/Bin/Roslyn/csc.exe'
$managed = 'F:/Steam/steamapps/common/RimWorld/RimWorldWin64_Data/Managed'
$harmonyDir = 'F:/Steam/steamapps/workshop/content/294100/2009463077/Current/Assemblies'
$testDir = Join-Path $env:TEMP ('aom-floor-native-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testDir | Out-Null
try {
    Copy-Item -LiteralPath (Join-Path $harmonyDir '0Harmony.dll') -Destination (Join-Path $testDir '0Harmony.dll')
    $exe=Join-Path $testDir 'Probe.exe'
    & $compiler /nologo /target:exe /langversion:latest /warn:0 "/out:$exe" "/reference:$managed/Assembly-CSharp.dll" "/reference:$managed/UnityEngine.CoreModule.dll" "/reference:$managed/netstandard.dll" "/reference:$harmonyDir/0Harmony.dll" (Join-Path $PSScriptRoot 'ConstructionFloorNativeProbe.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Floor selection native probe compilation failed.' }
    $probeArgs=@($managed,(Join-Path $rcRoot '1.6/Assemblies'),$harmonyDir)
    if ($PreviousDecision) { $probeArgs+='--previous' }
    if ($PreviousRefreshDecision) { $probeArgs+='--previous-refresh' }
    $output=& $exe @probeArgs 2>&1
    $result=$LASTEXITCODE
    if ($PreviousRefreshDecision) {
        if ($result -ne 1 -or ($output -join "`n") -notmatch 'blueprint floor continuation must survive outfit preparation') { throw ('Unexpected refresh negative control result: '+($output -join "`n")) }
        Write-Output 'PASS: previous cell query rejects blueprint floor continuation (negative control).'
    } elseif ($PreviousDecision) {
        if ($result -ne 1 -or ($output -join "`n") -notmatch 'claimed floor must be rejected during native eligibility') { throw ('Unexpected negative control result: '+($output -join "`n")) }
        Write-Output 'PASS: previous scanner promises a claimed floor job (negative control).'
    } else {
        $output | Write-Output
        if ($result -ne 0) { throw 'Floor selection native checks failed.' }
    }
} finally {
    foreach ($name in @('Probe.exe','0Harmony.dll')) { $file=Join-Path $testDir $name; if (Test-Path -LiteralPath $file) { Remove-Item -LiteralPath $file } }
    Remove-Item -LiteralPath $testDir
}
