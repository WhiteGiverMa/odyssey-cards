using System;
using System.Collections.Generic;
using OdysseyCards.Domain.Combat.Events;

namespace OdysseyCards.Presentation.Presenters
{
    public class CombatEventPresenter
    {
        private readonly List<ICombatEventHandler> _handlers = new();

        public event EventHandler<UnitDeployedEventArgs> OnUnitDeployed;
        public event EventHandler<UnitMovedEventArgs> OnUnitMoved;
        public event EventHandler<DamageAppliedEventArgs> OnDamageApplied;
        public event EventHandler<UnitDestroyedEventArgs> OnUnitDestroyed;
        public event EventHandler<TurnStartedEventArgs> OnTurnStarted;
        public event EventHandler<TurnEndedEventArgs> OnTurnEnded;
        public event EventHandler<CombatEndedEventArgs> OnCombatEnded;

        public void RegisterHandler(ICombatEventHandler handler)
        {
            _handlers.Add(handler);
        }

        public void UnregisterHandler(ICombatEventHandler handler)
        {
            _handlers.Remove(handler);
        }

        public void HandleEvent(CombatEvent evt)
        {
            foreach (var handler in _handlers)
            {
                handler.HandleEvent(evt);
            }

            switch (evt)
            {
                case UnitDeployedEvent deployed:
                    OnUnitDeployed?.Invoke(this, new UnitDeployedEventArgs(
                        deployed.UnitId,
                        deployed.NodeId,
                        deployed.UnitName,
                        deployed.OwnerId));
                    break;

                case UnitMovedEvent moved:
                    OnUnitMoved?.Invoke(this, new UnitMovedEventArgs(
                        moved.UnitId,
                        moved.FromNodeId,
                        moved.ToNodeId,
                        moved.UnitName));
                    break;

                case DamageAppliedEvent damage:
                    OnDamageApplied?.Invoke(this, new DamageAppliedEventArgs(
                        damage.SourceUnitId,
                        damage.TargetUnitId,
                        damage.TargetHQOwnerId,
                        damage.Amount));
                    break;

                case UnitDestroyedEvent destroyed:
                    OnUnitDestroyed?.Invoke(this, new UnitDestroyedEventArgs(
                        destroyed.UnitId,
                        destroyed.UnitName));
                    break;

                case TurnStartedEvent turnStarted:
                    OnTurnStarted?.Invoke(this, new TurnStartedEventArgs(
                        turnStarted.Turn,
                        turnStarted.ActiveActorId));
                    break;

                case TurnEndedEvent turnEnded:
                    OnTurnEnded?.Invoke(this, new TurnEndedEventArgs(
                        turnEnded.Turn,
                        turnEnded.ActorId));
                    break;

                case CombatEndedEvent combatEnded:
                    OnCombatEnded?.Invoke(this, new CombatEndedEventArgs(
                        combatEnded.WinnerActorId,
                        combatEnded.Reason,
                        combatEnded.IsVictory));
                    break;
            }
        }
    }

    public class UnitDeployedEventArgs : EventArgs
    {
        public int UnitId { get; }
        public int NodeId { get; }
        public string UnitName { get; }
        public int OwnerId { get; }

        public UnitDeployedEventArgs(int unitId, int nodeId, string unitName, int ownerId)
        {
            UnitId = unitId;
            NodeId = nodeId;
            UnitName = unitName;
            OwnerId = ownerId;
        }
    }

    public class UnitMovedEventArgs : EventArgs
    {
        public int UnitId { get; }
        public int FromNodeId { get; }
        public int ToNodeId { get; }
        public string UnitName { get; }

        public UnitMovedEventArgs(int unitId, int fromNodeId, int toNodeId, string unitName)
        {
            UnitId = unitId;
            FromNodeId = fromNodeId;
            ToNodeId = toNodeId;
            UnitName = unitName;
        }
    }

    public class DamageAppliedEventArgs : EventArgs
    {
        public int? SourceUnitId { get; }
        public int? TargetUnitId { get; }
        public int TargetHQOwnerId { get; }
        public int Amount { get; }

        public DamageAppliedEventArgs(int? sourceUnitId, int? targetUnitId, int targetHQOwnerId, int amount)
        {
            SourceUnitId = sourceUnitId;
            TargetUnitId = targetUnitId;
            TargetHQOwnerId = targetHQOwnerId;
            Amount = amount;
        }
    }

    public class UnitDestroyedEventArgs : EventArgs
    {
        public int UnitId { get; }
        public string UnitName { get; }

        public UnitDestroyedEventArgs(int unitId, string unitName)
        {
            UnitId = unitId;
            UnitName = unitName;
        }
    }

    public class TurnStartedEventArgs : EventArgs
    {
        public int Turn { get; }
        public int ActiveActorId { get; }

        public TurnStartedEventArgs(int turn, int activeActorId)
        {
            Turn = turn;
            ActiveActorId = activeActorId;
        }
    }

    public class TurnEndedEventArgs : EventArgs
    {
        public int Turn { get; }
        public int ActorId { get; }

        public TurnEndedEventArgs(int turn, int actorId)
        {
            Turn = turn;
            ActorId = actorId;
        }
    }

    public class CombatEndedEventArgs : EventArgs
    {
        public int WinnerActorId { get; }
        public string Reason { get; }
        public bool IsVictory { get; }

        public CombatEndedEventArgs(int winnerActorId, string reason, bool isVictory)
        {
            WinnerActorId = winnerActorId;
            Reason = reason;
            IsVictory = isVictory;
        }
    }

    public interface ICombatEventHandler
    {
        void HandleEvent(CombatEvent evt);
    }
}
