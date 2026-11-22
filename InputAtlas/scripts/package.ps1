param([string]$OutputDirectory)
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
if(-not $OutputDirectory){$OutputDirectory=Join-Path $repo 'artifacts\releases'}
$out=[IO.Path]::GetFullPath($OutputDirectory)
[IO.Directory]::CreateDirectory($out)|Out-Null
$binary=Join-Path $out 'staging\InputAtlas'
& (Join-Path $PSScriptRoot 'build.ps1') -OutputDirectory $binary
Copy-Item -LiteralPath (Join-Path $repo 'docs\QUICKSTART.txt') -Destination $binary -Force
foreach($doc in @('PRIVACY.md','ARCHITECTURE.md','RELEASE_NOTES.md')){Copy-Item -LiteralPath (Join-Path $repo ('docs\'+$doc)) -Destination $binary -Force}
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
function Archive([string]$path,[string]$base,[IO.FileInfo[]]$files){
 if(Test-Path -LiteralPath $path){throw ('Archive already exists: '+$path)}
 $stream=[IO.File]::Open($path,[IO.FileMode]::CreateNew)
 $zip=New-Object IO.Compression.ZipArchive($stream,[IO.Compression.ZipArchiveMode]::Create,$false)
 try{foreach($file in $files){$relative=$file.FullName.Substring($base.Length).TrimStart('\').Replace('\','/');$entry=$zip.CreateEntry('InputAtlas/'+$relative,[IO.Compression.CompressionLevel]::Optimal);$input=[IO.File]::OpenRead($file.FullName);$output=$entry.Open();try{$input.CopyTo($output)}finally{$input.Dispose();$output.Dispose()}}}finally{$zip.Dispose();$stream.Dispose()}
}
$source=New-Object 'Collections.Generic.List[IO.FileInfo]'
foreach($name in @('README.md','README.zh-CN.md','LICENSE','THIRD_PARTY_NOTICES.md','.gitignore','.gitattributes')){$source.Add((Get-Item -LiteralPath (Join-Path $repo $name) -Force))}
foreach($dir in @('src','scripts','tests','docs','assets','tools')){foreach($file in (Get-ChildItem -LiteralPath (Join-Path $repo $dir) -File -Recurse -Force)){$source.Add($file)}}
Archive (Join-Path $out 'InputAtlas-v0.1.0-beta-source.zip') $repo $source.ToArray()
Archive (Join-Path $out 'InputAtlas-v0.1.0-beta-win-x64.zip') $binary @(Get-ChildItem -LiteralPath $binary -File)
$sums=Get-ChildItem -LiteralPath $out -Filter '*.zip' -File|Sort-Object Name|ForEach-Object {((Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant())+'  '+$_.Name}
[IO.File]::WriteAllLines((Join-Path $out 'SHA256SUMS.txt'),$sums,(New-Object Text.UTF8Encoding($false)))
Write-Output ('Packaged '+$source.Count+' source files in '+$out)
