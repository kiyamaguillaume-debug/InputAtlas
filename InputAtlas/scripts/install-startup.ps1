$ErrorActionPreference='Stop'
$exe=Join-Path $PSScriptRoot 'InputAtlas.exe'
if(-not (Test-Path -LiteralPath $exe)){$exe=Join-Path (Split-Path $PSScriptRoot -Parent) 'artifacts\InputAtlas\InputAtlas.exe'}
if(-not (Test-Path -LiteralPath $exe)){throw 'Build or extract the complete InputAtlas release first.'}
$exe=(Resolve-Path -LiteralPath $exe).Path
$startup=[Environment]::GetFolderPath('Startup')
$link=Join-Path $startup 'InputAtlas.lnk'
$shell=New-Object -ComObject WScript.Shell
$shortcut=$shell.CreateShortcut($link)
$shortcut.TargetPath=$exe
$shortcut.Arguments='--start-background'
$shortcut.WorkingDirectory=Split-Path $exe -Parent
$shortcut.WindowStyle=7
$shortcut.Description='InputAtlas background collection'
$shortcut.IconLocation=$exe+',0'
$shortcut.Save()
Write-Output ('Startup enabled: '+$link)
