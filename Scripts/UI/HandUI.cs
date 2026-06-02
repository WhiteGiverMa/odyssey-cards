using System;
using System.Collections.Generic;
using Godot;
using OdysseyCards.Card;
using OdysseyCards.Character;
using OdysseyCards.Combat;

namespace OdysseyCards.UI;

/// <summary>
/// 手牌管理组件。
/// 在屏幕底部水平展示玩家手牌，支持点击选中卡牌。
/// </summary>
public partial class HandUI : Control
{
	[Export] public PackedScene CardScene { get; set; }

	public event Action<Card.Card>? OnCardSelectedForPlay;
	public event Action<Card.Card, ICommander>? OnCardPlayRequested;
	public event Action? OnCardCancelled;

	/// <summary>
	/// 手牌选择模式——由 CombatUI 设置。
	/// 为 true 时，点击手牌不再触发拖拽，而是切换选中状态。
	/// </summary>
	public bool HandSelectMode { get; set; }

	/// <summary>
	/// 手牌选择模式下，点击卡牌切换选中时触发。
	/// 参数：(被点击的卡牌, 是否变为选中)
	/// </summary>
	public event Action<Card.Card, bool>? OnCardSelectionToggled;

	private HBoxContainer _cardContainer = null!;
	private Player? _player;
	private CombatManager? _combat;
	private readonly List<CardUI> _cardUIs = new();
	private Card.Card? _selectedCard;

	public override void _Ready()
	{
		_cardContainer = GetNodeOrNull<HBoxContainer>("CardContainer");
		if (_cardContainer == null)
		{
			_cardContainer = new HBoxContainer
			{
				Name = "CardContainer",
				Alignment = BoxContainer.AlignmentMode.Center,
				AnchorLeft = 0,
				AnchorTop = 0,
				AnchorRight = 1,
				AnchorBottom = 1,
			};
			AddChild(_cardContainer);
		}
	}

	public void Initialize(Player player, CombatManager combat)
	{
		_player = player;
		_combat = combat;
	}

	public void Initialize(Player player)
	{
		Initialize(player, CombatManager.Instance!);
	}

	public void RefreshHand()
	{
		foreach (var cardUI in _cardUIs)
			cardUI.QueueFree();
		_cardUIs.Clear();
		_selectedCard = null;

		if (_player == null) return;

		foreach (var card in _combat!.PlayerHero.Hand)
		{
			var cardUI = CreateCardUI(card);
			_cardUIs.Add(cardUI);
			_cardContainer.AddChild(cardUI);
		}
	}

    public void DeselectCard()
    {
        if (_selectedCard != null)
        {
            foreach (var cardUI in _cardUIs)
            {
                if (cardUI.Card == _selectedCard)
                    cardUI.Deselect();
            }
            _selectedCard = null;
        }
    }

    /// <summary>
    /// 将指定 CardUI 从手牌内部列表中移除，但不销毁节点。
    /// 用于卡牌被重 parent 到 DragLayer 时保持列表一致性。
    /// </summary>
    public void DetachCardFromList(CardUI cardUI)
    {
        _cardUIs.Remove(cardUI);
        if (_selectedCard == cardUI.Card)
            _selectedCard = null;
    }

	public Card.Card? PlaySelectedCard()
	{
		var card = _selectedCard;
		if (card != null)
			RemoveCardFromHand(card);
		return card;
	}

	public Card.Card? PlaySelectedCardOnTarget()
	{
		return PlaySelectedCard();
	}

	public void UpdateHand(IReadOnlyList<Card.Card> hand)
	{
		RefreshHand();
	}

	private CardUI CreateCardUI(Card.Card card)
	{
		CardUI cardUI = CardScene != null
			? CardScene.Instantiate<CardUI>()
			: new CardUI();

		cardUI.SetCard(card);
		cardUI.CustomMinimumSize = new Vector2(100, 140);
		cardUI.OnCardClicked += OnCardClicked;
		cardUI.OnCardRightClicked += OnCardRightClicked;
		return cardUI;
	}

	/// <summary>
	/// 右键取消选中——卡牌归位并退出所有选择模式。
	/// </summary>
	private void OnCardRightClicked(CardUI cardUI)
	{
		DeselectCard();
		OnCardCancelled?.Invoke();
	}

	/// <summary>
	/// 根据运行时卡牌实例查找对应的 CardUI。
	/// 用于拖拽时重 parent 到 DragLayer。
	/// </summary>
	public CardUI? GetCardUIFor(Card.Card card)
	{
		foreach (var cardUI in _cardUIs)
		{
			if (cardUI.Card == card)
				return cardUI;
		}
		return null;
	}

	private void OnCardClicked(CardUI cardUI)
	{
		if (cardUI.Card == null) return;

		// 手牌选择模式：点击切换选中，不触发拖拽
		if (HandSelectMode)
		{
			OnCardSelectionToggled?.Invoke(cardUI.Card, true);
			return;
		}

		if (_selectedCard == cardUI.Card)
		{
			// 再次点击同一张卡牌 → 取消选中
			DeselectCard();
			OnCardCancelled?.Invoke();
			return;
		}

		// 选中了不同的卡牌 → 将旧卡归还手牌（重parent回 CardContainer）
		if (_selectedCard != null)
		{
			var oldUI = _cardUIs.Find(c => c.Card == _selectedCard);
			if (oldUI != null)
			{
				oldUI.CancelDragSilent();
				oldUI.GetParent()?.RemoveChild(oldUI);
				_cardContainer.AddChild(oldUI);
				oldUI.Deselect();
			}
			// 即使 oldUI 已脱离列表（例如已被 DetachCardFromList 移动到 DragLayer），
			// 也必须清空 _selectedCard，避免后续切换时引用幽灵卡。
			_selectedCard = null;
		}

		_selectedCard = cardUI.Card;
		cardUI.Select();

		OnCardSelectedForPlay?.Invoke(_selectedCard);
#pragma warning disable CS0618
		OnCardPlayRequested?.Invoke(_selectedCard, _player!);
#pragma warning restore CS0618
	}

	private void RemoveCardFromHand(Card.Card card)
	{
		for (int i = _cardUIs.Count - 1; i >= 0; i--)
		{
			if (_cardUIs[i].Card == card)
			{
				_cardUIs[i].QueueFree();
				_cardUIs.RemoveAt(i);
				break;
			}
		}
		_selectedCard = null;
	}
}
