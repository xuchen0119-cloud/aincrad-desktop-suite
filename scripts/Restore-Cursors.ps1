$ErrorActionPreference='Stop'
$bundle=Split-Path -Parent $PSScriptRoot
$runtime=Join-Path $env:LOCALAPPDATA 'AincradSuite'
$pointer=Join-Path $runtime 'last-cursor-backup.txt'
if(-not(Test-Path -LiteralPath $pointer)){throw 'No cursor backup exists for this suite.'}
$name=([IO.File]::ReadAllText($pointer)).Trim()
if($name -notmatch '^cursor-backup-[a-f0-9]{32}\.json$'){throw 'Invalid backup name.'}
$backup=Get-Content -Raw -LiteralPath (Join-Path $runtime $name) -Encoding UTF8|ConvertFrom-Json
$allowed=@('Arrow','Help','AppStarting','Wait','Crosshair','IBeam','NWPen','No','SizeNS','SizeWE','SizeNWSE','SizeNESW','SizeAll','UpArrow','Hand','Pin','Person','','SchemeSource')
if($backup.schemaVersion -ne 1 -or @($backup.changedValues).Count -ne $allowed.Count){throw 'Invalid cursor backup.'}
if(@($backup.changedValues|Where-Object{$_.name -notin $allowed}).Count){throw 'Unexpected cursor setting in backup.'}
if(-not ('CursorNative' -as [type])){Add-Type -TypeDefinition ([IO.File]::ReadAllText((Join-Path $bundle 'src\CursorNative.cs')))}
$key=[Microsoft.Win32.Registry]::CurrentUser.OpenSubKey('Control Panel\Cursors',$true)
$schemes=$null
try{
  foreach($record in $backup.changedValues){
    if(-not $record.present){$key.DeleteValue([string]$record.name,$false);continue}
    $kind=[Microsoft.Win32.RegistryValueKind][Enum]::Parse([Microsoft.Win32.RegistryValueKind],[string]$record.kind)
    $data=$record.data
    if($kind -eq [Microsoft.Win32.RegistryValueKind]::DWord){$data=[int]$data}
    if($kind -eq [Microsoft.Win32.RegistryValueKind]::QWord){$data=[long]$data}
    $key.SetValue([string]$record.name,$data,$kind)
  }
  $schemes=[Microsoft.Win32.Registry]::CurrentUser.OpenSubKey('Control Panel\Cursors\Schemes',$true)
  if($schemes -and $schemes.GetValue([string]$backup.addedScheme) -ceq $backup.addedSchemeData){$schemes.DeleteValue([string]$backup.addedScheme,$false)}
  $key.Flush();[CursorNative]::Reload();Write-Output 'Previous cursor scheme restored.'
}finally{if($schemes){$schemes.Dispose()};$key.Dispose()}
