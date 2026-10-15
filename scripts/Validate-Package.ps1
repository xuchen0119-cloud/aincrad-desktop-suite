$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$count=0
foreach($file in Get-ChildItem -LiteralPath $root -Recurse -File -Filter '*.ps1'){
  $tokens=$null;$parseErrors=$null
  [void][Management.Automation.Language.Parser]::ParseFile($file.FullName,[ref]$tokens,[ref]$parseErrors)
  if($parseErrors.Count){throw ('PowerShell parse error in '+$file.Name+': '+($parseErrors|Out-String))}
  $count++
}
$cursorCount=0
foreach($scheme in @('silverwing','lightsaber','frostblade')){
  & (Join-Path $PSScriptRoot 'Install-Cursors.ps1') -Scheme $scheme -ValidateOnly
  if($LASTEXITCODE -ne 0){throw ('Cursor validation failed: '+$scheme)}
  $cursorCount+=17
}
$validation=Get-Content -Raw -LiteralPath (Join-Path $root 'pet\yui\asset-validation.json') -Encoding UTF8|ConvertFrom-Json
$pet=Join-Path $root 'pet\yui\spritesheet.png'
if(-not $validation.ok -or (Get-FileHash -LiteralPath $pet).Hash.ToLowerInvariant() -cne $validation.sha256){throw 'Pet bytes differ from validated asset.'}
if((Get-Item -LiteralPath $pet).Length -ne $validation.encoded_bytes){throw 'Pet byte length differs from validated asset.'}
$required=@('AincradSuite.exe','launcher\AincradLauncher.exe','Aincrad-Theme-v2.8.zip','theme\theme.json','theme\theme.css','theme\background.png','pet\yui\layout.json')
foreach($path in $required){if(-not(Test-Path -LiteralPath (Join-Path $root $path))){throw ('Missing package component: '+$path)}}
foreach($path in @('AincradSuite.exe','launcher\AincradLauncher.exe')){
  $bytes=[IO.File]::ReadAllBytes((Join-Path $root $path))
  if($bytes[0] -ne 77 -or $bytes[1] -ne 90){throw 'Invalid compiled Windows executable.'}
}
[ordered]@{ok=$true;powerShellFilesParsed=$count;nativeCursorRolesValidated=$cursorCount;validatedPetHashMatches=$true;requiredComponentsPresent=$true}|ConvertTo-Json
