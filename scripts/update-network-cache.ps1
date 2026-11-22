param([Parameter(Mandatory=$true)][string]$StartDate,[Parameter(Mandatory=$true)][string]$EndDate,[Parameter(Mandatory=$true)][string]$CacheDir)
$ErrorActionPreference='Stop'
$mutex=New-Object Threading.Mutex($false,'Local\InputAtlasNetworkCacheV1')
if(-not $mutex.WaitOne(0)){exit 0}
function Write-AtomicJson($path,$value) {
  $tmp=$path+'.'+$PID+'.tmp'
  [IO.File]::WriteAllText($tmp,(ConvertTo-Json -InputObject $value -Depth 8 -Compress),[Text.UTF8Encoding]::new($false))
  Move-Item -LiteralPath $tmp -Destination $path -Force
}
function Await-Usage($operation) {
  $resultType=[Collections.Generic.IReadOnlyList`1].MakeGenericType([Windows.Networking.Connectivity.AttributedNetworkUsage])
  $method=[System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object {$_.Name -eq 'AsTask' -and $_.GetGenericArguments().Count -eq 1 -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1'} | Select-Object -First 1
  $task=$method.MakeGenericMethod($resultType).Invoke($null,@($operation))
  if(-not $task.Wait(8000)){throw 'Windows network query exceeded 8 seconds'}
  $task.Result
}
$statusPath=Join-Path $CacheDir 'status.json'
$errors=New-Object Collections.Generic.List[string]
$started=[DateTimeOffset]::Now.ToUnixTimeSeconds()
try {
  [IO.Directory]::CreateDirectory($CacheDir) | Out-Null
  Add-Type -AssemblyName System.Runtime.WindowsRuntime
  $null=[Windows.Networking.Connectivity.NetworkInformation,Windows.Networking,ContentType=WindowsRuntime]
  $profiles=@([Windows.Networking.Connectivity.NetworkInformation]::GetConnectionProfiles())
  $internet=[Windows.Networking.Connectivity.NetworkInformation]::GetInternetConnectionProfile()
  if($null -ne $internet){$profiles+=@($internet)}
  $profiles=@($profiles | Group-Object { [string]$_.ProfileName + '|' + [string]$_.NetworkAdapter.NetworkAdapterId } | ForEach-Object {$_.Group[0]})
  if($profiles.Count -eq 0){throw 'No Windows network profiles available'}
  $states=New-Object Windows.Networking.Connectivity.NetworkUsageStates
  $first=[DateTime]::ParseExact($StartDate,'yyyy-MM-dd',[Globalization.CultureInfo]::InvariantCulture)
  $last=[DateTime]::ParseExact($EndDate,'yyyy-MM-dd',[Globalization.CultureInfo]::InvariantCulture)
  # Recent dates first, so the current view receives useful results quickly.
  for($date=$last;$date -ge $first;$date=$date.AddDays(-1)) {
    if(([DateTimeOffset]::Now.ToUnixTimeSeconds()-$started) -gt 110){break}
    $day=$date.ToString('yyyy-MM-dd');$path=Join-Path $CacheDir ($day+'.json')
    $ttl=if($date -ge [DateTime]::Today.AddDays(-2)){300}else{86400}
    if(Test-Path -LiteralPath $path){
      $age=([DateTime]::Now-(Get-Item -LiteralPath $path).LastWriteTime).TotalSeconds
      if($age -lt $ttl){continue}
    }
    Write-AtomicJson $statusPath @{running=$true;at=[DateTimeOffset]::Now.ToUnixTimeSeconds();day=$day;errors=@($errors)}
    try {
      $start=[DateTimeOffset]::new([DateTime]::SpecifyKind($date,[DateTimeKind]::Local))
      $end=[DateTimeOffset]::new([DateTime]::SpecifyKind($date.AddDays(1),[DateTimeKind]::Local))
      if($end -gt [DateTimeOffset]::Now){$end=[DateTimeOffset]::Now}
      if($end -le $start){continue}
      $rows=New-Object Collections.Generic.List[object]
      foreach($profile in $profiles) {
        foreach($usage in @(Await-Usage ($profile.GetAttributedNetworkUsageAsync($start,$end,$states)))) {
          $id=[string]$usage.AttributionId
          $app=if([string]::IsNullOrWhiteSpace($id)){'System / unknown'}else{Split-Path -Leaf $id}
          if([string]::IsNullOrWhiteSpace($app)){$app=$id}
          $rows.Add([pscustomobject]@{application=$app;download_bytes=[double]$usage.BytesReceived;upload_bytes=[double]$usage.BytesSent})
        }
      }
      $merged=@($rows | Group-Object application | ForEach-Object {
        [pscustomobject]@{application=$_.Name;download_bytes=($_.Group | Measure-Object download_bytes -Sum).Sum;upload_bytes=($_.Group | Measure-Object upload_bytes -Sum).Sum}
      })
      Write-AtomicJson $path @{schema=2;day=$day;at=[DateTimeOffset]::Now.ToUnixTimeSeconds();profile_count=$profiles.Count;rows=$merged}
    } catch {$errors.Add($day+': '+$_.Exception.Message)}
  }
} catch {$errors.Add($_.Exception.Message)}
finally {
  try {Write-AtomicJson $statusPath @{running=$false;at=[DateTimeOffset]::Now.ToUnixTimeSeconds();errors=@($errors)}} catch {}
  $mutex.ReleaseMutex();$mutex.Dispose()
}
