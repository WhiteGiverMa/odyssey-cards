using System;

namespace OdysseyCards.Domain.Combat.Model
{
    public sealed class UnitModel
    {
        public int Id { get; init; }
        public string Name { get; init; }
        public int OwnerId { get; init; }
        public int NodeId { get; set; }
        public int CurrentHealth { get; set; }
        public int MaxHealth { get; init; }
        public int Attack { get; init; }
        public int Range { get; init; }
        public int DeployCost { get; init; }

        public bool CanMoveThisTurn { get; set; }
        public bool CanAttackThisTurn { get; set; }
        public bool HasAmbush { get; set; }
        public bool IsImmune { get; set; }

        public bool IsDead => CurrentHealth <= 0;

        public static UnitModel Create(int id, string name, int ownerId, int maxHealth, int attack, int range, int deployCost)
        {
            return new UnitModel
            {
                Id = id,
                Name = name,
                OwnerId = ownerId,
                NodeId = -1,
                CurrentHealth = maxHealth,
                MaxHealth = maxHealth,
                Attack = attack,
                Range = range,
                DeployCost = deployCost,
                CanMoveThisTurn = true,
                CanAttackThisTurn = true,
                HasAmbush = false,
                IsImmune = false
            };
        }

        public void TakeDamage(int amount)
        {
            CurrentHealth = Math.Max(0, CurrentHealth - amount);
        }

        public void UseMoveAction()
        {
            CanMoveThisTurn = false;
        }

        public void UseAttackAction()
        {
            CanAttackThisTurn = false;
        }

        public void ResetTurnActions()
        {
            CanMoveThisTurn = true;
            CanAttackThisTurn = true;
        }

        public bool CanMove() => CanMoveThisTurn && !IsDead;
        public bool CanAttack() => CanAttackThisTurn && !IsDead;
    }

    public sealed class CardModel
    {
        public int Id { get; init; }
        public string Name { get; init; }
        public CardType Type { get; init; }
        public int Cost { get; init; }
        public int OwnerId { get; init; }

        public static CardModel Create(int id, string name, CardType type, int cost, int ownerId)
        {
            return new CardModel
            {
                Id = id,
                Name = name,
                Type = type,
                Cost = cost,
                OwnerId = ownerId
            };
        }
    }

    public enum CardType
    {
        Unit,
        Order
    }
}
