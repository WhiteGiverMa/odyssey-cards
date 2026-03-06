using System;
using System.Collections.Generic;
using Godot;
using OdysseyCards.Application.Combat;
using OdysseyCards.Application.Combat.UseCases;
using OdysseyCards.Application.Ports;
using OdysseyCards.Application.Reward;
using OdysseyCards.Card;
using OdysseyCards.Character;
using OdysseyCards.Core;
using OdysseyCards.Domain.Combat.AI;
using OdysseyCards.Domain.Combat.Commands;
using OdysseyCards.Domain.Combat.Engine;
using OdysseyCards.Domain.Combat.Events;
using OdysseyCards.Domain.Combat.Model;
using OdysseyCards.Infrastructure.Replay;
using OdysseyCards.Map;
using OdysseyCards.Presentation.Events;
using OdysseyCards.Presentation.Input;

namespace OdysseyCards.Combat
{
    public enum CombatState
    {
        NotStarted,
        PlayerTurn,
        EnemyTurn,
        Victory,
        Defeat
    }

    public enum SelectionMode
    {
        None,
        DeployUnit,
        MoveUnit,
        AttackTarget
    }

    public partial class CombatManager : Node, CombatEventUIBridge, CombatSnapshotProvider
    {
        public static CombatManager Instance { get; private set; }

        public CombatState State { get; private set; } = CombatState.NotStarted;
        public Player Player { get; private set; }
        public List<Enemy> Enemies { get; private set; } = [];
        public int TurnCount { get; private set; }
        public bool IsPlayerFirst { get; private set; }
        public BattleMap BattleMap { get; private set; }
        public SelectionMode CurrentSelectionMode { get; private set; } = SelectionMode.None;

        public event Action OnCombatStart;
        public event Action OnTurnStart;
        public event Action OnTurnEnd;
        public event Action<CombatState> OnCombatEnd;
        public event Action<int, int, string> OnUnitDeployed;
        public event Action<int, int, int, string> OnUnitMoved;
        public event Action<int, int?, int, int> OnUnitAttacked;
        public event Action<List<int>> OnAttackRangeShow;
        public event Action OnAttackRangeHide;
        public event Action<List<ICardData>> OnCombatRewards;

        private CombatApplicationService _applicationService;
        private JsonlReplayWriter _replayWriter;
        private ProcessRewardUseCase _processRewardUseCase;
        private IRewardService _rewardService;
        private CombatEventProcessor _eventProcessor;
        private DefaultCombatEventUIBridge _uiBridge;
        private DomainEnemyAI _enemyAI;

        private int _selectedUnitId = -1;
        private int _selectedCardInstanceId = -1;
        private string _selectedCardName = "";
        private int _playerId = 1;
        private int _enemyId = 2;
        private readonly Dictionary<int, Unit> _unitInstances = new();
        private int _nextUnitInstanceId = 1000;

        public override void _Ready()
        {
            Instance = this;
            _ = CallDeferred(nameof(InitializeCombat));
        }

        private void InitializeCombat()
        {
            GD.Print("[CombatManager] InitializeCombat started");

            BattleMap = new BattleMap();
            AddChild(BattleMap);

            InitializePlayer();
            InitializeEnemies();
            InitializeUI();
            InitializeCommandSystem();
            StartCombat();

            GD.Print($"[CombatManager] After StartCombat, player hand count: {Player.Hand.Count}");
        }

        private void InitializePlayer()
        {
            if (GameManager.Instance?.CurrentPlayer == null)
            {
                GD.PrintErr("CombatManager: No player found! Creating fallback player.");
                Player = new Player
                {
                    CharacterName = "Player",
                    MaxHealth = 80,
                    MaxEnergy = 3
                };

                var deck = new Deck();
                deck.Initialize(CardFactory.GetStarterDeck1());
                Player.Initialize(deck);
            }
            else
            {
                Player = GameManager.Instance.CurrentPlayer;
                GD.Print($"[CombatManager] Got player from GameManager: {Player.CharacterName}");
            }

            if (GameManager.Instance != null)
            {
                (int currentHealth, int maxHealth) = GameManager.Instance.GetPlayerHQHealth();
                Player.RestoreHQHealth(currentHealth, maxHealth);
            }
        }

        private void InitializeEnemies()
        {
            Enemies = [];

            EnemyDeckData enemyDeckData = ResourceLoader.Load<EnemyDeckData>("res://Resources/EnemyDeckData.tres");
            enemyDeckData ??= CreateFallbackEnemyDeck();

            var enemy = new Enemy();
            enemy.Initialize(enemyDeckData);
            Enemies.Add(enemy);
        }

        private void InitializeUI()
        {
            GD.Print("[CombatManager] Looking for CombatUI in group");
            if (GetTree().GetFirstNodeInGroup("CombatUI") is UI.CombatUI ui)
            {
                GD.Print("[CombatManager] Found CombatUI, initializing...");
                ui.Initialize(Player, this);
            }
            else
            {
                GD.PrintErr("[CombatManager] CombatUI not found in group!");
            }
        }

        private void InitializeCommandSystem()
        {
            GD.Print("[CombatManager] Initializing command system...");

            var domainEngine = new DomainCombatEngine();

            string replayPath = $"user://replays/combat_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}.jsonl";
            _replayWriter = new JsonlReplayWriter(ProjectSettings.GlobalizePath(replayPath));

            _enemyAI = new DomainEnemyAI();
            _applicationService = new CombatApplicationService(domainEngine, _replayWriter, null, _enemyAI);

            _uiBridge = new DefaultCombatEventUIBridge(this);
            _eventProcessor = new CombatEventProcessor(_applicationService, this);

            _eventProcessor.OnCombatStarted += evt => OnCombatStart?.Invoke();
            _eventProcessor.OnTurnStarted += evt => OnTurnStart?.Invoke();
            _eventProcessor.OnTurnEnded += evt => OnTurnEnd?.Invoke();
            _eventProcessor.OnCombatEnded += evt =>
            {
                State = evt.IsVictory ? CombatState.Victory : CombatState.Defeat;
                OnCombatEnd?.Invoke(State);
            };
            _eventProcessor.OnUnitDeployed += evt =>
            {
                OnUnitDeployed?.Invoke(evt.UnitId, evt.NodeId, evt.UnitName);
            };
            _eventProcessor.OnUnitMoved += evt =>
            {
                OnUnitMoved?.Invoke(evt.UnitId, evt.FromNodeId, evt.ToNodeId, evt.UnitName);
            };
            _eventProcessor.OnDamageApplied += evt =>
            {
                OnUnitAttacked?.Invoke(evt.Amount, evt.SourceUnitId, evt.TargetUnitId ?? -1, evt.TargetHQOwnerId);
            };

            _ = new CombatInputAdapter(_applicationService);

            var resourceLoader = new Infrastructure.Godot.ResourceLoading.GodotCardResourceLoader();
            var logger = new Infrastructure.Godot.Logging.GodotLogger();
            var deckService = new Infrastructure.Godot.Services.GodotDeckService();
            _rewardService = new CardRewardService(resourceLoader, logger, deckService);
            _processRewardUseCase = new ProcessRewardUseCase(_rewardService);
            _processRewardUseCase.OnRewardsGenerated += HandleRewardsGenerated;

            GD.Print($"[CombatManager] Command system initialized, replay path: {replayPath}");
        }

        public void StartCombat()
        {
            GD.Print("[CombatManager] StartCombat called");

            var random = new RandomNumberGenerator();
            random.Randomize();
            IsPlayerFirst = random.Randf() > 0.5f;

            GD.Print($"[CombatManager] IsPlayerFirst: {IsPlayerFirst}");

            TurnCount = 1;

            foreach (Enemy enemy in Enemies)
            {
                enemy.StartTurn();
            }

            var setup = new CombatSetup
            {
                PlayerId = _playerId,
                PlayerStartingHealth = Player.HQMaxHealth,
                PlayerStartingEnergy = 1,
                PlayerMaxEnergy = 3,
                EnemyIds = new List<int> { _enemyId },
                EnemyStartingHealths = new List<int> { Enemies[0].HQMaxHealth },
                EnemyStartingEnergies = new List<int> { 3 },
                EnemyMaxEnergies = new List<int> { 3 },
                IsPlayerFirst = IsPlayerFirst
            };

            _applicationService.StartCombat(setup, (int)random.Seed);

            if (IsPlayerFirst)
            {
                State = CombatState.PlayerTurn;
                Player.SetEnergy(1, 1);
                GD.Print("[CombatManager] Player first, drawing 4 cards");
                Player.DrawCards(4);
            }
            else
            {
                State = CombatState.EnemyTurn;
                Player.SetEnergy(0, 0);
                GD.Print("[CombatManager] Enemy first, drawing 5 cards");
                Player.DrawCards(5);
                ExecuteEnemyTurns();
            }

            GD.Print($"[CombatManager] Combat started, player hand count: {Player.Hand.Count}");
        }

        public void EndPlayerTurn()
        {
            if (State != CombatState.PlayerTurn)
            {
                return;
            }

            CancelSelection();

            foreach (Unit unit in GetPlayerUnits())
            {
                unit.OnTurnEnd();
            }

            Player.EndTurn();

            var command = new EndTurnCommand(TurnCount, _playerId);
            _applicationService.Submit(command);

            State = CombatState.EnemyTurn;
            OnTurnEnd?.Invoke();

            ExecuteEnemyTurns();
        }

        private void ExecuteEnemyTurns()
        {
            var snapshot = _applicationService.GetSnapshot();
            var context = BuildAIContext(snapshot);

            var events = _applicationService.ExecuteEnemyTurn(_enemyId, context);

            foreach (var evt in events)
            {
                if (evt is CombatEndedEvent)
                {
                    return;
                }
            }

            StartNewTurn();
        }

        private AIContext BuildAIContext(CombatSnapshot snapshot)
        {
            var enemy = Enemies[0];
            var ownUnits = new List<UnitSnapshot>();
            var enemyUnits = new List<UnitSnapshot>();

            foreach (var unit in snapshot.EnemyUnits)
            {
                ownUnits.Add(unit);
            }

            foreach (var unit in snapshot.PlayerUnits)
            {
                enemyUnits.Add(unit);
            }

            return new AIContext
            {
                ActorId = _enemyId,
                Turn = snapshot.Turn,
                Energy = enemy.CurrentEnergy,
                MaxEnergy = enemy.MaxEnergy,
                OwnUnits = ownUnits,
                EnemyUnits = enemyUnits,
                EnemyHQNodeId = BattleMap.PlayerHQNodeId,
                OwnHQNodeId = BattleMap.EnemyHQNodeId,
                HandCardIds = GetEnemyHandCardIds(),
                Board = new BoardInfo
                {
                    PlayerDeploymentNodeId = BattleMap.PlayerDeploymentNodeId,
                    EnemyDeploymentNodeId = BattleMap.EnemyDeploymentNodeId,
                    PlayerHQNodeId = BattleMap.PlayerHQNodeId,
                    EnemyHQNodeId = BattleMap.EnemyHQNodeId,
                    AllNodeIds = GetAllNodeIds()
                }
            };
        }

        private IReadOnlyList<int> GetEnemyHandCardIds()
        {
            var ids = new List<int>();
            foreach (var card in Enemies[0].Hand)
            {
                ids.Add(card.Id.GetHashCode());
            }
            return ids;
        }

        private IReadOnlyList<int> GetAllNodeIds()
        {
            var ids = new List<int>();
            for (int i = 0; i < 7; i++)
            {
                ids.Add(i);
            }
            return ids;
        }

        private void StartNewTurn()
        {
            TurnCount++;
            State = CombatState.PlayerTurn;

            foreach (Enemy enemy in Enemies)
            {
                if (!enemy.IsDead)
                {
                    enemy.StartTurn();
                    enemy.DrawCards(1);
                }
            }

            Player.StartTurn();
            Player.SetEnergy(1, 1);
            Player.DrawCards(1);

            foreach (Unit unit in GetPlayerUnits())
            {
                unit.OnTurnStart();
            }

            foreach (Unit unit in GetEnemyUnits())
            {
                unit.OnTurnStart();
            }

            OnTurnStart?.Invoke();
        }

        public void PlayCard(Card.Card card, Character.Character target)
        {
            GD.Print($"[CombatManager] PlayCard called: {card?.CardName}, target: {target?.CharacterName}, State: {State}");

            if (State != CombatState.PlayerTurn)
            {
                GD.Print($"[CombatManager] Cannot play card - not player turn");
                return;
            }

            if (card is Unit unit)
            {
                GD.Print($"[CombatManager] Card is Unit: {unit.CardName}");
                StartDeployMode(unit);
                return;
            }

            if (card is Order order)
            {
                GD.Print($"[CombatManager] Card is Order: {order.CardName}, Cost: {order.Cost}, Energy: {Player.CurrentEnergy}");
                if (!order.CanPlay(Player.CurrentEnergy))
                {
                    GD.Print($"[CombatManager] Cannot play card - not enough energy");
                    return;
                }

                GD.Print($"[CombatManager] Playing card {card.CardName}");
                Player.SpendEnergy(order.Cost);
                GD.Print($"[CombatManager] About to call order.Play, target: {target?.CharacterName}");
                order.Play(Player, target);
                GD.Print($"[CombatManager] order.Play completed");

                if (order.ShouldReturnToDeck)
                {
                    Player.ReturnToDrawPile(card);
                    GD.Print($"[CombatManager] Card returned to draw pile");
                }
                else
                {
                    Player.DiscardCard(card);
                    GD.Print($"[CombatManager] Card discarded");
                }

                var command = new PlayCardCommand(
                    TurnCount,
                    _playerId,
                    card.Id.GetHashCode(),
                    null,
                    null
                );
                _applicationService.Submit(command);
            }
            else
            {
                GD.Print($"[CombatManager] Card is neither Unit nor Order! Type: {card?.GetType().Name}");
            }
        }

        private void StartDeployMode(Unit unit)
        {
            GD.Print($"[CombatManager] StartDeployMode called for {unit.CardName}, DeployCost: {unit.DeployCost}, Energy: {Player.CurrentEnergy}");

            _selectedCardInstanceId = unit.Id.GetHashCode();
            _selectedCardName = unit.CardName;
            _selectedUnitId = -1;
            CurrentSelectionMode = SelectionMode.DeployUnit;
            GD.Print($"[CombatManager] SelectionMode changed to DeployUnit for {unit.CardName}, deploy node: {BattleMap.PlayerDeploymentNodeId}");
        }

        public void StartMoveMode(Unit unit)
        {
            GD.Print($"[CombatManager] StartMoveMode called for {unit.CardName}");
            if (!unit.CanMove())
            {
                GD.Print($"[CombatManager] Unit cannot move");
                return;
            }

            _selectedUnitId = unit.Id.GetHashCode();
            _selectedCardName = unit.CardName;
            _selectedCardInstanceId = -1;
            CurrentSelectionMode = SelectionMode.MoveUnit;

            List<int> movableNodes = BattleMap.GetMovableNodes(unit.CurrentNode, unit.OwnerType);
            OnAttackRangeShow?.Invoke(movableNodes);
            GD.Print($"[CombatManager] SelectionMode changed to MoveUnit for {unit.CardName}, can move to {movableNodes.Count} nodes");
        }

        public void StartAttackMode(Unit unit)
        {
            GD.Print($"[CombatManager] StartAttackMode called for {unit.CardName}");
            if (!unit.CanAttack())
            {
                GD.Print($"[CombatManager] Unit cannot attack");
                return;
            }

            _selectedUnitId = unit.Id.GetHashCode();
            _selectedCardName = unit.CardName;
            _selectedCardInstanceId = -1;
            CurrentSelectionMode = SelectionMode.AttackTarget;

            List<int> nodesInRange = BattleMap.GetNodesInRange(unit.CurrentNode, unit.Range);
            OnAttackRangeShow?.Invoke(nodesInRange);

            GD.Print($"[CombatManager] SelectionMode changed to AttackTarget for {unit.CardName}, nodes in range: {nodesInRange.Count}");
        }

        public void CancelSelection()
        {
            GD.Print($"[CombatManager] CancelSelection: previous mode={CurrentSelectionMode}");
            _selectedUnitId = -1;
            _selectedCardInstanceId = -1;
            _selectedCardName = "";
            CurrentSelectionMode = SelectionMode.None;
            OnAttackRangeHide?.Invoke();
            GD.Print("[CombatManager] SelectionMode reset to None");
        }

        public bool OnNodeSelected(int nodeId)
        {
            GD.Print($"[CombatManager] OnNodeSelected: nodeId={nodeId}, current mode={CurrentSelectionMode}");

            switch (CurrentSelectionMode)
            {
                case SelectionMode.DeployUnit:
                    GD.Print($"[CombatManager] Attempting to deploy to node {nodeId}");
                    return DeployUnitToNode(nodeId);

                case SelectionMode.MoveUnit:
                    GD.Print($"[CombatManager] Attempting to move to node {nodeId}");
                    return MoveUnitToNode(nodeId);

                case SelectionMode.AttackTarget:
                    GD.Print($"[CombatManager] Attempting to attack at node {nodeId}");
                    return AttackTargetAtNode(nodeId);

                case SelectionMode.None:
                    GD.Print($"[CombatManager] OnNodeSelected called with None mode - ignoring");
                    break;
            }

            return false;
        }

        private bool DeployUnitToNode(int nodeId)
        {
            if (!BattleMap.CanDeployTo(nodeId, NodeOwner.Player))
            {
                GD.Print($"[CombatManager] Cannot deploy to node {nodeId}");
                return false;
            }

            var snapshot = _applicationService.GetSnapshot();
            if (snapshot.PlayerEnergy < 1)
            {
                GD.Print($"[CombatManager] Not enough energy to deploy");
                return false;
            }

            var command = new DeployUnitCommand(
                TurnCount,
                _playerId,
                _selectedCardInstanceId,
                nodeId
            );

            var events = _applicationService.Submit(command);

            if (events.Count > 0)
            {
                Player.SpendEnergy(1);
                CancelSelection();
                return true;
            }

            return false;
        }

        private bool MoveUnitToNode(int nodeId)
        {
            var snapshot = _applicationService.GetSnapshot();
            UnitSnapshot unitSnapshot = null;

            foreach (var u in snapshot.PlayerUnits)
            {
                if (u.UnitId == _selectedUnitId)
                {
                    unitSnapshot = u;
                    break;
                }
            }

            if (unitSnapshot == null || !unitSnapshot.CanMove)
            {
                GD.Print($"[CombatManager] Unit cannot move");
                return false;
            }

            if (!BattleMap.CanMoveTo(unitSnapshot.NodeId, nodeId, NodeOwner.Player))
            {
                GD.Print($"[CombatManager] Cannot move to node {nodeId}");
                return false;
            }

            var command = new MoveUnitCommand(
                TurnCount,
                _playerId,
                _selectedUnitId,
                nodeId
            );

            var events = _applicationService.Submit(command);

            if (events.Count > 0)
            {
                CancelSelection();
                return true;
            }

            return false;
        }

        private bool AttackTargetAtNode(int nodeId)
        {
            var snapshot = _applicationService.GetSnapshot();
            UnitSnapshot attackerSnapshot = null;

            foreach (var u in snapshot.PlayerUnits)
            {
                if (u.UnitId == _selectedUnitId)
                {
                    attackerSnapshot = u;
                    break;
                }
            }

            if (attackerSnapshot == null || !attackerSnapshot.CanAttack)
            {
                GD.Print($"[CombatManager] Unit cannot attack");
                return false;
            }

            if (!BattleMap.IsInAttackRange(attackerSnapshot.NodeId, nodeId, attackerSnapshot.Range))
            {
                GD.Print($"[CombatManager] Target out of range");
                return false;
            }

            int? targetUnitId = null;
            foreach (var enemyUnit in snapshot.EnemyUnits)
            {
                if (enemyUnit.NodeId == nodeId)
                {
                    targetUnitId = enemyUnit.UnitId;
                    break;
                }
            }

            var command = new AttackCommand(
                TurnCount,
                _playerId,
                _selectedUnitId,
                nodeId,
                targetUnitId
            );

            var events = _applicationService.Submit(command);

            if (events.Count > 0)
            {
                CancelSelection();
                return true;
            }

            return false;
        }

        public Unit GetUnitAtNode(int nodeId)
        {
            foreach (var unit in GetPlayerUnits())
            {
                if (unit.CurrentNode == nodeId)
                {
                    return unit;
                }
            }

            foreach (var unit in GetEnemyUnits())
            {
                if (unit.CurrentNode == nodeId)
                {
                    return unit;
                }
            }

            return null;
        }

        public List<int> GetValidTargets(SelectionMode mode)
        {
            List<int> targets = [];

            switch (mode)
            {
                case SelectionMode.DeployUnit:
                    targets.Add(BattleMap.PlayerDeploymentNodeId);
                    break;

                case SelectionMode.MoveUnit:
                    var snapshot = _applicationService.GetSnapshot();
                    foreach (var unit in snapshot.PlayerUnits)
                    {
                        if (unit.UnitId == _selectedUnitId)
                        {
                            targets = BattleMap.GetMovableNodes(unit.NodeId, NodeOwner.Player);
                            break;
                        }
                    }
                    break;

                case SelectionMode.AttackTarget:
                    var snap = _applicationService.GetSnapshot();
                    foreach (var unit in snap.PlayerUnits)
                    {
                        if (unit.UnitId == _selectedUnitId)
                        {
                            targets = BattleMap.GetNodesInRange(unit.NodeId, unit.Range);
                            break;
                        }
                    }
                    break;
            }

            return targets;
        }

        public List<Unit> PlayerUnits => GetPlayerUnits();
        public List<Unit> EnemyUnits => GetEnemyUnits();

        private List<Unit> GetPlayerUnits()
        {
            var units = new List<Unit>();
            foreach (var card in Player.Hand)
            {
                if (card is Unit unit && unit.IsDeployed)
                {
                    units.Add(unit);
                }
            }
            return units;
        }

        private List<Unit> GetEnemyUnits()
        {
            var units = new List<Unit>();
            foreach (var enemy in Enemies)
            {
                foreach (var card in enemy.Hand)
                {
                    if (card is Unit unit && unit.IsDeployed)
                    {
                        units.Add(unit);
                    }
                }
            }
            return units;
        }

        public CombatSnapshot GetSnapshot()
        {
            return _applicationService?.GetSnapshot();
        }

        private void HandleRewardsGenerated(IReadOnlyList<CardRewardOption> rewards)
        {
            if (rewards == null || rewards.Count == 0)
            {
                return;
            }

            var cardDataList = new List<ICardData>();
            foreach (var option in rewards)
            {
                if (option.CardResource is ICardData cardData)
                {
                    cardDataList.Add(cardData);
                }
            }

            if (cardDataList.Count > 0)
            {
                OnCombatRewards?.Invoke(cardDataList);
            }
        }

        public void SelectReward(ICardData cardData)
        {
            if (cardData == null || GameManager.Instance == null)
            {
                return;
            }

            if (_processRewardUseCase != null)
            {
                var option = new CardRewardOption(cardData.Id, cardData.CardName, cardData.Rarity.ToString(), cardData);
                _processRewardUseCase.SelectReward(0, option);
                GD.Print($"[CombatManager] Added card to deck via new pipeline: {cardData.CardName}");
            }
        }

        public void SkipReward()
        {
            GD.Print("[CombatManager] Reward skipped");
        }

        private EnemyDeckData CreateFallbackEnemyDeck()
        {
            var deckData = new EnemyDeckData
            {
                EnemyName = "Enemy Commander",
                StartingHealth = 8,
                StartingEnergy = 3,
                MaxEnergy = 3
            };
            return deckData;
        }

        void CombatEventUIBridge.HandleCombatStarted(CombatStartedEvent evt)
        {
            GD.Print($"[CombatManager] Bridge: Combat started");
        }

        void CombatEventUIBridge.HandleTurnStarted(TurnStartedEvent evt)
        {
            GD.Print($"[CombatManager] Bridge: Turn {evt.Turn} started");
        }

        void CombatEventUIBridge.HandleTurnEnded(TurnEndedEvent evt)
        {
            GD.Print($"[CombatManager] Bridge: Turn {evt.Turn} ended");
        }

        void CombatEventUIBridge.HandleUnitDeployed(UnitDeployedEvent evt)
        {
            GD.Print($"[CombatManager] Bridge: Unit {evt.UnitName} deployed at node {evt.NodeId}");
        }

        void CombatEventUIBridge.HandleUnitMoved(UnitMovedEvent evt)
        {
            GD.Print($"[CombatManager] Bridge: Unit {evt.UnitName} moved from {evt.FromNodeId} to {evt.ToNodeId}");
        }

        void CombatEventUIBridge.HandleDamageApplied(DamageAppliedEvent evt)
        {
            GD.Print($"[CombatManager] Bridge: Damage {evt.Amount} applied");
        }

        void CombatEventUIBridge.HandleUnitDestroyed(UnitDestroyedEvent evt)
        {
            GD.Print($"[CombatManager] Bridge: Unit {evt.UnitName} destroyed");
        }

        void CombatEventUIBridge.HandleCombatEnded(CombatEndedEvent evt)
        {
            GD.Print($"[CombatManager] Bridge: Combat ended, victory: {evt.IsVictory}");

            if (GameManager.Instance != null && evt.IsVictory)
            {
                var snapshot = GetSnapshot();
                GameManager.Instance.SavePlayerHQHealth(snapshot.PlayerHQHealth, snapshot.PlayerHQMaxHealth);
            }

            if (_processRewardUseCase != null)
            {
                var combatEndedEvent = new CombatEndedEvent(
                    Guid.Empty,
                    TurnCount,
                    evt.WinnerActorId,
                    evt.Reason,
                    evt.IsVictory
                );
                _ = _processRewardUseCase.Execute(combatEndedEvent);
            }
        }

        public override void _ExitTree()
        {
            _eventProcessor?.Dispose();
        }
    }
}
