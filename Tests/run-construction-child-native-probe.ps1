$ErrorActionPreference='Stop'
$rcRoot=Split-Path $PSScriptRoot -Parent
$vsDir=& "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" -latest -products '*' -requires Microsoft.Component.MSBuild -property installationPath
$compiler=Join-Path $vsDir 'MSBuild/Current/Bin/Roslyn/csc.exe'
$managed='F:/Steam/steamapps/common/RimWorld/RimWorldWin64_Data/Managed'
$harmonyDir='F:/Steam/steamapps/workshop/content/294100/2009463077/Current/Assemblies'
$testDir=Join-Path $env:TEMP ('aom-child-native-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testDir | Out-Null
try {
 $exe=Join-Path $testDir 'Probe.exe'
 Copy-Item -LiteralPath (Join-Path $harmonyDir '0Harmony.dll') -Destination (Join-Path $testDir '0Harmony.dll')
 & $compiler /nologo /target:exe /langversion:latest /warn:0 "/out:$exe" "/reference:$managed/Assembly-CSharp.dll" "/reference:$managed/UnityEngine.CoreModule.dll" "/reference:$managed/netstandard.dll" "/reference:$harmonyDir/0Harmony.dll" (Join-Path $PSScriptRoot 'ConstructionChildNativeProbe.cs')
 if($LASTEXITCODE -ne 0){throw 'Native construction probe compilation failed'}
 & $exe $managed (Join-Path $rcRoot '1.6/Assemblies') $harmonyDir
 if($LASTEXITCODE -ne 0){throw 'Native construction probe failed'}
}finally{
 foreach($name in @('Probe.exe','0Harmony.dll')){ $file=Join-Path $testDir $name;if(Test-Path -LiteralPath $file){Remove-Item -LiteralPath $file} }
 Remove-Item -LiteralPath $testDir
}
exit 0
