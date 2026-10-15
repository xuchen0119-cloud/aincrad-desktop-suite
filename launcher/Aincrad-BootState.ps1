function Test-AincradStatePredatesBoot {
  param([AllowNull()][object]$State,[datetime]$BootUtc,[datetime]$FileWrittenUtc,[datetime]$NowUtc=[DateTime]::UtcNow)
  if(-not $State -or -not $State.injectorStartedAt){return $false}
  $boot=$BootUtc.ToUniversalTime()
  if($boot -gt $NowUtc.ToUniversalTime().AddSeconds(5)){return $false}
  $started=[DateTimeOffset]::MinValue
  if(-not [DateTimeOffset]::TryParse([string]$State.injectorStartedAt,[ref]$started)){return $false}
  if($started.UtcDateTime -ge $boot -or $FileWrittenUtc.ToUniversalTime() -ge $boot){return $false}
  if($State.createdAt) {
    $created=[DateTimeOffset]::MinValue
    if(-not [DateTimeOffset]::TryParse([string]$State.createdAt,[ref]$created)){return $false}
    if($created.UtcDateTime -ge $boot){return $false}
  }
  return $true
}

function Repair-AincradExpiredBootState {
  # Caller must hold the existing Dream Skin operation lock.
  param([string]$StateRoot,[AllowNull()][object]$State)
  if(-not $State){return $null}
  $statePath=Join-Path $StateRoot 'state.json'
  $file=Get-Item -LiteralPath $statePath -ErrorAction Stop
  $boot=(Get-CimInstance Win32_OperatingSystem -ErrorAction Stop).LastBootUpTime.ToUniversalTime()
  if(-not (Test-AincradStatePredatesBoot -State $State -BootUtc $boot -FileWrittenUtc $file.LastWriteTimeUtc)){return $null}
  # A process cannot survive an OS reboot. Archive only the proven old record;
  # never stop the process that has since reused its numeric PID.
  return Archive-DreamSkinStateFile -Path $statePath
}
