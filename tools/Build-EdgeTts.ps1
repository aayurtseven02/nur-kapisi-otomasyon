param([Parameter(Mandatory=$true)][string]$OutputDirectory,[string]$PythonCommand='python')
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$edgeRoot=[IO.Path]::GetFullPath($OutputDirectory)
$edgeRuntime=Join-Path $edgeRoot 'runtime';$edgePackages=Join-Path $edgeRoot 'packages';$edgeDownloads=Join-Path (Split-Path $edgeRoot) 'edge-downloads'
New-Item -ItemType Directory -Force -Path $edgeRoot,$edgeRuntime,$edgePackages,$edgeDownloads | Out-Null
$edgeZip=Join-Path $edgeDownloads 'python-3.12.10-embed-amd64.zip'
if(!(Test-Path -LiteralPath $edgeZip)){Invoke-WebRequest 'https://www.python.org/ftp/python/3.12.10/python-3.12.10-embed-amd64.zip' -OutFile $edgeZip}
if((Get-FileHash -LiteralPath $edgeZip -Algorithm SHA256).Hash -ne '4ACBED6DD1C744B0376E3B1CF57CE906F9DC9E95E68824584C8099A63025A3C3'){throw 'Edge Python SHA256 verification failed.'}
Expand-Archive -LiteralPath $edgeZip -DestinationPath $edgeRuntime -Force
$edgeLock=Join-Path $PSScriptRoot 'edge-tts/requirements.lock'
& $PythonCommand -m pip download --only-binary=:all: --python-version 3.12 --platform win_amd64 --implementation cp --abi cp312 --no-deps --require-hashes --dest $edgeDownloads -r $edgeLock
if($LASTEXITCODE -ne 0){throw 'Edge dependency download or hash verification failed.'}
foreach($wheel in Get-ChildItem -LiteralPath $edgeDownloads -Filter '*.whl'){
    $archive=[IO.Compression.ZipFile]::OpenRead($wheel.FullName)
    try{foreach($entry in $archive.Entries){
        $target=[IO.Path]::GetFullPath((Join-Path $edgePackages $entry.FullName))
        if(!$target.StartsWith($edgePackages+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Unsafe wheel path.'}
        if(!$entry.Name){New-Item -ItemType Directory -Force -Path $target | Out-Null;continue}
        New-Item -ItemType Directory -Force -Path (Split-Path $target) | Out-Null
        [IO.Compression.ZipFileExtensions]::ExtractToFile($entry,$target,$true)
    }}finally{$archive.Dispose()}
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'edge-tts/bridge.py') -Destination $edgeRoot -Force
& (Join-Path $edgeRuntime 'python.exe') (Join-Path $edgeRoot 'bridge.py') --check
if($LASTEXITCODE -ne 0){throw 'Packaged Edge bridge failed its import check.'}
Copy-Item -LiteralPath $edgeLock -Destination (Join-Path $edgeRoot 'requirements.lock') -Force
Write-Host 'Edge TTS runtime prepared; package license files are preserved.'
