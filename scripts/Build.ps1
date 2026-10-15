$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
Add-Type -AssemblyName PresentationFramework,PresentationCore,WindowsBase
$refs=@('System.dll','System.Core.dll','System.Xml.dll','System.Xaml.dll','System.Web.Extensions.dll',[Windows.Window].Assembly.Location,[Windows.Media.Imaging.BitmapImage].Assembly.Location,[Windows.Threading.DispatcherTimer].Assembly.Location)
foreach($item in @(@('src\AincradSuite.cs','AincradSuite.exe'),@('src\Cinematic.cs','launcher\AincradLauncher.exe'))){
  $destination=Join-Path $root $item[1]
  if(Test-Path -LiteralPath $destination){throw ('Build output already exists; use a fresh checkout or rename the existing generated file: '+$item[1])}
  $sources=@((Join-Path $root $item[0]),(Join-Path $root 'src\SuiteState.cs'))
  Add-Type -Path $sources -ReferencedAssemblies $refs -OutputAssembly $destination -OutputType WindowsApplication
}
Add-Type -AssemblyName System.IO.Compression.FileSystem
$themeZip=Join-Path $root 'Aincrad-Theme-v2.8.zip'
if(-not(Test-Path -LiteralPath $themeZip)){
  [IO.Compression.ZipFile]::CreateFromDirectory((Join-Path $root 'theme'),$themeZip)
}
Write-Output 'Built AincradSuite.exe, the opening launcher, and the theme ZIP from source.'
