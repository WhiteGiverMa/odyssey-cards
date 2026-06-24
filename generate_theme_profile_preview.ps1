<#
    generate_theme_profile_preview.ps1
    Generates human-readable Markdown of ThemeProfile .tres files,
    decoding CardMechanicTag bitmask values and Keyword enum values
    to Chinese display names by reading the C# enum source files.

    Usage:
      .\generate_theme_profile_preview.ps1          # One-shot generation
      .\generate_theme_profile_preview.ps1 -Watch   # Auto-regen on .tres / enum file changes
#>
param([switch]$Watch)
$ErrorActionPreference = "Stop"
$root = $PSScriptRoot

# ====================================================================
# 1. Extract enum value -> display name from C# source files
# ====================================================================

function Get-MechanicTagMap {
    $map = @{}
    $file = "$root\Scripts\Core\CardMechanicTag.cs"
    $lines = Get-Content -LiteralPath $file -Encoding UTF8

    $pendingCn = $null
    for ($i = 0; $i -lt $lines.Count; $i++) {
        $line = $lines[$i].Trim()
        if ($line -match '^\s*///\s*<summary>(.+)</summary>') {
            $text = $Matches[1]
            $sep = $text.IndexOf([char]0xFF1A)
            $pendingCn = if ($sep -ge 0) { $text.Substring(0, $sep).Trim() } else { $text.Trim() }
            continue
        }
        if ($line -match '^(\w+)\s*=\s*(\d+)\s*,?') {
            $name = if ($pendingCn) { $pendingCn } else { $Matches[1] }
            if ([int]$Matches[2] -ne 0) {
                $map[[int]$Matches[2]] = @{ Name = $Matches[1]; Cn = $name }
            }
            $pendingCn = $null
        }
    }
    return $map
}

function Get-KeywordMap {
    $map = @{}
    $file = "$root\Scripts\Core\Keyword.cs"
    $lines = Get-Content -LiteralPath $file -Encoding UTF8

    $pendingCn = $null
    $cur = 0
    $foundNone = $false
    for ($i = 0; $i -lt $lines.Count; $i++) {
        $line = $lines[$i].Trim()
        if (-not $foundNone -and $line -cmatch '^\s*None\b') { $foundNone = $true; $cur = 0; continue }
        if ($line -match '^\s*///\s*<summary>(.+)</summary>') {
            $text = $Matches[1]
            $sep = $text.IndexOf([char]0xFF1A)
            $pendingCn = if ($sep -ge 0) { $text.Substring(0, $sep).Trim() } else { $text.Trim() }
            continue
        }
        if ($line -match '^(\w+)\s*,?\s*$' -and $line -notmatch '^///') {
            $cur++
            $name = if ($pendingCn) { $pendingCn } else { $Matches[1] }
            $map[[int]$cur] = @{ Name = $Matches[1]; Cn = $name }
            $pendingCn = $null
            continue
        }
        if ($line -match '^(\w+)\s*=\s*(\d+)\s*,?') {
            $cur = [int]$Matches[2]
            $name = if ($pendingCn) { $pendingCn } else { $Matches[1] }
            $map[[int]$cur] = @{ Name = $Matches[1]; Cn = $name }
            $pendingCn = $null
        }
    }
    return $map
}

# ====================================================================
# 2. Parse .tres dictionary blocks
# ====================================================================

function Parse-TresDict {
    param([string[]]$Lines, [ref]$Index)
    $dict = @{}
    $s = $Index.Value
    while ($s -lt $Lines.Count -and $Lines[$s] -notmatch '\{') { $s++ }
    if ($s -ge $Lines.Count) { $Index.Value = $s; return $dict }
    # Empty dict: { }
    if ($Lines[$s] -match '\{\s*\}') {
        $Index.Value = $s + 1
        return $dict
    }
    $s++
    for ($j = $s; $j -lt $Lines.Count; $j++) {
        $line = $Lines[$j].Trim()
        if ($line -eq '') { continue }
        if ($line -eq '}') { $Index.Value = $j + 1; break }
        if ($line -match '^(\d+)\s*:\s*(-?\d+)\s*,?\s*$') {
            $dict[[int]$Matches[1]] = [int]$Matches[2]
        }
    }
    return $dict
}

function Parse-TresArray {
    param([string[]]$Lines, [ref]$Index)
    $result = @()
    $s = $Index.Value
    while ($s -lt $Lines.Count -and $Lines[$s] -notmatch '\(') { $s++ }
    if ($s -ge $Lines.Count) { $Index.Value = $s; return $result }
    $line = $Lines[$s]
    foreach ($m in [regex]::Matches($line, '"([^"]*)"')) { $result += $m.Groups[1].Value }
    $Index.Value = $s + 1
    return $result
}

function Fmt { param([int]$v) if ($v -gt 0) { "+$v" } else { "$v" } }

# ====================================================================
# 3. Core: build Markdown
# ====================================================================

function Build-Markdown {
    # Known Chinese display names as fallback (multi-line summaries aren't reliably parsed)
    $mechCnFallback = @{
        "DirectDamage"   = "直伤";     "DamageOverTime" = "持续伤害"; "Heal" = "治疗"
        "Armor"          = "护甲";     "Draw"            = "抽牌";     "Discover" = "发现"
        "Summon"         = "召唤";     "Buff"            = "增益";     "Silence" = "沉默"
        "Discard"        = "弃牌";     "Domain"          = "领域";     "WeaponSynergy" = "武器协同"
        "ManaRamp"       = "法力增益"; "StatusApply"     = "状态施加"; "Shuffle" = "洗牌"
        "Token"          = "衍生牌"
    }
    $kwCnFallback = @{
        "Charge"      = "闪击";  "Taunt"       = "嘲讽";   "Battlecry"  = "战吼"
        "Deathrattle" = "亡语";  "Windfury"    = "风怒";   "Ambush"     = "伏击"
        "Impact"      = "冲击";  "Recycle"     = "轮战";   "Unplayable" = "不可打出"
        "Ethereal"    = "虚无";  "Qiqiao"      = "奇巧"
    }

    $mech = Get-MechanicTagMap
    foreach ($k in $mech.Keys) {
        $e = $mech[$k]; $en = $e.Name
        if ($e.Cn -eq $en -and $mechCnFallback.ContainsKey($en)) { $e.Cn = $mechCnFallback[$en] }
    }
    $kw   = Get-KeywordMap
    foreach ($k in $kw.Keys) {
        $e = $kw[$k]; $en = $e.Name
        if ($e.Cn -eq $en -and $kwCnFallback.ContainsKey($en)) { $e.Cn = $kwCnFallback[$en] }
    }

    $sb = [System.Text.StringBuilder]::new()
    [void]$sb.AppendLine("# Theme Profile Preview")
    [void]$sb.AppendLine()
    [void]$sb.AppendLine("> Auto-generated $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')  |  Source: Resources/Themes/*.tres")
    [void]$sb.AppendLine("> Refresh: .\generate_theme_profile_preview.ps1  or  -Watch")
    [void]$sb.AppendLine()
    [void]$sb.AppendLine("---")
    [void]$sb.AppendLine()

    # ---- Enum quick reference ----
    [void]$sb.AppendLine("## Enum Quick Reference")
    [void]$sb.AppendLine()
    [void]$sb.AppendLine("### CardMechanicTag (bitmask / [Flags])")
    [void]$sb.AppendLine()
    [void]$sb.AppendLine("| CN | Enum | Value |")
    [void]$sb.AppendLine("|----|------|-------|")
    foreach ($k in ($mech.Keys | Sort-Object)) {
        $e = $mech[$k]
        [void]$sb.AppendLine("| $($e.Cn) | $($e.Name) | $k |")
    }
    [void]$sb.AppendLine()
    [void]$sb.AppendLine("### Keyword (enum value)")
    [void]$sb.AppendLine()
    [void]$sb.AppendLine("| CN | Enum | Value |")
    [void]$sb.AppendLine("|----|------|-------|")
    foreach ($k in ($kw.Keys | Sort-Object { [int]$_ })) {
        $e = $kw[$k]
        [void]$sb.AppendLine("| $($e.Cn) | $($e.Name) | $k |")
    }
    [void]$sb.AppendLine()
    [void]$sb.AppendLine("---")
    [void]$sb.AppendLine()

    # ---- Per-profile sections ----
    $tresFiles = Get-ChildItem -LiteralPath "$root\Resources\Themes" -Filter "ThemeProfile_*.tres" | Sort-Object Name
    foreach ($f in $tresFiles) {
        $lines = @(Get-Content -LiteralPath $f.FullName -Encoding UTF8)
        $heroId = $null; $themeName = $null; $target = 20; $maxDup = 2; $maxDom = 3
        $tagW = @{}; $kwW = @{}; $core = @(); $ovr = @{}

        for ($i = 0; $i -lt $lines.Count; $i++) {
            $line = $lines[$i].Trim()
            if     ($line -match '^HeroId\s*=\s*"(.+)"')                 { $heroId = $Matches[1] }
            elseif ($line -match '^ThemeName\s*=\s*"(.+)"')              { $themeName = $Matches[1] }
            elseif ($line -match '^TargetDeckSize\s*=\s*(\d+)')          { $target = $Matches[1] }
            elseif ($line -match '^MaxDuplicatesPerCard\s*=\s*(\d+)')    { $maxDup = $Matches[1] }
            elseif ($line -match '^MaxDomainCards\s*=\s*(\d+)')          { $maxDom = $Matches[1] }
            elseif ($line -eq 'TagWeights = {') {
                $idx = $i; $tagW = Parse-TresDict $lines ([ref]$idx); $i = $idx - 1
            }
            elseif ($line -eq 'KeywordWeights = {') {
                $idx = $i; $kwW = Parse-TresDict $lines ([ref]$idx); $i = $idx - 1
            }
            elseif ($line -match '^CoreCardIds\s*=\s*PackedStringArray\(') {
                $idx = $i; $core = Parse-TresArray $lines ([ref]$idx); $i = $idx - 1
            }
            elseif ($line -eq 'CardWeightOverrides = {') {
                $idx = $i; $ovr = Parse-TresDict $lines ([ref]$idx); $i = $idx - 1
            }
        }

        $title = if ($themeName) { "$themeName ($($f.BaseName))" } else { $f.BaseName }

        [void]$sb.AppendLine("## $title")
        [void]$sb.AppendLine()
        [void]$sb.AppendLine("| Param | Value |")
        [void]$sb.AppendLine("|-------|-------|")
        [void]$sb.AppendLine("| HeroId | $heroId |")
        [void]$sb.AppendLine("| ThemeName | $themeName |")
        [void]$sb.AppendLine("| TargetDeckSize | $target |")
        [void]$sb.AppendLine("| MaxDuplicatesPerCard | $maxDup |")
        [void]$sb.AppendLine("| MaxDomainCards | $maxDom |")
        [void]$sb.AppendLine()

        # TagWeights
        if ($tagW.Count -gt 0) {
            $sorted = $tagW.GetEnumerator() | Sort-Object { [int]$_.Key }
            [void]$sb.AppendLine("### TagWeights")
            [void]$sb.AppendLine()
            [void]$sb.AppendLine("| CN | Enum (Value) | Weight |")
            [void]$sb.AppendLine("|----|--------------|--------|")
            foreach ($kv in $sorted) {
                $v = [int]$kv.Key; $w = [int]$kv.Value
                $n = if ($mech.ContainsKey($v)) { $mech[$v] } else { @{Cn='?'; Name='?'} }
                [void]$sb.AppendLine("| $($n.Cn) | $($n.Name) ($v) | $(Fmt $w) |")
            }
            [void]$sb.AppendLine()
        }

        # KeywordWeights
        if ($kwW.Count -gt 0) {
            $sorted = $kwW.GetEnumerator() | Sort-Object { [int]$_.Key }
            [void]$sb.AppendLine("### KeywordWeights")
            [void]$sb.AppendLine()
            [void]$sb.AppendLine("| CN | Enum (Value) | Weight |")
            [void]$sb.AppendLine("|----|--------------|--------|")
            foreach ($kv in $sorted) {
                $v = [int]$kv.Key; $w = [int]$kv.Value
                $n = if ($kw.ContainsKey($v)) { $kw[$v] } else { @{Cn='?'; Name='?'} }
                [void]$sb.AppendLine("| $($n.Cn) | $($n.Name) ($v) | $(Fmt $w) |")
            }
            [void]$sb.AppendLine()
        }

        # CoreCardIds
        if ($core.Count -gt 0) {
            [void]$sb.AppendLine("### CoreCardIds")
            [void]$sb.AppendLine()
            foreach ($id in $core) { [void]$sb.AppendLine("- $id") }
            [void]$sb.AppendLine()
        }

        # CardWeightOverrides
        if ($ovr.Count -gt 0) {
            $sorted = $ovr.GetEnumerator() | Sort-Object { $_.Key }
            [void]$sb.AppendLine("### CardWeightOverrides")
            [void]$sb.AppendLine()
            [void]$sb.AppendLine("| Card ID | Weight |")
            [void]$sb.AppendLine("|---------|--------|")
            foreach ($kv in $sorted) {
                [void]$sb.AppendLine("| $($kv.Key) | $(Fmt $kv.Value) |")
            }
            [void]$sb.AppendLine()
        }

        [void]$sb.AppendLine("---")
        [void]$sb.AppendLine()
    }

    $outPath = "$root\Resources\Themes\ThemeProfiles.preview.md"
    $sb.ToString() | Set-Content -LiteralPath $outPath -Encoding UTF8
    Write-Host "Generated: $outPath" -ForegroundColor Green
    return $outPath
}

# ====================================================================
# 4. Entry
# ====================================================================

$outPath = Build-Markdown

if ($Watch) {
    Write-Host "Watching *.tres / enum files for changes... Ctrl+C to exit." -ForegroundColor Cyan

    $w1 = [System.IO.FileSystemWatcher]::new()
    $w1.Path = "$root\Resources\Themes"
    $w1.Filter = "*.tres"
    $w1.NotifyFilter = [System.IO.NotifyFilters]::LastWrite

    $w2 = [System.IO.FileSystemWatcher]::new()
    $w2.Path = "$root\Scripts\Core"
    $w2.Filter = "*.cs"
    $w2.NotifyFilter = [System.IO.NotifyFilters]::LastWrite

    $action = {
        $path = $Event.SourceEventArgs.FullPath
        $name = Split-Path $path -Leaf
        if ($name -in @('CardMechanicTag.cs', 'Keyword.cs') -or $name -like 'ThemeProfile_*.tres') {
            Write-Host "Changed: $name -> regenerating..." -ForegroundColor Yellow
            try { Build-Markdown | Out-Null } catch { Write-Host "ERROR: $_" -ForegroundColor Red }
        }
    }

    $j1 = Register-ObjectEvent $w1 "Changed" -Action $action
    $j2 = Register-ObjectEvent $w2 "Changed" -Action $action
    $w1.EnableRaisingEvents = $true
    $w2.EnableRaisingEvents = $true

    try { while ($true) { Start-Sleep -Seconds 1 } }
    finally {
        $w1.EnableRaisingEvents = $false; $w2.EnableRaisingEvents = $false
        Unregister-Event $j1.Name; Unregister-Event $j2.Name
        $w1.Dispose(); $w2.Dispose()
    }
}
