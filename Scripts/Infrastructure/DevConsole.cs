using Godot;
using OdysseyCards.AI;
using OdysseyCards.Combat;
using OdysseyCards.Core;
using OdysseyCards.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OdysseyCards.Infrastructure;

/// <summary>
/// 开发者控制台 — Autoload 单例。
/// 按反引号键 (`) 呼出/隐藏。支持文本命令和 AI 远程调用。
/// </summary>
/// <remarks>
/// AI 调用方式（godot-mcp）：
///   game_call_method(nodePath="/root/DevConsole", method="DevCommand", args=["/damage 10"])
///
/// 命令列表：
///   /damage N              对敌方英雄造成 N 点伤害。加 -c 进入点击模式。
///   /damage_enemy N         同上（显式）。
///   /damage_self N          对己方英雄造成 N 点伤害。
///   /damage_eslot X N       对敌方槽位 X(0-4) 随从造成 N 点伤害。
///   /damage_pslot X N       对己方槽位 X(0-4) 随从造成 N 点伤害。
///   /damage_all N           对所有敌方随从造成 N 点伤害。
///   /draw N                 抽 N 张牌。别名 /d。
///   /mana N                 获得 N 点法力。别名 /m。
///   /heal N                 恢复 N 点生命值。别名 /h。
///   /armor N                获得 N 点护甲。别名 /a。
///   /end                    强制结束回合。
///   /refresh                刷新 UI。别名 /r。
///   /clear                  清空控制台输出。别名 /cls。
///   /token &lt;card_id&gt;       将指定 ID 的卡牌加入手牌。别名 /t。
///   /play &lt;card_id&gt;        从手牌打出领域/无目标法术。别名 /p。
///   /summon_player &lt;id&gt; &lt;slot&gt;  在己方槽位召唤随从（QA）。别名 /sp。
///   /unlock_all             解锁全部卡牌加入收藏。
///   /intent_debug            显示当前敌方意图目标（QA）。
///   /qa_tombstone            验证墓碑伤害结算（QA）。
///   /qa_bait_tactics         验证诱饵战术双阵营触发（QA）。
///   /fight &lt;enemy&gt;          直接与指定敌人战斗，跳过地图。
///   /help                    显示帮助。别名 /?。
/// </remarks>
public partial class DevConsole : Node
{
    // ===== UI 组件 =====

    private CanvasLayer _canvasLayer = null!;
    private Panel _panel = null!;
    private RichTextLabel _output = null!;
    private LineEdit _input = null!;
    private RichTextLabel _completionLabel = null!;
    private bool _visible;

    // ===== 命令注册表 =====

    private record DevCommandDef(string Name, string[] Aliases, string Signature, string Description, string[]? ArgHints = null);

    private readonly List<DevCommandDef> _commands = new();
    private readonly Dictionary<string, OdysseyCards.Core.CardData> _cardCache = new();

    // ===== 生命周期 =====

    public override void _Ready()
    {
        // 创建 UI 层级（置于最顶层）
        _canvasLayer = new CanvasLayer { Layer = 128, Name = "DevConsoleLayer" };
        AddChild(_canvasLayer);

        // 半透明背景面板
        _panel = new Panel
        {
            Name = "ConsolePanel",
            Size = new Vector2(800, 400),
            Position = new Vector2(240, 60),
            Visible = false,
        };
        var panelStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.05f, 0.05f, 0.1f, 0.92f),
            BorderWidthLeft = 2, BorderWidthRight = 2,
            BorderWidthTop = 2, BorderWidthBottom = 2,
            BorderColor = new Color(0.3f, 0.6f, 0.3f),
            CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4,
        };
        _panel.AddThemeStyleboxOverride("panel", panelStyle);
        _canvasLayer.AddChild(_panel);

        // VBox 布局
        var vbox = new VBoxContainer
        {
            AnchorLeft = 0, AnchorTop = 0, AnchorRight = 1, AnchorBottom = 1,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        _panel.AddChild(vbox);

        // 标题栏
        var titleBar = new HBoxContainer();
        var title = new Label
        {
            Text = "  DevConsole",
            CustomMinimumSize = new Vector2(0, 28),
        };
        title.AddThemeColorOverride("font_color", new Color(0.4f, 1f, 0.4f));
        title.AddThemeFontSizeOverride("font_size", 14);
        titleBar.AddChild(title);
        vbox.AddChild(titleBar);

        // 输出区域
        _output = new RichTextLabel
        {
            Name = "Output",
            BbcodeEnabled = true,
            ScrollFollowing = true,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 300),
        };
        vbox.AddChild(_output);

        // 输入栏
        _input = new LineEdit
        {
            Name = "Input",
            PlaceholderText = "输入命令，如 /damage 10, /draw 5, /help ...",
            CustomMinimumSize = new Vector2(0, 32),
        };
        _input.TextSubmitted += OnCommandSubmitted;
        _input.TextChanged += OnTextChanged;
        vbox.AddChild(_input);

        // 补全提示 Label（输入栏上方）
        _completionLabel = new RichTextLabel
        {
            Name = "Completion",
            BbcodeEnabled = true,
            FitContent = true,
            ScrollFollowing = true,
        };
        vbox.AddChild(_completionLabel);

        // 注册命令
        RegisterCommands();

        // 构建卡牌缓存
        BuildCardCache();

        WriteLine("[color=#66ff66][DevConsole] 按 ` 键呼出/隐藏。输入 /help 查看命令[/color]");
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo)
        {
            if (keyEvent.Keycode == Key.Quoteleft) // 反引号键
            {
                Toggle();
                GetViewport().SetInputAsHandled();
            }
            else if (_visible && keyEvent.Keycode == Key.Escape)
            {
                Hide();
                GetViewport().SetInputAsHandled();
            }
        }
    }

    // ===== 可见性 =====

    public void Toggle()
    {
        if (_visible) Hide();
        else Show();
    }

    private void Show()
    {
        _visible = true;
        _panel.Visible = true;
        _completionLabel.Visible = true;
        _input.GrabFocus();
        _input.Clear();
        OnTextChanged("");
    }

    private void Hide()
    {
        _visible = false;
        _panel.Visible = false;
        _completionLabel.Visible = false;
        _input.ReleaseFocus();
    }

    // ===== 命令处理 =====

    private void OnCommandSubmitted(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        _input.Clear();
        DevCommand(text);
        _input.GrabFocus();
    }

    /// <summary>
    /// 执行开发者命令。供 UI 输入和 AI 远程调用共用。
    /// </summary>
    /// <param name="cmd">命令字符串，格式: /&lt;action&gt; [参数]</param>
    public void DevCommand(string cmd)
    {
        cmd = cmd.Trim();
        WriteLine($"[color=#aaaaaa]> {cmd}[/color]");

        if (!cmd.StartsWith("/"))
        {
            WriteLine("[color=#ff6644]命令需以 / 开头，输入 /help 查看帮助[/color]");
            return;
        }

        var parts = cmd[1..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return;

        var action = parts[0].ToLowerInvariant();

        try
        {
            Execute(action, parts);
        }
        catch (Exception e)
        {
            WriteLine($"[color=#ff4444]命令执行异常: {e.Message}[/color]");
        }
    }

    /// <summary>
    /// 根据 action 执行对应的游戏操作。
    /// </summary>
    private void Execute(string action, string[] parts)
    {
        var cm = CombatManager.Instance;
        if (cm == null && action != "help" && action != "clear")
        {
            WriteLine("[color=#ffaa44]未在战斗中，此命令需要 CombatManager[/color]");
            return;
        }

        int Arg(int i = 1) => parts.Length > i && int.TryParse(parts[i], out var v) ? v : 1;

        switch (action)
        {
            // ===== 伤害 =====
            case "damage":
            case "dmg":
                if (TryHandleClickDamage(parts, cm!)) return;
                cm!.EnemyHero.TakeDamage(Arg(), null);
                WriteLine($"[color=#ff6644]对敌方英雄造成 {Arg()} 点伤害（剩余 {cm.EnemyHero.CurrentHealth}）[/color]");
                cm.CheckVictoryOrDefeat();
                RefreshCombatUI(cm);
                break;

            // ===== 伤害敌方随从 =====
            case "damage_eslot":
            case "des":
                if (parts.Length < 3)
                { WriteLine("[color=#ffaa44]用法: /damage_eslot <槽位0-4> <伤害值>[/color]"); break; }
                if (!int.TryParse(parts[1], out var eslot) || eslot < 0 || eslot > 4)
                { WriteLine("[color=#ffaa44]槽位需为 0-4[/color]"); break; }
                int edmg = int.Parse(parts[2]);
                var em = cm!.Board.GetMinionAt(eslot, isPlayerSide: false);
                if (em == null || em.IsDead)
                { WriteLine($"[color=#ffaa44]敌方槽位 {eslot} 无有效随从[/color]"); break; }
                em.TakeDamage(edmg, null);
                WriteLine($"[color=#ff6644]对敌方槽位{eslot} {em.CardName} 造成 {edmg} 点伤害（剩余 {em.CurrentHealth}）[/color]");
                if (em.IsDead) { cm.Board.RemoveMinion(em); }
                cm.CheckDeaths();
                cm.CheckVictoryOrDefeat();
                RefreshCombatUI(cm);
                break;

            // ===== 伤害己方随从 =====
            case "damage_pslot":
            case "dps":
                if (parts.Length < 3)
                { WriteLine("[color=#ffaa44]用法: /damage_pslot <槽位0-4> <伤害值>[/color]"); break; }
                if (!int.TryParse(parts[1], out var pslot) || pslot < 0 || pslot > 4)
                { WriteLine("[color=#ffaa44]槽位需为 0-4[/color]"); break; }
                int pdmg = int.Parse(parts[2]);
                var pm = cm!.Board.GetMinionAt(pslot, isPlayerSide: true);
                if (pm == null || pm.IsDead)
                { WriteLine($"[color=#ffaa44]己方槽位 {pslot} 无有效随从[/color]"); break; }
                pm.TakeDamage(pdmg, null);
                WriteLine($"[color=#ff6644]对己方槽位{pslot} {pm.CardName} 造成 {pdmg} 点伤害（剩余 {pm.CurrentHealth}）[/color]");
                if (pm.IsDead) { cm.Board.RemoveMinion(pm); }
                cm.CheckDeaths();
                cm.CheckVictoryOrDefeat();
                RefreshCombatUI(cm);
                break;

            // ===== 伤害己方英雄 =====
            case "damage_self":
            case "dself":
                cm!.PlayerHero.TakeDamage(Arg(), null);
                WriteLine($"[color=#ff6644]对己方英雄造成 {Arg()} 点伤害（剩余 {cm.PlayerHero.CurrentHealth}）[/color]");
                cm.CheckVictoryOrDefeat();
                RefreshCombatUI(cm);
                break;

            // ===== 伤害敌方英雄（显式） =====
            case "damage_enemy":
            case "denemy":
                cm!.EnemyHero.TakeDamage(Arg(), null);
                WriteLine($"[color=#ff6644]对敌方英雄造成 {Arg()} 点伤害（剩余 {cm.EnemyHero.CurrentHealth}）[/color]");
                cm.CheckVictoryOrDefeat();
                RefreshCombatUI(cm);
                break;

            // ===== 伤害全部敌方随从 =====
            case "damage_all":
            case "dall":
                var enemies = cm!.Board.GetEnemyMinions().Where(m => !m.IsDead).ToList();
                foreach (var e in enemies) { e.TakeDamage(Arg(), null);
                    if (e.IsDead) { cm.Board.RemoveMinion(e); } }
                WriteLine($"[color=#ff6644]对所有敌方随从造成 {Arg()} 点伤害（命中 {enemies.Count} 个目标）[/color]");
                cm.CheckDeaths();
                cm.CheckVictoryOrDefeat();
                RefreshCombatUI(cm);
                break;

            // ===== 抽牌 =====
            case "draw":
            case "d":
                cm!.PlayerHero.DrawCards(Arg());
                WriteLine($"[color=#44aaff]抽 {Arg()} 张牌（手牌 {cm.PlayerHero.Hand.Count}）[/color]");
                break;

            // ===== 法力 =====
            case "mana":
            case "m":
                cm!.PlayerHero.GainMana(Arg());
                WriteLine($"[color=#44ddff]获得 {Arg()} 点法力（当前 {cm.PlayerHero.CurrentMana}）[/color]");
                break;

            // ===== 治疗 =====
            case "heal":
            case "h":
                cm!.PlayerHero.Heal(Arg());
                WriteLine($"[color=#44ff44]恢复 {Arg()} 点生命值（当前 {cm.PlayerHero.CurrentHealth}）[/color]");
                break;

            // ===== 护甲 =====
            case "armor":
            case "a":
                cm!.PlayerHero.GainArmor(Arg());
                WriteLine($"[color=#aaaaff]获得 {Arg()} 点护甲（当前 {cm.PlayerHero.CurrentArmor}）[/color]");
                break;

            // ===== 强制结束回合 =====
            case "end":
            case "endturn":
                cm!.EndPlayerTurn();
                WriteLine("[color=#ffaa44]强制结束玩家回合[/color]");
                break;

            // ===== 刷新 UI =====
            case "refresh":
            case "r":
                // 通过场景树查找 CombatUI 并刷新
                var combatUINode = cm!.GetNodeOrNull<Control>("../CanvasLayer/CombatUI");
                if (combatUINode != null)
                {
                    var refreshMethod = combatUINode.GetType().GetMethod("RefreshAll");
                    refreshMethod?.Invoke(combatUINode, null);
                }
                WriteLine("[color=#aaaaaa]UI 已刷新[/color]");
                break;

            // ===== 帮助 =====
            case "help":
            case "?":
                WriteLine("[color=#66ff66]=== 开发者命令 ===");
                WriteLine("  /damage N       — 对敌方英雄造成 N 点伤害");
                WriteLine("  /damage_enemy N — 同上（显式）");
                WriteLine("  /damage_self N  — 对己方英雄造成 N 点伤害");
                WriteLine("  /damage_eslot X N — 对敌方槽位 X(0-4) 随从造成 N 点伤害");
                WriteLine("  /damage_pslot X N — 对己方槽位 X(0-4) 随从造成 N 点伤害");
                WriteLine("  /damage_all N   — 对所有敌方随从造成 N 点伤害");
                WriteLine("  /draw N         — 抽 N 张牌");
                WriteLine("  /mana N         — 获得 N 点法力");
                WriteLine("  /heal N         — 恢复 N 点生命值");
                WriteLine("  /armor N        — 获得 N 点护甲");
                WriteLine("  /end            — 强制结束回合");
                WriteLine("  /refresh        — 刷新 UI");
                WriteLine("  /clear          — 清空输出");
                WriteLine("  /token <id>     — 将指定ID的卡牌加入手牌");
                WriteLine("  /play <id>      — 从手牌打出指定ID卡牌（领域/无目标法术）");
                WriteLine("  /summon_player <id> <slot> — 在己方槽位直接召唤随从（QA）");
                WriteLine("  /intent_debug   — 显示当前敌方意图目标与箭头坐标（QA）");
                WriteLine("  /fight <enemy>  — 直接与指定敌人战斗（跳过地图）");
                WriteLine("  /qa_tombstone   — 验证墓碑伤害结算");
                WriteLine("  /unlock_all     — 解锁全部卡牌（加入收藏）");
                WriteLine("  /help           — 显示帮助[/color]");
                WriteLine("  /tags           — 显示所有卡牌标签分布（QA）[/color]");
                break;

            // ===== 加入手牌 =====
            case "token":
            case "t":
                if (parts.Length < 2)
                {
                    WriteLine("[color=#ffaa44]用法: /token <card_id>  可用ID见补全提示[/color]");
                    break;
                }
                var tokenId = parts[1];
                if (!_cardCache.TryGetValue(tokenId, out var cardData))
                {
                    // 尝试大小写不敏感匹配
                    var match = _cardCache.Keys.FirstOrDefault(k =>
                        string.Equals(k, tokenId, StringComparison.OrdinalIgnoreCase));
                    if (match != null)
                        _cardCache.TryGetValue(match, out cardData);
                }
                if (cardData == null)
                {
                    WriteLine($"[color=#ffaa44]未找到卡牌: {tokenId}  可用ID见补全提示[/color]");
                    break;
                }
                var tokenCard = new OdysseyCards.Card.Card(cardData);
                cm!.AddCardToHand(tokenCard);
                WriteLine($"[color=#66ff66]将「{cardData.CardName}」加入手牌（手牌 {cm.PlayerHero.Hand.Count} 张）[/color]");
                break;

            // ===== 从手牌打出卡牌 =====
            case "play":
            case "p":
                if (parts.Length < 2)
                {
                    WriteLine("[color=#ffaa44]用法: /play <card_id>[/color]");
                    break;
                }
                var playId = parts[1];
                var cardToPlay = cm!.PlayerHero.Hand.FirstOrDefault(c =>
                    string.Equals(c.Id, playId, StringComparison.OrdinalIgnoreCase));
                if (cardToPlay == null)
                {
                    WriteLine($"[color=#ffaa44]手牌中没有卡牌: {playId}[/color]");
                    break;
                }

                bool played = cardToPlay.Type switch
                {
                    CardType.Domain => cm.PlayDomain(cardToPlay),
                    CardType.Spell when !cardToPlay.Data.RequiresTarget => cm.PlaySpell(cardToPlay, cm.PlayerHero),
                    _ => false
                };

                WriteLine(played
                    ? $"[color=#66ff66]打出「{cardToPlay.GetLocalizedName()}」[/color]"
                    : $"[color=#ffaa44]无法通过 /play 打出「{cardToPlay.GetLocalizedName()}」[/color]");
                break;

            // ===== QA：直接召唤玩家随从 =====
            case "summon_player":
            case "sp":
                SummonPlayerMinion(parts, cm!);
                break;

            // ===== QA：显示当前敌方意图目标 =====
            case "intent_debug":
                WriteIntentDebug(cm!);
                break;

            // ===== 清空输出 =====
            case "clear":
            case "cls":
                _output.Clear();
                break;

            // ===== 解锁全部卡牌 =====
            case "unlock_all":
                GameManager.Instance?.UnlockAllCards();
                GameManager.Instance?.SaveToDisk();
                WriteLine("[color=#66ff66]已解锁全部卡牌并保存到磁盘[/color]");
                break;

            // ===== 直接战斗（跳过地图） =====
            case "fight":
                if (parts.Length < 2)
                {
                    WriteLine($"[color=#ffaa44]用法: /fight <enemy>  可用: {string.Join(", ", EnemyRegistry.AllIds)}[/color]");
                    break;
                }
                var fightId = parts[1].ToLowerInvariant();
                var fightEnemies = EnemyRegistry.Create(fightId);
                if (fightEnemies.Count == 0)
                {
                    WriteLine($"[color=#ff6644]未知敌人: {fightId}，可用: {string.Join(", ", EnemyRegistry.AllIds)}[/color]");
                    break;
                }
                GameManager.Instance!.FightOverride = fightEnemies;
                WriteLine($"[color=#66ff66]即将与 {string.Join(", ", fightEnemies.Select(e => e.Name))} 战斗…[/color]");
                GetTree().ChangeSceneToFile("res://Scenes/Combat.tscn");
                break;

            // ===== QA：墓碑伤害结算 =====
            case "qa_tombstone":
                RunTombstoneDamageQa();
                break;

            // ===== QA：诱饵战术 =====
            case "qa_bait_tactics":
                RunBaitTacticsQa(cm!);
                break;

            // ===== 标签分布 =====
            case "tags":
                ShowTagsDistribution();
                break;

            default:
                WriteLine($"[color=#ff6644]未知命令: /{action}，输入 /help 查看帮助[/color]");
                break;
        }
    }

    /// <summary>
    /// QA 命令：直接在玩家槽位召唤指定随从，用于构造自动化验证场景。
    /// </summary>
    private void SummonPlayerMinion(string[] parts, CombatManager cm)
    {
        if (parts.Length < 3)
        {
            WriteLine("[color=#ffaa44]用法: /summon_player <card_id> <slot0-4>[/color]");
            return;
        }

        if (!TryGetCardData(parts[1], out var cardData) || cardData == null)
        {
            WriteLine($"[color=#ffaa44]未找到卡牌: {parts[1]}  可用ID见补全提示[/color]");
            return;
        }

        if (!cardData.IsMinion)
        {
            WriteLine($"[color=#ffaa44]「{cardData.CardName}」不是随从牌[/color]");
            return;
        }

        if (!int.TryParse(parts[2], out var slot) || slot < 0 || slot >= Board.MaxSlotsPerSide)
        {
            WriteLine("[color=#ffaa44]槽位需为 0-4[/color]");
            return;
        }

        var minion = new OdysseyCards.Card.Minion(cardData, isPlayerSide: true);
        cm.Board.PlaceMinion(minion, slot);
        RefreshCombatUI(cm);
        WriteLine($"[color=#66ff66]已在己方槽位 {slot} 召唤「{minion.GetLocalizedName()}」[/color]");
    }

    /// <summary>
    /// QA 命令：输出敌方意图目标与箭头坐标快照。
    /// </summary>
    private void WriteIntentDebug(CombatManager cm)
    {
        for (int i = 0; i < cm.EnemyUnits.Count; i++)
        {
            var unit = cm.EnemyUnits[i];
            var intent = unit.GetCurrentIntent(cm);
            var target = intent.GetTarget(cm);
            WriteLine($"[color=#66ff66]Enemy[{i}] {unit.Brain.Name}: {intent.Type} -> {DescribeTarget(target)}, damage={intent.GetEffectiveDamage(cm)}[/color]");
        }

        var combatUI = cm.GetNodeOrNull<CombatUI>("CanvasLayer/CombatUI");
        var arrows = combatUI?.GetIntentArrowDebugInfo();
        WriteLine(string.IsNullOrEmpty(arrows)
            ? "[color=#aaaaaa]Arrows: <none>[/color]"
            : $"[color=#aaaaaa]Arrows:\n{arrows}[/color]");
    }

    private bool TryGetCardData(string cardId, out CardData? cardData)
    {
        if (_cardCache.TryGetValue(cardId, out cardData))
            return true;

        var match = _cardCache.Keys.FirstOrDefault(k =>
            string.Equals(k, cardId, StringComparison.OrdinalIgnoreCase));
        return match != null && _cardCache.TryGetValue(match, out cardData);
    }

    private static string DescribeTarget(IDamageTarget? target)
    {
        return target switch
        {
            OdysseyCards.Card.Hero h => h.IsPlayerSide ? "Hero:Player" : "Hero:Enemy",
            OdysseyCards.Card.Minion m => $"Minion:{m.GetLocalizedName()}@{m.BoardSlotIndex}",
            null => "<none>",
            _ => target.GetType().Name,
        };
    }

    // ===== 输出 =====

    private void WriteLine(string text)
    {
        _output.AppendText($"{text}\n");
    }

    /// <summary>
    /// 通过场景树查找 CombatUI 并调用 RefreshAll，确保 UI 反映最新状态。
    /// </summary>
    private static void RefreshCombatUI(CombatManager cm)
    {
        var ui = cm.GetNodeOrNull<Control>("CanvasLayer/CombatUI");
        if (ui != null)
        {
            var m = ui.GetType().GetMethod("RefreshAll");
            m?.Invoke(ui, null);
        }
    }

    /// <summary>
    /// 验证墓碑的关键伤害规则：
    /// 1. 效果伤害无视防御力，但仍计算来源侧造成伤害加成；
    /// 2. 攻击伤害先计算造成伤害加成，再受目标防御力减免。
    /// 3. 效果伤害不触发英雄武器反击；武器反击伤害必须正常吃墓碑防御力。
    /// </summary>
    private void RunTombstoneDamageQa()
    {
        var tombstoneData = GD.Load<CardData>("res://Resources/Cards/Minion_Tombstone.tres");
        if (tombstoneData == null)
        {
            WriteLine("[color=#ff6644]QA失败：无法加载墓碑资源[/color]");
            return;
        }

        var tombstone = new OdysseyCards.Card.Minion(tombstoneData, isPlayerSide: true);
        var defendedTargetData = new CardData
        {
            Id = "qa_defended_target",
            CardName = "QA防御目标",
            Attack = 1,
            Health = 20,
            Defense = 1,
        };
        var defendedTarget = new OdysseyCards.Card.Minion(defendedTargetData, isPlayerSide: false);

        int battlecryDamage = DamageResolver.ResolveDamage(1, tombstone, defendedTarget, DamageKind.Effect);
        int attackDamage = DamageResolver.ResolveDamage(tombstone.Attack, tombstone, defendedTarget, DamageKind.Attack);

        var friendlyHeroCore = new OdysseyCards.Character.CommanderCore();
        friendlyHeroCore.InitializeHealth(30);
        var friendlyHero = new OdysseyCards.Card.Hero(friendlyHeroCore, isPlayerSide: true)
        {
            Weapon = new OdysseyCards.Card.IonPistol(),
        };
        friendlyHero.ModifyDefense(1);
        friendlyHero.TakeDamage(1, tombstone, DamageKind.Effect);
        bool effectDidNotCounter = tombstone.CurrentHealth == tombstone.MaxHealth;
        bool effectDamageResolved = friendlyHero.CurrentHealth == 27;

        var counterHeroCore = new OdysseyCards.Character.CommanderCore();
        counterHeroCore.InitializeHealth(30);
        var counterHero = new OdysseyCards.Card.Hero(counterHeroCore, isPlayerSide: false)
        {
            Weapon = new OdysseyCards.Card.RollingLog(),
        };
        counterHero.TakeDamage(tombstone.Attack, tombstone, DamageKind.Attack);
        bool counterDamageUsedDefense = tombstone.CurrentHealth == tombstone.MaxHealth;

        bool passed = battlecryDamage == 3
            && attackDamage == 8
            && effectDamageResolved
            && effectDidNotCounter
            && counterDamageUsedDefense;

        WriteLine(passed
            ? $"[color=#66ff66]墓碑QA通过：战吼效果={battlecryDamage}，攻击={attackDamage}，Effect不反击={effectDidNotCounter}，反击吃防={counterDamageUsedDefense}[/color]"
            : $"[color=#ff6644]墓碑QA失败：战吼效果={battlecryDamage}（期望3），攻击={attackDamage}（期望8），Effect后墓碑血={tombstone.CurrentHealth}（期望{tombstone.MaxHealth}），友方英雄血={friendlyHero.CurrentHealth}（期望27）[/color]");
    }

    /// <summary>
    /// 验证「诱饵战术」：法术可指定任意随从，且不论目标阵营，被攻击时都降低玩家敌方的英雄防御力。
    /// </summary>
    private void RunBaitTacticsQa(CombatManager cm)
    {
        var baitData = GD.Load<CardData>("res://Resources/Cards/Spell_BaitTactics.tres");
        var playerMinionData = GD.Load<CardData>("res://Resources/Cards/Minion_18thRegiment.tres");
        var enemyMinionData = GD.Load<CardData>("res://Resources/Cards/Minion_Slime.tres");

        if (baitData == null || playerMinionData == null || enemyMinionData == null)
        {
            WriteLine("[color=#ff6644]诱饵战术QA失败：无法加载所需卡牌资源[/color]");
            return;
        }

        cm.PlayerHero.GainMana(20);
        int initialDefense = cm.EnemyHero.Defense;

        var friendlyTarget = new OdysseyCards.Card.Minion(playerMinionData, isPlayerSide: true);
        var enemyAttacker = new OdysseyCards.Card.Minion(enemyMinionData, isPlayerSide: false);
        var friendlySpell = new OdysseyCards.Card.Card(baitData);
        cm.AddCardToHand(friendlySpell);
        bool friendlySpellPlayed = cm.PlaySpell(friendlySpell, friendlyTarget);
        bool friendlyBuffApplied = friendlyTarget.HasAmbush && friendlyTarget.HasImpact && friendlyTarget.HasBaitTacticsOnAttacked;
        cm.ResolveMinionCombat(enemyAttacker, friendlyTarget);
        bool friendlyTriggerWorked = cm.EnemyHero.Defense == initialDefense - 1;

        var enemyTarget = new OdysseyCards.Card.Minion(enemyMinionData, isPlayerSide: false);
        var playerAttacker = new OdysseyCards.Card.Minion(playerMinionData, isPlayerSide: true);
        var enemySpell = new OdysseyCards.Card.Card(baitData);
        cm.AddCardToHand(enemySpell);
        bool enemySpellPlayed = cm.PlaySpell(enemySpell, enemyTarget);
        bool enemyBuffApplied = enemyTarget.HasAmbush && enemyTarget.HasImpact && enemyTarget.HasBaitTacticsOnAttacked;
        cm.ResolveMinionCombat(playerAttacker, enemyTarget);
        bool enemyTriggerWorked = cm.EnemyHero.Defense == initialDefense - 2;

        RefreshCombatUI(cm);

        WriteLine(friendlySpellPlayed
            && friendlyBuffApplied
            && friendlyTriggerWorked
            && enemySpellPlayed
            && enemyBuffApplied
            && enemyTriggerWorked
            ? $"[color=#66ff66]诱饵战术QA通过：友方目标触发、敌方目标触发，玩家敌方的英雄防御 {initialDefense}→{cm.EnemyHero.Defense}[/color]"
            : $"[color=#ff6644]诱饵战术QA失败：friendlySpell={friendlySpellPlayed}, friendlyBuff={friendlyBuffApplied}, friendlyTrigger={friendlyTriggerWorked}, enemySpell={enemySpellPlayed}, enemyBuff={enemyBuffApplied}, enemyTrigger={enemyTriggerWorked}, defense={cm.EnemyHero.Defense}[/color]");
    }

    /// <summary>
    /// 进入点击伤害模式：隐藏控制台，通过 CombatUI 进入交互模式。
    /// </summary>
    private void EnterClickDamageMode(int damageAmount)
    {
        Hide();

        var cm = CombatManager.Instance;
        if (cm == null) { Show(); return; }

        var combatUI = cm.GetNodeOrNull<CombatUI>("CanvasLayer/CombatUI");
        if (combatUI == null) { Show(); return; }

        combatUI.EnterDevDamageMode(damageAmount);
        combatUI.OnDevDamageModeCompleted += () =>
        {
            WriteLine("[color=#66ff66]伤害选择完成[/color]");
            Show();
        };
    }

    /// <summary>
    /// 处理 /damage -c N 命令：进入点击伤害模式。
    /// </summary>
    private bool TryHandleClickDamage(string[] parts, CombatManager cm)
    {
        // /damage -c N → 交互式点击伤害
        if (parts.Length >= 3 && parts[1] == "-c" && int.TryParse(parts[2], out int dmg))
        {
            EnterClickDamageMode(dmg);
            return true;
        }
        return false;
    }

    // ===== 命令注册与补全 =====

    private void RegisterCommands()
    {
        _commands.AddRange(new[]
        {
            new DevCommandDef("damage",       ["dmg"],    "/damage [-c] N",       "对敌方英雄造成 N 点伤害",     ["N", "-c N（点击模式）"]),
            new DevCommandDef("damage_enemy", ["denemy"], "/damage_enemy N",      "对敌方英雄造成 N 点伤害（显式）", ["N"]),
            new DevCommandDef("damage_self",  ["dself"],  "/damage_self N",       "对己方英雄造成 N 点伤害",     ["N"]),
            new DevCommandDef("damage_eslot", ["des"],    "/damage_eslot X N",    "对敌方槽位 X(0-4) 随从造成 N 点伤害", ["X(0-4)", "N"]),
            new DevCommandDef("damage_pslot", ["dps"],    "/damage_pslot X N",    "对己方槽位 X(0-4) 随从造成 N 点伤害", ["X(0-4)", "N"]),
            new DevCommandDef("damage_all",   ["dall"],   "/damage_all N",        "对所有敌方随从造成 N 点伤害",   ["N"]),
            new DevCommandDef("draw",         ["d"],      "/draw N",              "抽 N 张牌",                  ["N"]),
            new DevCommandDef("mana",         ["m"],      "/mana N",              "获得 N 点法力",              ["N"]),
            new DevCommandDef("heal",         ["h"],      "/heal N",              "恢复 N 点生命值",            ["N"]),
            new DevCommandDef("armor",        ["a"],      "/armor N",             "获得 N 点护甲",              ["N"]),
            new DevCommandDef("end",          ["endturn"],"/end",                 "强制结束回合",               null),
            new DevCommandDef("refresh",      ["r"],      "/refresh",             "刷新 UI",                   null),
            new DevCommandDef("clear",        ["cls"],    "/clear",               "清空输出",                   null),
            new DevCommandDef("token",        ["t"],      "/token <card_id>",     "将指定ID的卡牌加入手牌",        _cardCache.Keys.ToArray()),
            new DevCommandDef("play",         ["p"],      "/play <card_id>",      "从手牌打出领域/无目标法术",      _cardCache.Keys.ToArray()),
            new DevCommandDef("summon_player",["sp"],     "/summon_player <card_id> <slot>", "在己方槽位直接召唤随从（QA）", _cardCache.Keys.ToArray()),
            new DevCommandDef("intent_debug", [],          "/intent_debug",        "显示当前敌方意图目标（QA）",     null),
            new DevCommandDef("qa_bait_tactics", [],       "/qa_bait_tactics",     "验证诱饵战术双阵营触发（QA）",     null),
            new DevCommandDef("unlock_all",   [],         "/unlock_all",          "解锁全部卡牌（加入收藏）",     null),
            new DevCommandDef("fight",        [],         "/fight <enemy>",       "直接与指定敌人战斗（跳过地图）",  EnemyRegistry.AllIds.ToArray()),
            new DevCommandDef("help",         ["?"],      "/help",                "显示帮助",                   null),
            new DevCommandDef("tags",         [],         "/tags",                "显示所有卡牌标签分布（QA）",     null),
        });
    }

    /// <summary>
    /// 构建卡牌 ID → CardData 缓存。扫描 Resources/Cards/ 目录。
    /// </summary>
    private void BuildCardCache()
    {
        _cardCache.Clear();

        // 从 GameManager 注册表构建缓存（编辑器和导出版本均可用）
        var allCards = Core.GameManager.Instance.GetAllCards();
        foreach (var cardData in allCards)
        {
            if (cardData != null && !string.IsNullOrEmpty(cardData.Id))
                _cardCache[cardData.Id] = cardData;
        }

        GD.Print($"[DevConsole] 卡牌缓存已构建，共 {_cardCache.Count} 张");
    }

    /// <summary>
    /// 输入框文本变化时更新补全提示。
    /// </summary>
    private void OnTextChanged(string text)
    {
        _completionLabel.Text = GetCompletionHint(text);
    }

    /// <summary>
    /// 根据当前输入生成补全提示文本。
    /// </summary>
    private string GetCompletionHint(string input)
    {
        if (string.IsNullOrEmpty(input) || !input.StartsWith("/"))
            return "";

        var content = input[1..]; // 去掉 /
        var spaceIdx = content.IndexOf(' ');
        var partialCmd = spaceIdx < 0 ? content.ToLowerInvariant() : content[..spaceIdx].ToLowerInvariant();
        var argPart = spaceIdx < 0 ? "" : content[(spaceIdx + 1)..].TrimStart();

        // 还没输入完整命令名 → 显示匹配的命令
        if (spaceIdx < 0 || string.IsNullOrEmpty(argPart))
        {
            var matches = _commands
                .Where(c => c.Name.StartsWith(partialCmd) || c.Aliases.Any(a => a.StartsWith(partialCmd)))
                .Take(6)
                .ToList();

            if (matches.Count == 0)
                return $"[color=#ff6644]未知命令: /{partialCmd}[/color]";

            var lines = matches.Select(m =>
            {
                var aliasStr = m.Aliases.Length > 0 ? $"（别名: {string.Join(", ", m.Aliases)}）" : "";
                return $"[color=#66ff66]/{m.Name}[/color] [color=#aaaaaa]{m.Signature.Split(' ', 2).ElementAtOrDefault(1) ?? ""}[/color] [color=#888888]— {m.Description}{aliasStr}[/color]";
            });
            return string.Join("\n", lines);
        }

        // 已输入命令名 + 空格 → 显示参数提示
        var cmd = _commands.FirstOrDefault(c =>
            c.Name.Equals(partialCmd, StringComparison.OrdinalIgnoreCase) ||
            c.Aliases.Any(a => a.Equals(partialCmd, StringComparison.OrdinalIgnoreCase)));

        if (cmd == null)
            return "";

        // token 命令特殊处理：显示可用 card_id
        if (partialCmd is "token" or "t")
        {
            var filtered = _cardCache.Keys
                .Where(id => id.StartsWith(argPart, StringComparison.OrdinalIgnoreCase))
                .OrderBy(id => id)
                .Take(8)
                .ToList();

            if (filtered.Count == 0)
                return $"[color=#ffaa44]无匹配的卡牌ID[/color]";

            var lines = filtered.Select(id =>
            {
                var c = _cardCache[id];
                return $"  [color=#66ff66]{id}[/color] [color=#aaaaaa]— {c.CardName}（{c.Cost}费）[/color]";
            });
            return "[color=#aaaaaa]可用卡牌ID:[/color]\n" + string.Join("\n", lines);
        }

        // 通用参数提示
        if (cmd.ArgHints != null && cmd.ArgHints.Length > 0)
        {
            var hints = string.Join("  ", cmd.ArgHints);
            return $"[color=#aaaaaa]参数: {hints}[/color]";
        }

        return "";
    }

    /// <summary>
    /// 显示所有卡牌的标签分布。扫描 _cardCache 按 CardTag 分组。
    /// </summary>
    private void ShowTagsDistribution()
    {
        WriteLine("[color=#66ff66]=== 标签分布 ===[/color]");

        // 按 CardTag 枚举值分组
        var allTags = Enum.GetValues<OdysseyCards.Core.CardTag>();
        foreach (var tag in allTags)
        {
            if (tag == OdysseyCards.Core.CardTag.None) continue;

            var cards = _cardCache.Values
                .Where(c => c.Tags.HasFlag(tag))
                .OrderBy(c => c.CardName)
                .ToList();

            WriteLine($"  [color=#ffcc44]{tag}[/color] ({cards.Count} 张):");
            foreach (var c in cards)
            {
                WriteLine($"    [color=#66ff66]{c.Id}[/color] [color=#aaaaaa]— {c.CardName}（{c.Cost}费 {c.Type}）[/color]");
            }
        }

        // 无标签卡牌
        var untagged = _cardCache.Values
            .Where(c => c.Tags == OdysseyCards.Core.CardTag.None)
            .ToList();
        WriteLine($"  [color=#aaaaaa]无标签[/color] ({untagged.Count} 张)");
    }
}
