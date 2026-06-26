<#
  OdysseyCards 一键导出脚本
  用法:
    .\build_export.ps1              Release 导出
    .\build_export.ps1 -Debug       Debug 导出（带调试符号）
    .\build_export.ps1 -SkipBuild   跳过 dotnet build（已手动编译时用）
#>
param([switch]$Debug, [switch]$SkipBuild)
$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$godot = "G:\dev\Godot_v4.7\godot.exe"
$preset = "Windows Desktop"
$out = "$root\export\windows"
$exe = "$out\OdysseyCards.exe"
$proj = "$root\project.godot"

function step($msg) { Write-Host $msg -ForegroundColor Cyan }

step "=== OdysseyCards 导出 ==="

# 0. dotnet build
if (-not $SkipBuild) {
    step "[0] dotnet build -c Release..."
    dotnet build "$root\OdysseyCards.sln" -c Release -nologo 2>&1 | Select-Object -Last 5
    if ($LASTEXITCODE -ne 0) { throw "dotnet build 失败" }
}

# 1. 检查 Godot
if (-not (Test-Path $godot)) { throw "Godot 未找到: $godot" }
step "[1] Godot: $godot"

# 2. 输出目录
New-Item -ItemType Directory -Force -Path $out | Out-Null
step "[2] 输出: $out"

# 3. 临时版本号注入
$bak = "$proj.bak"
Copy-Item $proj $bak -Force
$restore = $false

# 版本号注入到 export_presets.cfg（Windows file_version/product_version）
$presets = "$root\export_presets.cfg"
$presetsBak = "$presets.bak"
$restorePresets = $false
if (Test-Path $presets) {
    Copy-Item $presets $presetsBak -Force
}

try {
	$txt = Get-Content $proj -Raw

	# 注入版本号到 project.godot [application] 段
    # 唯一真源是仓库根目录 VERSION 文件，这里临时写入导出元数据，finally 恢复
    $version = (Get-Content "$root\VERSION" -Raw).Trim()
    if ($txt -match 'config/version=[^\r\n]*') {
        $txt = $txt -replace 'config/version=[^\r\n]*', "config/version=`"$version`""
    } else {
        $txt = $txt -replace '(config/name=[^\r\n]*)', "`$1`r`nconfig/version=`"$version`""
    }
    Set-Content $proj -Value $txt -NoNewline
    $restore = $true
    step "[3.1] 已注入版本号: $version"

    # 注入版本号到 export_presets.cfg (Windows preset: file_version/product_version)
    if (Test-Path $presets) {
        $pcfg = Get-Content $presets -Raw
        $pcfg = $pcfg -replace 'application/file_version="[^"]*"', "application/file_version=`"$version`""
        $pcfg = $pcfg -replace 'application/product_version="[^"]*"', "application/product_version=`"$version`""
        Set-Content $presets -Value $pcfg -NoNewline
        $restorePresets = $true
        step "[3.2] export_presets.cfg 已注入 Windows 版本号"
    }

    $flag = if ($Debug) { "--export-debug" } else { "--export-release" }
    step "[4] godot --headless $flag ..."
    $godotOutput = & $godot --headless --path $root $flag $preset $exe 2>&1
    Write-Host ($godotOutput -join "`n")
    if ($LASTEXITCODE -ne 0) { throw "导出失败，退出码: $LASTEXITCODE" }
    if (-not (Test-Path $exe)) { throw "导出失败: 未生成 $exe" }
} finally {
    Copy-Item $bak $proj -Force
    Remove-Item $bak -Force
    if ($restore) { step "[✓] project.godot 已恢复" }
    if ($restorePresets -and (Test-Path $presetsBak)) {
        Copy-Item $presetsBak $presets -Force
        step "[✓] export_presets.cfg 已恢复"
    }
    Remove-Item $presetsBak -Force -ErrorAction SilentlyContinue
}

# 4. 显示结果
$size = 0
Get-ChildItem $out -Recurse -File | ForEach-Object { $size += $_.Length }
$mb = [math]::Round($size / 1MB, 1)
Write-Host "`n========================================" -ForegroundColor Green
Write-Host "  导出完成!  总大小: $mb MB" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host "运行: $exe`n"
