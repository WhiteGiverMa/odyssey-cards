using System;

namespace OdysseyCards.Domain.Combat.Events
{
    public abstract record CombatEvent
    {
        public Guid EventId { get; init; } = Guid.NewGuid();
        public Guid CausedByCommandId { get; init; }
        public int Turn { get; init; }
        public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

        protected CombatEvent() { }

        protected CombatEvent(Guid commandId, int turn)
        {
            CausedByCommandId = commandId;
            Turn = turn;
        }
    }

    public sealed record TurnStartedEvent : CombatEvent
    {
        public int ActiveActorId { get; init; }

        public TurnStartedEvent() { }

        public TurnStartedEvent(Guid commandId, int turn, int activeActorId) : base(commandId, turn)
        {
            ActiveActorId = activeActorId;
        }
    }

    public sealed record CombatStartedEvent : CombatEvent
    {
        public int PlayerId { get; init; }
        public bool IsPlayerFirst { get; init; }
        public int Seed { get; init; }

        public CombatStartedEvent() { }

        public CombatStartedEvent(Guid commandId, int turn, int playerId, bool isPlayerFirst, int seed) : base(commandId, turn)
        {
            PlayerId = playerId;
            IsPlayerFirst = isPlayerFirst;
            Seed = seed;
        }
    }

    public sealed record CardPlayedEvent : CombatEvent
    {
        public int ActorId { get; init; }
        public int CardInstanceId { get; init; }
        public string CardName { get; init; }

        public CardPlayedEvent() { }

        public CardPlayedEvent(Guid commandId, int turn, int actorId, int cardInstanceId, string cardName) : base(commandId, turn)
        {
            ActorId = actorId;
            CardInstanceId = cardInstanceId;
            CardName = cardName;
        }
    }

    public sealed record UnitDeployedEvent : CombatEvent
    {
        public int UnitId { get; init; }
        public int NodeId { get; init; }
        public string UnitName { get; init; }
        public int OwnerId { get; init; }

        public UnitDeployedEvent() { }

        public UnitDeployedEvent(Guid commandId, int turn, int unitId, int nodeId, string unitName, int ownerId) : base(commandId, turn)
        {
            UnitId = unitId;
            NodeId = nodeId;
            UnitName = unitName;
            OwnerId = ownerId;
        }
    }

    public sealed record UnitMovedEvent : CombatEvent
    {
        public int UnitId { get; init; }
        public int FromNodeId { get; init; }
        public int ToNodeId { get; init; }
        public string UnitName { get; init; }

        public UnitMovedEvent() { }

        public UnitMovedEvent(Guid commandId, int turn, int unitId, int fromNodeId, int toNodeId, string unitName) : base(commandId, turn)
        {
            UnitId = unitId;
            FromNodeId = fromNodeId;
            ToNodeId = toNodeId;
            UnitName = unitName;
        }
    }

    public sealed record DamageAppliedEvent : CombatEvent
    {
        public int? SourceUnitId { get; init; }
        public int? TargetUnitId { get; init; }
        public int TargetHQOwnerId { get; init; }
        public int Amount { get; init; }

        public DamageAppliedEvent() { }

        public DamageAppliedEvent(Guid commandId, int turn, int? sourceUnitId, int? targetUnitId, int targetHQOwnerId, int amount) : base(commandId, turn)
        {
            SourceUnitId = sourceUnitId;
            TargetUnitId = targetUnitId;
            TargetHQOwnerId = targetHQOwnerId;
            Amount = amount;
        }
    }

    public sealed record UnitDestroyedEvent : CombatEvent
    {
        public int UnitId { get; init; }
        public string UnitName { get; init; }

        public UnitDestroyedEvent() { }

        public UnitDestroyedEvent(Guid commandId, int turn, int unitId, string unitName) : base(commandId, turn)
        {
            UnitId = unitId;
            UnitName = unitName;
        }
    }

    public sealed record CombatEndedEvent : CombatEvent
    {
        public int WinnerActorId { get; init; }
        public string Reason { get; init; }
        public bool IsVictory { get; init; }

        public CombatEndedEvent() { }

        public CombatEndedEvent(Guid commandId, int turn, int winnerActorId, string reason, bool isVictory) : base(commandId, turn)
        {
            WinnerActorId = winnerActorId;
            Reason = reason;
            IsVictory = isVictory;
        }
    }

    public sealed record TurnEndedEvent : CombatEvent
    {
        public int ActorId { get; init; }

        public TurnEndedEvent() { }

        public TurnEndedEvent(Guid commandId, int turn, int actorId) : base(commandId, turn)
        {
            ActorId = actorId;
        }
    }

    public sealed record SelectionCancelledEvent : CombatEvent
    {
        public SelectionCancelledEvent() { }

        public SelectionCancelledEvent(Guid commandId, int turn) : base(commandId, turn) { }
    }
}
