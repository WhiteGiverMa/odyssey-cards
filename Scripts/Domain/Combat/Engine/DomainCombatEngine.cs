using System;
using System.Collections.Generic;
using Godot;
using OdysseyCards.Domain.Combat.Commands;
using OdysseyCards.Domain.Combat.Events;
using OdysseyCards.Domain.Combat.Model;
using OdysseyCards.Domain.Combat.State;

namespace OdysseyCards.Domain.Combat.Engine
{
    public class DomainCombatEngine : ICombatEngine
    {
        private CombatState _state;
        private readonly Dictionary<int, UnitModel> _units = new();
        private readonly Dictionary<int, CardModel> _cards = new();
        private int _nextUnitId = 1;
        private int _nextCardId = 1;

        public bool IsFinished => _state?.IsFinished ?? false;

        public event Action<CombatEvent> OnEvent;

        public void StartCombat(CombatSetup setup, int seed)
        {
            _state = new CombatState();
            _state.Initialize(setup, seed);
            _state.Board.InitializeDefaultMap();

            var evt = new CombatStartedEvent(
                Guid.Empty,
                0,
                setup.PlayerId,
                setup.IsPlayerFirst,
                seed
            );
            OnEvent?.Invoke(evt);

            var turnEvt = new TurnStartedEvent(
                Guid.Empty,
                _state.Turn,
                _state.CurrentActorId
            );
            OnEvent?.Invoke(turnEvt);
        }

        public IReadOnlyList<CombatEvent> Submit(CombatCommand command)
        {
            var events = new List<CombatEvent>();

            if (_state == null || _state.IsFinished)
            {
                return events;
            }

            switch (command)
            {
                case StartCombatCommand startCmd:
                    events.AddRange(HandleStartCombat(startCmd));
                    break;

                case EndTurnCommand endTurnCmd:
                    events.AddRange(HandleEndTurn(endTurnCmd));
                    break;

                case DeployUnitCommand deployCmd:
                    events.AddRange(HandleDeployUnit(deployCmd));
                    break;

                case MoveUnitCommand moveCmd:
                    events.AddRange(HandleMoveUnit(moveCmd));
                    break;

                case AttackCommand attackCmd:
                    events.AddRange(HandleAttack(attackCmd));
                    break;

                case PlayCardCommand playCardCmd:
                    events.AddRange(HandlePlayCard(playCardCmd));
                    break;

                case CancelSelectionCommand cancelCmd:
                    events.AddRange(HandleCancelSelection(cancelCmd));
                    break;
            }

            foreach (var evt in events)
            {
                OnEvent?.Invoke(evt);
            }

            return events;
        }

        public CombatSnapshot GetSnapshot()
        {
            if (_state == null)
            {
                return new CombatSnapshot
                {
                    Turn = 0,
                    CurrentActorId = 0,
                    IsPlayerTurn = true,
                    IsFinished = true,
                    WinnerId = null,
                    PlayerHQHealth = 0,
                    PlayerHQMaxHealth = 0,
                    PlayerEnergy = 0,
                    PlayerMaxEnergy = 0,
                    PlayerUnits = new List<UnitSnapshot>(),
                    EnemyUnits = new List<UnitSnapshot>(),
                    EnemyHQHealths = new List<int>()
                };
            }

            var playerUnits = new List<UnitSnapshot>();
            var enemyUnits = new List<UnitSnapshot>();

            foreach (var unit in _units.Values)
            {
                var snapshot = new UnitSnapshot
                {
                    UnitId = unit.Id,
                    Name = unit.Name,
                    NodeId = unit.NodeId,
                    CurrentHealth = unit.CurrentHealth,
                    MaxHealth = unit.MaxHealth,
                    Attack = unit.Attack,
                    Range = unit.Range,
                    CanMove = unit.CanMove(),
                    CanAttack = unit.CanAttack(),
                    OwnerId = unit.OwnerId
                };

                if (unit.OwnerId == _state.Player.Id)
                {
                    playerUnits.Add(snapshot);
                }
                else
                {
                    enemyUnits.Add(snapshot);
                }
            }

            var enemyHQHealths = new List<int>();
            foreach (var enemy in _state.Enemies)
            {
                enemyHQHealths.Add(enemy.HQHealth);
            }

            return new CombatSnapshot
            {
                Turn = _state.Turn,
                CurrentActorId = _state.CurrentActorId,
                IsPlayerTurn = _state.IsPlayerTurn,
                IsFinished = _state.IsFinished,
                WinnerId = _state.IsFinished ? (_state.Phase == CombatPhase.Victory ? _state.Player.Id : _state.Enemies[0].Id) : null,
                PlayerHQHealth = _state.Player.HQHealth,
                PlayerHQMaxHealth = _state.Player.HQMaxHealth,
                PlayerEnergy = _state.Player.Energy,
                PlayerMaxEnergy = _state.Player.MaxEnergy,
                PlayerUnits = playerUnits,
                EnemyUnits = enemyUnits,
                EnemyHQHealths = enemyHQHealths
            };
        }

        private IReadOnlyList<CombatEvent> HandleStartCombat(StartCombatCommand command)
        {
            return new List<CombatEvent>
            {
                new CombatStartedEvent(command.CommandId, 0, 0, true, command.Seed)
            };
        }

        private IReadOnlyList<CombatEvent> HandleEndTurn(EndTurnCommand command)
        {
            var events = new List<CombatEvent>();

            events.Add(new TurnEndedEvent(command.CommandId, _state.Turn, command.ActorId));

            foreach (var unit in _units.Values)
            {
                unit.ResetTurnActions();
            }

            if (_state.IsPlayerTurn)
            {
                _state.EndPlayerTurn();
            }
            else
            {
                _state.EndEnemyTurn();
                _state.Player.ResetEnergy();
            }

            events.Add(new TurnStartedEvent(command.CommandId, _state.Turn, _state.CurrentActorId));

            return events;
        }

        private IReadOnlyList<CombatEvent> HandleDeployUnit(DeployUnitCommand command)
        {
            var events = new List<CombatEvent>();

            NodeOwner owner = command.ActorId == _state.Player.Id ? NodeOwner.Player : NodeOwner.Enemy;

            if (!_state.Board.CanDeployTo(command.TargetNodeId, owner))
            {
                return events;
            }

            int energyCost = 1;
            if (owner == NodeOwner.Player)
            {
                if (_state.Player.Energy < energyCost)
                {
                    return events;
                }
                _state.Player.SpendEnergy(energyCost);
            }
            else
            {
                var enemy = _state.Enemies.Find(e => e.Id == command.ActorId);
                if (enemy == null || enemy.Energy < energyCost)
                {
                    return events;
                }
                enemy.SpendEnergy(energyCost);
            }

            var unit = UnitModel.Create(
                _nextUnitId++,
                $"Unit_{command.CardInstanceId}",
                command.ActorId,
                5,
                2,
                1,
                energyCost
            );
            unit.NodeId = command.TargetNodeId;

            _units[unit.Id] = unit;
            _state.Board.PlaceUnit(command.TargetNodeId, unit.Id);

            events.Add(new UnitDeployedEvent(
                command.CommandId,
                _state.Turn,
                unit.Id,
                command.TargetNodeId,
                unit.Name,
                command.ActorId
            ));

            return events;
        }

        private IReadOnlyList<CombatEvent> HandleMoveUnit(MoveUnitCommand command)
        {
            var events = new List<CombatEvent>();

            if (!_units.TryGetValue(command.UnitId, out UnitModel unit))
            {
                return events;
            }

            if (!unit.CanMove())
            {
                return events;
            }

            NodeOwner owner = unit.OwnerId == _state.Player.Id ? NodeOwner.Player : NodeOwner.Enemy;

            if (!_state.Board.CanMoveTo(unit.NodeId, command.ToNodeId, owner))
            {
                return events;
            }

            int fromNode = unit.NodeId;
            _state.Board.MoveUnit(fromNode, command.ToNodeId);
            unit.NodeId = command.ToNodeId;
            unit.UseMoveAction();

            events.Add(new UnitMovedEvent(
                command.CommandId,
                _state.Turn,
                unit.Id,
                fromNode,
                command.ToNodeId,
                unit.Name
            ));

            return events;
        }

        private IReadOnlyList<CombatEvent> HandleAttack(AttackCommand command)
        {
            var events = new List<CombatEvent>();

            if (!_units.TryGetValue(command.AttackerUnitId, out UnitModel attacker))
            {
                return events;
            }

            if (!attacker.CanAttack())
            {
                return events;
            }

            if (!_state.Board.IsInAttackRange(attacker.NodeId, command.TargetNodeId, attacker.Range))
            {
                return events;
            }

            attacker.UseAttackAction();

            int? targetUnitId = command.TargetUnitId ?? _state.Board.GetUnitAtNode(command.TargetNodeId);

            if (targetUnitId.HasValue && _units.TryGetValue(targetUnitId.Value, out UnitModel target))
            {
                if (target.OwnerId == attacker.OwnerId)
                {
                    return events;
                }

                int damageToTarget = attacker.Attack;
                target.TakeDamage(damageToTarget);

                events.Add(new DamageAppliedEvent(
                    command.CommandId,
                    _state.Turn,
                    attacker.Id,
                    target.Id,
                    -1,
                    damageToTarget
                ));

                if (target.IsDead)
                {
                    _state.Board.RemoveUnit(target.NodeId);
                    _units.Remove(target.Id);
                    events.Add(new UnitDestroyedEvent(
                        command.CommandId,
                        _state.Turn,
                        target.Id,
                        target.Name
                    ));
                }

                if (!attacker.IsDead && !target.IsImmune)
                {
                    int damageToAttacker = target.Attack;
                    attacker.TakeDamage(damageToAttacker);

                    events.Add(new DamageAppliedEvent(
                        command.CommandId,
                        _state.Turn,
                        target.Id,
                        attacker.Id,
                        -1,
                        damageToAttacker
                    ));

                    if (attacker.IsDead)
                    {
                        _state.Board.RemoveUnit(attacker.NodeId);
                        _units.Remove(attacker.Id);
                        events.Add(new UnitDestroyedEvent(
                            command.CommandId,
                            _state.Turn,
                            attacker.Id,
                            attacker.Name
                        ));
                    }
                }
            }
            else
            {
                if (command.TargetNodeId == _state.Board.EnemyHQNodeId && attacker.OwnerId == _state.Player.Id)
                {
                    int damage = attacker.Attack;
                    var enemy = _state.Enemies[0];
                    enemy.TakeDamage(damage);

                    events.Add(new DamageAppliedEvent(
                        command.CommandId,
                        _state.Turn,
                        attacker.Id,
                        null,
                        enemy.Id,
                        damage
                    ));

                    if (enemy.IsDead)
                    {
                        _state.SetVictory();
                        events.Add(new CombatEndedEvent(
                            command.CommandId,
                            _state.Turn,
                            _state.Player.Id,
                            "EnemyHQDestroyed",
                            true
                        ));
                    }
                }
                else if (command.TargetNodeId == _state.Board.PlayerHQNodeId && attacker.OwnerId != _state.Player.Id)
                {
                    int damage = attacker.Attack;
                    _state.Player.TakeDamage(damage);

                    events.Add(new DamageAppliedEvent(
                        command.CommandId,
                        _state.Turn,
                        attacker.Id,
                        null,
                        _state.Player.Id,
                        damage
                    ));

                    if (_state.Player.IsDead)
                    {
                        _state.SetDefeat();
                        events.Add(new CombatEndedEvent(
                            command.CommandId,
                            _state.Turn,
                            _state.Enemies[0].Id,
                            "PlayerHQDestroyed",
                            false
                        ));
                    }
                }
            }

            return events;
        }

        private IReadOnlyList<CombatEvent> HandlePlayCard(PlayCardCommand command)
        {
            var events = new List<CombatEvent>();

            string cardName = $"Card_{command.CardInstanceId}";

            events.Add(new CardPlayedEvent(
                command.CommandId,
                _state.Turn,
                command.ActorId,
                command.CardInstanceId,
                cardName
            ));

            return events;
        }

        private IReadOnlyList<CombatEvent> HandleCancelSelection(CancelSelectionCommand command)
        {
            return new List<CombatEvent>
            {
                new SelectionCancelledEvent(command.CommandId, _state.Turn)
            };
        }
    }
}
