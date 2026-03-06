using System;
using System.Collections.Generic;
using OdysseyCards.Domain.Combat.Engine;

namespace OdysseyCards.Domain.Combat.State
{
    public enum CombatPhase
    {
        NotStarted,
        PlayerTurn,
        EnemyTurn,
        Victory,
        Defeat
    }

    public sealed class CombatState
    {
        public int Turn { get; private set; }
        public CombatPhase Phase { get; private set; }
        public int CurrentActorId { get; private set; }
        public bool IsPlayerFirst { get; private set; }
        public int Seed { get; private set; }

        public PlayerState Player { get; private set; }
        public List<EnemyState> Enemies { get; private set; }
        public BoardState Board { get; private set; }

        public List<int> PlayerHand { get; private set; }
        public List<int> PlayerDeck { get; private set; }
        public List<int> PlayerDiscard { get; private set; }

        public CombatState()
        {
            Enemies = new List<EnemyState>();
            PlayerHand = new List<int>();
            PlayerDeck = new List<int>();
            PlayerDiscard = new List<int>();
        }

        public void Initialize(CombatSetup setup, int seed)
        {
            Seed = seed;
            Turn = 1;
            IsPlayerFirst = setup.IsPlayerFirst;
            Phase = IsPlayerFirst ? CombatPhase.PlayerTurn : CombatPhase.EnemyTurn;
            CurrentActorId = IsPlayerFirst ? setup.PlayerId : setup.EnemyIds[0];

            Player = new PlayerState
            {
                Id = setup.PlayerId,
                HQHealth = setup.PlayerStartingHealth,
                HQMaxHealth = setup.PlayerStartingHealth,
                Energy = setup.PlayerStartingEnergy,
                MaxEnergy = setup.PlayerMaxEnergy
            };

            Enemies = new List<EnemyState>();
            for (int i = 0; i < setup.EnemyIds.Count; i++)
            {
                Enemies.Add(new EnemyState
                {
                    Id = setup.EnemyIds[i],
                    HQHealth = setup.EnemyStartingHealths[i],
                    HQMaxHealth = setup.EnemyStartingHealths[i],
                    Energy = setup.EnemyStartingEnergies[i],
                    MaxEnergy = setup.EnemyMaxEnergies[i]
                });
            }

            Board = new BoardState();
        }

        public void EndPlayerTurn()
        {
            Phase = CombatPhase.EnemyTurn;
            CurrentActorId = Enemies.Count > 0 ? Enemies[0].Id : -1;
        }

        public void EndEnemyTurn()
        {
            Turn++;
            Phase = CombatPhase.PlayerTurn;
            CurrentActorId = Player.Id;
        }

        public void SetVictory()
        {
            Phase = CombatPhase.Victory;
        }

        public void SetDefeat()
        {
            Phase = CombatPhase.Defeat;
        }

        public bool IsFinished => Phase == CombatPhase.Victory || Phase == CombatPhase.Defeat;
        public bool IsPlayerTurn => Phase == CombatPhase.PlayerTurn;
        public bool IsEnemyTurn => Phase == CombatPhase.EnemyTurn;
    }

    public sealed class PlayerState
    {
        public int Id { get; init; }
        public int HQHealth { get; set; }
        public int HQMaxHealth { get; init; }
        public int Energy { get; set; }
        public int MaxEnergy { get; init; }

        public bool IsDead => HQHealth <= 0;

        public void TakeDamage(int amount)
        {
            HQHealth = Math.Max(0, HQHealth - amount);
        }

        public void SpendEnergy(int cost)
        {
            Energy = Math.Max(0, Energy - cost);
        }

        public void ResetEnergy()
        {
            Energy = MaxEnergy;
        }
    }

    public sealed class EnemyState
    {
        public int Id { get; init; }
        public int HQHealth { get; set; }
        public int HQMaxHealth { get; init; }
        public int Energy { get; set; }
        public int MaxEnergy { get; init; }

        public bool IsDead => HQHealth <= 0;

        public void TakeDamage(int amount)
        {
            HQHealth = Math.Max(0, HQHealth - amount);
        }

        public void SpendEnergy(int cost)
        {
            Energy = Math.Max(0, Energy - cost);
        }

        public void ResetEnergy()
        {
            Energy = MaxEnergy;
        }
    }
}
