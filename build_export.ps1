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
$godot = "G:\dev\Godot_v4.6.2\godot.exe"
$preset = "Windows Desktop"
$out = "$root\export\windows"
$exe = "$out\OdysseyCards.exe"
$proj = "$root\project.godot"
$mcp  = 'McpInteractionServer="*res://addons/godot_mcp/mcp_interaction_server.gd"'

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

# 3. 临时处理 MCP Autoload
$bak = "$proj.bak"
Copy-Item $proj $bak -Force
$restore = $false
try {
    $txt = Get-Content $proj -Raw
    if ($txt -match [regex]::Escape($mcp)) {
        $txt = $txt -replace "`r`n$([regex]::Escape($mcp))", ""
        Set-Content $proj -Value $txt -NoNewline
        $restore = $true
        step "[3] 已临时移除 MCP Autoload"
    } else {
        step "[3] MCP Autoload 不存在，跳过"
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
}

# 4. 显示结果
$size = 0
Get-ChildItem $out -Recurse -File | ForEach-Object { $size += $_.Length }
$mb = [math]::Round($size / 1MB, 1)
Write-Host "`n========================================" -ForegroundColor Green
Write-Host "  导出完成!  总大小: $mb MB" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host "运行: $exe`n"
