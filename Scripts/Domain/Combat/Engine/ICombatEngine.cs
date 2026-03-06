using System;
using System.Collections.Generic;
using OdysseyCards.Domain.Combat.Commands;
using OdysseyCards.Domain.Combat.Events;

namespace OdysseyCards.Domain.Combat.Engine
{
    public interface ICombatEngine
    {
        void StartCombat(CombatSetup setup, int seed);

        IReadOnlyList<CombatEvent> Submit(CombatCommand command);

        CombatSnapshot GetSnapshot();

        bool IsFinished { get; }

        event Action<CombatEvent> OnEvent;
    }

    public sealed record CombatSetup
    {
        public int PlayerId { get; init; }
        public int PlayerStartingHealth { get; init; }
        public int PlayerStartingEnergy { get; init; }
        public int PlayerMaxEnergy { get; init; }

        public IReadOnlyList<int> EnemyIds { get; init; }
        public IReadOnlyList<int> EnemyStartingHealths { get; init; }
        public IReadOnlyList<int> EnemyStartingEnergies { get; init; }
        public IReadOnlyList<int> EnemyMaxEnergies { get; init; }

        public bool IsPlayerFirst { get; init; }
    }

    public sealed record CombatSnapshot
    {
        public int Turn { get; init; }
        public int CurrentActorId { get; init; }
        public bool IsPlayerTurn { get; init; }
        public bool IsFinished { get; init; }
        public int? WinnerId { get; init; }

        public int PlayerHQHealth { get; init; }
        public int PlayerHQMaxHealth { get; init; }
        public int PlayerEnergy { get; init; }
        public int PlayerMaxEnergy { get; init; }

        public IReadOnlyList<UnitSnapshot> PlayerUnits { get; init; }
        public IReadOnlyList<UnitSnapshot> EnemyUnits { get; init; }
        public IReadOnlyList<int> EnemyHQHealths { get; init; }
    }

    public sealed record UnitSnapshot
    {
        public int UnitId { get; init; }
        public string Name { get; init; }
        public int NodeId { get; init; }
        public int CurrentHealth { get; init; }
        public int MaxHealth { get; init; }
        public int Attack { get; init; }
        public int Range { get; init; }
        public bool CanMove { get; init; }
        public bool CanAttack { get; init; }
        public int OwnerId { get; init; }
    }
}
