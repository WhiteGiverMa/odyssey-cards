using System.Collections.Generic;
using Godot;
using OdysseyCards.AI.Intents;
using OdysseyCards.Card;
using OdysseyCards.Combat;

namespace OdysseyCards.UI;

/// <summary>
/// 敌方英雄身份卡——单个敌人的紧凑信息面板。
/// 显示名称、HP 条、护甲、防御、武器、状态效果、意图。
/// 多敌人时 CombatUI 按敌人数量生成一排卡片。
/// </summary>
public partial class EnemyIdentityCard : Panel
{
    public int EnemyIndex { get; }

    private readonly Label _nameLabel;
    private readonly HealthBar _healthBar;
    private readonly Label _armorLabel;
    private readonly Label _defenseLabel;
    private readonly Label _weaponLabel;
    private readonly Label _intentLabel;
    private readonly Button _attackButton;
    private readonly Button _spellButton;
    private readonly HBoxContainer _statusContainer;
    private readonly EffectBar _effectBar;
    private readonly HBoxContainer _intentIconContainer;

    // Colors
    private static readonly Color _nameColor = new(1f, 0.5f, 0.5f);
    private static readonly Color _armorColor = new(0.7f, 0.7f, 0.3f);
    private static readonly Color _defenseColor = new(0.3f, 0.7f, 1f);
    private static readonly Color _intentColor = new(1f, 0.4f, 0.4f);
    private static readonly Color _bgColor = new(0.15f, 0.1f, 0.1f, 0.85f);
    private static readonly Color _borderColor = new(0.4f, 0.2f, 0.2f, 0.9f);

    public EnemyIdentityCard(int enemyIndex, CombatManager combat)
    {
        EnemyIndex = enemyIndex;
        var unit = combat.EnemyUnits[enemyIndex];

        // Panel setup
        CustomMinimumSize = new Vector2(180, 110);
        SizeFlagsHorizontal = SizeFlags.ExpandFill;

        // --- Style: border via StyleBoxFlat ---
        var style = new StyleBoxFlat
        {
            BgColor = _bgColor,
            BorderColor = _borderColor,
            BorderWidthLeft = 2,
            BorderWidthRight = 2,
            BorderWidthTop = 2,
            BorderWidthBottom = 2,
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
            CornerRadiusBottomLeft = 6,
            CornerRadiusBottomRight = 6,
        };
        AddThemeStyleboxOverride("panel", style);

        // --- Content VBox ---
        var content = new VBoxContainer
        {
            Name = "CardContent",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.Fill,
        };
        AddChild(content);

        // Row 1: Name + HP
        var row1 = new HBoxContainer();
        _nameLabel = new Label
        {
            Text = unit.Brain.Name,
            CustomMinimumSize = new Vector2(50, 0),
        };
        _nameLabel.AddThemeColorOverride("font_color", _nameColor);
        _nameLabel.AddThemeFontSizeOverride("font_size", 13);
        row1.AddChild(_nameLabel);

        _healthBar = new HealthBar
        {
            CustomMinimumSize = new Vector2(60, 10),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        _healthBar.UpdateHealth(unit.Body.CurrentHealth, unit.Body.MaxHealth);
        row1.AddChild(_healthBar);
        content.AddChild(row1);

        // Row 2: Armor + Defense
        var row2 = new HBoxContainer();
        _armorLabel = new Label { Text = "", Visible = false };
        _armorLabel.AddThemeColorOverride("font_color", _armorColor);
        _armorLabel.AddThemeFontSizeOverride("font_size", 12);
        row2.AddChild(_armorLabel);

        _defenseLabel = new Label { Text = "", Visible = false };
        _defenseLabel.AddThemeColorOverride("font_color", _defenseColor);
        _defenseLabel.AddThemeFontSizeOverride("font_size", 11);
        row2.AddChild(_defenseLabel);
        content.AddChild(row2);

        // Row 3: Weapon
        _weaponLabel = new Label { Text = "" };
        _weaponLabel.AddThemeFontSizeOverride("font_size", 11);
        content.AddChild(_weaponLabel);

        // Row 4: Intent
        _intentLabel = new Label { Text = "" };
        _intentLabel.AddThemeColorOverride("font_color", _intentColor);
        _intentLabel.AddThemeFontSizeOverride("font_size", 12);
        content.AddChild(_intentLabel);

        // Row 5: Status effects
        _statusContainer = new HBoxContainer();
        content.AddChild(_statusContainer);
        _effectBar = new EffectBar();
        content.AddChild(_effectBar);

        // Intent icon container (for new MoveState system)
        _intentIconContainer = new HBoxContainer
        {
            Name = "IntentIcons",
            Alignment = BoxContainer.AlignmentMode.Center,
            Visible = false,
        };
        _intentIconContainer.AddThemeConstantOverride("separation", -8); // overlap like STS2
        content.AddChild(_intentIconContainer);

        // Row 6: Target buttons (attack/spell)
        var btnRow = new HBoxContainer();
        _attackButton = new Button
        {
            Text = "⚔",
            CustomMinimumSize = new Vector2(28, 22),
            Flat = true,
            Visible = false,
        };
        _attackButton.AddThemeFontSizeOverride("font_size", 11);
        btnRow.AddChild(_attackButton);

        _spellButton = new Button
        {
            Text = "✦",
            CustomMinimumSize = new Vector2(28, 22),
            Flat = true,
            Visible = false,
        };
        _spellButton.AddThemeFontSizeOverride("font_size", 11);
        btnRow.AddChild(_spellButton);
        content.AddChild(btnRow);

        // Mouse filter
        MouseFilter = MouseFilterEnum.Stop;
    }

    /// <summary>攻击按钮点击事件。</summary>
    public Button AttackButton => _attackButton;

    /// <summary>法术按钮点击事件。</summary>
    public Button SpellButton => _spellButton;

    /// <summary>更新面板中所有数据。</summary>
    public void Refresh(CombatManager combat)
    {
        var unit = combat.EnemyUnits[EnemyIndex];
        var body = unit.Body;
        var brain = unit.Brain;

        // Name
        _nameLabel.Text = brain.Name;

        // HP
        RefreshHealth(body.CurrentHealth, body.MaxHealth);

        // Armor
        RefreshArmor(body.CurrentArmor);

        // Defense
        int def = body.Defense;
        _defenseLabel.Visible = def != 0;
        if (def != 0)
        {
            _defenseLabel.Text = Localization.Localization.T("ui.combat.defense_format", "防御: {value}").Replace("{value}", def >= 0 ? $"+{def}" : $"{def}");
            _defenseLabel.AddThemeColorOverride("font_color", def > 0 ? new Color(0.3f, 0.7f, 1f) : new Color(1f, 0.3f, 0.3f));
        }

        // Weapon
        var weapon = body.Weapon;
        if (weapon != null)
        {
            string disabledText = weapon.IsDisabled
                ? $" [{Localization.Localization.T("ui.combat.weapon_disabled", "禁用")}]"
                : "";
            _weaponLabel.Text = $"{weapon.Name} {weapon.Attack}{Localization.Localization.T("ui.combat.attack_suffix", "攻")}{disabledText}";
        }
        else
        {
            _weaponLabel.Text = "";
        }

        // Intent display - new or old system
        var move = brain.GetCurrentMove(combat, body);
        if (brain.HasMoveStates)
        {
            // New system: show intent icons
            _intentLabel.Visible = false;
            _intentIconContainer.Visible = true;
            UpdateIntentIcons(move.Intents, combat);
        }
        else
        {
            // Old system: show text label (backward compat)
            _intentIconContainer.Visible = false;
            _intentLabel.Visible = true;
            var intent = brain.GetCurrentIntent(combat, body);
            _intentLabel.Text = intent.GetDisplayDescription(combat);
        }

        // Status effects — old text display kept for backward compat
        foreach (var child in _statusContainer.GetChildren())
            child.QueueFree();
        foreach (var (id, effect) in body.StatusEffects)
        {
            var badge = new Label
            {
                Text = $"[{effect.Stacks}]",
                CustomMinimumSize = new Vector2(18, 14),
            };
            badge.AddThemeColorOverride("font_color", new Color(0.8f, 0.5f, 1f));
            badge.AddThemeFontSizeOverride("font_size", 9);
            _statusContainer.AddChild(badge);
        }

        // EffectBar — unified display for all effect types
        _effectBar.Populate(body.GetDisplayableEffects());
    }

    /// <summary>仅刷新血量条和标签（轻量版，不重新计算意图）。</summary>
    public void RefreshHealth(int current, int max)
    {
        _healthBar.UpdateHealth(current, max);
    }

    /// <summary>仅刷新护甲显示（轻量版）。</summary>
    public void RefreshArmor(int armor)
    {
        _armorLabel.Visible = armor > 0;
        if (armor > 0)
            _armorLabel.Text = Localization.Localization.T("ui.combat.armor_format", "护甲: {value}").Replace("{value}", armor.ToString());
    }

    /// <summary>
    /// Diff-based update: reconcile intent icon children with current intent list.
    /// Reuses existing IntentIcon nodes, creates new ones, removes extras.
    /// Hooks up hover events to show IntentTooltip.
    /// </summary>
    private void UpdateIntentIcons(IReadOnlyList<AbstractIntent> intents, CombatManager combat)
    {
        int newCount = intents.Count;
        int currentCount = _intentIconContainer.GetChildCount();

        // Remove extra icons
        while (_intentIconContainer.GetChildCount() > newCount)
        {
            var child = _intentIconContainer.GetChild(_intentIconContainer.GetChildCount() - 1);
            _intentIconContainer.RemoveChild(child);
            child.QueueFree();
        }

        // Update or create icons
        for (int i = 0; i < newCount; i++)
        {
            var intent = intents[i];
            int typeId = AbstractIntent.GetIconTypeId(intent.Type);
            string label = intent.GetIntentLabel(combat);
            int value = (intent is AttackIntent atk) ? atk.GetSingleDamage(combat) : 0;
            var tip = intent.GetHoverTip(combat);

            IntentIcon icon;
            if (i < currentCount)
            {
                // Reuse existing icon
                icon = (IntentIcon)_intentIconContainer.GetChild(i);
                icon.UpdateIntent(typeId, label, value);
            }
            else
            {
                // Create new icon
                icon = new IntentIcon(typeId, label, value);
                icon.OnHovered += OnIntentIconHovered;
                icon.OnUnhovered += OnIntentIconUnhovered;
                _intentIconContainer.AddChild(icon);
            }

            icon.SetMeta("tip_title", tip.Title ?? label);
            icon.SetMeta("tip_desc", tip.Description);
            icon.SetMeta("tip_is_debuff", tip.IsDebuff);
        }
    }

    private void OnIntentIconHovered(IntentIcon icon)
    {
        // Find the tooltip layer on the root
        var root = GetTree()?.Root;
        if (root == null) return;

		// Look for IntentTooltipContent (Control child of CanvasLayer)
		var tipLayer = root.FindChild("IntentTooltipContent", recursive: true, owned: false) as Control;
        if (tipLayer == null) return;

        string title = icon.GetMeta("tip_title", icon.GetLabelText()).AsString();
        string desc = icon.GetMeta("tip_desc", "").AsString();
        bool isDebuff = icon.GetMeta("tip_is_debuff", false).AsBool();
        var color = IntentTooltip.GetAccentColor(icon.GetIntentTypeId());

        IntentTooltip.Show(tipLayer, icon.GlobalPosition, title, desc, isDebuff, color);
    }

    private void OnIntentIconUnhovered(IntentIcon icon)
    {
        IntentTooltip.HideCurrent();
    }
}
