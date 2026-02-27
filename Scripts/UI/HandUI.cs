using System;
using System.Collections.Generic;
using Godot;

namespace OdysseyCards.UI;

public partial class HandUI : Control
{
    [Export] public PackedScene CardScene { get; set; }

    private HBoxContainer _cardContainer;
    private Character.Player _player;
    private Combat.CombatManager _combatManager;

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

    public void SetCombatManager(Combat.CombatManager manager)
    {
        _combatManager = manager;
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
        GD.Print($"[HandUI] OnCardSelected called: {card?.Data.CardName}");
        
        if (OnCardPlayRequested != null && _player != null)
        {
            Character.Character target = null;
            
            if (card.Data.Target == Core.CardTarget.SingleEnemy)
            {
                if (_combatManager != null && _combatManager.Enemies.Count > 0)
                {
                    target = _combatManager.Enemies[0];
                }
                else
                {
                    var enemies = GetTree().GetNodesInGroup("Enemy");
                    GD.Print($"[HandUI] Found {enemies.Count} enemies via group");
                    if (enemies.Count > 0)
                    {
                        target = enemies[0] as Character.Character;
                    }
                }
            }
            
            GD.Print($"[HandUI] Target: {target?.CharacterName ?? "null"}");
            OnCardPlayRequested.Invoke(card, target);
        }
        else
        {
            GD.PrintErr($"[HandUI] Cannot play card - OnCardPlayRequested null: {OnCardPlayRequested == null}, _player null: {_player == null}");
        }
    }
}
