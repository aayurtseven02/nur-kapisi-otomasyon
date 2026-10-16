param([int]$ParentId,[Parameter(Mandatory=$true)][string]$Source,[Parameter(Mandatory=$true)][string]$Target,[Parameter(Mandatory=$true)][string]$PackageSha256,[string]$RestartArgsBase64='W10=',[switch]$NoRestart)
$ErrorActionPreference='Stop'
$Source=[IO.Path]::GetFullPath($Source).TrimEnd('\')
$taskTarget=[IO.Path]::GetFullPath($Target).TrimEnd('\')
$taskSource=[IO.Path]::GetFullPath((Join-Path $Source 'Program')).TrimEnd('\')
$taskBackup=Join-Path $Source 'Backup'
$taskFiles=Get-Content -LiteralPath (Join-Path $Source 'files.json') -Raw | ConvertFrom-Json
$taskChanged=[Collections.Generic.List[string]]::new()
function File-Sha([string]$path){
    $digest=[Security.Cryptography.SHA256]::Create();$stream=[IO.File]::OpenRead($path)
    try{return [BitConverter]::ToString($digest.ComputeHash($stream)).Replace('-','')}finally{$stream.Dispose();$digest.Dispose()}
}
function Bounded([string]$root,[string]$relative){
    $taskPath=[IO.Path]::GetFullPath((Join-Path $root $relative))
    if(!$taskPath.StartsWith($root+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Gecersiz program dosyasi yolu.'}
    return $taskPath
}
function Open-Anka([bool]$skipCheck=$false) {
    if($NoRestart){return}
    $taskArgs=@(ConvertFrom-Json ([Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($RestartArgsBase64))))
    $taskAllArgs=[Collections.Generic.List[string]]::new()
    if($skipCheck){$taskAllArgs.Add('--skip-update-once')}
    foreach($taskArg in $taskArgs){if($null -ne $taskArg){$taskAllArgs.Add([string]$taskArg)}}
    $taskArguments=($taskAllArgs | ForEach-Object {'"'+$_.Replace('"','\"')+'"'}) -join ' '
    $taskApp=Join-Path $taskTarget 'AnkaStudio.exe'
    # Ask the interactive desktop shell to restart with the user's normal desktop token.
    try{$taskShell=New-Object -ComObject Shell.Application;$taskShell.ShellExecute($taskApp,$taskArguments,$taskTarget,'open',1)}
    catch{Start-Process -FilePath $taskApp -ArgumentList $taskArguments -WorkingDirectory $taskTarget}
}
try{
    foreach($taskOld in @('install-error.txt','install-result.txt')){Remove-Item -LiteralPath (Join-Path $Source $taskOld) -ErrorAction SilentlyContinue}
    Add-Type -AssemblyName System.Windows.Forms
    $taskWindow=New-Object Windows.Forms.Form
    $taskWindow.Text='TubeFlow - Guncelleme';$taskWindow.Width=540;$taskWindow.Height=170;$taskWindow.StartPosition='CenterScreen';$taskWindow.ControlBox=$false
    $taskLabel=New-Object Windows.Forms.Label;$taskLabel.Text='Guncelleme yukleniyor. Lutfen bekleyin...';$taskLabel.SetBounds(20,20,480,36)
    $taskBar=New-Object Windows.Forms.ProgressBar;$taskBar.SetBounds(20,65,480,25)
    $taskWindow.Controls.AddRange(@($taskLabel,$taskBar));$taskWindow.Show();[Windows.Forms.Application]::DoEvents()
    if(!(Test-Path -LiteralPath (Join-Path $taskTarget 'AnkaStudio.exe'))){throw 'Hedef uygulama bulunamadi.'}
    if((File-Sha (Join-Path $Source 'download.zip')) -ne $PackageSha256){throw 'Indirilen paket kurulumdan once degismis.'}
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $taskArchive=[IO.Compression.ZipFile]::OpenRead((Join-Path $Source 'download.zip'))
    try{
        $taskArchiveCount=0
        foreach($taskZipEntry in $taskArchive.Entries){
            if($taskZipEntry.FullName.EndsWith('/') -or $taskZipEntry.FullName.EndsWith('\')){continue}
            $taskRelative=$taskZipEntry.FullName.Replace('\','/');$null=Bounded $taskTarget $taskRelative
            $taskDigest=[Security.Cryptography.SHA256]::Create();$taskStream=$taskZipEntry.Open()
            try{$taskExpected=[BitConverter]::ToString($taskDigest.ComputeHash($taskStream)).Replace('-','')}finally{$taskStream.Dispose();$taskDigest.Dispose()}
            if($taskFiles.PSObject.Properties[$taskRelative].Value -ne $taskExpected){throw 'Paket listesi ozgun ZIP ile eslesmiyor.'}
            $taskArchiveCount++
        }
        if($taskArchiveCount -ne @($taskFiles.PSObject.Properties).Count){throw 'Paket listesinde fazladan dosya var.'}
    }finally{$taskArchive.Dispose()}
    if($ParentId -gt 0){$taskParent=Get-Process -Id $ParentId -ErrorAction SilentlyContinue;if($taskParent){$null=$taskParent.WaitForExit(60000);if(!$taskParent.HasExited){throw 'Anka kapanmadi; dosyalar degistirilmedi.'}}}
    if(Get-Process AnkaStudio -ErrorAction SilentlyContinue | Where-Object {$_.Path -and [IO.Path]::GetFullPath($_.Path).StartsWith($taskTarget+'\',[StringComparison]::OrdinalIgnoreCase)}){throw 'Baska Anka penceresi acik; guncelleme ertelendi.'}
    foreach($taskEntry in $taskFiles.PSObject.Properties){
        $taskFrom=Bounded $taskSource $taskEntry.Name;$null=Bounded $taskTarget $taskEntry.Name
        if((File-Sha $taskFrom) -ne $taskEntry.Value){throw 'Paket dosyasi degismis; kurulum iptal edildi.'}
    }
    foreach($taskEntry in $taskFiles.PSObject.Properties){
        $taskDest=Bounded $taskTarget $taskEntry.Name
        if(Test-Path -LiteralPath $taskDest -PathType Leaf){$taskSaved=Bounded $taskBackup $taskEntry.Name;New-Item -ItemType Directory -Force -Path (Split-Path $taskSaved) | Out-Null;Copy-Item -LiteralPath $taskDest -Destination $taskSaved}
    }
    $taskDone=0;$taskCount=@($taskFiles.PSObject.Properties).Count
    foreach($taskEntry in $taskFiles.PSObject.Properties){
        $taskDest=Bounded $taskTarget $taskEntry.Name;New-Item -ItemType Directory -Force -Path (Split-Path $taskDest) | Out-Null
        $taskChanged.Add($taskEntry.Name);Copy-Item -LiteralPath (Bounded $taskSource $taskEntry.Name) -Destination $taskDest -Force
        if((File-Sha $taskDest) -ne $taskEntry.Value){throw 'Kurulan dosya dogrulanamadi.'}
        $taskDone++;$taskBar.Value=[Math]::Min(100,[int]($taskDone*100/$taskCount));[Windows.Forms.Application]::DoEvents()
    }
    $taskCheck=Join-Path $Source 'LaunchCheck'
    $taskReport=Join-Path $taskCheck 'health.json'
    $taskTest=Start-Process -FilePath (Join-Path $taskTarget 'AnkaStudio.exe') -ArgumentList @('--startup-health-check','--health-report',('"'+$taskReport+'"')) -WindowStyle Hidden -PassThru
    if(!$taskTest.WaitForExit(30000)){Stop-Process -Id $taskTest.Id -Force;throw 'Yeni surum zamaninda acilamadi.'}
    if($taskTest.ExitCode -ne 0 -or !(Test-Path -LiteralPath $taskReport)){throw 'Yeni surum acilis testi gecemedi.'}
    $taskResult=Get-Content -LiteralPath $taskReport -Raw | ConvertFrom-Json
    if(!$taskResult.Success){throw 'Yeni surum kontrolu basarisiz.'}
    'Guncelleme tamamlandi.' | Set-Content -LiteralPath (Join-Path $Source 'install-result.txt')
    $taskWindow.Close()
    Open-Anka
}catch{
    $_.Exception.Message | Set-Content -LiteralPath (Join-Path $Source 'install-error.txt')
    foreach($taskRelative in $taskChanged){$taskSaved=Bounded $taskBackup $taskRelative;if(Test-Path -LiteralPath $taskSaved -PathType Leaf){Copy-Item -LiteralPath $taskSaved -Destination (Bounded $taskTarget $taskRelative) -Force}else{Remove-Item -LiteralPath (Bounded $taskTarget $taskRelative) -Force -ErrorAction SilentlyContinue}}
    if($taskWindow){$taskWindow.Close()}
    Open-Anka $true
    exit 1
}
