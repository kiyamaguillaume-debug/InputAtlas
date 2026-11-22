param([string]$BuildDirectory)
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
if(-not $BuildDirectory){$BuildDirectory=Join-Path $repo 'artifacts\InputAtlas'}
$BuildDirectory=[IO.Path]::GetFullPath($BuildDirectory)
$exe=Join-Path $BuildDirectory 'InputAtlas.exe'
if(-not (Test-Path -LiteralPath $exe)){throw 'Run scripts\build.ps1 first.'}
$fx=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$refs=@('System.dll','System.Core.dll','System.Web.Extensions.dll','System.Xaml.dll','WPF\WindowsBase.dll','WPF\PresentationCore.dll','WPF\PresentationFramework.dll')|ForEach-Object {'/reference:'+(Join-Path $fx $_)}
$tests=Join-Path $repo ('artifacts\tests\'+[Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($tests)|Out-Null
$package=Join-Path $BuildDirectory 'InputAtlas.PackageTests.exe'
$ui=Join-Path $BuildDirectory 'InputAtlas.UiSmoke.exe'
$previousProfile=$env:INPUTATLAS_DATA_DIR
function Run([string]$File,[string[]]$Arguments){$p=Start-Process -FilePath $File -ArgumentList ($Arguments|ForEach-Object {'"'+$_+'"'}) -WindowStyle Hidden -PassThru;if(-not $p.WaitForExit(30000)){Stop-Process -Id $p.Id;throw ('Test timed out: '+$File)};if($p.ExitCode -ne 0){throw ('Test failed: '+$File+'; inspect '+$tests)}}
try{
 & (Join-Path $fx 'csc.exe') /nologo /target:exe /platform:x64 /codepage:65001 ('/out:'+$package) ('/reference:'+$exe) $refs (Join-Path $repo 'tests\PackageTests.cs') (Join-Path $repo 'src\NativeInputData.cs') (Join-Path $repo 'src\Collector.cs')
 if($LASTEXITCODE -ne 0){throw 'Could not build package tests.'}
 & (Join-Path $fx 'csc.exe') /nologo /target:winexe /platform:x64 /codepage:65001 ('/out:'+$ui) ('/reference:'+$exe) $refs (Join-Path $repo 'tests\UiSmoke.cs')
 if($LASTEXITCODE -ne 0){throw 'Could not build UI smoke tests.'}
 Run $package @((Join-Path $tests 'model'))
 $env:INPUTATLAS_DATA_DIR=Join-Path $tests 'fresh-ui'
 Run $exe @('--self-test')
 Run $ui @($env:INPUTATLAS_DATA_DIR,(Join-Path $tests 'fresh-capture'))
 Run $ui @((Join-Path $tests 'model'),(Join-Path $tests 'model-capture'))
 $env:INPUTATLAS_DATA_DIR=Join-Path $tests 'workers'
 Run (Join-Path $BuildDirectory 'InputAtlas.Collector.exe') @('--probe-seconds','4')
 Run (Join-Path $BuildDirectory 'InputAtlas.Gamepad.Collector.exe') @('--probe-seconds','3')
 Run (Join-Path $BuildDirectory 'InputAtlas.Monitor.exe') @('--probe',(Join-Path $tests 'monitor-probe.json'))
 foreach($log in @('native-input-error.log','controller-error.log')){if(Test-Path -LiteralPath (Join-Path $env:INPUTATLAS_DATA_DIR $log)){throw ('Worker error: '+$log)}}
 $heartbeat=Get-Content -LiteralPath (Join-Path $env:INPUTATLAS_DATA_DIR 'collector-heartbeat.json') -Raw|ConvertFrom-Json
 if($heartbeat.error){throw $heartbeat.error}
 Write-Output ('All isolated tests passed. Results: '+$tests)
}finally{
 $env:INPUTATLAS_DATA_DIR=$previousProfile
 foreach($file in @($package,$ui)){if(Test-Path -LiteralPath $file){Remove-Item -LiteralPath $file}}
}
