using Godot;
using System.Collections.Generic;
using System.Linq;

namespace OdysseyCards.Map
{
    public partial class BattleMap : Node2D
    {
        public Dictionary<int, MapNode> Nodes { get; private set; } = new Dictionary<int, MapNode>();
        public List<MapEdge> Edges { get; private set; } = new List<MapEdge>();

        public Headquarters PlayerHQ { get; private set; }
        public Headquarters EnemyHQ { get; private set; }

        public int PlayerDeploymentNodeId { get; private set; } = -1;
        public int EnemyDeploymentNodeId { get; private set; } = -1;

        private int _nextNodeId = 0;

        public override void _Ready()
        {
            InitializeDefaultMap();
        }

        public void InitializeDefaultMap()
        {
            Nodes.Clear();
            Edges.Clear();
            _nextNodeId = 0;

            CreateDefaultMapLayout();
            CalculateDistances();
        }

        private void CreateDefaultMapLayout()
        {
            int playerDeployId = CreateNode(new Vector2I(0, 0), NodeOwner.Player, true, true);
            int enemyDeployId = CreateNode(new Vector2I(4, 0), NodeOwner.Enemy, true, false);

            PlayerDeploymentNodeId = playerDeployId;
            EnemyDeploymentNodeId = enemyDeployId;

            int n1 = CreateNode(new Vector2I(1, 0));
            int n2 = CreateNode(new Vector2I(2, 0));
            int n3 = CreateNode(new Vector2I(3, 0));
            int n4 = CreateNode(new Vector2I(1, 1));
            int n5 = CreateNode(new Vector2I(2, 1));
            int n6 = CreateNode(new Vector2I(3, 1));
            int n7 = CreateNode(new Vector2I(2, 2));

            CreateEdge(playerDeployId, n1);
            CreateEdge(n1, n2);
            CreateEdge(n2, n3);
            CreateEdge(n3, enemyDeployId);
            CreateEdge(n1, n4);
            CreateEdge(n2, n5);
            CreateEdge(n3, n6);
            CreateEdge(n4, n5);
            CreateEdge(n5, n6);
            CreateEdge(n5, n7);

            PlayerHQ = new Headquarters(NodeOwner.Player, 8, playerDeployId);
            EnemyHQ = new Headquarters(NodeOwner.Enemy, 8, enemyDeployId);
        }

        public int CreateNode(Vector2I gridPosition, NodeOwner owner = NodeOwner.None, bool isDeploymentPoint = false, bool isPlayerDeploymentPoint = true)
        {
            int id = _nextNodeId++;
            var node = new MapNode(id, gridPosition)
            {
                Owner = owner,
                IsDeploymentPoint = isDeploymentPoint,
                IsPlayerDeploymentPoint = isDeploymentPoint && isPlayerDeploymentPoint,
                IsEnemyDeploymentPoint = isDeploymentPoint && !isPlayerDeploymentPoint
            };
            Nodes[id] = node;
            return id;
        }

        public void CreateEdge(int fromNodeId, int toNodeId)
        {
            if (!Nodes.TryGetValue(fromNodeId, out var fromNode) || !Nodes.TryGetValue(toNodeId, out var toNode))
                return;

            var existingEdge = Edges.FirstOrDefault(e => e.Connects(fromNodeId, toNodeId));
            if (existingEdge != null)
                return;

            Edges.Add(new MapEdge(fromNodeId, toNodeId));
            fromNode.AddNeighbor(toNodeId);
            toNode.AddNeighbor(fromNodeId);
        }

        public void CalculateDistances()
        {
            foreach (var node in Nodes.Values)
            {
                node.DistanceToPlayerHQ = CalculateShortestPath(node.Id, PlayerDeploymentNodeId);
                node.DistanceToEnemyHQ = CalculateShortestPath(node.Id, EnemyDeploymentNodeId);
            }
        }

        public int CalculateShortestPath(int fromNodeId, int toNodeId)
        {
            if (!Nodes.ContainsKey(fromNodeId) || !Nodes.ContainsKey(toNodeId))
                return -1;

            if (fromNodeId == toNodeId)
                return 0;

            var visited = new HashSet<int>();
            var queue = new Queue<(int nodeId, int distance)>();

            queue.Enqueue((fromNodeId, 0));
            visited.Add(fromNodeId);

            while (queue.Count > 0)
            {
                var (currentId, distance) = queue.Dequeue();

                var current = Nodes[currentId];
                foreach (var neighborId in current.NeighborIds)
                {
                    if (neighborId == toNodeId)
                        return distance + 1;

                    if (visited.Add(neighborId))
                    {
                        queue.Enqueue((neighborId, distance + 1));
                    }
                }
            }

            return -1;
        }

        public int GetDistance(int nodeIdA, int nodeIdB)
        {
            return CalculateShortestPath(nodeIdA, nodeIdB);
        }

        public bool IsInAttackRange(int attackerNodeId, int targetNodeId, int attackRange)
        {
            int distance = GetDistance(attackerNodeId, targetNodeId);
            return distance >= 0 && distance <= attackRange;
        }

        public bool CanMoveTo(int unitNodeId, int targetNodeId, NodeOwner unitOwner)
        {
            if (!Nodes.TryGetValue(unitNodeId, out var unitNode) || !Nodes.TryGetValue(targetNodeId, out var targetNode))
                return false;

            if (targetNode.IsEnemyDeploymentPoint && unitOwner == NodeOwner.Player)
                return false;

            if (targetNode.IsPlayerDeploymentPoint && unitOwner == NodeOwner.Enemy)
                return false;

            return unitNode.HasNeighbor(targetNodeId);
        }

        public bool CanDeployTo(int targetNodeId, NodeOwner owner)
        {
            if (!Nodes.TryGetValue(targetNodeId, out var targetNode))
                return false;

            if (owner == NodeOwner.Player)
                return targetNode.IsPlayerDeploymentPoint;

            if (owner == NodeOwner.Enemy)
                return targetNode.IsEnemyDeploymentPoint;

            return false;
        }

        public List<int> GetMovableNodes(int fromNodeId, NodeOwner owner)
        {
            var result = new List<int>();

            if (!Nodes.TryGetValue(fromNodeId, out var node))
                return result;

            foreach (var neighborId in node.NeighborIds)
            {
                if (CanMoveTo(fromNodeId, neighborId, owner))
                {
                    result.Add(neighborId);
                }
            }

            return result;
        }

        public List<int> GetNodesInRange(int fromNodeId, int range)
        {
            var result = new List<int>();

            foreach (var node in Nodes.Values)
            {
                if (node.Id != fromNodeId)
                {
                    int distance = GetDistance(fromNodeId, node.Id);
                    if (distance >= 0 && distance <= range)
                    {
                        result.Add(node.Id);
                    }
                }
            }

            return result;
        }

        public MapNode GetNode(int nodeId)
        {
            return Nodes.TryGetValue(nodeId, out var node) ? node : null;
        }
    }
}
