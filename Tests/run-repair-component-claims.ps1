$ErrorActionPreference = 'Stop'
$rcRoot = Split-Path $PSScriptRoot -Parent
$vsDir = & "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" -latest -products '*' -requires Microsoft.Component.MSBuild -property installationPath
$compiler = Join-Path $vsDir 'MSBuild/Current/Bin/Roslyn/csc.exe'
$outDir = Join-Path $env:TEMP ('aom-repair-claims-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $outDir | Out-Null
try {
    $fixture = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'ManagedWorkCandidateTests.cs')
    $start = $fixture.IndexOf('namespace Verse')
    if ($start -lt 0) { throw 'Shared claim test types not found.' }
    $stubs = Join-Path $outDir 'Stubs.cs'
    Set-Content -LiteralPath $stubs -Value ('using System;using System.Collections.Generic;using Verse;using Verse.AI;using AutomaticOutfitManager.State;' + $fixture.Substring($start))
    $exe = Join-Path $outDir 'Tests.exe'
    & $compiler /nologo /target:exe /langversion:latest /warn:0 "/out:$exe" $stubs (Join-Path $PSScriptRoot 'RepairComponentClaimTests.cs') (Join-Path $rcRoot 'Source/Detection/ManagedWorkClaimRegistry.cs') (Join-Path $rcRoot 'Source/Detection/RepairComponentClaims.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Repair component tests failed to compile.' }
    & $exe
    if ($LASTEXITCODE -ne 0) { throw 'Repair component tests failed.' }
} finally {
    foreach ($name in @('Stubs.cs', 'Tests.exe')) {
        $file = Join-Path $outDir $name
        if (Test-Path -LiteralPath $file) { Remove-Item -LiteralPath $file }
    }
    Remove-Item -LiteralPath $outDir
}
