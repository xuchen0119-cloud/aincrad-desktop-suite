param([ValidateSet('silverwing','lightsaber','frostblade')][string]$Scheme='frostblade',[switch]$ValidateOnly)
$ErrorActionPreference='Stop'
$bundle=Split-Path -Parent $PSScriptRoot
$source=Join-Path $bundle ('cursors\'+$Scheme)
$destination=Join-Path $env:LOCALAPPDATA ('AincradSuite\cursors\'+$Scheme)
$manifest=Get-Content -Raw -LiteralPath (Join-Path $source 'manifest.json') -Encoding UTF8 | ConvertFrom-Json
$roles=@('Arrow','Help','AppStarting','Wait','Crosshair','IBeam','NWPen','No','SizeNS','SizeWE','SizeNWSE','SizeNESW','SizeAll','UpArrow','Hand','Pin','Person')
if(-not ('CursorNative' -as [type])){Add-Type -TypeDefinition ([IO.File]::ReadAllText((Join-Path $bundle 'src\CursorNative.cs')))}
foreach($role in $roles){
  $name=[string]$manifest.$role.file
  if([IO.Path]::GetFileName($name) -cne $name -or $name -notmatch '\.(cur|ani)$'){throw 'Invalid cursor filename.'}
  $info=[CursorNative]::InspectFile((Join-Path $source ('cursors\'+$name)))
  if($info.isIcon -or $info.width -lt 16 -or $info.hotspotX -ge $info.width -or $info.hotspotY -ge $info.height){throw ('Invalid cursor: '+$role)}
}
if($ValidateOnly){Write-Output ('Validated 17 cursor roles: '+$Scheme);exit 0}
$runtime=Join-Path $env:LOCALAPPDATA 'AincradSuite'
New-Item -ItemType Directory -Path $destination -Force | Out-Null
foreach($role in $roles){Copy-Item -LiteralPath (Join-Path $source ('cursors\'+$manifest.$role.file)) -Destination (Join-Path $destination $manifest.$role.file) -Force}
$key=[Microsoft.Win32.Registry]::CurrentUser.OpenSubKey('Control Panel\Cursors',$true)
if(-not $key){throw 'Current-user cursor settings are unavailable.'}
$schemes=$null;$backup=$null;$changed=$false
function Restore-Value($RegistryKey,$Record){
  if(-not $Record.present){$RegistryKey.DeleteValue([string]$Record.name,$false);return}
  $kind=[Microsoft.Win32.RegistryValueKind][Enum]::Parse([Microsoft.Win32.RegistryValueKind],[string]$Record.kind)
  $data=$Record.data
  if($kind -eq [Microsoft.Win32.RegistryValueKind]::DWord){$data=[int]$data}
  if($kind -eq [Microsoft.Win32.RegistryValueKind]::QWord){$data=[long]$data}
  $RegistryKey.SetValue([string]$Record.name,$data,$kind)
}
try{
  $names=@($key.GetValueNames());$records=@()
  foreach($name in ($roles+@('','SchemeSource'))){
    $exists=$names -contains $name
    $records+=[ordered]@{name=$name;present=$exists;kind=if($exists){$key.GetValueKind($name).ToString()}else{$null};data=if($exists){$key.GetValue($name,$null,[Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames)}else{$null}}
  }
  $schemes=[Microsoft.Win32.Registry]::CurrentUser.CreateSubKey('Control Panel\Cursors\Schemes')
  $schemeName='Aincrad Suite - '+$Scheme+' - '+(Get-Date -Format 'yyyyMMddHHmmss')
  $schemeData=($roles|ForEach-Object{Join-Path $destination $manifest.$_.file}) -join ','
  $backup=[ordered]@{schemaVersion=1;changedValues=$records;addedScheme=$schemeName;addedSchemeData=$schemeData}
  $backupPath=Join-Path $runtime ('cursor-backup-'+[guid]::NewGuid().ToString('N')+'.json')
  $backup|ConvertTo-Json -Depth 7|Set-Content -LiteralPath $backupPath -Encoding UTF8
  [IO.File]::WriteAllText((Join-Path $runtime 'last-cursor-backup.txt'),[IO.Path]::GetFileName($backupPath))
  $changed=$true
  foreach($role in $roles){$key.SetValue($role,(Join-Path $destination $manifest.$role.file),[Microsoft.Win32.RegistryValueKind]::ExpandString)}
  $key.SetValue('',$schemeName,[Microsoft.Win32.RegistryValueKind]::String)
  $key.SetValue('SchemeSource',1,[Microsoft.Win32.RegistryValueKind]::DWord)
  $schemes.SetValue($schemeName,$schemeData,[Microsoft.Win32.RegistryValueKind]::String)
  $key.Flush();$schemes.Flush();[CursorNative]::Reload()
  Write-Output 'Cursor scheme installed. The previous scheme was backed up locally.'
}catch{
  if($changed -and $backup){
    foreach($record in $backup.changedValues){Restore-Value $key $record}
    if($schemes -and $schemes.GetValue($schemeName) -ceq $schemeData){$schemes.DeleteValue($schemeName,$false)}
    $key.Flush();try{[CursorNative]::Reload()}catch{}
  }
  throw
}finally{if($schemes){$schemes.Dispose()};$key.Dispose()}
