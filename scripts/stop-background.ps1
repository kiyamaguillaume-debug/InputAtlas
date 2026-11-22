$ErrorActionPreference='Stop'
$exe=Join-Path $PSScriptRoot 'InputAtlas.exe'
if(-not (Test-Path -LiteralPath $exe)){$exe=Join-Path (Split-Path $PSScriptRoot -Parent) 'artifacts\InputAtlas\InputAtlas.exe'}
if(-not (Test-Path -LiteralPath $exe)){throw 'Run this script from an extracted release or a built source tree.'}
$process=Start-Process -FilePath $exe -ArgumentList '--stop-background' -WindowStyle Hidden -PassThru
if(-not $process.WaitForExit(15000)){throw 'Stop request timed out.'}
if($process.ExitCode -ne 0){throw 'InputAtlas could not write the stop request.'}
Write-Output 'Stop requested. Background components exit within a few seconds. Close the dashboard to keep them stopped; opening it resumes collection.'
