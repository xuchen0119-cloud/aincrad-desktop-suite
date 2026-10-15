$ErrorActionPreference='Stop'
$bundle=Split-Path -Parent $PSScriptRoot
Add-Type -Path (Join-Path $bundle 'src\SuiteState.cs') -ReferencedAssemblies System.Web.Extensions.dll
$fixture=Join-Path ([IO.Path]::GetTempPath()) ('aincrad-test-'+[guid]::NewGuid().ToString('N'))
$local=Join-Path $fixture 'local'
$engine=Join-Path $local 'CodexDreamSkin\engine'
$checks=0
function Assert($Condition,[string]$Message){if(-not $Condition){throw $Message};$script:checks++}
function Write-Fixture([string]$Path,[string]$Value){New-Item -ItemType Directory -Path (Split-Path -Parent $Path) -Force|Out-Null;[IO.File]::WriteAllText($Path,$Value)}
# Temporary fixtures contain no live account, session, registry or app state.
Assert (-not [SuiteState]::Inspect($bundle,$local).EngineInstalled) 'Fresh install must request the dependency.'
foreach($name in @('VERSION','scripts\common-windows.ps1','scripts\theme-windows.ps1','scripts\start-dream-skin.ps1','scripts\injector.mjs')){Write-Fixture (Join-Path $engine $name) 'fixture'}
Write-Fixture (Join-Path $engine 'VERSION') '1.5.20'
Assert ([SuiteState]::Inspect($bundle,$local).NextAction -eq '安装 / 更新皮肤') 'Installed engine must request theme import.'
foreach($folder in @('active-theme','themes\local-sao-aincrad-link-start')){
  $destination=Join-Path $local ('CodexDreamSkin\'+$folder)
  New-Item -ItemType Directory -Path $destination -Force|Out-Null
  Get-ChildItem -LiteralPath (Join-Path $bundle 'theme') -File|Copy-Item -Destination $destination
  # Official import rewrites JSON whitespace: semantic equality must still pass.
  $json=Get-Content -Raw -LiteralPath (Join-Path $destination 'theme.json') -Encoding UTF8|ConvertFrom-Json
  $json.image='art-imported.png'
  Move-Item -LiteralPath (Join-Path $destination 'background.png') -Destination (Join-Path $destination 'art-imported.png')
  $json|ConvertTo-Json -Depth 20|Set-Content -LiteralPath (Join-Path $destination 'theme.json') -Encoding UTF8
}
Write-Fixture (Join-Path $engine 'assets\dream-skin.css') ([IO.File]::ReadAllText((Join-Path $bundle 'theme-extension\aincrad-ui.css')))
Assert ([SuiteState]::Inspect($bundle,$local).ThemeCurrent) 'Reformatted matching theme should be recognized.'
Write-Fixture (Join-Path $local 'CodexDreamSkin\active-theme\theme.css') 'old css'
Assert (-not [SuiteState]::Inspect($bundle,$local).ThemeCurrent) 'Changed CSS must require an update.'
Copy-Item -LiteralPath (Join-Path $bundle 'theme\theme.css') -Destination (Join-Path $local 'CodexDreamSkin\active-theme\theme.css') -Force
Write-Fixture (Join-Path $engine 'assets\dream-skin.css') 'missing extension'
Assert (-not [SuiteState]::Inspect($bundle,$local).ThemeCurrent) 'Missing tested extension must require an update.'
Write-Fixture (Join-Path $engine 'VERSION') '9.0.0'
$state=[SuiteState]::Inspect($bundle,$local)
Assert ($state.ThemeCurrent -and -not $state.EngineSupported) 'Untested engine must use the base theme without requiring internal extensions.'
Write-Fixture (Join-Path $local 'CodexDreamSkin\themes\local-sao-aincrad-link-start\theme.css') 'old saved css'
Assert (-not [SuiteState]::Inspect($bundle,$local).ThemeCurrent) 'Missing or different saved theme must prevent launch-ready status.'
$launcher=Join-Path $bundle 'launcher'
Assert ([SuiteState]::ResolveArtwork($launcher) -eq (Join-Path $bundle 'theme\background.png')) 'Launcher must use the shared artwork.'
$result=Join-Path $fixture 'current-run.json'
Assert (-not [SuiteState]::IsVerified(0,$result)) 'Zero exit without this run status must not mean success.'
Write-Fixture $result '{"phase":"failed"}'
Assert (-not [SuiteState]::IsVerified(0,$result)) 'Failed status must not become a successful launch.'
Write-Fixture $result '{"phase":"verified"}'
Assert ([SuiteState]::IsVerified(0,$result)) 'Verified status and zero exit are both required.'
Assert (-not [SuiteState]::IsVerified(1,$result)) 'Nonzero exit must reject even a verified status.'
Write-Fixture $result '{"phase":'
Assert (-not [SuiteState]::IsVerified(0,$result)) 'Partial status writes must not crash or report success.'
$runtime=Join-Path $local 'AincradSuite'
Write-Fixture (Join-Path $runtime 'last-theme-backup.txt') '..\outside'
Assert (-not [SuiteState]::Inspect($bundle,$local).ThemeBackup) 'Backup pointers must stay under the suite runtime.'
. (Join-Path $launcher 'Aincrad-BootState.ps1')
$boot=[datetime]'2026-01-01T05:00:00Z';$now=$boot.AddHours(1)
$old=[pscustomobject]@{injectorStartedAt='2026-01-01T04:00:00Z'}
Assert (Test-AincradStatePredatesBoot -State $old -BootUtc $boot -FileWrittenUtc $boot.AddMinutes(-30) -NowUtc $now) 'Proven previous-boot state can be archived.'
Assert (-not (Test-AincradStatePredatesBoot -State $old -BootUtc $boot -FileWrittenUtc $boot.AddMinutes(1) -NowUtc $now)) 'Rewritten current-boot state must remain protected.'
[ordered]@{ok=$true;startupChecks=$checks;liveSettingsChanged=$false;processesStopped=0}|ConvertTo-Json
