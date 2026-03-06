using System;
using System.Collections.Generic;

namespace OdysseyCards.Domain.Combat.State
{
    public sealed class BoardState
    {
        private readonly Dictionary<int, MapNodeState> _nodes = new();
        private readonly List<int> _playerDeploymentNodes = new();
        private readonly List<int> _enemyDeploymentNodes = new();
        private int _playerHQNodeId = -1;
        private int _enemyHQNodeId = -1;

        public IReadOnlyDictionary<int, MapNodeState> Nodes => _nodes;
        public IReadOnlyList<int> PlayerDeploymentNodes => _playerDeploymentNodes;
        public IReadOnlyList<int> EnemyDeploymentNodes => _enemyDeploymentNodes;
        public int PlayerHQNodeId => _playerHQNodeId;
        public int EnemyHQNodeId => _enemyHQNodeId;

        public void InitializeDefaultMap()
        {
            _nodes.Clear();
            _playerDeploymentNodes.Clear();
            _enemyDeploymentNodes.Clear();

            for (int i = 0; i < 7; i++)
            {
                _nodes[i] = new MapNodeState
                {
                    NodeId = i,
                    Owner = NodeOwner.None,
                    UnitId = null
                };
            }

            _playerDeploymentNodes.Add(0);
            _playerHQNodeId = 0;
            _nodes[0].IsHQ = true;
            _nodes[0].Owner = NodeOwner.Player;

            _enemyDeploymentNodes.Add(6);
            _enemyHQNodeId = 6;
            _nodes[6].IsHQ = true;
            _nodes[6].Owner = NodeOwner.Enemy;
        }

        public bool CanDeployTo(int nodeId, NodeOwner owner)
        {
            if (!_nodes.TryGetValue(nodeId, out MapNodeState node))
            {
                return false;
            }

            if (node.UnitId.HasValue)
            {
                return false;
            }

            if (owner == NodeOwner.Player)
            {
                return _playerDeploymentNodes.Contains(nodeId);
            }

            return _enemyDeploymentNodes.Contains(nodeId);
        }

        public bool CanMoveTo(int fromNodeId, int toNodeId, NodeOwner owner)
        {
            if (!_nodes.TryGetValue(fromNodeId, out _) || !_nodes.TryGetValue(toNodeId, out MapNodeState toNode))
            {
                return false;
            }

            if (toNode.UnitId.HasValue)
            {
                return false;
            }

            return Math.Abs(toNodeId - fromNodeId) <= 1;
        }

        public bool IsInAttackRange(int fromNodeId, int toNodeId, int range)
        {
            return Math.Abs(toNodeId - fromNodeId) <= range;
        }

        public void PlaceUnit(int nodeId, int unitId)
        {
            if (_nodes.TryGetValue(nodeId, out MapNodeState node))
            {
                node.UnitId = unitId;
            }
        }

        public void RemoveUnit(int nodeId)
        {
            if (_nodes.TryGetValue(nodeId, out MapNodeState node))
            {
                node.UnitId = null;
            }
        }

        public void MoveUnit(int fromNodeId, int toNodeId)
        {
            if (_nodes.TryGetValue(fromNodeId, out MapNodeState fromNode) &&
                _nodes.TryGetValue(toNodeId, out MapNodeState toNode))
            {
                toNode.UnitId = fromNode.UnitId;
                fromNode.UnitId = null;
            }
        }

        public int? GetUnitAtNode(int nodeId)
        {
            if (_nodes.TryGetValue(nodeId, out MapNodeState node))
            {
                return node.UnitId;
            }
            return null;
        }
    }

    public sealed class MapNodeState
    {
        public int NodeId { get; init; }
        public NodeOwner Owner { get; set; }
        public int? UnitId { get; set; }
        public bool IsHQ { get; set; }
    }

    public enum NodeOwner
    {
        None,
        Player,
        Enemy
    }
}
