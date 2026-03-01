using System;
using System.Collections.Generic;
using Godot;
using OdysseyCards.AI;
using OdysseyCards.Card;
using OdysseyCards.Character;
using OdysseyCards.Core;
using OdysseyCards.Map;

namespace OdysseyCards.Combat
{
    /// <summary>
    /// Represents the current state of combat.
    /// </summary>
    public enum CombatState
    {
        NotStarted,
        PlayerTurn,
        EnemyTurn,
        Victory,
        Defeat
    }

    /// <summary>
    /// Represents the current selection mode for player input.
    /// </summary>
    public enum SelectionMode
    {
        None,
        DeployUnit,
        MoveUnit,
        AttackTarget
    }

    /// <summary>
    /// Manages combat encounters including turn order, unit deployment, and combat resolution.
    /// Coordinates between player, enemies, units, and the battle map.
    /// </summary>
    public partial class CombatManager : Node
    {
        /// <summary>
        /// Singleton instance for global access.
        /// </summary>
        public static CombatManager Instance { get; private set; }

        /// <summary>
        /// Current state of the combat encounter.
        /// </summary>
        public CombatState State { get; private set; } = CombatState.NotStarted;

        /// <summary>
        /// The player character in this combat.
        /// </summary>
        public Player Player { get; private set; }

        /// <summary>
        /// List of all enemies in this combat.
        /// </summary>
        public List<Enemy> Enemies { get; private set; } = [];

        /// <summary>
        /// Current turn number.
        /// </summary>
        public int TurnCount { get; private set; }

        /// <summary>
        /// Whether the player goes first in this combat.
        /// </summary>
        public bool IsPlayerFirst { get; private set; }
        private bool _isFirstTurn = true;

        /// <summary>
        /// The battle map for this combat.
        /// </summary>
        public BattleMap BattleMap { get; private set; }

        /// <summary>
        /// Units deployed by the player.
        /// </summary>
        public List<Unit> PlayerUnits { get; private set; } = [];

        /// <summary>
        /// Units deployed by enemies.
        /// </summary>
        public List<Unit> EnemyUnits { get; private set; } = [];

        /// <summary>
        /// Current selection mode for player actions.
        /// </summary>
        public SelectionMode CurrentSelectionMode { get; private set; } = SelectionMode.None;

        /// <summary>
        /// Currently selected unit for move/attack actions.
        /// </summary>
        public Unit SelectedUnit { get; private set; }

        /// <summary>
        /// Currently selected card for deployment.
        /// </summary>
        public Card.Card SelectedCard { get; private set; }

        /// <summary>
        /// Fired when combat starts.
        /// </summary>
        public event Action OnCombatStart;

        /// <summary>
        /// Fired at the start of each turn.
        /// </summary>
        public event Action OnTurnStart;

        /// <summary>
        /// Fired at the end of each turn.
        /// </summary>
        public event Action OnTurnEnd;

        /// <summary>
        /// Fired when combat ends with a result.
        /// </summary>
        public event Action<CombatState> OnCombatEnd;

        /// <summary>
        /// Fired when a unit is deployed.
        /// </summary>
        public event Action<Unit> OnUnitDeployed;

        /// <summary>
        /// Fired when a unit moves. Parameters: unit, fromNodeId, toNodeId.
        /// </summary>
        public event Action<Unit, int, int> OnUnitMoved;

        /// <summary>
        /// Fired when a unit attacks another.
        /// </summary>
        public event Action<Unit, Unit> OnUnitAttacked;

        /// <summary>
        /// Fired to show attack range indicators.
        /// </summary>
        public event Action<List<int>> OnAttackRangeShow;

        /// <summary>
        /// Fired to hide attack range indicators.
        /// </summary>
        public event Action OnAttackRangeHide;

        /// <summary>
        /// Fired when combat rewards are generated.
        /// </summary>
        public event Action<List<ICardData>> OnCombatRewards;

        private CardReward _cardReward;
        private List<ICardData> _currentRewards;

        public override void _Ready()
        {
            Instance = this;
            _ = CallDeferred(nameof(InitializeCombat));
        }

        private void InitializeCombat()
        {
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

            if (GetTree().GetFirstNodeInGroup("CombatUI") is UI.CombatUI ui)
            {
                ui.Initialize(Player, this);
                OnCombatEnd += (state) => ui.ShowCombatResult(state == CombatState.Victory);
            }

            StartCombat();
        }

        /// <summary>
        /// Starts the combat encounter with random initiative.
        /// </summary>
        public void StartCombat()
        {
            var random = new RandomNumberGenerator();
            random.Randomize();
            IsPlayerFirst = random.Randf() > 0.5f;
            _isFirstTurn = true;

            TurnCount = 1;

            foreach (Enemy enemy in Enemies)
            {
                enemy.StartTurn();
            }

            if (IsPlayerFirst)
            {
                State = CombatState.PlayerTurn;
                Player.SetEnergy(1, 1);
                Player.DrawCards(4);
            }
            else
            {
                State = CombatState.EnemyTurn;
                Player.SetEnergy(0, 0);
                Player.DrawCards(5);
                ExecuteEnemyTurns();
            }

            OnCombatStart?.Invoke();
            OnTurnStart?.Invoke();
        }

        /// <summary>
        /// Ends the player's turn and triggers enemy turns.
        /// </summary>
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
                    AIAction action = enemy.AI.DecideAction(enemy, this);

                    if (action.Type == AIActionType.EndTurn)
                    {
                        break;
                    }

                    if (!action.IsValid())
                    {
                        break;
                    }

                    switch (action.Type)
                    {
                        case AIActionType.DeployUnit:
                            if (action.Unit != null)
                            {
                                DeployEnemyUnit(enemy, action.Unit);
                            }

                            break;

                        case AIActionType.AttackWithUnit:
                            if (action.Unit != null && action.TargetNodeId >= 0)
                            {
                                ExecuteEnemyAttack(action.Unit, action.TargetNodeId);
                            }

                            break;

                        case AIActionType.PlayOrder:
                            if (action.Card is Order order)
                            {
                                ExecuteEnemyOrder(enemy, order);
                            }

                            break;

                        case AIActionType.None:
                        case AIActionType.MoveUnit:
                        case AIActionType.EndTurn:
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

        /// <summary>
        /// Checks if combat should end based on current game state.
        /// </summary>
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
                if (!enemy.IsDead)
                {
                    allEnemiesDead = false;
                    break;
                }
            }

            if (allEnemiesDead)
            {
                EndCombat(CombatState.Victory);
            }
            else if (Player.IsDead)
            {
                EndCombat(CombatState.Defeat);
            }
        }

        private void EndCombat(CombatState result)
        {
            State = result;

            if (GameManager.Instance != null && result == CombatState.Victory)
            {
                GameManager.Instance.SavePlayerHQHealth(Player.HQCurrentHealth, Player.HQMaxHealth);
                GenerateAndShowRewards();
            }

            OnCombatEnd?.Invoke(result);
        }

        private void GenerateAndShowRewards()
        {
            if (_cardReward == null)
            {
                _cardReward = new CardReward();
                LoadRewardPools();
            }

            CardRarity[] rarities = [CardRarity.Common, CardRarity.Uncommon, CardRarity.Rare];
            _currentRewards = _cardReward.GenerateRewardsFromMultiplePools(rarities, 3);

            if (_currentRewards.Count > 0)
            {
                OnCombatRewards?.Invoke(_currentRewards);
            }
        }

        private void LoadRewardPools()
        {
            string[] poolPaths =
            [
                "res://Resources/CardRewardPools/CommonPool.tres",
                    "res://Resources/CardRewardPools/UncommonPool.tres",
                    "res://Resources/CardRewardPools/RarePool.tres"
            ];

            CardRarity[] rarities = [CardRarity.Common, CardRarity.Uncommon, CardRarity.Rare];

            for (int i = 0; i < poolPaths.Length; i++)
            {
                if (ResourceLoader.Exists(poolPaths[i]))
                {
                    CardRewardPool pool = ResourceLoader.Load<CardRewardPool>(poolPaths[i]);
                    if (pool != null)
                    {
                        _cardReward.AddPool(rarities[i], pool);
                    }
                }
            }
        }

        /// <summary>
        /// Selects a reward card to add to the player's deck.
        /// </summary>
        /// <param name="cardData">The card data to add.</param>
        public void SelectReward(ICardData cardData)
        {
            if (cardData == null || GameManager.Instance == null)
            {
                return;
            }

            if (GameManager.Instance.AddCardToDeck(cardData as Resource))
            {
                GD.Print($"[CombatManager] Added card to deck: {cardData.CardName}");
                _currentRewards = null;
            }
            else
            {
                GD.Print($"[CombatManager] Failed to add card to deck: deck may be full");
            }
        }

        /// <summary>
        /// Skips the current reward selection.
        /// </summary>
        public void SkipReward()
        {
            _currentRewards = null;
            GD.Print("[CombatManager] Reward skipped");
        }

        /// <summary>
        /// Plays a card from the player's hand.
        /// </summary>
        /// <param name="card">The card to play.</param>
        /// <param name="target">Optional target character.</param>
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
                StartDeployMode(unit);
                return;
            }

            if (card is Order order)
            {
                if (!order.CanPlay(Player.CurrentEnergy))
                {
                    GD.Print($"[CombatManager] Cannot play card - not enough energy");
                    return;
                }

                GD.Print($"[CombatManager] Playing card {card.CardName}");
                Player.SpendEnergy(order.Cost);
                order.Play(Player, target);

                if (order.ShouldReturnToDeck)
                {
                    Player.ReturnToDrawPile(card);
                }
                else
                {
                    Player.DiscardCard(card);
                }
            }

            CheckCombatEnd();
        }

        private void StartDeployMode(Unit unit)
        {
            if (!Player.CanSpendEnergy(unit.DeployCost))
            {
                GD.Print($"[CombatManager] Cannot deploy unit - not enough energy");
                return;
            }

            SelectedCard = unit;
            SelectedUnit = null;
            CurrentSelectionMode = SelectionMode.DeployUnit;

            _ = new List<int> { BattleMap.PlayerDeploymentNodeId };
            GD.Print($"[CombatManager] Deploy mode started for {unit.CardName}");
        }

        /// <summary>
        /// Starts move mode for the specified unit.
        /// </summary>
        /// <param name="unit">The unit to move.</param>
        public void StartMoveMode(Unit unit)
        {
            if (!unit.CanMove())
            {
                GD.Print($"[CombatManager] Unit cannot move");
                return;
            }

            SelectedUnit = unit;
            SelectedCard = null;
            CurrentSelectionMode = SelectionMode.MoveUnit;

            List<int> movableNodes = BattleMap.GetMovableNodes(unit.CurrentNode, unit.Owner);
            GD.Print($"[CombatManager] Move mode started for {unit.CardName}, can move to {movableNodes.Count} nodes");
        }

        /// <summary>
        /// Starts attack mode for the specified unit.
        /// </summary>
        /// <param name="unit">The unit to attack with.</param>
        public void StartAttackMode(Unit unit)
        {
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

            GD.Print($"[CombatManager] Attack mode started for {unit.CardName}");
        }

        /// <summary>
        /// Cancels the current selection mode.
        /// </summary>
        public void CancelSelection()
        {
            SelectedCard = null;
            SelectedUnit = null;
            CurrentSelectionMode = SelectionMode.None;
            OnAttackRangeHide?.Invoke();
        }

        /// <summary>
        /// Handles node selection based on current selection mode.
        /// </summary>
        /// <param name="nodeId">The selected node ID.</param>
        /// <returns>True if the action was successful.</returns>
        public bool OnNodeSelected(int nodeId)
        {
            switch (CurrentSelectionMode)
            {
                case SelectionMode.DeployUnit:
                    return DeployUnitToNode(nodeId);

                case SelectionMode.MoveUnit:
                    return MoveUnitToNode(nodeId);

                case SelectionMode.AttackTarget:
                    return AttackTargetAtNode(nodeId);
                case SelectionMode.None:
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
            unit.Owner = NodeOwner.Player;
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

            if (!BattleMap.CanMoveTo(SelectedUnit.CurrentNode, nodeId, SelectedUnit.Owner))
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
            if (node?.IsEnemyDeploymentPoint == true && SelectedUnit.Owner == NodeOwner.Player)
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
                target.TakeDamage(attacker.Attack);
            }

            if (!attacker.HasAttackedThisTurn && !target.IsImmune)
            {
                attacker.TakeDamage(target.Attack);
            }

            if (target.HasAmbush)
            {
                target.TakeDamage(attacker.Attack);
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
            BattleMap.EnemyHQ.TakeDamage(attacker.Attack);

            GD.Print($"[CombatManager] {attacker.CardName} attacked Enemy HQ for {attacker.Attack} damage");

            CancelSelection();
            CheckCombatEnd();
        }

        private void RemoveUnit(Unit unit)
        {
            _ = unit.Owner == NodeOwner.Player ? PlayerUnits.Remove(unit) : EnemyUnits.Remove(unit);

            if (unit.ShouldReturnToDeck)
            {
                Player.ReturnToDrawPile(unit);
            }

            GD.Print($"[CombatManager] Unit {unit.CardName} was destroyed");
        }

        /// <summary>
        /// Gets the unit at the specified node.
        /// </summary>
        /// <param name="nodeId">The node ID to check.</param>
        /// <returns>The unit at the node, or null if none.</returns>
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

        /// <summary>
        /// Gets valid target nodes for the specified selection mode.
        /// </summary>
        /// <param name="mode">The selection mode.</param>
        /// <returns>List of valid target node IDs.</returns>
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
                        targets = BattleMap.GetMovableNodes(SelectedUnit.CurrentNode, SelectedUnit.Owner);
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
            unit.Owner = NodeOwner.Enemy;
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

            if (targetUnit != null && targetUnit.Owner == NodeOwner.Player)
            {
                if (!BattleMap.IsInAttackRange(attacker.CurrentNode, targetNodeId, attacker.Range))
                {
                    return;
                }

                ExecuteAttack(attacker, targetUnit);
                return;
            }

            MapNode node = BattleMap.GetNode(targetNodeId);
            if (node?.IsPlayerDeploymentPoint == true && attacker.Owner == NodeOwner.Enemy)
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
            BattleMap.PlayerHQ.TakeDamage(attacker.Attack);
            GD.Print($"[CombatManager] {attacker.CardName} attacked Player HQ for {attacker.Attack} damage");
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
