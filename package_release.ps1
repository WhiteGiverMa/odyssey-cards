<#
   OdysseyCards 发布打包脚本
   将 export/ 产物打包为 zip，准备上传 GitHub Releases
   版本号唯一真源：仓库根目录 VERSION 文件（单行，无 v 前缀）
  用法:
    .\package_release.ps1                 打包为 OdysseyCards_v<VERSION>.zip
    .\package_release.ps1 v1.0            手动指定版本号覆盖 VERSION
    .\package_release.ps1 -OpenFolder     打包后打开所在文件夹
#>
param([string]$Version, [switch]$OpenFolder)
$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$exportDir = "$root\export"

# 版本号默认从 VERSION 文件读取（唯一真源）
if (-not $Version) { $Version = (Get-Content "$root\VERSION" -Raw).Trim() }

# 检查
if (-not (Test-Path "$exportDir\OdysseyCards.exe")) {
    Write-Host "[ERROR] export\OdysseyCards.exe 不存在，请先运行 .\build_export.ps1" -ForegroundColor Red
    exit 1
}

$zipName = "OdysseyCards_v$Version.zip"
$zipPath = "$root\$zipName"

# 打包
Write-Host "正在打包..." -ForegroundColor Cyan
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }

Compress-Archive -Path @(
    "$exportDir\OdysseyCards.exe",
    "$exportDir\OdysseyCards.pck",
    (Get-ChildItem "$exportDir\data_OdysseyCards_*" -Directory).FullName
) -DestinationPath $zipPath -CompressionLevel Optimal

$sizeMB = [math]::Round((Get-Item $zipPath).Length / 1MB, 1)
Write-Host "`n========================================" -ForegroundColor Green
Write-Host "  $zipName  ($sizeMB MB)" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green

Write-Host "`n上传到 GitHub Releases:" -ForegroundColor White
Write-Host "  1. 打开 https://github.com/WhiteGiverMa/odyssey-cards/releases/new"
Write-Host "  2. Tag: v$Version"
Write-Host "  3. 拖入 $zipName`n"

if ($OpenFolder) { Invoke-Item $root }
