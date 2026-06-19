using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using OdysseyCards.Character;
using OdysseyCards.Core;
using OdysseyCards.Infrastructure;
using OdysseyCards.Roguelike;
using Loc = OdysseyCards.Localization.Localization;

namespace OdysseyCards.UI;

/// <summary>
/// 主题卡组选择覆盖层。
/// 玩家以 0 牌开始 roguelite 模式时，展示 3 套随机主题卡组（绮梦/理惠/溯光）供选择。
/// 纯代码构建，不依赖 .tscn。
/// </summary>
public partial class ThemedDeckSelectUI : Control
{
	// ===== 回调 =====

	private Action<string?>? _onDeckChosen;

	// ===== 状态 =====

	private ulong _openedTicks;
	private const ulong ClickProtectionMs = 350;

	/// <summary>已预生成的 3 套管卡组（Key=heroId）。</summary>
	private readonly Dictionary<string, ThemedDeckGenerator.GenerationResult> _generatedDecks = new();

	/// <summary>已预生成卡组对应的 CardData 列表（Key=heroId）。</summary>
	private readonly Dictionary<string, List<CardData>> _generatedCards = new();

	// ===== UI 控件 =====

	private ColorRect _background = null!;
	private Button _cancelButton = null!;
	private readonly List<Button> _selectButtons = new();
	private readonly Dictionary<string, VBoxContainer> _deckColumns = new();

	/// <summary>
	/// 显示主题卡组选择界面。
	/// </summary>
	/// <param name="currentHeroId">当前选中的英雄 ID，用于高亮对应列。</param>
	/// <param name="onDeckChosen">用户选择后回调，参数为选中的 heroId（或 null 表示取消）。</param>
	public void ShowDeckSelection(string currentHeroId, Action<string?> onDeckChosen)
	{
		_onDeckChosen = onDeckChosen;
		_openedTicks = Time.GetTicksMsec();

		// 1. 预生成 3 套管卡组
		var cardPool = GameManager.Instance.GetAllCards();
		int seed = (int)Time.GetTicksMsec();

		foreach (var heroId in new[] { "ayame", "rie", "sokou" })
		{
			var profile = LoadThemeProfile(heroId);
			if (profile == null)
				continue;

			// 使用不同种子确保每套卡组不同
			var rng = new Random(unchecked(seed + heroId.GetHashCode()));
			var result = ThemedDeckGenerator.Generate(profile, cardPool, rng);
			_generatedDecks[heroId] = result;

			// 从卡牌 ID 解析 CardData 对象
			var cards = new List<CardData>();
			foreach (var cardId in result.CardIds)
			{
				var card = GameManager.Instance.GetCardById(cardId);
				if (card != null)
					cards.Add(card);
				else
					GD.PushWarning($"[ThemedDeckSelectUI] 无法解析卡牌 ID: {cardId}");
			}
			_generatedCards[heroId] = cards;

			GD.Print($"[ThemedDeckSelectUI] 已生成「{GetHeroDisplayName(heroId)}」卡组：{cards.Count} 张");
		}

		// 2. 构建 UI
		BuildOverlay(currentHeroId);

		// 3. 订阅语言变更
		GameManager.Instance.LanguageChanged += OnLanguageChanged;
	}

	// ===== ThemeProfile 加载 =====

	private static ThemeProfile? LoadThemeProfile(string heroId)
	{
		// heroId 首字母大写匹配文件名
		string capitalized = char.ToUpper(heroId[0]) + heroId[1..];
		string path = $"res://Resources/Themes/ThemeProfile_{capitalized}.tres";

		if (!ResourceLoader.Exists(path))
		{
			GD.PushWarning($"[ThemedDeckSelectUI] ThemeProfile 资源不存在: {path}");
			return null;
		}

		var profile = GD.Load<ThemeProfile>(path);
		if (profile == null)
		{
			GD.PushWarning($"[ThemedDeckSelectUI] ThemeProfile 加载失败: {path}");
		}
		return profile;
	}

	// ===== UI 构建 =====

	private void BuildOverlay(string currentHeroId)
	{
		float s = UIScaler.Instance?.GetScaleFactor() ?? 1f;

		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		MouseFilter = MouseFilterEnum.Stop;
		ZIndex = 200;

		// 半透明暗色背景
		_background = new ColorRect
		{
			Color = new Color(0, 0, 0, 0.82f),
			MouseFilter = MouseFilterEnum.Ignore,
		};
		_background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(_background);

		// 居中根容器
		var centerRoot = new CenterContainer
		{
			MouseFilter = MouseFilterEnum.Ignore,
		};
		centerRoot.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(centerRoot);

		// 面板
		var panel = new PanelContainer();
		panel.CustomMinimumSize = new Vector2(1000 * s, 560 * s);
		centerRoot.AddChild(panel);

		var mainVBox = new VBoxContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
		};
		mainVBox.AddThemeConstantOverride("separation", Mathf.RoundToInt(10 * s));
		panel.AddChild(mainVBox);

		// 标题
		var titleLabel = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Center,
		};
		titleLabel.AddThemeFontSizeOverride("font_size", Mathf.RoundToInt(26 * s));
		titleLabel.AddThemeColorOverride("font_color", new Color(1, 1, 1, 0.95f));
		mainVBox.AddChild(titleLabel);

		// 3 列布局
		var columnsHBox = new HBoxContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
			Alignment = BoxContainer.AlignmentMode.Center,
		};
		columnsHBox.AddThemeConstantOverride("separation", Mathf.RoundToInt(16 * s));
		mainVBox.AddChild(columnsHBox);

		_selectButtons.Clear();
		_deckColumns.Clear();

		foreach (var heroId in new[] { "ayame", "rie", "sokou" })
		{
			var column = BuildDeckColumn(heroId, s, heroId == currentHeroId);
			columnsHBox.AddChild(column);
		}

		// 取消按钮
		_cancelButton = new Button
		{
			CustomMinimumSize = new Vector2(120 * s, 36 * s),
		};
		_cancelButton.AddThemeFontSizeOverride("font_size", Mathf.RoundToInt(16 * s));
		_cancelButton.Pressed += OnCancelPressed;
		mainVBox.AddChild(_cancelButton);

		RefreshLocalizedTexts();
	}

	/// <summary>
	/// 构建单列卡组展示面板。
	/// </summary>
	private VBoxContainer BuildDeckColumn(string heroId, float s, bool isCurrentHero)
	{
		var column = new VBoxContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(280 * s, 0),
		};
		column.AddThemeConstantOverride("separation", Mathf.RoundToInt(6 * s));

		if (!_generatedDecks.TryGetValue(heroId, out var result) ||
			!_generatedCards.TryGetValue(heroId, out var cards))
		{
			// 生成失败的兜底列
			var errorLabel = new Label { Text = $"{heroId}: 加载失败" };
			column.AddChild(errorLabel);
			return column;
		}

		var stats = result.Stats;

		// 主题名（如「绮梦·守护续航」）
		var themeNameLabel = new Label
		{
			Text = $"{GetHeroDisplayName(heroId)}·{result.Profile.ThemeName}",
			HorizontalAlignment = HorizontalAlignment.Center,
			ClipText = true,
		};
		themeNameLabel.AddThemeFontSizeOverride("font_size", Mathf.RoundToInt(18 * s));
		themeNameLabel.AddThemeColorOverride("font_color",
			isCurrentHero ? new Color(0.72f, 0.85f, 1f, 1) : new Color(1, 0.85f, 0.3f, 1));
		column.AddChild(themeNameLabel);

		// 分隔线
		column.AddChild(new HSeparator());

		// 统计摘要
		var statsLabel = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Left,
		};
		statsLabel.AddThemeFontSizeOverride("font_size", Mathf.RoundToInt(12 * s));
		statsLabel.AddThemeColorOverride("font_color", new Color(0.85f, 0.85f, 0.85f, 1));
		statsLabel.Text = BuildStatsText(stats);
		column.AddChild(statsLabel);

		// Top 3 标签
		var topTags = stats.TagCounts
			.OrderByDescending(kv => kv.Value)
			.Take(3)
			.Select(kv => $"{TagToDisplayName(kv.Key)}={kv.Value}");
		var topTagsStr = string.Join(", ", topTags);
		if (!string.IsNullOrEmpty(topTagsStr))
		{
			var tagsLabel = new Label
			{
				Text = topTagsStr,
				HorizontalAlignment = HorizontalAlignment.Left,
			};
			tagsLabel.AddThemeFontSizeOverride("font_size", Mathf.RoundToInt(11 * s));
			tagsLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.9f, 1));
			column.AddChild(tagsLabel);
		}

		// 卡牌列表（可滚动）
		var scrollContainer = new ScrollContainer
		{
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(0, 140 * s),
		};
		var cardListVBox = new VBoxContainer();
		cardListVBox.AddThemeConstantOverride("separation", 1);
		foreach (var card in cards)
		{
			var cardLabel = new Label
			{
				Text = $"[{card.Cost}费] {card.GetLocalizedName()}",
				HorizontalAlignment = HorizontalAlignment.Left,
			};
			cardLabel.AddThemeFontSizeOverride("font_size", Mathf.RoundToInt(10 * s));
			cardLabel.AddThemeColorOverride("font_color", GetRarityColor(card.Rarity));
			cardListVBox.AddChild(cardLabel);
		}
		scrollContainer.AddChild(cardListVBox);
		column.AddChild(scrollContainer);

		// 选择按钮
		var selectButton = new Button
		{
			CustomMinimumSize = new Vector2(160 * s, 34 * s),
		};
		selectButton.AddThemeFontSizeOverride("font_size", Mathf.RoundToInt(14 * s));
		string capturedHeroId = heroId;
		selectButton.Pressed += () => OnDeckSelected(capturedHeroId);
		column.AddChild(selectButton);
		_selectButtons.Add(selectButton);

		_deckColumns[heroId] = column;
		return column;
	}

	/// <summary>
	/// 构建统计摘要文本。
	/// </summary>
	private static string BuildStatsText(ThemedDeckGenerator.GenerationStats stats)
	{
		var lines = new List<string>
		{
			Loc.T("ui.themed_deck_select.total_cards", "{count} 张")
				.Replace("{count}", stats.TotalCards.ToString()),
			Loc.T("ui.themed_deck_select.type_breakdown", "随从{minion}/法术{spell}/领域{domain}")
				.Replace("{minion}", stats.MinionCount.ToString())
				.Replace("{spell}", stats.SpellCount.ToString())
				.Replace("{domain}", stats.DomainCount.ToString()),
		};

		// 法力曲线
		if (stats.ManaCurve.Length >= 3)
		{
			lines.Add(Loc.T("ui.themed_deck_select.mana_curve", "曲线 {low}/{mid}/{high}")
				.Replace("{low}", stats.ManaCurve[0].ToString())
				.Replace("{mid}", stats.ManaCurve[1].ToString())
				.Replace("{high}", stats.ManaCurve[2].ToString()));
		}

		return string.Join("\n", lines);
	}

	// ===== 事件处理 =====

	private void OnDeckSelected(string heroId)
	{
		if (_onDeckChosen == null)
			return;

		if (Time.GetTicksMsec() - _openedTicks < ClickProtectionMs)
		{
			GD.Print($"[ThemedDeckSelectUI] 点击太快，忽略（350ms 保护）");
			return;
		}

		GD.Print($"[ThemedDeckSelectUI] 玩家选择了「{GetHeroDisplayName(heroId)}」主题卡组");

		// 将生成的卡牌写入临时战斗覆盖——不碰 ActiveDeck，确保持久空卡组保留
		if (_generatedCards.TryGetValue(heroId, out var cards) && cards.Count > 0)
		{
			var gm = GameManager.Instance;
			var overrideDeck = new Deck { Name = $"Roguelite_{heroId}" };
			overrideDeck.Initialize(cards);
			gm.CombatDeckOverride = overrideDeck;
			GD.Print($"[ThemedDeckSelectUI] 已将 {cards.Count} 张卡牌写入 CombatDeckOverride（持久 ActiveDeck 不受影响）");
		}

		var callback = _onDeckChosen;
		_onDeckChosen = null;
		callback(heroId);

		QueueFree();
	}

	private void OnCancelPressed()
	{
		if (_onDeckChosen == null)
			return;

		GD.Print("[ThemedDeckSelectUI] 玩家取消选择");

		var callback = _onDeckChosen;
		_onDeckChosen = null;
		callback(null);

		QueueFree();
	}

	// ===== 语言切换 =====

	private void RefreshLocalizedTexts()
	{
		_cancelButton.Text = Loc.T("ui.hand_select.cancel", "取消");

		for (int i = 0; i < _selectButtons.Count && i < 3; i++)
		{
			string? heroId = i switch
			{
				0 => "ayame",
				1 => "rie",
				2 => "sokou",
				_ => null,
			};
			if (heroId != null)
			{
				_selectButtons[i].Text = Loc.T("ui.themed_deck_select.select_format", "选择{name}")
					.Replace("{name}", GetHeroDisplayName(heroId));
			}
		}
	}

	private void OnLanguageChanged(string lang)
	{
		RefreshLocalizedTexts();
	}

	// ===== 右键取消 =====

	public override void _GuiInput(InputEvent @event)
	{
		if (MobileInputHelper.IsMobile)
			return;

		if (@event is InputEventMouseButton mb
			&& mb.Pressed
			&& mb.ButtonIndex == MouseButton.Right)
		{
			OnCancelPressed();
			AcceptEvent();
		}
	}

	// ===== 生命周期 =====

	public override void _ExitTree()
	{
		GameManager.Instance.LanguageChanged -= OnLanguageChanged;
		_onDeckChosen = null;
		_selectButtons.Clear();
		_deckColumns.Clear();
		_generatedDecks.Clear();
		_generatedCards.Clear();
	}

	// ===== 工具方法 =====

	/// <summary>
	/// 获取英雄显示名称（通过本地化）。
	/// </summary>
	private static string GetHeroDisplayName(string heroId)
	{
		return heroId switch
		{
			"ayame" => Loc.T("hero.ayame.name", "绮梦"),
			"rie" => Loc.T("hero.rie.name", "理惠"),
			"sokou" => Loc.T("hero.sokou.name", "溯光"),
			_ => heroId,
		};
	}

	/// <summary>
	/// 根据 CardRarity 返回稀有度颜色。
	/// </summary>
	private static Color GetRarityColor(CardRarity rarity)
	{
		return rarity switch
		{
			CardRarity.Master => new Color(1f, 0.55f, 0f, 1),       // 橙金
			CardRarity.Excellent => new Color(0.64f, 0.21f, 0.93f, 1), // 紫
			CardRarity.Good => new Color(0.26f, 0.52f, 0.96f, 1),      // 蓝
			CardRarity.Common => new Color(0.6f, 0.6f, 0.6f, 1),       // 灰
			_ => new Color(0.5f, 0.5f, 0.5f, 1),                       // 衍生/特殊/状态
		};
	}

	/// <summary>
	/// 将 CardMechanicTag 枚举名转为展示用中文名。
	/// </summary>
	private static string TagToDisplayName(string tagName)
	{
		return tagName switch
		{
			"DirectDamage" => Loc.T("tag.direct_damage", "直伤"),
			"DamageOverTime" => Loc.T("tag.damage_over_time", "持续"),
			"Heal" => Loc.T("tag.heal", "治疗"),
			"Armor" => Loc.T("tag.armor", "护甲"),
			"Draw" => Loc.T("tag.draw", "抽牌"),
			"Discover" => Loc.T("tag.discover", "发现"),
			"Summon" => Loc.T("tag.summon", "召唤"),
			"Buff" => Loc.T("tag.buff", "增益"),
			"Silence" => Loc.T("tag.silence", "沉默"),
			"Discard" => Loc.T("tag.discard", "弃牌"),
			"Domain" => Loc.T("tag.domain", "领域"),
			"WeaponSynergy" => Loc.T("tag.weapon_synergy", "武器"),
			"ManaRamp" => Loc.T("tag.mana_ramp", "跳费"),
			"StatusApply" => Loc.T("tag.status_apply", "状态"),
			"Shuffle" => Loc.T("tag.shuffle", "洗切"),
			"Token" => Loc.T("tag.token", "衍生"),
			_ => tagName,
		};
	}
}
