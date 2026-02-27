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
    private CardUI _selectedCardUI;

    public Action<Card.Card, Character.Character> OnCardPlayRequested { get; set; }

    public override void _Ready()
    {
        _cardContainer = GetNode<HBoxContainer>("CardContainer");
        
        var inputManager = GetTree().Root.GetNode("InputManager");
        if (inputManager == null)
        {
            inputManager = new Node { Name = "InputManager" };
            GetTree().Root.AddChild(inputManager);
        }
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

        _selectedCardUI = null;

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
        cardUI.OnCardDeselected += OnCardDeselected;
        cardUI.OnCardDraggedToTarget += OnCardDraggedToTarget;
        _cardContainer.AddChild(cardUI);
    }

    private void OnCardDraggedToTarget(CardUI cardUI, Character.Character target)
    {
        GD.Print($"[HandUI] OnCardDraggedToTarget: {cardUI.Card?.Data.CardName} -> {target.CharacterName}");
        
        if (cardUI.Card.Data.Target == Core.CardTarget.SingleEnemy)
        {
            if (OnCardPlayRequested != null)
            {
                OnCardPlayRequested.Invoke(cardUI.Card, target);
            }
            
            cardUI.SetSelected(false);
            cardUI.ResetDragState();
            HighlightEnemies(false);
        }
    }

    private void OnCardSelected(CardUI cardUI)
    {
        GD.Print($"[HandUI] OnCardSelected: {cardUI.Card?.Data.CardName}");
        
        if (_selectedCardUI != null && _selectedCardUI != cardUI)
        {
            _selectedCardUI.SetSelected(false);
        }
        
        _selectedCardUI = cardUI;
        
        if (cardUI.Card.Data.Target == Core.CardTarget.SingleEnemy)
        {
            GD.Print("[HandUI] Waiting for target selection...");
            HighlightEnemies(true);
        }
    }

    private void OnCardDeselected(CardUI cardUI)
    {
        GD.Print($"[HandUI] OnCardDeselected: {cardUI.Card?.Data.CardName}");
        
        if (_selectedCardUI == cardUI)
        {
            _selectedCardUI = null;
            HighlightEnemies(false);
        }
    }

    private void HighlightEnemies(bool highlight)
    {
        var enemies = GetTree().GetNodesInGroup("Enemy");
        foreach (var node in enemies)
        {
            if (node is VBoxContainer container)
            {
                var bg = container.GetChild<ColorRect>(0);
                if (bg != null)
                {
                    bg.Color = highlight ? new Color(1f, 0.5f, 0.5f) : new Color(0.5f, 0.4f, 0.35f);
                }
            }
        }
    }

    public void OnEnemyClicked(Character.Enemy enemy)
    {
        GD.Print($"[HandUI] OnEnemyClicked: {enemy.CharacterName}");
        
        if (_selectedCardUI != null && _selectedCardUI.Card != null)
        {
            if (_selectedCardUI.Card.Data.Target == Core.CardTarget.SingleEnemy)
            {
                GD.Print($"[HandUI] Playing card {_selectedCardUI.Card.Data.CardName} on {enemy.CharacterName}");
                
                if (OnCardPlayRequested != null)
                {
                    OnCardPlayRequested.Invoke(_selectedCardUI.Card, enemy);
                }
                
                _selectedCardUI.SetSelected(false);
                _selectedCardUI = null;
                HighlightEnemies(false);
            }
        }
    }

    public void ClearSelection()
    {
        if (_selectedCardUI != null)
        {
            _selectedCardUI.SetSelected(false);
            _selectedCardUI = null;
            HighlightEnemies(false);
        }
    }
}
