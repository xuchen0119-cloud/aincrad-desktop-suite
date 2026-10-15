param([switch]$RestartExisting,[switch]$CheckOnly,[string]$StatusPath)
$ErrorActionPreference='Stop'
$env:PSModulePath=$PSHOME+'\Modules;'+$env:PSModulePath
$root=Join-Path $env:LOCALAPPDATA 'CodexDreamSkin'
$engine=Join-Path $root 'engine'
$runtime=Join-Path $env:LOCALAPPDATA 'AincradSuite\runtime'
$legacyStatusPath=Join-Path $runtime 'skin-start-status.json'
if($StatusPath){
  $fullStatusPath=[IO.Path]::GetFullPath($StatusPath)
  if([IO.Path]::GetDirectoryName($fullStatusPath) -ine [IO.Path]::GetFullPath($runtime) -or [IO.Path]::GetFileName($fullStatusPath) -notmatch '^launch-[a-f0-9]{32}\.json$'){throw 'Invalid launch status path.'}
  $StatusPath=$fullStatusPath
}else{$StatusPath=$legacyStatusPath}
$logPath=Join-Path $runtime 'skin-start.log'
$verifyPath=Join-Path $runtime 'skin-verify.json'
$clock=[Diagnostics.Stopwatch]::StartNew()
$lock=$null
$mode='cold'
$failureHint='启动未完成，请查看本机诊断。'
function Status([string]$Phase,[string]$Detail) {
  $payload=[ordered]@{phase=$Phase;detail=$Detail;mode=$mode;seconds=[Math]::Round($clock.Elapsed.TotalSeconds,3);helperPid=$PID;updatedUtc=[DateTime]::UtcNow.ToString('o')} | ConvertTo-Json
  [IO.File]::WriteAllText($StatusPath,$payload,(New-Object Text.UTF8Encoding($false)))
  if($StatusPath -ine $legacyStatusPath){[IO.File]::WriteAllText($legacyStatusPath,$payload,(New-Object Text.UTF8Encoding($false)))}
}
function Mark([string]$Message) {
  [IO.File]::AppendAllText($logPath,([Math]::Round($clock.Elapsed.TotalSeconds,3).ToString()+'s '+$Message+[Environment]::NewLine),(New-Object Text.UTF8Encoding($false)))
}
try {
  $failureHint='未找到完整的 Dream Skin。请返回整合入口，先从官方项目安装。'
  foreach($file in @('VERSION','scripts\common-windows.ps1','scripts\theme-windows.ps1','scripts\start-dream-skin.ps1','scripts\injector.mjs')){
    if(-not(Test-Path -LiteralPath (Join-Path $engine $file))){throw ('Missing engine component: '+$file)}
  }
  $failureHint='未找到已安装的艾恩葛朗特主题。请返回整合入口，点击安装 / 更新皮肤。'
  foreach($file in @('theme.json','theme.css')){
    if(-not(Test-Path -LiteralPath (Join-Path $root ('themes\local-sao-aincrad-link-start\'+$file)))){throw ('Missing saved theme component: '+$file)}
  }
  $savedDirectory=Join-Path $root 'themes\local-sao-aincrad-link-start'
  $savedTheme=Get-Content -Raw -LiteralPath (Join-Path $savedDirectory 'theme.json') -Encoding UTF8|ConvertFrom-Json
  $savedImage=[string]$savedTheme.image
  if($savedTheme.id -cne 'local-sao-aincrad-link-start' -or -not $savedImage -or [IO.Path]::GetFileName($savedImage) -cne $savedImage -or -not(Test-Path -LiteralPath (Join-Path $savedDirectory $savedImage))){throw 'Saved theme identity or artwork is invalid.'}
  if($CheckOnly){[ordered]@{ok=$true;engineVersion=([IO.File]::ReadAllText((Join-Path $engine 'VERSION'))).Trim();engineFilesPresent=$true;savedThemePresent=$true;settingsChanged=$false;processesStarted=0}|ConvertTo-Json;exit 0}
  New-Item -ItemType Directory -Path $runtime -Force | Out-Null
  Status 'starting' 'Opening the managed skin profile.'
  $failureHint='换肤工具接口与当前包不兼容。请查看安装说明中的支持版本。'
  . (Join-Path $engine 'scripts\common-windows.ps1')
  . (Join-Path $engine 'scripts\theme-windows.ps1')
  . (Join-Path $PSScriptRoot 'Aincrad-BootState.ps1')
  foreach($command in @('Enter-DreamSkinOperationLock','Get-DreamSkinVerifiedCdpIdentityForAnyRegistered','Get-DreamSkinNodeRuntime','Start-DreamSkinCodexDirect','Invoke-DreamSkinNative')){
    if(-not(Get-Command -Name $command -ErrorAction SilentlyContinue)){throw ('Missing engine API: '+$command)}
  }
  Mark 'Runtime loaded'
  $failureHint='已有换肤操作正在执行。请等待它完成后再试。'
  $lock=Enter-DreamSkinOperationLock -TimeoutMilliseconds 1500
  $failureHint='无法确认当前应用会话。请正常关闭受管应用，再从整合入口启动。'
  $state=Read-DreamSkinState -Path (Join-Path $root 'state.json')
  $staleArchive=Repair-AincradExpiredBootState -StateRoot $root -State $state
  if($staleArchive){Mark ('Archived previous-boot state: '+$staleArchive);$state=$null}
  $port=if($state -and $state.port){[int]$state.port}else{9335}
  $profile=[IO.Path]::GetFullPath((Join-Path $root 'cdp-profile'))
  $session=Get-DreamSkinVerifiedCdpIdentityForAnyRegistered -Port $port
  Mark 'Verified session discovery complete'
  if($session) {
    if($state -and $state.profilePath -and -not(Test-DreamSkinPathEqual -Left $state.profilePath -Right $profile)) {throw 'The recorded managed profile differs; no other profile was opened.'}
    $mode='reuse'
    $node=Get-DreamSkinNodeRuntime
    Mark 'Signed Node runtime validated'
    $null=Start-DreamSkinCodexDirect -Codex $session.Codex -Arguments @(('--user-data-dir='+$profile),('--remote-debugging-port='+$port))
    Mark 'Managed window requested'
    $watcher=if($state -and $state.injectorPid){Get-CimInstance Win32_Process -Filter ("ProcessId = "+[int]$state.injectorPid) -ErrorAction SilentlyContinue}else{$null}
    $active=Read-DreamSkinTheme -ThemeDirectory (Join-Path $root 'active-theme')
    $watcherValid=$watcher -and (Test-DreamSkinPathEqual -Left $watcher.ExecutablePath -Right $node.Path) -and
      $watcher.CommandLine.Contains('--watch') -and $watcher.CommandLine.Contains($session.Identity.BrowserId) -and
      $state.browserId -ceq $session.Identity.BrowserId -and
      (Get-DreamSkinProcessStartedAt -ProcessId ([int]$state.injectorPid)) -ceq $state.injectorStartedAt
    if($watcherValid -and $active.Theme.id -ceq 'local-sao-aincrad-link-start') {
      Status 'verifying' 'Checking the visible managed window and theme.'
      $verify=Invoke-DreamSkinNative -FilePath $node.Path -ArgumentList @((Join-Path $engine 'scripts\injector.mjs'),'--verify','--port',"$port",'--browser-id',$session.Identity.BrowserId,'--theme-dir',(Join-Path $root 'active-theme'),'--timeout-ms','5000')
      ($verify.Output -join "`r`n") | Set-Content -LiteralPath $verifyPath -Encoding utf8
      Mark ('Warm verification exit='+$verify.ExitCode)
      if($verify.ExitCode -eq 0){Status 'verified' 'Existing managed window has a visible verified SAO skin.';exit 0}
    }
  }
  Exit-DreamSkinOperationLock -Mutex $lock
  $lock=$null
  $mode='initialize'
  Status 'applying' 'Initializing the managed skin engine.'
  $failureHint='应用启动未完成。若取消了应用重启确认，请正常关闭受管应用，再点击启动。'
  $native=Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe'
  $startArgs=@('-NoProfile','-STA','-ExecutionPolicy','RemoteSigned','-File',(Join-Path $engine 'scripts\start-dream-skin.ps1'),'-SavedThemeDirectory',(Join-Path $root 'themes\local-sao-aincrad-link-start'))
  if($RestartExisting){$startArgs+='-RestartExisting'}else{$startArgs+='-PromptRestart'}
  & $native @startArgs *> (Join-Path $runtime 'skin-engine.log')
  if($LASTEXITCODE -ne 0){throw 'The managed skin engine did not complete. See skin-engine.log.'}
  $state=Read-DreamSkinState -Path (Join-Path $root 'state.json')
  if(-not $state){throw 'The skin engine did not record a session; startup may have been cancelled.'}
  $node=Get-DreamSkinNodeRuntime
  $session=Get-DreamSkinVerifiedCdpIdentityForAnyRegistered -Port ([int]$state.port)
  if(-not $session -or $session.Identity.BrowserId -cne $state.browserId){throw 'The skin session could not be confirmed.'}
  Status 'verifying' 'Checking the visible managed window and theme.'
  $failureHint='应用已打开，但皮肤显示没有通过检查。请返回整合入口重新安装主题后再试。'
  $verify=Invoke-DreamSkinNative -FilePath $node.Path -ArgumentList @((Join-Path $engine 'scripts\injector.mjs'),'--verify','--port',"$($state.port)",'--browser-id',$session.Identity.BrowserId,'--theme-dir',(Join-Path $root 'active-theme'),'--timeout-ms','8000')
  ($verify.Output -join "`r`n") | Set-Content -LiteralPath $verifyPath -Encoding utf8
  if($verify.ExitCode -ne 0){throw 'The SAO skin is not confirmed on a visible window.'}
  Status 'verified' 'Managed GPT window and visible SAO skin verified.'
  Mark 'Initialization verified'
} catch {
  if($CheckOnly){throw}
  New-Item -ItemType Directory -Path $runtime -Force | Out-Null
  Status 'failed' $failureHint
  Mark $_.Exception.Message
  exit 1
} finally {if($lock){Exit-DreamSkinOperationLock -Mutex $lock}}
