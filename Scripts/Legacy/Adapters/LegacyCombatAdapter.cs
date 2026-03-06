using System;
using System.Collections.Generic;
using OdysseyCards.Combat;
using OdysseyCards.Domain.Combat.Commands;
using OdysseyCards.Domain.Combat.Events;

namespace OdysseyCards.Legacy.Adapters
{
    public class LegacyCombatAdapter
    {
        private readonly CombatManager _combatManager;

        public LegacyCombatAdapter(CombatManager combatManager)
        {
            _combatManager = combatManager ?? throw new ArgumentNullException(nameof(combatManager));
        }

        public IReadOnlyList<CombatEvent> ExecuteLegacy(CombatCommand command)
        {
            var events = new List<CombatEvent>();

            switch (command)
            {
                case EndTurnCommand endTurnCmd:
                    ExecuteEndTurn(endTurnCmd, events);
                    break;

                case DeployUnitCommand deployCmd:
                    ExecuteDeployUnit(deployCmd, events);
                    break;

                case MoveUnitCommand moveCmd:
                    ExecuteMoveUnit(moveCmd, events);
                    break;

                case AttackCommand attackCmd:
                    ExecuteAttack(attackCmd, events);
                    break;

                case CancelSelectionCommand cancelCmd:
                    ExecuteCancelSelection(cancelCmd, events);
                    break;

                case PlayCardCommand playCardCmd:
                    ExecutePlayCard(playCardCmd, events);
                    break;

                default:
                    throw new NotSupportedException($"Unknown command type: {command.GetType().Name}");
            }

            return events;
        }

        private void ExecuteEndTurn(EndTurnCommand command, List<CombatEvent> events)
        {
            _combatManager.EndPlayerTurn();

            events.Add(new TurnEndedEvent(command.CommandId, command.Turn, command.ActorId));
        }

        private void ExecuteDeployUnit(DeployUnitCommand command, List<CombatEvent> events)
        {
            bool success = _combatManager.OnNodeSelected(command.TargetNodeId);

            if (success)
            {
                events.Add(new UnitDeployedEvent(
                    command.CommandId,
                    command.Turn,
                    command.CardInstanceId,
                    command.TargetNodeId,
                    "Unit",
                    command.ActorId
                ));
            }
        }

        private void ExecuteMoveUnit(MoveUnitCommand command, List<CombatEvent> events)
        {
            bool success = _combatManager.OnNodeSelected(command.ToNodeId);

            if (success)
            {
                events.Add(new UnitMovedEvent(
                    command.CommandId,
                    command.Turn,
                    command.UnitId,
                    -1,
                    command.ToNodeId,
                    "Unit"
                ));
            }
        }

        private void ExecuteAttack(AttackCommand command, List<CombatEvent> events)
        {
            bool success = _combatManager.OnNodeSelected(command.TargetNodeId);

            if (success)
            {
                events.Add(new DamageAppliedEvent(
                    command.CommandId,
                    command.Turn,
                    command.AttackerUnitId,
                    command.TargetUnitId,
                    -1,
                    0
                ));
            }
        }

        private void ExecuteCancelSelection(CancelSelectionCommand command, List<CombatEvent> events)
        {
            _combatManager.CancelSelection();

            events.Add(new SelectionCancelledEvent(command.CommandId, command.Turn));
        }

        private void ExecutePlayCard(PlayCardCommand command, List<CombatEvent> events)
        {
            events.Add(new CardPlayedEvent(
                command.CommandId,
                command.Turn,
                command.ActorId,
                command.CardInstanceId,
                "Card"
            ));
        }
    }
}
