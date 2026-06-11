<#
  OdysseyCards — Android 调试构建 + 安装 + 日志 一键脚本
  用法:
    .\build_android.ps1              完整流程（构建→导出→安装→日志）
    .\build_android.ps1 -SkipBuild   跳过 dotnet build
    .\build_android.ps1 -ExportOnly  仅导出，不安装/不打开日志
#>
param([switch]$SkipBuild, [switch]$ExportOnly)
$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$godot = "G:\dev\Godot_v4.6.2\godot.exe"
$preset = "Android"
$apk   = "$root\export\android\OdysseyCards.apk"
$proj  = "$root\project.godot"

function step($msg) { Write-Host "`n▶ $msg" -ForegroundColor Cyan }

# ============================================================
# 0. 清理缓存（避免 Godot dotnet publish 使用旧 DLL）
# ============================================================
step "[0/4] 清理构建缓存..."
# Godot mono 临时构建输出
$godotTemp = "$root\.godot\mono\temp"
if (Test-Path $godotTemp) {
    Remove-Item -Recurse -Force $godotTemp 2>$null
    Write-Host "  已清理: .godot/mono/temp" -ForegroundColor DarkGray
}
# 所有 obj 目录（dotnet publish 的中间输出缓存）
Get-ChildItem -Path $root -Recurse -Directory -Filter "obj" -Depth 3 | ForEach-Object {
    Remove-Item -Recurse -Force $_.FullName 2>$null
}
Write-Host "  已清理: 所有 obj/ 目录" -ForegroundColor DarkGray
# dotnet clean
dotnet clean "$root\OdysseyCards.sln" -nologo 2>&1 | Out-Null
Write-Host "  已清理: dotnet clean" -ForegroundColor DarkGray
# 强制 dotnet publish（确保 Godot 导出使用最新编译结果）
dotnet publish "$root\OdysseyCards.sln" -c Debug -nologo 2>&1 | Out-Null
Write-Host "  已发布: dotnet publish" -ForegroundColor DarkGray

# ============================================================
# 1. dotnet build
# ============================================================
if (-not $SkipBuild) {
    step "[1/4] dotnet build..."
    dotnet build "$root\OdysseyCards.sln" -nologo 2>&1 | Select-Object -Last 3
    if ($LASTEXITCODE -ne 0) { throw "dotnet build 失败" }
} else {
    step "[1/4] 跳过 dotnet build"
}

# ============================================================
# 2. Godot 导出
# ============================================================
if (-not (Test-Path $godot)) { throw "Godot 未找到: $godot" }
New-Item -ItemType Directory -Force -Path "$root\export" | Out-Null

step "[2/4] Godot 导出 Android APK..."
$outDir = Split-Path $apk -Parent
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

& $godot --headless --path $root --export-debug $preset $apk 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "导出失败，退出码: $LASTEXITCODE" -ForegroundColor Red
    exit $LASTEXITCODE
}
Write-Host "  导出成功: $apk" -ForegroundColor Green
if (Test-Path $apk) {
    $sizeMB = [math]::Round((Get-Item $apk).Length / 1MB, 1)
    Write-Host "  大小: $sizeMB MB" -ForegroundColor Green
} else {
    throw "APK 未生成: $apk"
}

if ($ExportOnly) {
    Write-Host "完成! (仅导出)" -ForegroundColor Green
    exit 0
}

# ============================================================
# 3. adb 安装
# ============================================================
step "[3/4] adb 安装到设备..."

# 检查 adb
$adb = Get-Command adb -ErrorAction SilentlyContinue
if (-not $adb) { throw "adb 未找到。请安装 Android SDK Platform-Tools 并加入 PATH" }

# 检查设备连接
$devices = & adb devices 2>&1 | Select-String -Pattern "\tdevice"
if (-not $devices) { throw "未检测到已连接的 Android 设备。请用 USB 连接并开启 USB 调试" }
Write-Host ("  已连接设备: " + ($devices -replace "\tdevice",""))

# 安装
& adb install -r $apk 2>&1
if ($LASTEXITCODE -ne 0) { throw "adb install 失败" }
Write-Host "  安装成功" -ForegroundColor Green

# ============================================================
# 4. 打开日志
# ============================================================
step "[4/4] adb logcat（仅 Godot 标签, Ctrl+C 退出）..."

# 清除旧日志
& adb logcat -c 2>&1 | Out-Null

# 启动应用
$pkg = "com.whitegiverma.odysseycards"
$activity = "com.godot.game.GodotApp"
& adb shell am start -n "$pkg/$activity" 2>&1 | Out-Null
Write-Host "  应用已启动" -ForegroundColor Green
Write-Host ("━" * 60) -ForegroundColor DarkGray
Write-Host " 日志输出（Ctrl+C 退出）：" -ForegroundColor Yellow
Write-Host ("━" * 60) -ForegroundColor DarkGray

# 实时查看 Godot 日志
& adb logcat -s godot
