$ErrorActionPreference='Stop'
$link=Join-Path ([Environment]::GetFolderPath('Startup')) 'InputAtlas.lnk'
if(Test-Path -LiteralPath $link){Remove-Item -LiteralPath $link}
Write-Output 'InputAtlas startup shortcut removed. Local recordings are retained.'
