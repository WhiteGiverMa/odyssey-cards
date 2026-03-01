using Godot;

namespace OdysseyCards.UI;

public partial class CombatUI : Control
{
    [Export] public PackedScene HealthBarScene { get; set; }

    private HealthBar _playerHealthBar;
    private HandUI _handUI;
    private Button _endTurnButton;
    private Label _energyLabel;
    private Button _drawPileButton;
    private Button _exhaustPileButton;
    private Label _drawPileCountLabel;
    private Label _exhaustPileCountLabel;

    private BattleMapUI _battleMapUI;
    private DeckViewUI _deckViewUI;

    private Combat.CombatManager _combatManager;
    private Character.Player _player;

    public override void _Ready()
    {
        GD.Print("[CombatUI] _Ready called");
        AddToGroup("CombatUI");

        _playerHealthBar = GetNode<HealthBar>("PlayerArea/PlayerPanel/HealthBar");
        _handUI = GetNode<HandUI>("PlayerArea/HandUI");
        _endTurnButton = GetNode<Button>("PlayerArea/EndTurnButton");
        _energyLabel = GetNode<Label>("PlayerArea/PlayerPanel/EnergyLabel");
        _drawPileButton = GetNode<Button>("PlayerArea/DrawPileButton");
        _exhaustPileButton = GetNode<Button>("PlayerArea/ExhaustPileButton");
        _drawPileCountLabel = GetNode<Label>("PlayerArea/DrawPileButton/DrawPileCount");
        _exhaustPileCountLabel = GetNode<Label>("PlayerArea/ExhaustPileButton/ExhaustPileCount");

        _battleMapUI = GetNode<BattleMapUI>("MapArea/BattleMapUI");

        GD.Print($"[CombatUI] Node references - HealthBar: {_playerHealthBar != null}, HandUI: {_handUI != null}, EnergyLabel: {_energyLabel != null}");

        _endTurnButton.Pressed += OnEndTurnPressed;
        _drawPileButton.Pressed += OnDrawPileClicked;
        _exhaustPileButton.Pressed += OnExhaustPileClicked;

        CreateDeckViewUI();
    }

    private void CreateDeckViewUI()
    {
        _deckViewUI = new DeckViewUI();
        _deckViewUI.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(_deckViewUI);
    }

    private void OnDrawPileClicked()
    {
        if (_player != null)
        {
            _deckViewUI.ShowDeckList($"抽牌堆 ({_player.DrawPile.Count})", _player.DrawPile);
        }
    }

    private void OnExhaustPileClicked()
    {
        if (_player != null)
        {
            _deckViewUI.ShowDeckList($"消耗堆 ({_player.ExhaustPile.Count})", _player.ExhaustPile);
        }
    }

    public void Initialize(Character.Player player, Combat.CombatManager combatManager)
    {
        GD.Print($"[CombatUI] Initialize called, player is null: {player == null}, combatManager is null: {combatManager == null}");

        _player = player;
        _combatManager = combatManager;

        if (_playerHealthBar != null)
        {
            GD.Print("[CombatUI] Setting health bar target");
            _playerHealthBar.SetTarget(player);
        }
        else
        {
            GD.PrintErr("[CombatUI] HealthBar is null!");
        }

        if (_handUI != null)
        {
            GD.Print("[CombatUI] Setting HandUI player");
            _handUI.SetPlayer(player);
            _handUI.SetCombatManager(combatManager);
            _handUI.OnCardPlayRequested += OnCardPlayRequested;
        }
        else
        {
            GD.PrintErr("[CombatUI] HandUI is null!");
        }

        _player.OnEnergyChanged += UpdateEnergy;
        _player.OnDrawPileChanged += UpdateDrawPile;
        _player.OnExhaustPileChanged += UpdateExhaustPile;

        GD.Print("[CombatUI] Event handlers subscribed");

        UpdateEnergy(_player.CurrentEnergy, _player.MaxEnergy);
        GD.Print("[CombatUI] UpdateEnergy called");

        UpdateDrawPile();
        GD.Print("[CombatUI] UpdateDrawPile called");

        UpdateExhaustPile();
        GD.Print("[CombatUI] UpdateExhaustPile called");

        GD.Print("[CombatUI] Calling InitializeBattleMap");
        InitializeBattleMap();
        GD.Print("[CombatUI] Calling ConnectCombatManagerEvents");
        ConnectCombatManagerEvents();

        GD.Print("[CombatUI] Initialize completed");
    }

    private void InitializeBattleMap()
    {
        GD.Print($"[CombatUI] InitializeBattleMap called, _battleMapUI is null: {_battleMapUI == null}, _combatManager is null: {_combatManager == null}");

        if (_battleMapUI == null || _combatManager == null)
        {
            GD.Print("[CombatUI] InitializeBattleMap early return - null check failed");
            return;
        }

        GD.Print($"[CombatUI] BattleMap is null: {_combatManager.BattleMap == null}");
        GD.Print("[CombatUI] Calling SetBattleMap");
        _battleMapUI.SetBattleMap(_combatManager.BattleMap);
        GD.Print("[CombatUI] SetBattleMap completed, connecting OnNodeDropTarget");
        _battleMapUI.OnNodeDropTarget += OnNodeDropTarget;
        GD.Print("[CombatUI] InitializeBattleMap completed");
    }

    private void ConnectCombatManagerEvents()
    {
        if (_combatManager == null)
        {
            return;
        }

        _combatManager.OnUnitDeployed += OnUnitDeployed;
        _combatManager.OnUnitMoved += OnUnitMoved;
        _combatManager.OnUnitAttacked += OnUnitAttacked;
        _combatManager.OnAttackRangeShow += OnAttackRangeShow;
        _combatManager.OnAttackRangeHide += OnAttackRangeHide;
    }

    private void OnNodeDropTarget(int nodeId, Card.Card card)
    {
        if (_combatManager == null)
        {
            return;
        }

        if (_combatManager.CurrentSelectionMode == Combat.SelectionMode.DeployUnit)
        {
            _ = _combatManager.OnNodeSelected(nodeId);
        }
    }

    private void OnUnitDeployed(Card.Unit unit)
    {
        GD.Print($"[CombatUI] Unit deployed: {unit.CardName} at node {unit.CurrentNode}");
        UpdateBattleMapDisplay();
    }

    private void OnUnitMoved(Card.Unit unit, int fromNode, int toNode)
    {
        GD.Print($"[CombatUI] Unit moved: {unit.CardName} from {fromNode} to {toNode}");
        UpdateBattleMapDisplay();
    }

    private void OnUnitAttacked(Card.Unit attacker, Card.Unit target)
    {
        GD.Print($"[CombatUI] Attack: {attacker.CardName} -> {target.CardName}");
    }

    private void OnAttackRangeShow(System.Collections.Generic.List<int> nodeIds)
    {
        if (_battleMapUI != null)
        {
            _battleMapUI.SetAttackMode(true, nodeIds);
        }
    }

    private void OnAttackRangeHide()
    {
        if (_battleMapUI != null)
        {
            _battleMapUI.SetAttackMode(false, null);
        }
    }

    private void UpdateBattleMapDisplay()
    {
        if (_battleMapUI == null)
        {
            return;
        }

        _battleMapUI.RebuildUI();
    }

    private void OnCardPlayRequested(Card.Card card, Character.Character target)
    {
        GD.Print($"[CombatUI] OnCardPlayRequested called: {card?.CardName}, target: {target?.CharacterName}");
        GD.Print($"[CombatUI] _combatManager is null: {_combatManager == null}");
        _combatManager?.PlayCard(card, target);
    }

    private void UpdateEnergy(int current, int max)
    {
        if (_energyLabel != null)
        {
            _energyLabel.Text = $"{current}/{max}";
        }
    }

    private void UpdateDrawPile()
    {
        if (_drawPileCountLabel != null && _player != null)
        {
            _drawPileCountLabel.Text = $"{_player.DrawPile.Count}";
        }
    }

    private void UpdateExhaustPile()
    {
        if (_exhaustPileCountLabel != null && _player != null)
        {
            _exhaustPileCountLabel.Text = $"{_player.ExhaustPile.Count}";
        }
    }

    private void OnEndTurnPressed()
    {
        _combatManager?.EndPlayerTurn();
    }

    public void ShowCombatResult(bool victory)
    {
        Label resultLabel = GetNodeOrNull<Label>("ResultLabel");
        if (resultLabel != null)
        {
            resultLabel.Text = victory ? "VICTORY!" : "DEFEAT";
            resultLabel.Visible = true;
        }
    }

    public override void _ExitTree()
    {
        if (_combatManager != null)
        {
            _combatManager.OnUnitDeployed -= OnUnitDeployed;
            _combatManager.OnUnitMoved -= OnUnitMoved;
            _combatManager.OnUnitAttacked -= OnUnitAttacked;
            _combatManager.OnAttackRangeShow -= OnAttackRangeShow;
            _combatManager.OnAttackRangeHide -= OnAttackRangeHide;
        }

        if (_battleMapUI != null)
        {
            _battleMapUI.OnNodeDropTarget -= OnNodeDropTarget;
        }
    }
}
