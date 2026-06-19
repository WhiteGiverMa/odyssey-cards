using Godot;
using OdysseyCards.Core;
using OdysseyCards.Roguelike;
using System;
using System.Collections.Generic;
using Loc = OdysseyCards.Localization.Localization;

namespace OdysseyCards.UI;

/// <summary>
/// 商店界面——使用 MobileDialogHost 弹窗展示可购买的卡牌。
/// 玩家花费金币（GameManager.RunGold）购买卡牌，卡牌加入牌堆。
/// </summary>
public partial class ShopUI : Control
{
	private readonly RoomDefinition _room;
	private readonly Action _onComplete;
	private readonly HashSet<string> _boughtCardIds = new();
	private Control _dialog = null!;
	private Label _goldLabel = null!;
	private readonly List<ShopCardSlot> _slots = new();

	/// <summary>
	/// 商店中单张卡牌的展示槽位。
	/// </summary>
	private sealed class ShopCardSlot
	{
		public required CardData CardData;
		public required Button BuyButton;
		public required int GoldCost;
	}

	/// <summary>
	/// 打开商店界面。
	/// </summary>
	/// <param name="room">商店房间定义</param>
	/// <param name="onComplete">完成回调——调用方在此处理 CompleteRoomAndAdvance</param>
	/// <remarks>
	/// 构造只存数据，UI 构建延后到 <see cref="_Ready"/>。
	/// <see cref="MobileDialogHost.CreateDialog"/> 内部调用 <c>parent.GetViewportRect()</c>，
	/// 要求 parent 已入树；若在构造里构建，调用方 <c>new ShopUI(...)</c> 后才 <c>AddChild</c>，
	/// 节点尚未入树会导致 <c>is_inside_tree()==false</c> 报错（参考 <see cref="CardFlyVfx"/> 同样纪律）。
	/// </remarks>
	public ShopUI(RoomDefinition room, Action onComplete)
	{
		_room = room;
		_onComplete = onComplete;
	}

	/// <summary>
	/// 节点入树后构建商店 UI——此时 <see cref="MobileDialogHost.CreateDialog"/> 取 viewport 才安全。
	/// </summary>
	public override void _Ready()
	{
		BuildShop();
	}

	// ===== 构建 UI =====

	private void BuildShop()
	{
		var (dialog, content, buttonRow) = MobileDialogHost.CreateDialog(
			this,
			$"🏪 {_room.DisplayName}",
			width: 520);

		_dialog = dialog;

		// 金币显示
		int currentGold = GameManager.Instance?.RunGold ?? 0;
		_goldLabel = new Label
		{
			Text = Loc.T("ui.shop.gold_display", "金币: {gold}").Replace("{gold}", currentGold.ToString()),
			HorizontalAlignment = HorizontalAlignment.Center,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		_goldLabel.AddThemeFontSizeOverride("font_size", 22);
		_goldLabel.AddThemeColorOverride("font_color", new Color(1, 0.85f, 0.3f, 1)); // 金色
		content.AddChild(_goldLabel);

		// 分隔线
		content.AddChild(CreateSeparator());

		// 随机选 5 张卡
		var eligible = GameManager.Instance?.GetRewardEligibleCards();
		if (eligible == null || eligible.Count == 0)
		{
			content.AddChild(CreateEmptyLabel());
		}
		else
		{
			Shuffle(eligible);
			int cardCount = Mathf.Min(eligible.Count, 5);
			for (int i = 0; i < cardCount; i++)
			{
				var cardData = eligible[i];
				BuildCardRow(content, cardData);
			}
		}

		// 分隔线
		content.AddChild(CreateSeparator());

		// 离开按钮
		var leaveBtn = MobileDialogHost.CreateDialogButton(
			Loc.T("ui.shop.leave", "离开"));
		leaveBtn.Pressed += () =>
		{
			MobileDialogHost.CloseDialog(dialog, this);
			_onComplete?.Invoke();
		};
		buttonRow.AddChild(leaveBtn);
	}

	// ===== 卡牌行构建 =====

	private void BuildCardRow(VBoxContainer content, CardData cardData)
	{
		int goldCost = GetGoldCost(cardData.Rarity);

		// 行容器
		var row = new HBoxContainer
		{
			MouseFilter = MouseFilterEnum.Ignore,
		};
		row.AddThemeConstantOverride("separation", 12);

		// 左侧：卡牌信息
		var infoBox = new VBoxContainer
		{
			MouseFilter = MouseFilterEnum.Ignore,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		infoBox.AddThemeConstantOverride("separation", 2);

		// 卡牌名称行：名称 + 稀有度标签
		var nameRow = new HBoxContainer
		{
			MouseFilter = MouseFilterEnum.Ignore,
			Alignment = BoxContainer.AlignmentMode.Begin,
		};
		nameRow.AddThemeConstantOverride("separation", 6);

		var nameLabel = new Label
		{
			Text = cardData.GetLocalizedName(),
			MouseFilter = MouseFilterEnum.Ignore,
		};
		nameLabel.AddThemeFontSizeOverride("font_size", 18);
		nameLabel.AddThemeColorOverride("font_color", GetRarityColor(cardData.Rarity));
		nameRow.AddChild(nameLabel);

		// 法力 + 类型行
		var metaRow = new HBoxContainer
		{
			MouseFilter = MouseFilterEnum.Ignore,
		};
		metaRow.AddThemeConstantOverride("separation", 6);

		// 法力消耗：💧 图标 + 数值
		var costLabel = new Label
		{
			Text = $"💧{cardData.Cost}",
			MouseFilter = MouseFilterEnum.Ignore,
		};
		costLabel.AddThemeFontSizeOverride("font_size", 14);
		costLabel.AddThemeColorOverride("font_color", new Color(0.4f, 0.7f, 1f, 1)); // 蓝色法力色
		metaRow.AddChild(costLabel);

		// 类型标签
		var typeLabel = new Label
		{
			Text = GetTypeBadge(cardData),
			MouseFilter = MouseFilterEnum.Ignore,
		};
		typeLabel.AddThemeFontSizeOverride("font_size", 13);
		typeLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.75f, 1));
		metaRow.AddChild(typeLabel);

		infoBox.AddChild(nameRow);
		infoBox.AddChild(metaRow);

		// 随从属性行 (攻击/生命/防御)
		if (cardData.Type == CardType.Minion)
		{
			var statRow = new HBoxContainer
			{
				MouseFilter = MouseFilterEnum.Ignore,
			};
			statRow.AddThemeConstantOverride("separation", 8);

			var atkLabel = new Label
			{
				Text = $"⚔️ {cardData.Attack}",
				MouseFilter = MouseFilterEnum.Ignore,
			};
			atkLabel.AddThemeFontSizeOverride("font_size", 13);
			atkLabel.AddThemeColorOverride("font_color", new Color(1f, 0.5f, 0.3f, 1));
			statRow.AddChild(atkLabel);

			var hpLabel = new Label
			{
				Text = $"❤️ {cardData.Health}",
				MouseFilter = MouseFilterEnum.Ignore,
			};
			hpLabel.AddThemeFontSizeOverride("font_size", 13);
			hpLabel.AddThemeColorOverride("font_color", new Color(1f, 0.3f, 0.3f, 1));
			statRow.AddChild(hpLabel);

			if (cardData.Defense != 0)
			{
				var defLabel = new Label
				{
					Text = $"🛡️ {cardData.Defense}",
					MouseFilter = MouseFilterEnum.Ignore,
				};
				defLabel.AddThemeFontSizeOverride("font_size", 13);
				defLabel.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.7f, 1));
				statRow.AddChild(defLabel);
			}

			infoBox.AddChild(statRow);
		}

		// 描述文本（限制高度）
		var descLabel = new Label
		{
			Text = cardData.GetLocalizedDescription(),
			MouseFilter = MouseFilterEnum.Ignore,
			AutowrapMode = TextServer.AutowrapMode.Word,
			ClipText = true,
		};
		descLabel.AddThemeFontSizeOverride("font_size", 12);
		descLabel.AddThemeColorOverride("font_color", new Color(0.75f, 0.75f, 0.8f, 0.9f));
		infoBox.AddChild(descLabel);

		row.AddChild(infoBox);

		// 右侧：购买按钮
		var buyBtn = new Button
		{
			Text = GetBuyButtonText(cardData.Rarity, goldCost),
			CustomMinimumSize = new Vector2(80, MobileDialogHost.MinTouchTargetHeight),
		};
		buyBtn.AddThemeFontSizeOverride("font_size", 16);

		int currentGold = GameManager.Instance?.RunGold ?? 0;
		if (currentGold < goldCost)
		{
			buyBtn.Disabled = true;
			buyBtn.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.5f, 1));
		}

		buyBtn.Pressed += () => OnBuyCard(cardData, goldCost, buyBtn);

		row.AddChild(buyBtn);

		var slot = new ShopCardSlot
		{
			CardData = cardData,
			BuyButton = buyBtn,
			GoldCost = goldCost,
		};
		_slots.Add(slot);

		content.AddChild(row);
	}

	// ===== 购买逻辑 =====

	private void OnBuyCard(CardData cardData, int goldCost, Button buyBtn)
	{
		if (_boughtCardIds.Contains(cardData.Id))
			return;

		var gm = GameManager.Instance;
		if (gm == null)
			return;

		if (!gm.SpendGold(goldCost))
			return;

		_boughtCardIds.Add(cardData.Id);
		gm.AddCardToDeckInCombat(cardData);
		GD.Print($"[ShopUI] 购买了 {cardData.GetLocalizedName()}，花费 {goldCost} 金币");

		// 更新按钮状态
		buyBtn.Text = Loc.T("ui.shop.bought", "已购买");
		buyBtn.Disabled = true;
		buyBtn.AddThemeColorOverride("font_color", new Color(0.4f, 0.8f, 0.4f, 1)); // 绿色表示已购买

		// 更新金币显示
		int remaining = gm.RunGold;
		_goldLabel.Text = Loc.T("ui.shop.gold_display", "金币: {gold}").Replace("{gold}", remaining.ToString());

		// 更新其他按钮的可用性
		RefreshBuyButtons(remaining);
	}

	/// <summary>
	/// 金币变化后刷新所有购买按钮的可用状态。
	/// </summary>
	private void RefreshBuyButtons(int remainingGold)
	{
		foreach (var slot in _slots)
		{
			if (_boughtCardIds.Contains(slot.CardData.Id))
				continue;

			slot.BuyButton.Disabled = remainingGold < slot.GoldCost;
			if (slot.BuyButton.Disabled)
			{
				slot.BuyButton.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.5f, 1));
			}
		}
	}

	// ===== 辅助方法 =====

	/// <summary>
	/// 稀有度→商店金币价格。
	/// </summary>
	public static int GetGoldCost(CardRarity rarity)
	{
		return rarity switch
		{
			CardRarity.Common => 50,
			CardRarity.Good => 75,
			CardRarity.Excellent => 100,
			CardRarity.Master => 150,
			_ => 999, // 衍生卡等不出现在商店中
		};
	}

	/// <summary>
	/// 卡牌稀有度对应的显示颜色。
	/// </summary>
	public static Color GetRarityColor(CardRarity rarity)
	{
		return rarity switch
		{
			CardRarity.Common => new Color(0.9f, 0.9f, 0.9f, 1),     // 白色 — 普通
			CardRarity.Good => new Color(0.3f, 0.85f, 0.3f, 1),      // 绿色 — 良好
			CardRarity.Excellent => new Color(0.3f, 0.5f, 1f, 1),     // 蓝色 — 极佳
			CardRarity.Master => new Color(1f, 0.84f, 0f, 1),         // 金色 — 大师
			CardRarity.Special => new Color(1f, 0.55f, 0f, 1),        // 橙色 — 特殊
			_ => new Color(0.6f, 0.6f, 0.6f, 1),                     // 灰色 — 未知
		};
	}

	/// <summary>
	/// 购买按钮文本。
	/// </summary>
	private static string GetBuyButtonText(CardRarity rarity, int goldCost)
	{
		string label = Loc.T("ui.shop.buy", "购买");
		string goldLabel = Loc.T("ui.shop.gold_cost", "💰{cost}").Replace("{cost}", goldCost.ToString());
		return $"{goldLabel}\n{label}";
	}

	/// <summary>
	/// 卡牌类型标签文本（随从显示攻击/生命/关键词，法术显示「法术」，领域显示「领域」）。
	/// </summary>
	private static string GetTypeBadge(CardData cardData)
	{
		return cardData.Type switch
		{
			CardType.Minion => Loc.T("ui.shop.type_minion", "随从"),
			CardType.Spell => Loc.T("ui.shop.type_spell", "法术"),
			CardType.Domain => Loc.T("ui.shop.type_domain", "领域"),
			_ => cardData.Type.ToString(),
		};
	}

	private static Label CreateEmptyLabel()
	{
		var label = new Label
		{
			Text = Loc.T("ui.shop.empty", "商店暂时没有可出售的卡牌。"),
			HorizontalAlignment = HorizontalAlignment.Center,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		label.AddThemeFontSizeOverride("font_size", 16);
		label.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.65f, 1));
		return label;
	}

	private static HSeparator CreateSeparator()
	{
		return new HSeparator
		{
			MouseFilter = MouseFilterEnum.Ignore,
			CustomMinimumSize = new Vector2(0, 4),
		};
	}

	private static void Shuffle<T>(List<T> list)
	{
		var rng = new Random();
		for (int i = list.Count - 1; i > 0; i--)
		{
			int j = rng.Next(i + 1);
			(list[i], list[j]) = (list[j], list[i]);
		}
	}
}
