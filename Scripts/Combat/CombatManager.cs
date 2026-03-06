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
using OdysseyCards.Domain.Combat.Engine;
using OdysseyCards.Domain.Combat.Events;
using OdysseyCards.Infrastructure.Replay;
using OdysseyCards.Map;
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

    public partial class CombatManager : Node
    {
        public static CombatManager Instance { get; private set; }

        public CombatState State { get; private set; } = CombatState.NotStarted;
        public Player Player { get; private set; }
        public List<Enemy> Enemies { get; private set; } = [];
        public int TurnCount { get; private set; }
        public bool IsPlayerFirst { get; private set; }
        public BattleMap BattleMap { get; private set; }
        public List<Unit> PlayerUnits { get; private set; } = [];
        public List<Unit> EnemyUnits { get; private set; } = [];
        public SelectionMode CurrentSelectionMode { get; private set; } = SelectionMode.None;
        public Unit SelectedUnit { get; private set; }
        public Card.Card SelectedCard { get; private set; }

        public event Action OnCombatStart;
        public event Action OnTurnStart;
        public event Action OnTurnEnd;
        public event Action<CombatState> OnCombatEnd;
        public event Action<Unit> OnUnitDeployed;
        public event Action<Unit, int, int> OnUnitMoved;
        public event Action<Unit, Unit> OnUnitAttacked;
        public event Action<List<int>> OnAttackRangeShow;
        public event Action OnAttackRangeHide;
        public event Action<List<ICardData>> OnCombatRewards;

        private ICombatEngine _combatEngine;
        private CombatApplicationService _applicationService;
        private JsonlReplayWriter _replayWriter;
        private Domain.Combat.Engine.ICombatEngine _domainEngine;
        private ProcessRewardUseCase _processRewardUseCase;
        private IRewardService _rewardService;
        private bool _isFirstTurn = true;

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

            Enemies = [];

            EnemyDeckData enemyDeckData = ResourceLoader.Load<EnemyDeckData>("res://Resources/EnemyDeckData.tres");
            enemyDeckData ??= CreateFallbackEnemyDeck();

            var enemy = new Enemy();
            enemy.Initialize(enemyDeckData);
            Enemies.Add(enemy);

            GD.Print("[CombatManager] Looking for CombatUI in group");
            if (GetTree().GetFirstNodeInGroup("CombatUI") is UI.CombatUI ui)
            {
                GD.Print("[CombatManager] Found CombatUI, initializing...");
                ui.Initialize(Player, this);
                OnCombatEnd += (state) => ui.ShowCombatResult(state == CombatState.Victory);
            }
            else
            {
                GD.PrintErr("[CombatManager] CombatUI not found in group!");
            }

            GD.Print("[CombatManager] Starting combat");
            InitializeCommandSystem();
            StartCombat();

            GD.Print($"[CombatManager] After StartCombat, player hand count: {Player.Hand.Count}");
        }

        private void InitializeCommandSystem()
        {
            GD.Print("[CombatManager] Initializing command system...");

            _domainEngine = new DomainCombatEngine();

            string replayPath = $"user://replays/combat_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}.jsonl";
            _replayWriter = new JsonlReplayWriter(ProjectSettings.GlobalizePath(replayPath));

            _applicationService = new CombatApplicationService(_domainEngine, _replayWriter);
            _ = new CombatInputAdapter(_applicationService);

            _rewardService = new CardRewardService();
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
            _isFirstTurn = true;

            GD.Print($"[CombatManager] IsPlayerFirst: {IsPlayerFirst}");

            TurnCount = 1;

            foreach (Enemy enemy in Enemies)
            {
                enemy.StartTurn();
            }

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

            OnCombatStart?.Invoke();
            OnTurnStart?.Invoke();

            GD.Print($"[CombatManager] Combat started, player hand count: {Player.Hand.Count}");
        }

        public void EndPlayerTurn()
        {
            if (State != CombatState.PlayerTurn)
            {
                return;
            }

            CancelSelection();

            foreach (Unit unit in PlayerUnits)
            {
                unit.OnTurnEnd();
            }

            Player.EndTurn();

            State = CombatState.EnemyTurn;
            OnTurnEnd?.Invoke();

            ExecuteEnemyTurns();
        }

        private void ExecuteEnemyTurns()
        {
            foreach (Enemy enemy in Enemies)
            {
                if (enemy.IsDead)
                {
                    continue;
                }

                while (true)
                {
                    AI.AIAction action = enemy.AI.DecideAction(enemy, this);

                    if (action.Type == AI.AIActionType.EndTurn)
                    {
                        break;
                    }

                    if (!action.IsValid())
                    {
                        break;
                    }

                    switch (action.Type)
                    {
                        case AI.AIActionType.DeployUnit:
                            if (action.Unit != null)
                            {
                                DeployEnemyUnit(enemy, action.Unit);
                            }
                            break;

                        case AI.AIActionType.AttackWithUnit:
                            if (action.Unit != null && action.TargetNodeId >= 0)
                            {
                                ExecuteEnemyAttack(action.Unit, action.TargetNodeId);
                            }
                            break;

                        case AI.AIActionType.PlayOrder:
                            if (action.Card is Order order)
                            {
                                ExecuteEnemyOrder(enemy, order);
                            }
                            break;

                        case AI.AIActionType.None:
                        case AI.AIActionType.MoveUnit:
                        case AI.AIActionType.EndTurn:
                        default:
                            break;
                    }

                    if (Player.IsDead)
                    {
                        EndCombat(CombatState.Defeat);
                        return;
                    }
                }
            }

            StartNewTurn();
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

            if (_isFirstTurn && !IsPlayerFirst)
            {
                Player.SetEnergy(1, 1);
            }

            Player.DrawCards(1);

            foreach (Unit unit in PlayerUnits)
            {
                unit.OnTurnStart();
            }

            foreach (Unit unit in EnemyUnits)
            {
                unit.OnTurnStart();
            }

            _isFirstTurn = false;

            OnTurnStart?.Invoke();
        }

        public void CheckCombatEnd()
        {
            if (BattleMap.EnemyHQ.IsDestroyed)
            {
                EndCombat(CombatState.Victory);
                return;
            }

            if (Player.HQCurrentHealth <= 0)
            {
                EndCombat(CombatState.Defeat);
                return;
            }

            bool allEnemiesDead = true;
            foreach (Enemy enemy in Enemies)
            {
                if (enemy.HQCurrentHealth > 0)
                {
                    allEnemiesDead = false;
                    break;
                }
            }

            if (allEnemiesDead)
            {
                EndCombat(CombatState.Victory);
            }
        }

        private void EndCombat(CombatState result)
        {
            State = result;

            if (GameManager.Instance != null && result == CombatState.Victory)
            {
                GameManager.Instance.SavePlayerHQHealth(Player.HQCurrentHealth, Player.HQMaxHealth);
            }

            if (_processRewardUseCase != null)
            {
                var combatEndedEvent = new CombatEndedEvent(
                    Guid.Empty,
                    TurnCount,
                    0,
                    result == CombatState.Victory ? "Victory" : "Defeat",
                    result == CombatState.Victory
                );
                _ = _processRewardUseCase.Execute(combatEndedEvent);
            }

            OnCombatEnd?.Invoke(result);
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
            }
            else
            {
                GD.Print($"[CombatManager] Card is neither Unit nor Order! Type: {card?.GetType().Name}");
            }

            CheckCombatEnd();
        }

        private void StartDeployMode(Unit unit)
        {
            GD.Print($"[CombatManager] StartDeployMode called for {unit.CardName}, DeployCost: {unit.DeployCost}, Energy: {Player.CurrentEnergy}");

            SelectedCard = unit;
            SelectedUnit = null;
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

            SelectedUnit = unit;
            SelectedCard = null;
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

            SelectedUnit = unit;
            SelectedCard = null;
            CurrentSelectionMode = SelectionMode.AttackTarget;

            List<int> nodesInRange = BattleMap.GetNodesInRange(unit.CurrentNode, unit.Range);
            OnAttackRangeShow?.Invoke(nodesInRange);

            GD.Print($"[CombatManager] SelectionMode changed to AttackTarget for {unit.CardName}, nodes in range: {nodesInRange.Count}");
        }

        public void CancelSelection()
        {
            GD.Print($"[CombatManager] CancelSelection: previous mode={CurrentSelectionMode}, card={SelectedCard?.CardName}, unit={SelectedUnit?.CardName}");
            SelectedCard = null;
            SelectedUnit = null;
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
                default:
                    break;
            }

            return false;
        }

        private bool DeployUnitToNode(int nodeId)
        {
            if (SelectedCard is not Unit unit)
            {
                return false;
            }

            if (!BattleMap.CanDeployTo(nodeId, NodeOwner.Player))
            {
                GD.Print($"[CombatManager] Cannot deploy to node {nodeId}");
                return false;
            }

            Player.SpendEnergy(unit.DeployCost);
            unit.OwnerType = NodeOwner.Player;
            unit.CurrentNode = nodeId;
            unit.OnDeploy();

            PlayerUnits.Add(unit);
            Player.RemoveFromHand(unit);

            GD.Print($"[CombatManager] Deployed {unit.CardName} to node {nodeId}");

            OnUnitDeployed?.Invoke(unit);
            CancelSelection();
            CheckCombatEnd();

            return true;
        }

        private bool MoveUnitToNode(int nodeId)
        {
            if (SelectedUnit == null)
            {
                return false;
            }

            if (!BattleMap.CanMoveTo(SelectedUnit.CurrentNode, nodeId, SelectedUnit.OwnerType))
            {
                GD.Print($"[CombatManager] Cannot move to node {nodeId}");
                return false;
            }

            int fromNode = SelectedUnit.CurrentNode;
            SelectedUnit.CurrentNode = nodeId;
            SelectedUnit.UseMoveAction();

            GD.Print($"[CombatManager] Moved {SelectedUnit.CardName} from node {fromNode} to {nodeId}");

            OnUnitMoved?.Invoke(SelectedUnit, fromNode, nodeId);
            CancelSelection();

            return true;
        }

        private bool AttackTargetAtNode(int nodeId)
        {
            if (SelectedUnit == null)
            {
                return false;
            }

            Unit targetUnit = GetUnitAtNode(nodeId);

            if (targetUnit != null && targetUnit.Owner != SelectedUnit.Owner)
            {
                if (!BattleMap.IsInAttackRange(SelectedUnit.CurrentNode, nodeId, SelectedUnit.Range))
                {
                    GD.Print($"[CombatManager] Target out of range");
                    return false;
                }

                ExecuteAttack(SelectedUnit, targetUnit);
                return true;
            }

            MapNode node = BattleMap.GetNode(nodeId);
            if (node?.IsEnemyDeploymentPoint == true && SelectedUnit.OwnerType == NodeOwner.Player)
            {
                if (!BattleMap.IsInAttackRange(SelectedUnit.CurrentNode, nodeId, SelectedUnit.Range))
                {
                    GD.Print($"[CombatManager] HQ out of range");
                    return false;
                }

                AttackEnemyHQ(SelectedUnit);
                return true;
            }

            GD.Print($"[CombatManager] No valid target at node {nodeId}");
            return false;
        }

        private void ExecuteAttack(Unit attacker, Unit target)
        {
            attacker.UseAttackAction();

            if (!target.HasAmbush)
            {
                target.TakeDamage(attacker.Attack, attacker);
            }

            if (!attacker.HasAttackedThisTurn && !target.IsImmune)
            {
                attacker.TakeDamage(target.Attack, target);
            }

            if (target.HasAmbush)
            {
                target.TakeDamage(attacker.Attack, attacker);
                target.HasAmbush = false;
            }

            GD.Print($"[CombatManager] {attacker.CardName} attacked {target.CardName}");

            OnUnitAttacked?.Invoke(attacker, target);

            if (target.CurrentHealth <= 0)
            {
                RemoveUnit(target);
            }

            if (attacker.CurrentHealth <= 0)
            {
                RemoveUnit(attacker);
            }

            CancelSelection();
            CheckCombatEnd();
        }

        private void AttackEnemyHQ(Unit attacker)
        {
            attacker.UseAttackAction();
            int damage = DamageResolver.ResolveDamage(attacker.Attack, attacker, null);
            BattleMap.EnemyHQ.TakeDamage(damage);

            GD.Print($"[CombatManager] {attacker.CardName} attacked Enemy HQ for {damage} damage");

            CancelSelection();
            CheckCombatEnd();
        }

        private void RemoveUnit(Unit unit)
        {
            _ = unit.OwnerType == NodeOwner.Player ? PlayerUnits.Remove(unit) : EnemyUnits.Remove(unit);

            if (unit.ShouldReturnToDeck)
            {
                Player.ReturnToDrawPile(unit);
            }

            GD.Print($"[CombatManager] Unit {unit.CardName} was destroyed");
        }

        public Unit GetUnitAtNode(int nodeId)
        {
            foreach (Unit unit in PlayerUnits)
            {
                if (unit.CurrentNode == nodeId)
                {
                    return unit;
                }
            }

            foreach (Unit unit in EnemyUnits)
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
                    if (SelectedUnit != null)
                    {
                        targets = BattleMap.GetMovableNodes(SelectedUnit.CurrentNode, SelectedUnit.OwnerType);
                    }
                    break;

                case SelectionMode.AttackTarget:
                    if (SelectedUnit != null)
                    {
                        targets = BattleMap.GetNodesInRange(SelectedUnit.CurrentNode, SelectedUnit.Range);
                    }
                    break;
                case SelectionMode.None:
                    break;
                default:
                    break;
            }

            return targets;
        }

        private void DeployEnemyUnit(Enemy enemy, Unit unit)
        {
            int deployNodeId = BattleMap.EnemyDeploymentNodeId;
            if (!BattleMap.CanDeployTo(deployNodeId, NodeOwner.Enemy))
            {
                return;
            }

            enemy.SpendEnergy(unit.DeployCost);
            unit.OwnerType = NodeOwner.Enemy;
            unit.CurrentNode = deployNodeId;
            unit.OnDeploy();

            EnemyUnits.Add(unit);
            enemy.RemoveFromHand(unit);

            GD.Print($"[CombatManager] Enemy deployed {unit.CardName} to node {deployNodeId}");
            OnUnitDeployed?.Invoke(unit);
        }

        private void ExecuteEnemyOrder(Enemy enemy, Order order)
        {
            enemy.SpendEnergy(order.Cost);
            order.Play(enemy, Player);

            if (order.ShouldReturnToDeck)
            {
                enemy.ReturnToDrawPile(order);
            }
            else
            {
                enemy.DiscardCard(order);
            }

            GD.Print($"[CombatManager] Enemy played order {order.CardName}");
        }

        private void ExecuteEnemyAttack(Unit attacker, int targetNodeId)
        {
            Unit targetUnit = GetUnitAtNode(targetNodeId);

            if (targetUnit != null && targetUnit.OwnerType == NodeOwner.Player)
            {
                if (!BattleMap.IsInAttackRange(attacker.CurrentNode, targetNodeId, attacker.Range))
                {
                    return;
                }

                ExecuteAttack(attacker, targetUnit);
                return;
            }

            MapNode node = BattleMap.GetNode(targetNodeId);
            if (node?.IsPlayerDeploymentPoint == true && attacker.OwnerType == NodeOwner.Enemy)
            {
                if (!BattleMap.IsInAttackRange(attacker.CurrentNode, targetNodeId, attacker.Range))
                {
                    return;
                }

                AttackPlayerHQ(attacker);
            }
        }

        private void AttackPlayerHQ(Unit attacker)
        {
            attacker.UseAttackAction();
            int damage = DamageResolver.ResolveDamage(attacker.Attack, attacker, null);
            BattleMap.PlayerHQ.TakeDamage(damage);
            GD.Print($"[CombatManager] {attacker.CardName} attacked Player HQ for {damage} damage");
            CancelSelection();
            CheckCombatEnd();
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
    }
}
