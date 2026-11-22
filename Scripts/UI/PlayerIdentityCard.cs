#nullable enable
using System.Collections.Generic;
using Godot;
using OdysseyCards.Core;
using Loc = OdysseyCards.Localization.Localization;

namespace OdysseyCards.UI;

/// <summary>
/// 玩家英雄身份卡——与 EnemyIdentityCard 对应，将玩家英雄面板、生命值条、
/// 护甲、防御和效果图标统一包装为带边框的卡片控件。
/// </summary>
public partial class PlayerIdentityCard : Control
{
	// ===== 样式常量 =====
	private static readonly Color _bgColor = new(0.08f, 0.1f, 0.18f, 0.85f);
	private static readonly Color _borderColor = new(0.25f, 0.5f, 0.9f);

	// ===== 基础字号（实际字号 = 基础字号 × 界面字体倍率） =====
	private const int NameBaseFontSize = 13;
	private const int SpellButtonBaseFontSize = 11;
	private const int ArmorBaseFontSize = 12;
	private const int DefenseBaseFontSize = 11;

	// ===== 子控件 =====
	private Panel _panel = null!;
	private Label _nameLabel = null!;
	private Label _armorLabel = null!;
	private Label _defenseLabel = null!;
	public HealthBar HealthBar { get; private set; } = null!;
	public Button HeroSpellButton { get; private set; } = null!;
	public EffectBar EffectBar { get; private set; } = null!;

	public PlayerIdentityCard()
	{
		CustomMinimumSize = new Vector2(160, 82);
		SizeFlagsHorizontal = SizeFlags.ExpandFill;

		// 面板 StyleBox（蓝色系，区分红色敌方卡）
		var panelStyle = new StyleBoxFlat
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

		_panel = new Panel();
		_panel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		_panel.AddThemeStyleboxOverride("panel", panelStyle);
		AddChild(_panel);

		// 内容 VBox
		var content = new VBoxContainer
		{
			Name = "CardContent",
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.Fill,
			MouseFilter = MouseFilterEnum.Pass,
		};
		_panel.AddChild(content);
		content.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		content.AddThemeConstantOverride("separation", 1);

		// Row 1: 底座光环 + Name + HeroSpellButton
		var nameRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		var aura = new HeroAuraRing
		{
			RingColor = new Color("#7fd8ff"), // 玩家青金
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
		};
		nameRow.AddChild(aura);

		_nameLabel = new Label
		{
			Text = Loc.T("ui.combat.player_hero", "我方英雄"),
			HorizontalAlignment = HorizontalAlignment.Center,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		_nameLabel.AddThemeColorOverride("font_color", new Color(0.5f, 0.7f, 1f));
		nameRow.AddChild(_nameLabel);

		HeroSpellButton = new Button
		{
			Name = "PlayerHeroSpellButton",
			Text = UiSymbolMode.Prefix(Loc.T("ui.combat.spell_player_hero", "对己方英雄施法"), "✦ "),
			CustomMinimumSize = new Vector2(120, 24),
			Visible = false,
		};
		HeroSpellButton.AddThemeColorOverride("font_color", new Color(0.5f, 0.7f, 1f));
		nameRow.AddChild(HeroSpellButton);
		content.AddChild(nameRow);

		// Row 2: HealthBar
		HealthBar = new HealthBar
		{
			CustomMinimumSize = new Vector2(60, 16),
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		content.AddChild(HealthBar);

		// Row 3: Armor + Defense
		var statsRow = new HBoxContainer();
		_armorLabel = new Label { Text = "", Visible = false };
		_armorLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.3f));
		statsRow.AddChild(_armorLabel);

		_defenseLabel = new Label { Text = "", Visible = false };
		_defenseLabel.AddThemeColorOverride("font_color", new Color(0.3f, 0.7f, 1f));
		statsRow.AddChild(_defenseLabel);
		content.AddChild(statsRow);

		// Row 4: EffectBar
		EffectBar = new EffectBar();
		content.AddChild(EffectBar);

		ApplyFontScale();
	}

	/// <summary>按界面字体倍率刷新身份卡内所有显式字号（复用同一控件时不重建节点）。</summary>
	public void ApplyFontScale()
	{
		_nameLabel.AddThemeFontSizeOverride("font_size", UIScaler.ApplyFontScale(NameBaseFontSize));
		HeroSpellButton.AddThemeFontSizeOverride("font_size", UIScaler.ApplyFontScale(SpellButtonBaseFontSize));
		_armorLabel.AddThemeFontSizeOverride("font_size", UIScaler.ApplyFontScale(ArmorBaseFontSize));
		_defenseLabel.AddThemeFontSizeOverride("font_size", UIScaler.ApplyFontScale(DefenseBaseFontSize));
	}

	/// <summary>完整刷新——更新生命值、护甲、防御和效果图标。</summary>
	public void Refresh(int currentHealth, int maxHealth, int armor, int defense,
		IReadOnlyList<DisplayableEffect> displayableEffects)
	{
		HealthBar.UpdateHealth(currentHealth, maxHealth);
		RefreshArmor(armor);
		RefreshDefense(defense);
		EffectBar.Populate(displayableEffects);
	}

	/// <summary>仅刷新血量条。</summary>
	public void RefreshHealth(int current, int max)
	{
		HealthBar.UpdateHealth(current, max);
	}

	/// <summary>仅刷新护甲显示。</summary>
	public void RefreshArmor(int armor)
	{
		_armorLabel.Visible = armor > 0;
		if (armor > 0)
			_armorLabel.Text = Loc.T("ui.combat.armor_format", "护甲: {value}").Replace("{value}", armor.ToString());
	}

	/// <summary>仅刷新防御显示。</summary>
	public void RefreshDefense(int defense)
	{
		_defenseLabel.Visible = defense != 0;
		if (defense != 0)
		{
			_defenseLabel.Text = Loc.T("ui.combat.defense_format", "防御: {value}").Replace("{value}", defense >= 0 ? $"+{defense}" : $"{defense}");
			_defenseLabel.AddThemeColorOverride("font_color",
				defense > 0 ? new Color(0.3f, 0.7f, 1f) : new Color(1f, 0.3f, 0.3f));
		}
	}

	/// <summary>仅刷新效果图标。</summary>
	public void RefreshEffects(IReadOnlyList<DisplayableEffect> effects)
	{
		EffectBar.Populate(effects);
	}

	/// <summary>获取生命值条屏幕中心坐标，供意图箭头绘制使用。</summary>
	public Vector2 GetHealthBarScreenCenter()
	{
		var rect = HealthBar.GetGlobalRect();
		return new Vector2(rect.Position.X + rect.Size.X / 2, rect.Position.Y + rect.Size.Y / 2);
	}

	public override void _ExitTree()
	{
		base._ExitTree();
	}
}
