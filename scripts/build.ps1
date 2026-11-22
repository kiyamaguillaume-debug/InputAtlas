param([string]$OutputDirectory)
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent;$src=Join-Path $repo 'src'
if(-not $OutputDirectory){$OutputDirectory=Join-Path $repo 'artifacts\InputAtlas'}
$out=[IO.Path]::GetFullPath($OutputDirectory);[IO.Directory]::CreateDirectory($out)|Out-Null
$fx=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319';$csc=Join-Path $fx 'csc.exe';if(-not (Test-Path -LiteralPath $csc)){throw 'The 64-bit .NET Framework compiler is required. Install .NET Framework 4.8 on Windows.'}
$system=@('System.dll','System.Core.dll','System.Web.Extensions.dll')|ForEach-Object {'/reference:'+(Join-Path $fx $_)}
$desktop=@('System.Xaml.dll','WPF\WindowsBase.dll','WPF\PresentationCore.dll','WPF\PresentationFramework.dll')|ForEach-Object {'/reference:'+(Join-Path $fx $_)}
$winforms='/reference:'+(Join-Path $fx 'System.Windows.Forms.dll');$drawing='/reference:'+(Join-Path $fx 'System.Drawing.dll')
$common=@('AssemblyInfo.cs','Storage.cs','NativeRuntime.cs','DatabaseBootstrap.cs','ControllerDefaults.cs','MonitorContracts.cs','MonitorMetrics.cs','NetworkSources.cs','NetworkSourceStore.cs')
$ui=@('Theme.cs','Charts.cs','App.cs','GamepadCore.cs','GamepadIdentity.cs','GamepadPhysicalIdentity.cs','GamepadView.cs','GamepadRefresh.cs','GamepadSummary.cs','GamepadHeatmap.cs','MonitorUI.cs','MonitorColorUI.cs','InputMapData.cs','InputMapCharts.cs','InputMapsUI.cs','LegacyKeyMap.cs','ShortcutCore.cs','ShortcutData.cs','ShortcutUI.cs','NetworkSourceUI.cs')
$collector=@('Foreground.cs','Collector.cs','NativeInputData.cs','NativeInputCollector.cs','ShortcutCore.cs')
$gamepad=@('Foreground.cs','GamepadCore.cs','GamepadCollector.cs','GamepadIdentity.cs','GamepadPhysicalIdentity.cs','XInputConnection.cs','XboxIdentity.cs')
$monitor=@('MonitorLayout.cs','MonitorColors.cs','MonitorTaskbar.cs','MonitorWorker.cs')
$watchdog=@('RecoveryPolicy.cs','CollectorWatchdog.cs')
function Compile([string]$name,[string]$main,[string[]]$sources,[string[]]$references,[switch]$Icon){$paths=$sources|Select-Object -Unique|ForEach-Object {Join-Path $src $_};$args=@('/nologo','/codepage:65001','/target:winexe','/platform:x64','/optimize+',('/main:'+$main),('/out:'+(Join-Path $out $name)))+$references;if($Icon){$iconPath=Join-Path $repo 'assets\InputAtlas.ico';$args+=('/resource:'+$iconPath+',InputAtlas.Icon.ico');$args+=('/win32icon:'+$iconPath)};& $csc $args $paths;if($LASTEXITCODE -ne 0){throw ('Build failed: '+$name)}}
Compile 'InputAtlas.exe' 'InputAtlas.Entry' ($common+$ui) ($system+$desktop) -Icon
Compile 'InputAtlas.Collector.exe' 'InputAtlas.NativeInputEntry' ($common+$collector) ($system+@($winforms))
Compile 'InputAtlas.Gamepad.Collector.exe' 'InputAtlas.GamepadEntry' ($common+$gamepad) ($system+@($winforms))
Compile 'InputAtlas.Monitor.exe' 'InputAtlas.MonitorWorker' ($common+$monitor) ($system+@($winforms,$drawing))
Compile 'InputAtlas.Watchdog.exe' 'InputAtlas.CollectorWatchdog' ($common+$watchdog) $system
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'update-network-cache.ps1') -Destination $out -Force
foreach($file in @('install-startup.ps1','remove-startup.ps1','stop-background.ps1')){if(Test-Path -LiteralPath (Join-Path $PSScriptRoot $file)){Copy-Item -LiteralPath (Join-Path $PSScriptRoot $file) -Destination $out -Force}}
foreach($file in @('LICENSE','THIRD_PARTY_NOTICES.md')){if(Test-Path -LiteralPath (Join-Path $repo $file)){Copy-Item -LiteralPath (Join-Path $repo $file) -Destination $out -Force}}
Write-Output ('Build complete: '+$out)
