using System;
using Godot;
using OdysseyCards.Domain.Combat.Commands;
using OdysseyCards.Domain.Combat.Engine;
using OdysseyCards.Domain.Combat.Events;
using OdysseyCards.Presentation.Input;

namespace OdysseyCards.UI
{
    public partial class HandUI : Control
    {
        [Export] public PackedScene CardScene { get; set; }

        private HBoxContainer _cardContainer;
        private Character.Player _player;
        private Control _dragLayer;
        private CardUI _draggingCard;
        private int _draggingCardIndex = -1;
        private bool _isUpdatingHand = false;
        private Control _dragPlaceholder;

        public event Action<Card.Card, Character.Character> OnCardPlayRequested;
        public event Action<Card.Card, Vector2> OnCardDragStarted;
        public event Action<Card.Card, Vector2> OnCardDragEnded;
        public event Action<Card.Card, int> OnCardDroppedOnNode;
        public event Action<Card.Unit> OnUnitDeployModeRequested;
        public event Action<Card.Card> OnCardPlayWithoutTarget;
        public event Action<bool> OnNoTargetCardDragStateChanged;

        public override void _Ready()
        {
            _cardContainer = GetNode<HBoxContainer>("CardContainer");
            CreateDragLayer();

            if (UIScaler.Instance != null)
            {
                UIScaler.Instance.OnResolutionChanged += OnResolutionChangedHandler;
            }
        }

        private void OnResolutionChangedHandler()
        {
            UpdateHand();
        }

        private void CreateDragLayer()
        {
            _dragLayer = new Control
            {
                Name = "DragLayer",
                MouseFilter = MouseFilterEnum.Pass
            };
            _dragLayer.SetAnchorsPreset(LayoutPreset.FullRect);
            AddChild(_dragLayer);
            _dragLayer.ZIndex = 1000;
            GD.Print("[HandUI] DragLayer created with MouseFilter=Pass");
        }

        public void SetPlayer(Character.Player player)
        {
            GD.Print($"[HandUI] SetPlayer called, player is null: {player == null}");

            if (_player != null)
            {
                _player.OnHandChanged -= UpdateHand;
            }

            _player = player ?? throw new ArgumentNullException(nameof(player));

            _player.OnHandChanged += UpdateHand;
            GD.Print($"[HandUI] Subscribed to OnHandChanged, current hand count: {_player.Hand.Count}");
            GD.Print("[HandUI] SetPlayer completed");
        }

        private void UpdateHand()
        {
            if (_isUpdatingHand)
            {
                return;
            }

            _isUpdatingHand = true;

            GD.Print($"[HandUI] UpdateHand called, _cardContainer is null: {_cardContainer == null}, _player is null: {_player == null}");

            if (_cardContainer == null || _player == null)
            {
                _isUpdatingHand = false;
                return;
            }

            GD.Print($"[HandUI] Hand count: {_player.Hand.Count}");

            foreach (Node child in _cardContainer.GetChildren())
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

            _isUpdatingHand = false;
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
            const float minScale = 0.6f;
            return Mathf.Max(scale, minScale);
        }

        private void CreateCardUI(Card.Card card, float scale = 1.0f)
        {
            var cardUI = new CardUI();
            cardUI.Scale = new Vector2(scale, scale);
            _cardContainer.AddChild(cardUI);
            cardUI.SetCard(card);
            cardUI.OnCardDraggedToTarget += OnCardDraggedToTarget;
            cardUI.OnDragStarted += OnCardDragStartedHandler;
            cardUI.OnDragEnded += OnCardDragEndedHandler;
            cardUI.OnDroppedOnNode += OnCardDroppedOnNodeHandler;
            cardUI.OnReturnToHandRequested += OnReturnToHandRequestedHandler;
            cardUI.OnPlayWithoutTarget += OnPlayWithoutTargetHandler;
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

            GD.Print($"[HandUI] Drag started: {cardUI.Card.CardName}, card type: {cardUI.Card.Type}");

            _draggingCard = cardUI;
            _draggingCardIndex = cardUI.GetIndex();

            Vector2 globalPos = cardUI.GlobalPosition;

            _dragPlaceholder = new Control
            {
                Name = "DragPlaceholder",
                CustomMinimumSize = cardUI.CustomMinimumSize
            };
            _cardContainer.AddChild(_dragPlaceholder);
            _cardContainer.MoveChild(_dragPlaceholder, _draggingCardIndex);

            cardUI.Reparent(_dragLayer, false);
            cardUI.GlobalPosition = globalPos;

            if (cardUI.Card is Card.Unit unit)
            {
                GD.Print($"[HandUI] Unit card drag - entering deploy mode for: {unit.CardName}");
                OnUnitDeployModeRequested?.Invoke(unit);
            }
            else if (cardUI.Card is Card.Order order && !order.RequiresTarget)
            {
                GD.Print($"[HandUI] No-target Order drag - highlighting play area");
                OnNoTargetCardDragStateChanged?.Invoke(true);
            }

            OnCardDragStarted?.Invoke(cardUI.Card, position);
        }

        private void OnCardDragEndedHandler(CardUI cardUI, Vector2 position)
        {
            if (cardUI.Card == null)
            {
                return;
            }

            GD.Print($"[HandUI] Drag ended: {cardUI.Card.CardName}, card type: {cardUI.Card.Type}");

            if (_dragPlaceholder != null)
            {
                _dragPlaceholder.QueueFree();
                _dragPlaceholder = null;
            }

            _draggingCard = null;
            _draggingCardIndex = -1;

            OnNoTargetCardDragStateChanged?.Invoke(false);

            OnCardDragEnded?.Invoke(cardUI.Card, position);
        }

        public void ReturnCardToHand(CardUI cardUI)
        {
            if (cardUI == null || _cardContainer == null)
            {
                return;
            }

            if (_dragPlaceholder != null)
            {
                _dragPlaceholder.QueueFree();
                _dragPlaceholder = null;
            }

            cardUI.Reparent(_cardContainer, false);

            int targetIndex = Mathf.Min(_draggingCardIndex >= 0 ? _draggingCardIndex : _cardContainer.GetChildCount() - 1, _cardContainer.GetChildCount() - 1);
            _cardContainer.MoveChild(cardUI, targetIndex);

            _draggingCard = null;
            _draggingCardIndex = -1;
        }

        private void OnCardDroppedOnNodeHandler(CardUI cardUI, int nodeId)
        {
            if (cardUI.Card == null)
            {
                return;
            }

            GD.Print($"[HandUI] Card dropped on node: {cardUI.Card.CardName} -> Node {nodeId}");

            if (CombatInputAdapter.Instance != null && cardUI.Card is Card.Unit unit)
            {
                CombatSnapshot snapshot = CombatInputAdapter.Instance.GetApplicationService()?.GetSnapshot();
                int turn = snapshot?.Turn ?? 0;
                int actorId = snapshot?.CurrentActorId ?? 1;
                var command = new DeployUnitCommand(
                    turn,
                    actorId,
                    unit.Id.GetHashCode(),
                    nodeId
                );
                var events = CombatInputAdapter.Instance.Submit(command);

                bool deploySuccess = false;
                foreach (CombatEvent evt in events)
                {
                    if (evt is UnitDeployedEvent)
                    {
                        deploySuccess = true;
                        break;
                    }
                }

                if (deploySuccess)
                {
                    GD.Print($"[HandUI] Deploy successful, removing card from hand");
                    _player.RemoveFromHand(cardUI.Card);
                    _player.SpendEnergy(1);
                    cardUI.QueueFree();
                }
                else
                {
                    GD.Print($"[HandUI] Deploy failed, returning card to hand");
                    ReturnCardToHand(cardUI);
                }

                GD.Print($"[HandUI] DeployUnit submitted via command pipeline");
                return;
            }
            else if (CombatInputAdapter.Instance != null && cardUI.Card is Card.Order order)
            {
                CombatSnapshot snapshot = CombatInputAdapter.Instance.GetApplicationService()?.GetSnapshot();
                int turn = snapshot?.Turn ?? 0;
                int actorId = snapshot?.CurrentActorId ?? 1;
                PlayCardCommand command = new PlayCardCommand(turn, actorId, order.Id.GetHashCode(), nodeId, null);
                System.Collections.Generic.IReadOnlyList<CombatEvent> events = CombatInputAdapter.Instance.Submit(command);

                bool playSuccess = false;
                foreach (CombatEvent evt in events)
                {
                    if (evt is CardPlayedEvent)
                    {
                        playSuccess = true;
                        break;
                    }
                }

                if (playSuccess)
                {
                    GD.Print($"[HandUI] Order play successful, removing card from hand");
                    _player.RemoveFromHand(cardUI.Card);
                    _player.SpendEnergy(order.Cost);
                    cardUI.QueueFree();
                }
                else
                {
                    GD.Print($"[HandUI] Order play failed, returning card to hand");
                    ReturnCardToHand(cardUI);
                }
                return;
            }

            OnCardDroppedOnNode?.Invoke(cardUI.Card, nodeId);
        }

        private void OnCardDraggedToTarget(CardUI cardUI, Character.Character target)
        {
            GD.Print($"[HandUI] Card dragged to target: {cardUI.Card?.CardName} -> {target.CharacterName}");

            if (cardUI.Card is Card.Order order)
            {
                GD.Print($"[HandUI] Card is Order, RequiresTarget: {order.RequiresTarget}");
                if (order.RequiresTarget)
                {
                    GD.Print($"[HandUI] Invoking OnCardPlayRequested");
                    OnCardPlayRequested?.Invoke(cardUI.Card, target);

                    cardUI.ResetDragState();
                }
                else
                {
                    GD.Print($"[HandUI] Order does not require target, should use OnPlayWithoutTarget");
                }
            }
            else
            {
                GD.Print($"[HandUI] Card is not an Order");
            }
        }

        private void OnPlayWithoutTargetHandler(CardUI cardUI)
        {
            GD.Print($"[HandUI] Play without target: {cardUI.Card?.CardName}");

            if (cardUI.Card is Card.Order order)
            {
                OnCardPlayWithoutTarget?.Invoke(cardUI.Card);
                cardUI.QueueFree();
            }
            else
            {
                GD.Print($"[HandUI] Card is not an Order, cannot play without target");
                ReturnCardToHand(cardUI);
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
