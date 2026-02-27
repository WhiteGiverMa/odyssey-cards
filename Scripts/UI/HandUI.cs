using System;
using System.Collections.Generic;
using Godot;

namespace OdysseyCards.UI;

public partial class HandUI : Control
{
    [Export] public PackedScene CardScene { get; set; }

    private HBoxContainer _cardContainer;
    private Character.Player _player;

    public Action<Card.Card, Character.Character> OnCardPlayRequested { get; set; }

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

    private void CreateCardUI(Card.Card card)
    {
        var cardUI = new CardUI();
        cardUI.SetCard(card);
        cardUI.OnCardSelected += OnCardSelected;
        _cardContainer.AddChild(cardUI);
    }

    private void OnCardSelected(Card.Card card)
    {
        if (OnCardPlayRequested != null && _player != null)
        {
            var enemies = GetTree().GetNodesInGroup("Enemy");
            Character.Character target = null;
            if (card.Data.Target == Core.CardTarget.SingleEnemy && enemies.Count > 0)
            {
                target = enemies[0] as Character.Character;
            }
            OnCardPlayRequested.Invoke(card, target);
        }
    }
}
