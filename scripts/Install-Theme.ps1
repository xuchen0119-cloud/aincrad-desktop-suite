param([switch]$ValidateOnly)
$ErrorActionPreference='Stop'
$env:PSModulePath=$PSHOME+'\Modules;'+$env:PSModulePath
$bundle=Split-Path -Parent $PSScriptRoot
$root=Join-Path $env:LOCALAPPDATA 'CodexDreamSkin'
$engine=Join-Path $root 'engine'
if(-not(Test-Path -LiteralPath (Join-Path $engine 'VERSION'))){throw 'Install Codex Dream Skin from its official GitHub Releases first.'}
$version=([IO.File]::ReadAllText((Join-Path $engine 'VERSION'))).Trim()
. (Join-Path $engine 'scripts\common-windows.ps1')
. (Join-Path $engine 'scripts\theme-windows.ps1')
$themeDir=Join-Path $bundle 'theme'
$theme=Read-DreamSkinTheme -ThemeDirectory $themeDir
Assert-DreamSkinSafeCssFile -Path (Join-Path $themeDir 'theme.css')
if($theme.Theme.id -cne 'local-sao-aincrad-link-start'){throw 'Unexpected theme identity.'}
if($ValidateOnly){Write-Output ('Theme payload valid; engine '+$version);exit 0}
$runtime=Join-Path $env:LOCALAPPDATA 'AincradSuite'
New-Item -ItemType Directory -Path $runtime -Force|Out-Null
$backup=Join-Path $runtime ('theme-backup-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $backup|Out-Null
$lock=Enter-DreamSkinOperationLock -TimeoutMilliseconds 5000
$prior=$null;$cssPath=Join-Path $engine 'assets\dream-skin.css';$engineChanged=$false
try{
  if(Test-Path -LiteralPath (Join-Path $root 'active-theme\theme.json')){
    $prior=Read-DreamSkinTheme -ThemeDirectory (Join-Path $root 'active-theme')
    foreach($name in @('theme.json','theme.css',[IO.Path]::GetFileName($prior.ImagePath))){
      $path=Join-Path $prior.Directory $name
      if(Test-Path -LiteralPath $path){Copy-Item -LiteralPath $path -Destination (Join-Path $backup $name)}
    }
  }
  $import=Import-DreamSkinThemeZip -ArchivePath (Join-Path $bundle 'Aincrad-Theme-v2.8.zip') -StateRoot $root
  $null=Use-DreamSkinSavedTheme -ThemeDirectory $import.Path -StateRoot $root
  # The optional native-shell extension is tested only against this engine version.
  # Keep unsupported versions on the Safe CSS theme rather than guessing internals.
  if($version -ceq '1.5.20'){
    $base=[IO.File]::ReadAllText($cssPath)
    Copy-Item -LiteralPath $cssPath -Destination (Join-Path $backup 'engine.css')
    $pattern='(?s)/\* AINCRAD_UI_V27_BEGIN.*?/\* AINCRAD_UI_V27_END \*/'
    $blocks=[regex]::Matches($base,$pattern)
    if($blocks.Count -gt 1){throw 'Ambiguous previous suite UI extension.'}
    if($blocks.Count -eq 1){$base=$base.Remove($blocks[0].Index,$blocks[0].Length)}
    $extension=[IO.File]::ReadAllText((Join-Path $bundle 'theme-extension\aincrad-ui.css'))
    [IO.File]::WriteAllText($cssPath,$base+[Environment]::NewLine+$extension,(New-Object Text.UTF8Encoding($false)))
    $engineChanged=$true
  }
  $session=Get-DreamSkinLiveSessionContext -StateRoot $root
  if($session){
    $apply=Invoke-DreamSkinNative -FilePath $session.NodePath -ArgumentList @($session.Injector,'--once','--port',"$($session.Port)",'--browser-id',$session.BrowserId,'--theme-dir',(Join-Path $root 'active-theme'),'--timeout-ms','12000')
    if($apply.ExitCode -notin @(0,2)){throw 'Theme saved but the managed window did not reload successfully.'}
  }
  [IO.File]::WriteAllText((Join-Path $runtime 'last-theme-backup.txt'),[IO.Path]::GetFileName($backup))
  Write-Output 'Theme installed. Use the opening launcher to start the managed desktop app.'
  if($version -cne '1.5.20'){Write-Output 'Native sidebar/frame extension was skipped on this untested engine version.'}
}catch{
  if($engineChanged){Copy-Item -LiteralPath (Join-Path $backup 'engine.css') -Destination $cssPath -Force}
  if($prior){$old=Read-DreamSkinTheme -ThemeDirectory $backup;$safe=Join-Path $backup 'theme.css';if(-not(Test-Path -LiteralPath $safe)){$safe=$null};$null=Set-DreamSkinActiveTheme -ImagePath $old.ImagePath -Theme $old.Theme -SafeCssPath $safe -StateRoot $root}
  throw
}finally{Exit-DreamSkinOperationLock -Mutex $lock}
