using Godot;
using OdysseyCards.Localization;

namespace OdysseyCards.UI;

public partial class CombatUI : Control
{
    [Export] public PackedScene HealthBarScene { get; set; }

    private HealthBar _playerHealthBar;
    private HandUI _handUI;
    private Button _endTurnButton;
    private Label _energyLabel;
    private Button _drawPileButton;
    private Button _discardPileButton;
    private Label _drawPileCountLabel;
    private Label _discardPileCountLabel;

    private BattleMapUI _battleMapUI;
    private DeckViewUI _deckViewUI;
    private HBoxContainer _enemyHandArea;

    private Combat.CombatManager _combatManager;
    private Character.Player _player;

    public override void _Ready()
    {
        GD.Print("[CombatUI] _Ready called");
        AddToGroup("CombatUI");

        // CombatUI 是全屏覆盖层；要让底层 MainContainer 按钮可点击，必须 Ignore 而不是 Pass
        // Pass 仍会命中该节点并沿父级冒泡，底层兄弟节点拿不到事件
        MouseFilter = MouseFilterEnum.Ignore;
        GD.Print("[CombatUI] MouseFilter set to Ignore for true click-through");

        _playerHealthBar = GetNode<HealthBar>("../MainContainer/BottomBar/MarginContainer/PlayerContainer/PlayerPanel/HealthBar");
        _handUI = GetNode<HandUI>("../MainContainer/BottomBar/MarginContainer/PlayerContainer/HandUI");
        _endTurnButton = GetNode<Button>("../MainContainer/BottomBar/MarginContainer/PlayerContainer/EndTurnButton");
        _energyLabel = GetNode<Label>("../MainContainer/BottomBar/MarginContainer/PlayerContainer/PlayerPanel/EnergyLabel");
        _drawPileButton = GetNode<Button>("../MainContainer/BottomBar/MarginContainer/PlayerContainer/PileButtonsContainer/DrawPileButton");
        _discardPileButton = GetNode<Button>("../MainContainer/BottomBar/MarginContainer/PlayerContainer/PileButtonsContainer/DiscardPileButton");
        _drawPileCountLabel = GetNode<Label>("../MainContainer/BottomBar/MarginContainer/PlayerContainer/PileButtonsContainer/DrawPileButton/DrawPileCount");
        _discardPileCountLabel = GetNode<Label>("../MainContainer/BottomBar/MarginContainer/PlayerContainer/PileButtonsContainer/DiscardPileButton/DiscardPileCount");

        _battleMapUI = GetNode<BattleMapUI>("../MainContainer/CenterContainer/MapArea/BattleMapUI");
        _enemyHandArea = GetNode<HBoxContainer>("../MainContainer/TopBar/EnemyArea/MarginContainer/EnemyHandArea");

        GD.Print($"[CombatUI] Node references - HealthBar: {_playerHealthBar != null}, HandUI: {_handUI != null}, EnergyLabel: {_energyLabel != null}");
        GD.Print($"[CombatUI] PileButtons - DrawPile: {_drawPileButton != null}, DiscardPile: {_discardPileButton != null}");

        if (_endTurnButton != null)
        {
            _endTurnButton.Pressed += OnEndTurnPressed;
        }
        if (_drawPileButton != null)
        {
            _drawPileButton.Pressed += OnDrawPileClicked;
        }
        if (_discardPileButton != null)
        {
            _discardPileButton.Pressed += OnDiscardPileClicked;
        }

        Localization.Localization.OnLanguageChanged += OnLanguageChanged;

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
        GD.Print($"[CombatUI] OnDrawPileClicked, _player is null: {_player == null}, _deckViewUI is null: {_deckViewUI == null}");
        if (_player != null && _deckViewUI != null)
        {
            string title = Localization.Localization.T("ui.combat.draw_pile", "抽牌堆", ("count", _player.DrawPile.Count.ToString()));
            _deckViewUI.ShowDeckList(title, _player.DrawPile);
        }
    }

    private void OnDiscardPileClicked()
    {
        GD.Print($"[CombatUI] OnDiscardPileClicked, _player is null: {_player == null}, _deckViewUI is null: {_deckViewUI == null}");
        if (_player != null && _deckViewUI != null)
        {
            string title = Localization.Localization.T("ui.combat.discard_pile", "弃牌堆", ("count", _player.DiscardPile.Count.ToString()));
            _deckViewUI.ShowDeckList(title, _player.DiscardPile);
        }
    }

    public void Initialize(Character.Player player, Combat.CombatManager combatManager)
    {
        GD.Print($"[CombatUI] Initialize called, player is null: {player == null}, combatManager is null: {combatManager == null}");

        _player = player ?? throw new System.ArgumentNullException(nameof(player));
        _combatManager = combatManager ?? throw new System.ArgumentNullException(nameof(combatManager));

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
            _handUI.OnCardDroppedOnNode += OnCardDroppedOnNode;
        }
        else
        {
            GD.PrintErr("[CombatUI] HandUI is null!");
        }

        _player.OnEnergyChanged += UpdateEnergy;
        _player.OnDrawPileChanged += UpdateDrawPile;
        _player.OnDiscardPileChanged += UpdateDiscardPile;

        GD.Print("[CombatUI] Event handlers subscribed");

        UpdateEnergy(_player.CurrentEnergy, _player.MaxEnergy);
        GD.Print("[CombatUI] UpdateEnergy called");

        UpdateDrawPile();
        GD.Print("[CombatUI] UpdateDrawPile called");

        UpdateDiscardPile();
        GD.Print("[CombatUI] UpdateDiscardPile called");

        GD.Print("[CombatUI] Calling InitializeBattleMap");
        InitializeBattleMap();
        GD.Print("[CombatUI] Calling ConnectCombatManagerEvents");
        ConnectCombatManagerEvents();
        GD.Print("[CombatUI] Calling InitializeEnemyUI");
        InitializeEnemyUI();

        GD.Print("[CombatUI] Initialize completed");
    }

    private void InitializeEnemyUI()
    {
        if (_enemyHandArea == null || _combatManager == null)
        {
            GD.Print("[CombatUI] InitializeEnemyUI - null check failed");
            return;
        }

        foreach (var enemy in _combatManager.Enemies)
        {
            var enemyUI = new EnemyUI();
            enemyUI.SetEnemy(enemy);
            _enemyHandArea.AddChild(enemyUI);
            GD.Print($"[CombatUI] Created EnemyUI for: {enemy.CharacterName}");
        }
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

    private void OnCardDroppedOnNode(Card.Card card, int nodeId)
    {
        GD.Print($"[CombatUI] OnCardDroppedOnNode: {card?.CardName} -> Node {nodeId}");

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

    private void UpdateDiscardPile()
    {
        if (_discardPileCountLabel != null && _player != null)
        {
            _discardPileCountLabel.Text = $"{_player.DiscardPile.Count}";
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
            resultLabel.Text = victory
                ? Localization.Localization.T("ui.combat.victory", "VICTORY!")
                : Localization.Localization.T("ui.combat.defeat", "DEFEAT");
            resultLabel.Visible = true;
        }
    }

    public override void _ExitTree()
    {
        Localization.Localization.OnLanguageChanged -= OnLanguageChanged;

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

    private void OnLanguageChanged(string newLanguage)
    {
        UpdatePileLabels();
    }

    private void UpdatePileLabels()
    {
        UpdateDrawPile();
        UpdateDiscardPile();
    }
}
