$ErrorActionPreference='Stop'
$env:PSModulePath=$PSHOME+'\Modules;'+$env:PSModulePath
$root=Join-Path $env:LOCALAPPDATA 'CodexDreamSkin'
$engine=Join-Path $root 'engine'
. (Join-Path $engine 'scripts\common-windows.ps1')
. (Join-Path $engine 'scripts\theme-windows.ps1')
$runtime=Join-Path $env:LOCALAPPDATA 'AincradSuite'
$name=([IO.File]::ReadAllText((Join-Path $runtime 'last-theme-backup.txt'))).Trim()
if($name -notmatch '^theme-backup-[a-f0-9]{32}$'){throw 'Invalid theme backup name.'}
$backup=Join-Path $runtime $name
$old=Read-DreamSkinTheme -ThemeDirectory $backup
$lock=Enter-DreamSkinOperationLock -TimeoutMilliseconds 5000
try{
  $path=Join-Path $engine 'assets\dream-skin.css';$css=[IO.File]::ReadAllText($path)
  $pattern='(?s)/\* AINCRAD_UI_V27_BEGIN.*?/\* AINCRAD_UI_V27_END \*/'
  $blocks=[regex]::Matches($css,$pattern)
  if($blocks.Count -gt 1){throw 'Ambiguous UI extension; restore stopped.'}
  if($blocks.Count -eq 1){[IO.File]::WriteAllText($path,$css.Remove($blocks[0].Index,$blocks[0].Length),(New-Object Text.UTF8Encoding($false)))}
  $safe=Join-Path $backup 'theme.css';if(-not(Test-Path -LiteralPath $safe)){$safe=$null}
  $null=Set-DreamSkinActiveTheme -ImagePath $old.ImagePath -Theme $old.Theme -SafeCssPath $safe -StateRoot $root
  $session=Get-DreamSkinLiveSessionContext -StateRoot $root
  if($session){$result=Invoke-DreamSkinNative -FilePath $session.NodePath -ArgumentList @($session.Injector,'--once','--port',"$($session.Port)",'--browser-id',$session.BrowserId,'--theme-dir',(Join-Path $root 'active-theme'),'--timeout-ms','12000');if($result.ExitCode -notin @(0,2)){throw 'Previous theme restored on disk; reopen the desktop app to load it.'}}
  Write-Output 'Previous theme restored. Suite sidebar/frame extension removed.'
}finally{Exit-DreamSkinOperationLock -Mutex $lock}
