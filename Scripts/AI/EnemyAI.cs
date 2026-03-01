using System.Collections.Generic;
using System.Linq;
using Godot;
using OdysseyCards.Card;
using OdysseyCards.Character;
using OdysseyCards.Combat;
using OdysseyCards.Map;

namespace OdysseyCards.AI
{
    public class EnemyAI
    {
        public AIAction DecideAction(Enemy enemy, CombatManager combat)
        {
            int currentEnergy = enemy.CurrentEnergy;

            List<Unit> deployableUnits = GetDeployableUnits(enemy, currentEnergy);
            if (deployableUnits.Count > 0)
            {
                Unit unitToDeploy = SelectBestDeployTarget(deployableUnits);
                if (unitToDeploy != null)
                {
                    return AIAction.DeployUnit(unitToDeploy);
                }
            }

            AIAction attackAction = TryGetAttackAction(enemy, combat);
            if (attackAction.IsValid())
            {
                return attackAction;
            }

            List<Order> playableOrders = GetPlayableOrders(enemy, currentEnergy);
            if (playableOrders.Count > 0)
            {
                return AIAction.PlayOrder(playableOrders[0]);
            }

            return AIAction.EndTurn();
        }

        public List<Unit> GetDeployableUnits(Enemy enemy, int currentEnergy)
        {
            List<Unit> result = new();

            if (enemy.Hand == null)
            {
                return result;
            }

            foreach (OdysseyCards.Card.Card card in enemy.Hand)
            {
                if (card is Unit unit && unit.DeployCost <= currentEnergy)
                {
                    result.Add(unit);
                }
            }

            return result;
        }

        public List<Order> GetPlayableOrders(Enemy enemy, int currentEnergy)
        {
            List<Order> result = new();

            if (enemy.Hand == null)
            {
                return result;
            }

            foreach (OdysseyCards.Card.Card card in enemy.Hand)
            {
                if (card is Order order && order.Cost <= currentEnergy)
                {
                    result.Add(order);
                }
            }

            return result;
        }

        public Unit SelectBestDeployTarget(List<Unit> units)
        {
            if (units == null || units.Count == 0)
            {
                return null;
            }

            return units.OrderBy(u => u.DeployCost).First();
        }

        public int SelectBestAttackTarget(Unit attacker, CombatManager combat)
        {
            if (combat == null || attacker == null)
            {
                return -1;
            }

            BattleMap battleMap = combat.BattleMap;
            if (battleMap == null)
            {
                return -1;
            }

            List<int> nodesInRange = battleMap.GetNodesInRange(attacker.CurrentNode, attacker.Range);

            foreach (int nodeId in nodesInRange)
            {
                Unit unitAtNode = combat.GetUnitAtNode(nodeId);
                if (unitAtNode != null && unitAtNode.Owner == NodeOwner.Player)
                {
                    return nodeId;
                }
            }

            if (nodesInRange.Contains(battleMap.PlayerDeploymentNodeId))
            {
                return battleMap.PlayerDeploymentNodeId;
            }

            return -1;
        }

        private AIAction TryGetAttackAction(Enemy enemy, CombatManager combat)
        {
            if (combat == null || combat.EnemyUnits == null)
            {
                return AIAction.None();
            }

            foreach (Unit unit in combat.EnemyUnits)
            {
                if (unit.CanAttack())
                {
                    int targetNodeId = SelectBestAttackTarget(unit, combat);
                    if (targetNodeId >= 0)
                    {
                        return AIAction.AttackWithUnit(unit, targetNodeId);
                    }
                }
            }

            return AIAction.None();
        }
    }
}
