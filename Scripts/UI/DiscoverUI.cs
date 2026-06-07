using Godot;
using OdysseyCards.Core;
using System;
using System.Collections.Generic;
using Loc = OdysseyCards.Localization.Localization;

namespace OdysseyCards.UI;

/// <summary>
/// 发现（N选1）选牌覆盖层 — 继承 CardSelectionScreen 基类。
/// 支持单选（N选1）和多选（N选M + 确认按钮）。
/// 参考炉石「发现」和杀戮尖塔「选牌」界面（STS2 NChooseACardSelectionScreen）。
/// </summary>
public partial class DiscoverUI : CardSelectionScreen
{
	// ===== 子类特有状态 =====

	private readonly List<CardUI> _selectedCardUIs = new();
	private IReadOnlyList<Card.Card>? _currentCards;
	private int _pickCount = 1;

	// ===== 回调 =====

	private Action<CardData?>? _onChosen;
	private Action<IReadOnlyList<Card.Card>>? _onCardsChosen;

	/// <summary>
	/// 自定义标题。如果设置了，则覆盖默认的本地化标题。
	/// 用于弃牌选择等场景（如刀盾危机、主动弃牌）。
	/// </summary>
	public string? CustomTitle { get; set; }

	// ===== 基类覆写 =====

	protected override string TitleText =>
		CustomTitle ?? (_pickCount > 1
			? Loc.T("ui.discover.pick_count", "选择 {count} 张").Replace("{count}", _pickCount.ToString())
			: Loc.T("ui.discover.title", "发现"));

	protected override string SkipButtonText => Loc.T("ui.discover.skip", "跳过");

	protected override string? ConfirmButtonText => _pickCount > 1
		? Loc.T("ui.discover.confirm", "确认")
		: null;

	protected override int DialogWidth => 600;
	protected override int OverlayZIndex => 200;
	protected override bool ShowSkipButton => true;

	protected override bool IsItemSelected(int index)
	{
		if (index < 0 || index >= _items.Count) return false;
		return _selectedCardUIs.Contains(_items[index]);
	}

	protected override void RefreshLocalizedTexts()
	{
		base.RefreshLocalizedTexts();
	}

	// ===== 公开 API =====

	/// <summary>
	/// 显示选牌界面——N选1（使用 CardData 列表）。
	/// </summary>
	public void ShowCards(IReadOnlyList<CardData> cards, bool canSkip, Action<CardData?> onChosen)
	{
		_onChosen = onChosen;
		_onCardsChosen = null;
		_pickCount = 1;
		_selectedCardUIs.Clear();

		var runtimeCards = new List<Card.Card>();
		foreach (var cardData in cards)
			runtimeCards.Add(new Card.Card(cardData));
		_currentCards = runtimeCards;

		_isShowing = true;
		_openedTicks = Time.GetTicksMsec();
		Show();

		BuildOverlay();
		PlayEntryAnimation();
	}

	/// <summary>
	/// 显示选牌界面——N选M（使用 Card 运行时实例列表）。
	/// </summary>
	public void ShowCards(IReadOnlyList<Card.Card> cards, int pickCount, bool canSkip,
		Action<IReadOnlyList<Card.Card>> onChosen)
	{
		_onChosen = null;
		_onCardsChosen = onChosen;
		_pickCount = Math.Max(1, pickCount);
		_selectedCardUIs.Clear();
		_currentCards = cards;

		_isShowing = true;
		_openedTicks = Time.GetTicksMsec();
		Show();

		BuildOverlay();
		PlayEntryAnimation();
	}

	// ===== 卡牌项构建 =====

	protected override void BuildCardItems()
	{
		float s = UIScaler.Instance?.GetScaleFactor() ?? 1f;

		if (_currentCards == null) return;

		foreach (var card in _currentCards)
		{
			var cardUI = new CardUI
			{
				Name = $"DiscoverCard_{card.Id}",
				CustomMinimumSize = new Vector2(130 * s, 195 * s),
				Modulate = new Color(1, 1, 1, 0), // 入场前透明，动画控制显示
			};
			cardUI.SetCard(card);
			_cardsContainer.AddChild(cardUI);
			RegisterItem(cardUI);
		}
	}

	// ===== 选择处理 =====

	protected override void OnItemSelected(int index)
	{
		var cardUI = _items[index];

		if (_pickCount > 1)
		{
			ToggleCardSelection(index, cardUI);
			return;
		}

		// 单选模式：直接确认
		var chosen = cardUI.Card?.Data;
		GD.Print($"[DiscoverUI] 玩家选择了：{chosen?.GetLocalizedName() ?? "(null)"}");

		cardUI.SelfModulate = SelectedColor;

		_isShowing = false;
		var callback = _onChosen;
		_onChosen = null;
		callback?.Invoke(chosen);
	}

	protected override void OnSkip()
	{
		GD.Print("[DiscoverUI] 玩家跳过选牌");

		var callback = _onChosen;
		var cardsCallback = _onCardsChosen;
		_onChosen = null;
		_onCardsChosen = null;
		callback?.Invoke(null);
		cardsCallback?.Invoke(Array.Empty<Card.Card>());
	}

	protected override void OnConfirm()
	{
		if (!_isShowing || _selectedCardUIs.Count != _pickCount) return;

		var chosenCards = new List<Card.Card>();
		foreach (var cardUI in _selectedCardUIs)
		{
			if (cardUI.Card != null)
				chosenCards.Add(cardUI.Card);
		}

		GD.Print($"[DiscoverUI] 玩家确认选择 {chosenCards.Count} 张牌");
		_isShowing = false;
		var callback = _onCardsChosen;
		_onCardsChosen = null;
		callback?.Invoke(chosenCards);
	}

	// ===== 多选切换 =====

	private void ToggleCardSelection(int index, CardUI cardUI)
	{
		if (_selectedCardUIs.Remove(cardUI))
		{
			cardUI.SelfModulate = Colors.White;
		}
		else
		{
			if (_selectedCardUIs.Count >= _pickCount) return;
			_selectedCardUIs.Add(cardUI);
			cardUI.SelfModulate = SelectedColor;
		}

		SetConfirmEnabled(_selectedCardUIs.Count == _pickCount);
	}
}
