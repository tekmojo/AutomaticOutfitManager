param([switch]$PreviousDecision)
$ErrorActionPreference='Stop'
$rcRoot=Split-Path $PSScriptRoot -Parent
$vswhere=Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
$vsDir=& $vswhere -latest -products * -requires Microsoft.Component.MSBuild -property installationPath
$compiler=Join-Path $vsDir 'MSBuild/Current/Bin/Roslyn/csc.exe'
$stem=Join-Path $env:TEMP ('aom-pause-cooldown-'+[Guid]::NewGuid().ToString('N'))
$generated=$stem+'.cs';$registryFile=$stem+'-registry.cs';$exe=$stem+'.exe'
try {
 $ui=Get-Content (Join-Path $rcRoot 'Source/UI/MainRulesWindow.cs') -Raw
 $start=$ui.IndexOf('private static void ToggleWorkPause(');if($start -lt 0){throw 'Missing pause toggle'}
 $end=$ui.IndexOf('{',$start)+1;$depth=1
 while($depth -gt 0){if($ui[$end] -eq '{'){$depth++};if($ui[$end] -eq '}'){$depth--};$end++}
 $toggle=$ui.Substring($start,$end-$start)
 $registry=Get-Content (Join-Path $rcRoot 'Source/Detection/UnavailableWorkRegistry.cs') -Raw
 if($PreviousDecision){$registry=$registry.Replace('1200, rule?.WorkAreaPaused == true','1200, false')}
 Set-Content -LiteralPath $registryFile -Value $registry
 Set-Content -LiteralPath $generated -Value ('using System.Linq;using System.Collections.Generic;using AutomaticOutfitManager.Core;using AutomaticOutfitManager.Detection;using AutomaticOutfitManager.Rules;namespace AutomaticOutfitManager.UI { public static partial class MainRulesWindow {'+$toggle+'}}')
 & $compiler /nologo /target:exe /langversion:latest /warn:0 "/out:$exe" $generated $registryFile (Join-Path $PSScriptRoot 'PauseCooldownTests.cs')
 if($LASTEXITCODE -ne 0){throw 'Pause cooldown contract compilation failed'}
 $pref=$ErrorActionPreference
 try{$ErrorActionPreference='Continue';$output=& $exe 2>&1;$result=$LASTEXITCODE}finally{$ErrorActionPreference=$pref}
 if($PreviousDecision){
  if($result -eq 0 -or ($output -join "`n") -notmatch 'resume clears prior pause entries before rapid repause'){throw "Negative control did not reproduce stale pause cooldown: $output"}
  'Previous unclassified cooldown fails the Resume regression (negative control).'
 }else{$output;if($result -ne 0){throw 'Pause cooldown contracts failed'}}
} finally {foreach($file in @($generated,$registryFile,$exe)){if(Test-Path -LiteralPath $file){Remove-Item -LiteralPath $file}}}
exit 0
