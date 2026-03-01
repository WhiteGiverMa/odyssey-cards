using System;
using Godot;

namespace OdysseyCards.UI
{
    public partial class HandUI : Control
    {
        [Export] public PackedScene CardScene { get; set; }

        private HBoxContainer _cardContainer;
        private Character.Player _player;
        private Combat.CombatManager _combatManager;

        public event Action<Card.Card, Character.Character> OnCardPlayRequested;
        public event Action<Card.Card, Vector2> OnCardDragStarted;
        public event Action<Card.Card, Vector2> OnCardDragEnded;
        public event Action<Card.Card, int> OnCardDroppedOnNode;

        public override void _Ready()
        {
            _cardContainer = GetNode<HBoxContainer>("CardContainer");
        }

        public void SetPlayer(Character.Player player)
        {
            if (_player != null)
            {
                _player.OnHandChanged -= UpdateHand;
            }

            _player = player;

            if (_player != null)
            {
                _player.OnHandChanged += UpdateHand;
                UpdateHand();
            }
        }

        public void SetCombatManager(Combat.CombatManager manager)
        {
            _combatManager = manager;
        }

        private void UpdateHand()
        {
            if (_cardContainer == null || _player == null)
            {
                return;
            }

            foreach (Node? child in _cardContainer.GetChildren())
            {
                child.QueueFree();
            }

            int handCount = _player.Hand.Count;
            float scale = CalculateCardScale(handCount);

            foreach (Card.Card card in _player.Hand)
            {
                CreateCardUI(card, scale);
            }
        }

        private float CalculateCardScale(int cardCount)
        {
            if (cardCount <= 5)
            {
                return 1.0f;
            }

            float baseScale = 1.0f;
            float minScale = 0.6f;

            float newScale = baseScale - ((cardCount - 5) * 0.05f);
            return Mathf.Max(newScale, minScale);
        }

        private void CreateCardUI(Card.Card card, float scale = 1.0f)
        {
            var cardUI = new CardUI();
            cardUI.SetCard(card);
            cardUI.OnCardDraggedToTarget += OnCardDraggedToTarget;
            cardUI.OnDragStarted += OnCardDragStartedHandler;
            cardUI.OnDragEnded += OnCardDragEndedHandler;
            cardUI.OnDroppedOnNode += OnCardDroppedOnNodeHandler;

            cardUI.Scale = new Vector2(scale, scale);

            _cardContainer.AddChild(cardUI);
        }

        private void OnCardDragStartedHandler(CardUI cardUI, Vector2 position)
        {
            if (cardUI.Card == null)
            {
                return;
            }

            GD.Print($"[HandUI] Drag started: {cardUI.Card.CardName}");

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

            OnCardDragEnded?.Invoke(cardUI.Card, position);
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
