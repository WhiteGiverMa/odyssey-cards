using Godot;
using System.Collections.Generic;
using OdysseyCards.Card;
using OdysseyCards.Core;

namespace OdysseyCards.UI;

public partial class HandUI : Control
{
    [Export] public PackedScene CardScene { get; set; }

    private HBoxContainer _cardContainer;
    private Player.Player _player;

    public override void _Ready()
    {
        _cardContainer = GetNode<HBoxContainer>("CardContainer");
    }

    public void SetPlayer(Player.Player player)
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

    private void UpdateHand()
    {
        if (_cardContainer == null || _player == null)
            return;

        foreach (var child in _cardContainer.GetChildren())
        {
            child.QueueFree();
        }

        foreach (var card in _player.Hand)
        {
            CreateCardUI(card);
        }
    }

    private void CreateCardUI(Card card)
    {
        var cardUI = new CardUI();
        cardUI.SetCard(card);
        _cardContainer.AddChild(cardUI);
    }
}

public partial class CardUI : Control
{
    private Card _card;
    private bool _isHovered = false;
    private bool _isSelected = false;

    public event Action<Card> OnCardSelected;
    public event Action<Card> OnCardPlayed;

    public void SetCard(Card card)
    {
        _card = card;
        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        if (_card == null)
            return;

        var nameLabel = GetNodeOrNull<Label>("NameLabel");
        var costLabel = GetNodeOrNull<Label>("CostLabel");
        var descLabel = GetNodeOrNull<Label>("DescLabel");

        if (nameLabel != null)
            nameLabel.Text = _card.Data.CardName;
        if (costLabel != null)
            costLabel.Text = _card.CurrentCost.ToString();
        if (descLabel != null)
            descLabel.Text = _card.Data.Description;
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseEvent)
        {
            if (mouseEvent.ButtonIndex == MouseButton.Left)
            {
                if (mouseEvent.Pressed)
                {
                    _isSelected = !_isSelected;
                    OnCardSelected?.Invoke(_card);
                }
            }
        }
    }

    public void OnMouseEntered()
    {
        _isHovered = true;
    }

    public void OnMouseExited()
    {
        _isHovered = false;
    }
}
