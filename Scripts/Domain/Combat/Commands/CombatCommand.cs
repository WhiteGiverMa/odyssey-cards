using System;

namespace OdysseyCards.Domain.Combat.Commands
{
    public abstract record CombatCommand
    {
        public Guid CommandId { get; init; } = Guid.NewGuid();
        public int Turn { get; init; }
        public int ActorId { get; init; }
        public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

        protected CombatCommand() { }

        protected CombatCommand(int turn, int actorId)
        {
            Turn = turn;
            ActorId = actorId;
        }
    }

    public sealed record StartCombatCommand : CombatCommand
    {
        public int Seed { get; init; }

        public StartCombatCommand() { }

        public StartCombatCommand(int seed) : base(0, 0)
        {
            Seed = seed;
        }
    }

    public sealed record EndTurnCommand : CombatCommand
    {
        public EndTurnCommand() { }

        public EndTurnCommand(int turn, int actorId) : base(turn, actorId) { }
    }

    public sealed record DeployUnitCommand : CombatCommand
    {
        public int CardInstanceId { get; init; }
        public int TargetNodeId { get; init; }
        public int Attack { get; init; }
        public int MaxHealth { get; init; }
        public int Range { get; init; }
        public string UnitName { get; init; }

        public DeployUnitCommand() { }

        public DeployUnitCommand(int turn, int actorId, int cardInstanceId, int targetNodeId, int attack, int maxHealth, int range, string unitName) : base(turn, actorId)
        {
            CardInstanceId = cardInstanceId;
            TargetNodeId = targetNodeId;
            Attack = attack;
            MaxHealth = maxHealth;
            Range = range;
            UnitName = unitName;
        }
    }

    public sealed record MoveUnitCommand : CombatCommand
    {
        public int UnitId { get; init; }
        public int ToNodeId { get; init; }

        public MoveUnitCommand() { }

        public MoveUnitCommand(int turn, int actorId, int unitId, int toNodeId) : base(turn, actorId)
        {
            UnitId = unitId;
            ToNodeId = toNodeId;
        }
    }

    public sealed record AttackCommand : CombatCommand
    {
        public int AttackerUnitId { get; init; }
        public int TargetNodeId { get; init; }
        public int? TargetUnitId { get; init; }

        public AttackCommand() { }

        public AttackCommand(int turn, int actorId, int attackerUnitId, int targetNodeId, int? targetUnitId = null) : base(turn, actorId)
        {
            AttackerUnitId = attackerUnitId;
            TargetNodeId = targetNodeId;
            TargetUnitId = targetUnitId;
        }
    }

    public sealed record CancelSelectionCommand : CombatCommand
    {
        public CancelSelectionCommand() { }

        public CancelSelectionCommand(int turn, int actorId) : base(turn, actorId) { }
    }

    public sealed record PlayCardCommand : CombatCommand
    {
        public int CardInstanceId { get; init; }
        public int? TargetNodeId { get; init; }
        public int? TargetUnitId { get; init; }
        public string? EffectType { get; init; }
        public int EffectValue { get; init; }
        public int? TargetHQOwnerId { get; init; }
        public int Cost { get; init; }

        public PlayCardCommand() { }

        public PlayCardCommand(int turn, int actorId, int cardInstanceId, int? targetNodeId = null, int? targetUnitId = null, string? effectType = null, int effectValue = 0, int? targetHQOwnerId = null, int cost = 0) : base(turn, actorId)
        {
            CardInstanceId = cardInstanceId;
            TargetNodeId = targetNodeId;
            TargetUnitId = targetUnitId;
            EffectType = effectType;
            EffectValue = effectValue;
            TargetHQOwnerId = targetHQOwnerId;
            Cost = cost;
        }
    }
}
