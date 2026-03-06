using Godot;
using OdysseyCards.Domain.Combat.Events;
using OdysseyCards.Domain.Combat.Engine;

namespace OdysseyCards.Presentation.Events
{
    public interface CombatEventUIBridge
    {
        void HandleCombatStarted(CombatStartedEvent evt);
        void HandleTurnStarted(TurnStartedEvent evt);
        void HandleTurnEnded(TurnEndedEvent evt);
        void HandleUnitDeployed(UnitDeployedEvent evt);
        void HandleUnitMoved(UnitMovedEvent evt);
        void HandleDamageApplied(DamageAppliedEvent evt);
        void HandleUnitDestroyed(UnitDestroyedEvent evt);
        void HandleCombatEnded(CombatEndedEvent evt);
    }
}
