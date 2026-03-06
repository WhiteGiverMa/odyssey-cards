using System.Collections.Generic;
using System.Linq;
using OdysseyCards.Domain.Combat.Commands;
using OdysseyCards.Domain.Combat.Engine;

namespace OdysseyCards.Domain.Combat.AI
{
    public sealed class DomainEnemyAI : IEnemyAI
    {
        public IReadOnlyList<CombatCommand> GenerateCommands(AIContext context)
        {
            var commands = new List<CombatCommand>();

            var deployCommand = TryDeployUnit(context);
            if (deployCommand != null)
            {
                commands.Add(deployCommand);
                return commands;
            }

            var attackCommand = TryAttack(context);
            if (attackCommand != null)
            {
                commands.Add(attackCommand);
                return commands;
            }

            commands.Add(new EndTurnCommand(context.Turn, context.ActorId));
            return commands;
        }

        private CombatCommand TryDeployUnit(AIContext context)
        {
            if (context.HandCardIds == null || context.HandCardIds.Count == 0)
            {
                return null;
            }

            if (context.Energy <= 0)
            {
                return null;
            }

            int deployNodeId = context.Board.EnemyDeploymentNodeId;

            bool hasUnitAtDeploy = context.OwnUnits?.Any(u => u.NodeId == deployNodeId) ?? false;
            if (hasUnitAtDeploy)
            {
                return null;
            }

            int cardId = context.HandCardIds[0];
            return new DeployUnitCommand(context.Turn, context.ActorId, cardId, deployNodeId);
        }

        private CombatCommand TryAttack(AIContext context)
        {
            if (context.OwnUnits == null || context.OwnUnits.Count == 0)
            {
                return null;
            }

            foreach (var unit in context.OwnUnits)
            {
                if (!unit.CanAttack)
                {
                    continue;
                }

                int? targetNodeId = FindBestTarget(unit, context);
                if (targetNodeId.HasValue)
                {
                    int? targetUnitId = null;
                    var targetUnit = context.EnemyUnits?.FirstOrDefault(u => u.NodeId == targetNodeId.Value);
                    if (targetUnit != null)
                    {
                        targetUnitId = targetUnit.UnitId;
                    }

                    return new AttackCommand(context.Turn, context.ActorId, unit.UnitId, targetNodeId.Value, targetUnitId);
                }
            }

            return null;
        }

        private int? FindBestTarget(UnitSnapshot attacker, AIContext context)
        {
            if (context.EnemyUnits != null)
            {
                foreach (var enemyUnit in context.EnemyUnits)
                {
                    if (context.Board.IsInAttackRange(attacker.NodeId, enemyUnit.NodeId, attacker.Range))
                    {
                        return enemyUnit.NodeId;
                    }
                }
            }

            if (context.Board.IsInAttackRange(attacker.NodeId, context.EnemyHQNodeId, attacker.Range))
            {
                return context.EnemyHQNodeId;
            }

            return null;
        }
    }
}
