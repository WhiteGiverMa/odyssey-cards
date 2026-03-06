using System.Collections.Generic;
using OdysseyCards.Domain.Combat.Engine;

namespace OdysseyCards.Domain.Combat.AI
{
    public sealed class AIContext
    {
        public int ActorId { get; init; }
        public int Turn { get; init; }
        public int Energy { get; init; }
        public int MaxEnergy { get; init; }
        public IReadOnlyList<UnitSnapshot> OwnUnits { get; init; }
        public IReadOnlyList<UnitSnapshot> EnemyUnits { get; init; }
        public int EnemyHQNodeId { get; init; }
        public int OwnHQNodeId { get; init; }
        public IReadOnlyList<int> HandCardIds { get; init; }
        public BoardInfo Board { get; init; }
    }

    public sealed class BoardInfo
    {
        public int PlayerDeploymentNodeId { get; init; }
        public int EnemyDeploymentNodeId { get; init; }
        public int PlayerHQNodeId { get; init; }
        public int EnemyHQNodeId { get; init; }
        public IReadOnlyList<int> AllNodeIds { get; init; }

        public bool IsInAttackRange(int fromNode, int toNode, int range)
        {
            int distance = System.Math.Abs(fromNode - toNode);
            return distance <= range;
        }

        public bool CanDeployTo(int nodeId, bool isEnemy)
        {
            int deployNode = isEnemy ? EnemyDeploymentNodeId : PlayerDeploymentNodeId;
            return nodeId == deployNode;
        }
    }
}
