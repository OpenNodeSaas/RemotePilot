$ErrorActionPreference = "Stop"

# 以脚本位置推算 desktop 根目录，保证任意工作目录执行都正确
$root = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $root "src\RemotePilot\RemotePilot.csproj"
$publishDir  = Join-Path $root "artifacts\publish"          # 目录版
$singleDir   = Join-Path $root "artifacts\publish_single"   # 单文件版
$zipFile       = Join-Path $root "artifacts\RemotePilot-win-x64.zip"
$singleZipFile = Join-Path $root "artifacts\RemotePilot-single-win-x64.zip"

Write-Host "发布项目: $projectPath"
foreach ($dir in @($publishDir, $singleDir)) {
    if (Test-Path $dir) { Remove-Item $dir -Recurse -Force }
}

# ---------- 目录版 ----------
dotnet publish $projectPath -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=false -p:SatelliteResourceLanguages=zh-Hans `
    -o $publishDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish 失败（目录版）" }

Write-Host "创建目录版 ZIP: $zipFile"
if (Test-Path $zipFile) { Remove-Item $zipFile -Force }
Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zipFile

# ---------- 单文件版：压缩单文件 exe，便于直接分发 ----------
Write-Host "发布单文件版: $singleDir"
dotnet publish $projectPath -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true -p:SatelliteResourceLanguages=zh-Hans `
    -o $singleDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish 失败（单文件版）" }

Write-Host "创建单文件版 ZIP: $singleZipFile"
if (Test-Path $singleZipFile) { Remove-Item $singleZipFile -Force }
Compress-Archive -Path (Join-Path $singleDir "*") -DestinationPath $singleZipFile

Write-Host "完成！"
Write-Host "  目录版 ZIP : $zipFile"
Write-Host "  单文件 exe : $(Join-Path $singleDir 'RemotePilot.exe')"
Write-Host "  单文件 ZIP : $singleZipFile"
