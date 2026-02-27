using System;
using System.Collections.Generic;
using Godot;

namespace OdysseyCards.UI;

public partial class CombatUI : Control
{
    [Export] public PackedScene HealthBarScene { get; set; }

    private HealthBar _playerHealthBar;
    private List<HealthBar> _enemyHealthBars = new();
    private HandUI _handUI;
    private Button _endTurnButton;
    private Label _energyLabel;
    private Label _drawPileLabel;
    private Label _discardPileLabel;

    private Combat.CombatManager _combatManager;
    private Character.Player _player;

    public override void _Ready()
    {
        AddToGroup("CombatUI");
        
        _playerHealthBar = GetNode<HealthBar>("PlayerPanel/HealthBar");
        _handUI = GetNode<HandUI>("HandUI");
        _endTurnButton = GetNode<Button>("EndTurnButton");
        _energyLabel = GetNode<Label>("PlayerPanel/EnergyLabel");
        _drawPileLabel = GetNode<Label>("DrawPileLabel");
        _discardPileLabel = GetNode<Label>("DiscardPileLabel");

        _endTurnButton.Pressed += OnEndTurnPressed;
    }

    public void Initialize(Character.Player player, Combat.CombatManager combatManager)
    {
        _player = player;
        _combatManager = combatManager;

        if (_playerHealthBar != null)
            _playerHealthBar.SetTarget(player);

        if (_handUI != null)
        {
            _handUI.SetPlayer(player);
            _handUI.OnCardPlayRequested += OnCardPlayRequested;
        }

        _player.OnEnergyChanged += UpdateEnergy;
        _player.OnDrawPileChanged += UpdateDrawPile;
        _player.OnDiscardPileChanged += UpdateDiscardPile;

        UpdateEnergy(_player.CurrentEnergy, _player.MaxEnergy);
        UpdateDrawPile();
        UpdateDiscardPile();

        CreateEnemyHealthBars();
    }

    private void OnCardPlayRequested(Card.Card card, Character.Character target)
    {
        _combatManager?.PlayCard(card, target);
    }

    private void CreateEnemyHealthBars()
    {
        if (_combatManager == null || HealthBarScene == null)
            return;

        var enemyContainer = GetNodeOrNull<Control>("EnemyContainer");
        if (enemyContainer == null)
            return;

        foreach (var enemy in _combatManager.Enemies)
        {
            var enemyPanel = CreateEnemyPlaceholder(enemy);
            enemyContainer.AddChild(enemyPanel);
            _enemyHealthBars.Add(enemyPanel.GetNode<HealthBar>("HealthBar"));
        }
    }

    private Control CreateEnemyPlaceholder(Character.Enemy enemy)
    {
        var container = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(200, 280)
        };
        container.AddToGroup("Enemy");

        var placeholder = new ColorRect
        {
            CustomMinimumSize = new Vector2(180, 180),
            Color = new Color(0.5f, 0.4f, 0.35f),
            SizeFlagsHorizontal = SizeFlags.Expand | SizeFlags.ShrinkCenter
        };
        container.AddChild(placeholder);

        var nameLabel = new Label
        {
            Text = enemy.CharacterName,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            LabelSettings = new LabelSettings { FontColor = new Color(1f, 1f, 1f), FontSize = 20 }
        };
        placeholder.AddChild(nameLabel);

        var healthBarContainer = new Control
        {
            CustomMinimumSize = new Vector2(180, 50)
        };
        container.AddChild(healthBarContainer);

        var healthBar = new ProgressBar
        {
            CustomMinimumSize = new Vector2(180, 24),
            MaxValue = enemy.MaxHealth,
            Value = enemy.CurrentHealth,
            ShowPercentage = false
        };
        healthBarContainer.AddChild(healthBar);

        var healthLabel = new Label
        {
            Text = $"{enemy.CurrentHealth}/{enemy.MaxHealth}",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            LabelSettings = new LabelSettings { FontColor = new Color(1f, 1f, 1f), FontSize = 14 }
        };
        healthBar.AddChild(healthLabel);

        enemy.OnHealthChanged += (current, max) =>
        {
            healthBar.MaxValue = max;
            healthBar.Value = current;
            healthLabel.Text = $"{current}/{max}";
        };

        container.SetMeta("Enemy", enemy);
        return container;
    }

    private void UpdateEnergy(int current, int max)
    {
        if (_energyLabel != null)
            _energyLabel.Text = $"{current}/{max}";
    }

    private void UpdateDrawPile()
    {
        if (_drawPileLabel != null && _player != null)
            _drawPileLabel.Text = $"Draw: {_player.DrawPile.Count}";
    }

    private void UpdateDiscardPile()
    {
        if (_discardPileLabel != null && _player != null)
            _discardPileLabel.Text = $"Discard: {_player.DiscardPile.Count}";
    }

    private void OnEndTurnPressed()
    {
        _combatManager?.EndPlayerTurn();
    }

    public void ShowCombatResult(bool victory)
    {
        var resultLabel = GetNodeOrNull<Label>("ResultLabel");
        if (resultLabel != null)
        {
            resultLabel.Text = victory ? "VICTORY!" : "DEFEAT";
            resultLabel.Visible = true;
        }
    }
}
