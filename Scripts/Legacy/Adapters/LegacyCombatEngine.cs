using System;
using System.Collections.Generic;
using OdysseyCards.Combat;
using OdysseyCards.Domain.Combat.Commands;
using OdysseyCards.Domain.Combat.Engine;
using OdysseyCards.Domain.Combat.Events;

namespace OdysseyCards.Legacy.Adapters
{
    public class LegacyCombatEngine : ICombatEngine
    {
        private readonly CombatManager _combatManager;
        private readonly LegacyCombatAdapter _adapter;
        private bool _isFinished;

        public bool IsFinished => _isFinished;

        public event Action<CombatEvent> OnEvent;

        public LegacyCombatEngine(CombatManager combatManager)
        {
            _combatManager = combatManager ?? throw new ArgumentNullException(nameof(combatManager));
            _adapter = new LegacyCombatAdapter(combatManager);

            _combatManager.OnCombatEnd += state =>
            {
                _isFinished = true;
                var evt = new CombatEndedEvent(
                    Guid.Empty,
                    _combatManager.TurnCount,
                    state == CombatState.Victory ? 0 : 1,
                    state.ToString(),
                    state == CombatState.Victory
                );
                OnEvent?.Invoke(evt);
            };
        }

        public void StartCombat(CombatSetup setup, int seed)
        {
            _combatManager.StartCombat();
            _isFinished = false;

            var evt = new TurnStartedEvent(
                Guid.Empty,
                _combatManager.TurnCount,
                setup.IsPlayerFirst ? setup.PlayerId : setup.EnemyIds[0]
            );
            OnEvent?.Invoke(evt);
        }

        public IReadOnlyList<CombatEvent> Submit(CombatCommand command)
        {
            var events = _adapter.ExecuteLegacy(command);

            foreach (var evt in events)
            {
                OnEvent?.Invoke(evt);
            }

            return events;
        }

        public CombatSnapshot GetSnapshot()
        {
            return new CombatSnapshot
            {
                Turn = _combatManager.TurnCount,
                CurrentActorId = _combatManager.State == CombatState.PlayerTurn ? 0 : 1,
                IsPlayerTurn = _combatManager.State == CombatState.PlayerTurn,
                IsFinished = _isFinished,
                WinnerId = _isFinished ? (_combatManager.State == CombatState.Victory ? 0 : 1) : null,
                PlayerHQHealth = _combatManager.Player?.HQCurrentHealth ?? 0,
                PlayerHQMaxHealth = _combatManager.Player?.HQMaxHealth ?? 0,
                PlayerEnergy = _combatManager.Player?.CurrentEnergy ?? 0,
                PlayerMaxEnergy = _combatManager.Player?.MaxEnergy ?? 0,
                PlayerUnits = GetUnitSnapshots(_combatManager.PlayerUnits, 0),
                EnemyUnits = GetUnitSnapshots(_combatManager.EnemyUnits, 1),
                EnemyHQHealths = new List<int> { _combatManager.BattleMap?.EnemyHQ?.CurrentHealth ?? 0 }
            };
        }

        private List<UnitSnapshot> GetUnitSnapshots(List<Card.Unit> units, int ownerId)
        {
            var snapshots = new List<UnitSnapshot>();
            foreach (var unit in units)
            {
                snapshots.Add(new UnitSnapshot
                {
                    UnitId = unit.Id.GetHashCode(),
                    Name = unit.CardName,
                    NodeId = unit.CurrentNode,
                    CurrentHealth = unit.CurrentHealth,
                    MaxHealth = unit.MaxHealth,
                    Attack = unit.Attack,
                    Range = unit.Range,
                    CanMove = unit.CanMove(),
                    CanAttack = unit.CanAttack(),
                    OwnerId = ownerId
                });
            }
            return snapshots;
        }
    }
}
