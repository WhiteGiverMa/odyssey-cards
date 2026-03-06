using System.Collections.Generic;
using Godot;
using OdysseyCards.Domain.Combat.Events;
using OdysseyCards.Domain.Combat.Engine;

namespace OdysseyCards.Presentation.Events
{
    public sealed class DefaultCombatEventUIBridge : CombatEventUIBridge
    {
        private readonly CombatSnapshotProvider _snapshotProvider;

        public event System.Action OnCombatStart;
        public event System.Action OnTurnStart;
        public event System.Action OnTurnEnd;
        public event System.Action<bool> OnCombatEnd;
        public event System.Action<int, int, string> OnUnitDeployed;
        public event System.Action<int, int, int, string> OnUnitMoved;
        public event System.Action<int, int?, int, int> OnDamageApplied;
        public event System.Action<int, string> OnUnitDestroyed;
        public event System.Action<List<int>> OnAttackRangeShow;
        public event System.Action OnAttackRangeHide;

        public DefaultCombatEventUIBridge(CombatSnapshotProvider snapshotProvider)
        {
            _snapshotProvider = snapshotProvider;
        }

        public void HandleCombatStarted(CombatStartedEvent evt)
        {
            GD.Print($"[UIBridge] Combat started - Player: {evt.PlayerId}, PlayerFirst: {evt.IsPlayerFirst}");
            OnCombatStart?.Invoke();
        }

        public void HandleTurnStarted(TurnStartedEvent evt)
        {
            GD.Print($"[UIBridge] Turn {evt.Turn} started - Active actor: {evt.ActiveActorId}");
            OnTurnStart?.Invoke();
        }

        public void HandleTurnEnded(TurnEndedEvent evt)
        {
            GD.Print($"[UIBridge] Turn {evt.Turn} ended - Actor: {evt.ActorId}");
            OnTurnEnd?.Invoke();
        }

        public void HandleUnitDeployed(UnitDeployedEvent evt)
        {
            GD.Print($"[UIBridge] Unit deployed: {evt.UnitName} at node {evt.NodeId}");
            OnUnitDeployed?.Invoke(evt.UnitId, evt.NodeId, evt.UnitName);
        }

        public void HandleUnitMoved(UnitMovedEvent evt)
        {
            GD.Print($"[UIBridge] Unit moved: {evt.UnitName} from {evt.FromNodeId} to {evt.ToNodeId}");
            OnUnitMoved?.Invoke(evt.UnitId, evt.FromNodeId, evt.ToNodeId, evt.UnitName);
        }

        public void HandleDamageApplied(DamageAppliedEvent evt)
        {
            GD.Print($"[UIBridge] Damage applied: {evt.Amount} from {evt.SourceUnitId} to {evt.TargetUnitId ?? evt.TargetHQOwnerId}");
            OnDamageApplied?.Invoke(evt.Amount, evt.SourceUnitId, evt.TargetUnitId ?? -1, evt.TargetHQOwnerId);
        }

        public void HandleUnitDestroyed(UnitDestroyedEvent evt)
        {
            GD.Print($"[UIBridge] Unit destroyed: {evt.UnitName}");
            OnUnitDestroyed?.Invoke(evt.UnitId, evt.UnitName);
        }

        public void HandleCombatEnded(CombatEndedEvent evt)
        {
            GD.Print($"[UIBridge] Combat ended - Victory: {evt.IsVictory}, Reason: {evt.Reason}");
            OnCombatEnd?.Invoke(evt.IsVictory);
        }

        public void ShowAttackRange(List<int> nodeIds)
        {
            OnAttackRangeShow?.Invoke(nodeIds);
        }

        public void HideAttackRange()
        {
            OnAttackRangeHide?.Invoke();
        }
    }

    public interface CombatSnapshotProvider
    {
        CombatSnapshot GetSnapshot();
    }
}
