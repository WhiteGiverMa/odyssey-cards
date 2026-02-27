using Godot;
using System.Collections.Generic;
using OdysseyCards.Character;
using OdysseyCards.Core;

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
    private Player.Player _player;

    public override void _Ready()
    {
        _playerHealthBar = GetNode<HealthBar>("PlayerPanel/HealthBar");
        _handUI = GetNode<HandUI>("HandUI");
        _endTurnButton = GetNode<Button>("EndTurnButton");
        _energyLabel = GetNode<Label>("PlayerPanel/EnergyLabel");
        _drawPileLabel = GetNode<Label>("DrawPileLabel");
        _discardPileLabel = GetNode<Label>("DiscardPileLabel");

        _endTurnButton.Pressed += OnEndTurnPressed;
    }

    public void Initialize(Player.Player player, Combat.CombatManager combatManager)
    {
        _player = player;
        _combatManager = combatManager;

        if (_playerHealthBar != null)
            _playerHealthBar.SetTarget(player);

        if (_handUI != null)
            _handUI.SetPlayer(player);

        _player.OnEnergyChanged += UpdateEnergy;
        _player.OnDrawPileChanged += UpdateDrawPile;
        _player.OnDiscardPileChanged += UpdateDiscardPile;

        UpdateEnergy(_player.CurrentEnergy, _player.MaxEnergy);
        UpdateDrawPile();
        UpdateDiscardPile();

        CreateEnemyHealthBars();
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
            var healthBar = HealthBarScene.Instantiate<HealthBar>();
            healthBar.SetTarget(enemy);
            enemyContainer.AddChild(healthBar);
            _enemyHealthBars.Add(healthBar);
        }
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
