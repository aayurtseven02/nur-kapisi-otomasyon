param([string]$Repository='aayurtseven02/nur-kapisi-otomasyon',[string]$PreviousManifest='',[string]$OutputDirectory='')
$ErrorActionPreference='Stop'
if($Repository -notmatch '^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$'){throw 'GitHub deposu kullanici/depo biciminde olmali.'}
$taskProject=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../src/AnkaStudio.csproj'))
$taskVersion=([xml](Get-Content -LiteralPath $taskProject -Raw)).Project.PropertyGroup.Version | Where-Object {$_} | Select-Object -First 1
if(!$OutputDirectory){$OutputDirectory=Join-Path $PSScriptRoot '../release'}
$taskOut=[IO.Path]::GetFullPath($OutputDirectory);$taskBuildId=[Guid]::NewGuid().ToString('N');$taskPayload=Join-Path $taskOut ('Program-'+$taskVersion+'-'+$taskBuildId);$taskBuild=Join-Path $taskOut ('Build-'+$taskBuildId)
New-Item -ItemType Directory -Force -Path $taskOut | Out-Null
dotnet publish $taskProject -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:DebugType=None -p:DebugSymbols=false -o $taskBuild
if($LASTEXITCODE -ne 0){throw 'Program paketi derlenemedi.'}
& dotnet (Join-Path $taskBuild 'TubeFlow.dll') --source-safety-test --source ([IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..')))
if($LASTEXITCODE -ne 0){throw 'Kaynak güvenlik kontrolü başarısız; güncelleme ZIP dosyası oluşturulmadı.'}
Copy-Item -LiteralPath (Join-Path $taskBuild 'TubeFlow.exe') -Destination (Join-Path $taskBuild 'AnkaStudio.exe')
Copy-Item -LiteralPath (Join-Path $taskBuild 'TubeFlow.dll') -Destination (Join-Path $taskBuild 'AnkaStudio.dll')
New-Item -ItemType Directory -Force -Path $taskPayload | Out-Null
foreach($taskFile in Get-ChildItem -LiteralPath $taskBuild -Recurse -File){
    $taskRelative=$taskFile.FullName.Substring($taskBuild.Length+1).Replace('\','/')
    $taskAllowed=$taskRelative -match '^[^/]+\.dll$|^(TubeFlow|AnkaStudio)\.exe$|^TubeFlow\.(deps|runtimeconfig)\.json$|^Update-Install\.ps1$|^[a-zA-Z-]+/[^/]+\.resources\.dll$|^runtimes/.+\.dll$|^tools/workflows/anka-(sdxl|flux2)\.json$|^tools/audio-alignment/align\.py$|^ChannelPackages/NurKapisi/nur-kapisi-[^/]+\.png$'
    if($taskAllowed){$taskDestination=Join-Path $taskPayload $taskRelative;New-Item -ItemType Directory -Force -Path (Split-Path $taskDestination) | Out-Null;Copy-Item -LiteralPath $taskFile.FullName -Destination $taskDestination}
}
@{FeedUrl="https://api.github.com/repos/$Repository/releases/latest"} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $taskPayload 'anka-update-source.json') -Encoding utf8
$taskName="TubeFlow-$taskVersion-win-x64.zip";$taskZip=Join-Path $taskOut $taskName
Compress-Archive -Path (Join-Path $taskPayload '*') -DestinationPath $taskZip -Force
$taskUpdates=@()
if($PreviousManifest -and (Test-Path -LiteralPath $PreviousManifest)){
    $taskOld=Get-Content -LiteralPath $PreviousManifest -Raw | ConvertFrom-Json
    if($taskOld.Updates){$taskUpdates=@($taskOld.Updates)}elseif($taskOld.Version){$taskUpdates=@($taskOld)}
}
$taskUpdates=@($taskUpdates | Where-Object {$_.Version -ne $taskVersion})
$taskUpdates+=@{Version=$taskVersion;DownloadUrl="https://github.com/$Repository/releases/download/v$taskVersion/$taskName";Sha256=(Get-FileHash -LiteralPath $taskZip -Algorithm SHA256).Hash}
@{Updates=@($taskUpdates | Sort-Object {[Version]$_.Version})} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $taskOut 'anka-update.json') -Encoding utf8
Write-Host 'GitHub Release dosyalari hazir: ZIP ve anka-update.json'
