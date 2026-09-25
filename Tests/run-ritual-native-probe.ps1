param([switch]$TestMissingDependencies)
$ErrorActionPreference='Stop'
$rcRoot=Split-Path $PSScriptRoot -Parent
$vswhere=Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
$vsDir=& $vswhere -latest -products * -requires Microsoft.Component.MSBuild -property installationPath
$compiler=Join-Path $vsDir 'MSBuild/Current/Bin/Roslyn/csc.exe'
$managed='F:/Steam/steamapps/common/RimWorld/RimWorldWin64_Data/Managed'
$harmonyDir='F:/Steam/steamapps/workshop/content/294100/2009463077/Current/Assemblies'
$testDir=Join-Path $env:TEMP ('aom-native-ritual-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testDir | Out-Null
try {
 $exe=Join-Path $testDir 'Probe.exe'
 Copy-Item (Join-Path $harmonyDir '0Harmony.dll') (Join-Path $testDir '0Harmony.dll')
 & $compiler /nologo /target:exe /langversion:latest /warn:0 "/out:$exe" "/reference:$managed/Assembly-CSharp.dll" "/reference:$managed/UnityEngine.CoreModule.dll" "/reference:$managed/netstandard.dll" "/reference:$harmonyDir/0Harmony.dll" (Join-Path $PSScriptRoot 'RitualNativeProbe.cs')
 if($LASTEXITCODE -ne 0){throw 'Native ritual probe compilation failed'}
 if($TestMissingDependencies) {
  # This directory deliberately has no game assembly. Startup must fail inside
  # the bootstrap catch, not through an unhandled CLR exception/Windows dialog.
  $ErrorActionPreference='Continue'
  $output=& $exe $testDir 2>&1
  $result=$LASTEXITCODE
  $ErrorActionPreference='Stop'
  if($result -ne 1 -or ($output -join "`n") -notmatch 'Native ritual probe failed: System.IO.FileNotFoundException') {
   throw "Missing-dependency startup was not handled: $output"
  }
  'PASS: missing game assembly returns a caught diagnostic and exit code 1.'
 } else {
  & $exe $managed (Join-Path $rcRoot '1.6/Assemblies') $harmonyDir
  if($LASTEXITCODE -ne 0){throw 'Native ritual probe failed'}
 }
} finally {
 foreach($name in @('Probe.exe','0Harmony.dll')) { $p=Join-Path $testDir $name;if(Test-Path -LiteralPath $p){Remove-Item -LiteralPath $p} }
 Remove-Item -LiteralPath $testDir
}
exit 0
