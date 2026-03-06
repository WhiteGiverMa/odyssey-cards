using Godot;
using System;
using System.Collections.Generic;
using OdysseyCards.Card;
using OdysseyCards.Core;
using OdysseyCards.Domain.Combat.Engine;
using OdysseyCards.Map;
using OdysseyCards.Presentation.Input;

namespace OdysseyCards.UI
{
    public partial class BattleMapUI : Control
    {
        public delegate void NodeDropTargetHandler(int nodeId, Card.Card card);
        public event NodeDropTargetHandler OnNodeDropTarget;

        private BattleMap _battleMap;
        private Dictionary<int, MapNodeUI> _nodeUIs = new Dictionary<int, MapNodeUI>();
        private List<MapEdgeUI> _edgeUIs = new List<MapEdgeUI>();

        private int _selectedNodeId = -1;
        private List<int> _highlightedNodes = new List<int>();

        private bool _isDeployMode = false;
        private bool _isAttackMode = false;
        private List<int> _attackTargetNodes = new List<int>();
        private Card.Card _draggingCard = null;
        private int _hoveredNodeId = -1;

        private Vector2 _lastSize = Vector2.Zero;
        private const float _minResizeThreshold = 10.0f;

        public BattleMap BattleMap => _battleMap;
        public int SelectedNodeId => _selectedNodeId;
        public bool IsDeployMode => _isDeployMode;
        public bool IsAttackMode => _isAttackMode;

        public override void _Ready()
        {
            CustomMinimumSize = new Vector2(800, 400);
            AnchorRight = 1.0f;
            AnchorBottom = 1.0f;
            Resized += OnResized;
        }

        private void OnResized()
        {
            if (_battleMap == null)
                return;

            float sizeDelta = (Size - _lastSize).Length();
            if (sizeDelta < _minResizeThreshold)
                return;

            _lastSize = Size;
            RebuildUI();
        }

        public void SetBattleMap(BattleMap battleMap)
        {
            GD.Print($"[BattleMapUI] SetBattleMap called, battleMap is null: {battleMap == null}");
            _battleMap = battleMap ?? throw new System.ArgumentNullException(nameof(battleMap));
            GD.Print("[BattleMapUI] Calling RebuildUI");
            RebuildUI();
            GD.Print("[BattleMapUI] SetBattleMap completed");
        }

        public void RebuildUI()
        {
            GD.Print("[BattleMapUI] RebuildUI started");
            foreach (var child in GetChildren())
            {
                child.QueueFree();
            }
            _nodeUIs.Clear();
            _edgeUIs.Clear();

            if (_battleMap == null)
            {
                GD.Print("[BattleMapUI] RebuildUI early return - battleMap is null");
                return;
            }

            _lastSize = Size;

            GD.Print($"[BattleMapUI] Creating edges, count: {_battleMap.Edges.Count}");
            CreateEdgeUIs();
            GD.Print($"[BattleMapUI] Creating nodes, count: {_battleMap.Nodes.Count}");
            CreateNodeUIs();
            GD.Print("[BattleMapUI] RebuildUI completed");
        }

        private void CreateNodeUIs()
        {
            float cellSize = UIScaler.Instance != null
                ? UIScaler.Instance.GetNodeSize(Size.Y)
                : 80.0f;

            float minX = float.MaxValue, maxX = float.MinValue;
            float minY = float.MaxValue, maxY = float.MinValue;

            foreach (var kvp in _battleMap.Nodes)
            {
                var node = kvp.Value;
                minX = Mathf.Min(minX, node.GridPosition.X);
                maxX = Mathf.Max(maxX, node.GridPosition.X);
                minY = Mathf.Min(minY, node.GridPosition.Y);
                maxY = Mathf.Max(maxY, node.GridPosition.Y);
            }

            float mapWidth = (maxX - minX) * cellSize;
            float mapHeight = (maxY - minY) * cellSize;

            float containerWidth = Size.X;
            float containerHeight = Size.Y;

            float offsetX = (containerWidth - mapWidth) / 2.0f - minX * cellSize;
            float offsetY = (containerHeight - mapHeight) / 2.0f - minY * cellSize;

            CombatSnapshot snapshot = CombatInputAdapter.Instance?.GetApplicationService()?.GetSnapshot();

            Dictionary<int, UnitSnapshot> unitsByNode = new Dictionary<int, UnitSnapshot>();
            if (snapshot != null)
            {
                foreach (var unit in snapshot.PlayerUnits)
                {
                    unitsByNode[unit.NodeId] = unit;
                }
                foreach (var unit in snapshot.EnemyUnits)
                {
                    unitsByNode[unit.NodeId] = unit;
                }
            }

            foreach (var kvp in _battleMap.Nodes)
            {
                var node = kvp.Value;
                var nodeUI = new MapNodeUI();
                nodeUI.SetNode(node);
                nodeUI.SetSize(cellSize);

                Vector2 position = new Vector2(
                    node.GridPosition.X * cellSize + offsetX,
                    node.GridPosition.Y * cellSize + offsetY
                );
                nodeUI.Position = position;

                nodeUI.ConnectButtonPressed(Callable.From(() => OnNodeClicked(node.Id)));

                if (unitsByNode.TryGetValue(node.Id, out UnitSnapshot unitOnNode))
                {
                    nodeUI.SetUnit(unitOnNode);
                }

                AddChild(nodeUI);
                _nodeUIs[node.Id] = nodeUI;
            }
        }

        private void CreateEdgeUIs()
        {
            float cellSize = UIScaler.Instance != null
                ? UIScaler.Instance.GetNodeSize(Size.Y)
                : 80.0f;
            float nodeCenter = cellSize / 2.0f;

            float minX = float.MaxValue, maxX = float.MinValue;
            float minY = float.MaxValue, maxY = float.MinValue;

            foreach (var kvp in _battleMap.Nodes)
            {
                var node = kvp.Value;
                minX = Mathf.Min(minX, node.GridPosition.X);
                maxX = Mathf.Max(maxX, node.GridPosition.X);
                minY = Mathf.Min(minY, node.GridPosition.Y);
                maxY = Mathf.Max(maxY, node.GridPosition.Y);
            }

            float mapWidth = (maxX - minX) * cellSize;
            float mapHeight = (maxY - minY) * cellSize;

            float containerWidth = Size.X;
            float containerHeight = Size.Y;

            float offsetX = (containerWidth - mapWidth) / 2.0f - minX * cellSize;
            float offsetY = (containerHeight - mapHeight) / 2.0f - minY * cellSize;

            foreach (var edge in _battleMap.Edges)
            {
                var fromNode = _battleMap.GetNode(edge.FromNodeId);
                var toNode = _battleMap.GetNode(edge.ToNodeId);

                if (fromNode == null || toNode == null)
                    continue;

                var edgeUI = new MapEdgeUI();

                Vector2 fromPos = new Vector2(
                    fromNode.GridPosition.X * cellSize + offsetX + nodeCenter,
                    fromNode.GridPosition.Y * cellSize + offsetY + nodeCenter
                );
                Vector2 toPos = new Vector2(
                    toNode.GridPosition.X * cellSize + offsetX + nodeCenter,
                    toNode.GridPosition.Y * cellSize + offsetY + nodeCenter
                );

                edgeUI.SetEdge(edge, fromPos, toPos);
                AddChild(edgeUI);
                _edgeUIs.Add(edgeUI);
            }
        }

        private void OnNodeClicked(int nodeId)
        {
            _selectedNodeId = nodeId;
            GD.Print($"Node {nodeId} clicked");
        }

        public void HighlightNodes(List<int> nodeIds, Color? color = null)
        {
            ClearHighlights();

            foreach (var nodeId in nodeIds)
            {
                if (_nodeUIs.TryGetValue(nodeId, out var nodeUI))
                {
                    nodeUI.SetHighlight(true, color);
                    _highlightedNodes.Add(nodeId);
                }
            }
        }

        public void ClearHighlights()
        {
            foreach (var nodeId in _highlightedNodes)
            {
                if (_nodeUIs.TryGetValue(nodeId, out var nodeUI))
                {
                    nodeUI.SetHighlight(false);
                }
            }
            _highlightedNodes.Clear();
        }

        public void SelectNode(int nodeId)
        {
            if (_nodeUIs.TryGetValue(nodeId, out var nodeUI))
            {
                _selectedNodeId = nodeId;
                nodeUI.SetHighlight(true, Colors.Cyan);
            }
        }

        public void DeselectNode()
        {
            if (_selectedNodeId >= 0 && _nodeUIs.TryGetValue(_selectedNodeId, out var nodeUI))
            {
                nodeUI.SetHighlight(false);
            }
            _selectedNodeId = -1;
        }

        public MapNodeUI GetNodeUI(int nodeId)
        {
            return _nodeUIs.TryGetValue(nodeId, out var nodeUI) ? nodeUI : null;
        }

        public int GetNodeAtPosition(Vector2 globalPos)
        {
            Vector2 localPos = GetGlobalRect().Position;
            Vector2 relativePos = globalPos - localPos;

            foreach (var kvp in _nodeUIs)
            {
                var nodeUI = kvp.Value;
                Rect2 nodeRect = new Rect2(nodeUI.Position, nodeUI.CustomMinimumSize);
                if (nodeRect.HasPoint(relativePos))
                {
                    return kvp.Key;
                }
            }
            return -1;
        }

        public void HighlightDeploymentNodes(bool highlight)
        {
            if (!highlight)
            {
                ClearHighlights();
                return;
            }

            var deploymentNodeIds = new List<int>();
            foreach (var kvp in _battleMap.Nodes)
            {
                if (kvp.Value.IsPlayerDeploymentPoint)
                {
                    deploymentNodeIds.Add(kvp.Key);
                }
            }
            HighlightNodes(deploymentNodeIds, MapNodeUI.DeploymentColor);
        }

        public void HighlightAttackTargets(List<int> nodeIds)
        {
            if (nodeIds == null || nodeIds.Count == 0)
            {
                ClearHighlights();
                return;
            }

            HighlightNodes(nodeIds, Colors.Red);
        }

        public void SetDeployMode(bool active)
        {
            _isDeployMode = active;
            _isAttackMode = false;
            _attackTargetNodes.Clear();

            if (active)
            {
                HighlightDeploymentNodes(true);
            }
            else
            {
                ClearHighlights();
            }
        }

        public void SetAttackMode(bool active, List<int> targetNodes)
        {
            _isAttackMode = active;
            _isDeployMode = false;
            _attackTargetNodes = targetNodes ?? new List<int>();

            if (active && _attackTargetNodes.Count > 0)
            {
                HighlightAttackTargets(_attackTargetNodes);
            }
            else
            {
                ClearHighlights();
            }
        }

        public void StartCardDrag(Card.Card card)
        {
            _draggingCard = card;
            _hoveredNodeId = -1;

            if (card != null && IsUnitCard(card))
            {
                SetDeployMode(true);
            }
        }

        public void EndCardDrag(bool success)
        {
            if (_draggingCard != null && _hoveredNodeId >= 0 && !success)
            {
                OnNodeDropTarget?.Invoke(_hoveredNodeId, _draggingCard);
            }

            _draggingCard = null;
            _hoveredNodeId = -1;
            SetDeployMode(false);
            SetAttackMode(false, null);
        }

        public void UpdateDragPosition(Vector2 globalPos)
        {
            if (_draggingCard == null)
                return;

            int newNodeId = GetNodeAtPosition(globalPos);
            if (newNodeId != _hoveredNodeId)
            {
                _hoveredNodeId = newNodeId;
                UpdateDragHighlight();
            }
        }

        private void UpdateDragHighlight()
        {
            if (_isDeployMode)
            {
                foreach (var kvp in _nodeUIs)
                {
                    bool isDeploymentPoint = _battleMap.Nodes.ContainsKey(kvp.Key) &&
                                             _battleMap.Nodes[kvp.Key].IsPlayerDeploymentPoint;
                    bool isHovered = kvp.Key == _hoveredNodeId;

                    if (isDeploymentPoint)
                    {
                        kvp.Value.SetHighlight(true, isHovered ? Colors.Green : MapNodeUI.DeploymentColor);
                    }
                }
            }
        }

        private bool IsUnitCard(Card.Card card)
        {
            return card != null && card.Type == CardType.Unit;
        }

        public void CancelDrag()
        {
            _draggingCard = null;
            _hoveredNodeId = -1;
            SetDeployMode(false);
            SetAttackMode(false, null);
        }
    }
}
