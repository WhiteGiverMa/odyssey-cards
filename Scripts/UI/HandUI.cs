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
				SizeFlagsHorizontal = SizeFlags.ExpandFill,
				SizeFlagsVertical = SizeFlags.ExpandFill,
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

		foreach (var card in _player.Hand)
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
		cardUI.OnCardSelected += OnCardClicked;
		return cardUI;
	}

	private void OnCardClicked(CardUI cardUI)
	{
		if (cardUI.Card == null) return;

		if (_selectedCard == cardUI.Card)
		{
			DeselectCard();
			return;
		}

		DeselectCard();
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
