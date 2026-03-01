using System;
using Godot;
using System.Collections.Generic;

namespace OdysseyCards.UI
{
	public partial class HandUI : Control
	{
		[Export] public PackedScene CardScene { get; set; }

		private HBoxContainer _cardContainer;
		private Character.Player _player;
		private Combat.CombatManager _combatManager;
		private Control _dragLayer;
		private CardUI _draggingCard;
		private int _draggingCardIndex = -1;

		public event Action<Card.Card, Character.Character> OnCardPlayRequested;
		public event Action<Card.Card, Vector2> OnCardDragStarted;
		public event Action<Card.Card, Vector2> OnCardDragEnded;
		public event Action<Card.Card, int> OnCardDroppedOnNode;

		public override void _Ready()
		{
			_cardContainer = GetNode<HBoxContainer>("CardContainer");
			CreateDragLayer();

			if (UIScaler.Instance != null)
			{
				UIScaler.Instance.OnResolutionChanged += OnResolutionChangedHandler;
			}

			_cardContainer.Resized += OnContainerResized;
		}

		private void OnResolutionChangedHandler()
		{
			UpdateHand();
		}

		private void OnContainerResized()
		{
			UpdateHand();
		}

		private void CreateDragLayer()
		{
			_dragLayer = new Control
			{
				Name = "DragLayer",
				MouseFilter = MouseFilterEnum.Ignore
			};
			_dragLayer.SetAnchorsPreset(LayoutPreset.FullRect);
			AddChild(_dragLayer);
			_dragLayer.ZIndex = 1000;
		}

		public void SetPlayer(Character.Player player)
		{
			GD.Print($"[HandUI] SetPlayer called, player is null: {player == null}");

			if (_player != null)
			{
				_player.OnHandChanged -= UpdateHand;
			}

			_player = player;

			if (_player != null)
			{
				_player.OnHandChanged += UpdateHand;
				GD.Print($"[HandUI] Subscribed to OnHandChanged, current hand count: {_player.Hand.Count}");
			}
			GD.Print("[HandUI] SetPlayer completed");
		}

		public void SetCombatManager(Combat.CombatManager manager)
		{
			GD.Print($"[HandUI] SetCombatManager called, manager is null: {manager == null}");
			_combatManager = manager;
			GD.Print("[HandUI] SetCombatManager completed");
		}

		private void UpdateHand()
		{
			GD.Print($"[HandUI] UpdateHand called, _cardContainer is null: {_cardContainer == null}, _player is null: {_player == null}");

			if (_cardContainer == null || _player == null)
			{
				return;
			}

			GD.Print($"[HandUI] Hand count: {_player.Hand.Count}");

			foreach (Node? child in _cardContainer.GetChildren())
			{
				child.QueueFree();
			}

			int handCount = _player.Hand.Count;
			float scale = CalculateCardScale(handCount);

			foreach (Card.Card card in _player.Hand)
			{
				GD.Print($"[HandUI] Creating CardUI for: {card.CardName}");
				CreateCardUI(card, scale);
			}
		}

		private float CalculateCardScale(int cardCount)
		{
			if (cardCount <= 0)
			{
				return 1.0f;
			}

			if (UIScaler.Instance == null)
			{
				return 1.0f;
			}

			float containerWidth = _cardContainer.Size.X;
			Vector2 cardSize = UIScaler.Instance.GetCardSize();
			float cardWidth = cardSize.X;

			if (cardWidth <= 0 || containerWidth <= 0)
			{
				return 1.0f;
			}

			float totalCardWidth = cardWidth * cardCount;
			if (totalCardWidth <= containerWidth)
			{
				return 1.0f;
			}

			float scale = containerWidth / totalCardWidth;
			float minScale = 0.6f;
			return Mathf.Max(scale, minScale);
		}

		private void CreateCardUI(Card.Card card, float scale = 1.0f)
		{
			var cardUI = new CardUI();
			cardUI.SetCard(card);
			cardUI.OnCardDraggedToTarget += OnCardDraggedToTarget;
			cardUI.OnDragStarted += OnCardDragStartedHandler;
			cardUI.OnDragEnded += OnCardDragEndedHandler;
			cardUI.OnDroppedOnNode += OnCardDroppedOnNodeHandler;
			cardUI.OnReturnToHandRequested += OnReturnToHandRequestedHandler;

			cardUI.Scale = new Vector2(scale, scale);

			_cardContainer.AddChild(cardUI);
		}

		private void OnReturnToHandRequestedHandler(CardUI cardUI)
		{
			ReturnCardToHand(cardUI);
		}

		private void OnCardDragStartedHandler(CardUI cardUI, Vector2 position)
		{
			if (cardUI.Card == null)
			{
				return;
			}

			GD.Print($"[HandUI] Drag started: {cardUI.Card.CardName}");

			_draggingCard = cardUI;
			_draggingCardIndex = cardUI.GetIndex();

			Vector2 globalPos = cardUI.GlobalPosition;
			cardUI.Reparent(_dragLayer, false);
			cardUI.GlobalPosition = globalPos;

			if (cardUI.Card is Card.Unit)
			{
				_combatManager?.PlayCard(cardUI.Card, null);
			}

			OnCardDragStarted?.Invoke(cardUI.Card, position);
		}

		private void OnCardDragEndedHandler(CardUI cardUI, Vector2 position)
		{
			if (cardUI.Card == null)
			{
				return;
			}

			GD.Print($"[HandUI] Drag ended: {cardUI.Card.CardName}");

			_draggingCard = null;
			_draggingCardIndex = -1;

			OnCardDragEnded?.Invoke(cardUI.Card, position);
		}

		public void ReturnCardToHand(CardUI cardUI)
		{
			if (cardUI == null || _cardContainer == null)
			{
				return;
			}

			Vector2 globalPos = cardUI.GlobalPosition;
			cardUI.Reparent(_cardContainer, false);
			cardUI.GlobalPosition = globalPos;

			int targetIndex = Mathf.Min(_draggingCardIndex >= 0 ? _draggingCardIndex : _cardContainer.GetChildCount(), _cardContainer.GetChildCount());
			_cardContainer.MoveChild(cardUI, targetIndex);
		}

		private void OnCardDroppedOnNodeHandler(CardUI cardUI, int nodeId)
		{
			if (cardUI.Card == null)
			{
				return;
			}

			GD.Print($"[HandUI] Card dropped on node: {cardUI.Card.CardName} -> Node {nodeId}");

			OnCardDroppedOnNode?.Invoke(cardUI.Card, nodeId);
		}

		private void OnCardDraggedToTarget(CardUI cardUI, Character.Character target)
		{
			GD.Print($"[HandUI] Card dragged to target: {cardUI.Card?.CardName} -> {target.CharacterName}");

			if (cardUI.Card is Card.Order order && order.Target == Core.CardTarget.SingleEnemy)
			{
				OnCardPlayRequested?.Invoke(cardUI.Card, target);

				cardUI.ResetDragState();
			}
		}

		public void PlayReturnAnimation(CardUI cardUI)
		{
			if (cardUI == null || CardAnimation.Instance == null)
			{
				return;
			}

			CardAnimation.Instance.PlayReturnAnimation(cardUI, cardUI.OriginalPosition);
		}
	}
}
