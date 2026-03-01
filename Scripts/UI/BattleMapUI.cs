using Godot;
using System;
using System.Collections.Generic;
using OdysseyCards.Card;
using OdysseyCards.Core;
using OdysseyCards.Map;

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
        
        public BattleMap BattleMap => _battleMap;
        public int SelectedNodeId => _selectedNodeId;
        public bool IsDeployMode => _isDeployMode;
        public bool IsAttackMode => _isAttackMode;

        public override void _Ready()
        {
            CustomMinimumSize = new Vector2(800, 400);
            AnchorRight = 1.0f;
            AnchorBottom = 1.0f;
        }

        public void SetBattleMap(BattleMap battleMap)
        {
            _battleMap = battleMap;
            RebuildUI();
        }

        public void RebuildUI()
        {
            foreach (var child in GetChildren())
            {
                child.QueueFree();
            }
            _nodeUIs.Clear();
            _edgeUIs.Clear();
            
            if (_battleMap == null) return;
            
            CreateEdgeUIs();
            CreateNodeUIs();
        }

        private void CreateNodeUIs()
        {
            float cellSize = 80.0f;
            Vector2 offset = new Vector2(100, 150);
            
            foreach (var kvp in _battleMap.Nodes)
            {
                var node = kvp.Value;
                var nodeUI = new MapNodeUI();
                nodeUI.SetNode(node);
                
                Vector2 position = new Vector2(
                    node.GridPosition.X * cellSize + offset.X,
                    node.GridPosition.Y * cellSize + offset.Y
                );
                nodeUI.Position = position;
                
                nodeUI.ConnectButtonPressed(Callable.From(() => OnNodeClicked(node.Id)));
                
                AddChild(nodeUI);
                _nodeUIs[node.Id] = nodeUI;
            }
        }

        private void CreateEdgeUIs()
        {
            float cellSize = 80.0f;
            Vector2 offset = new Vector2(100, 150);
            Vector2 nodeCenter = new Vector2(30, 30);
            
            foreach (var edge in _battleMap.Edges)
            {
                var fromNode = _battleMap.GetNode(edge.FromNodeId);
                var toNode = _battleMap.GetNode(edge.ToNodeId);
                
                if (fromNode == null || toNode == null) continue;
                
                var edgeUI = new MapEdgeUI();
                
                Vector2 fromPos = new Vector2(
                    fromNode.GridPosition.X * cellSize + offset.X + nodeCenter.X,
                    fromNode.GridPosition.Y * cellSize + offset.Y + nodeCenter.Y
                );
                Vector2 toPos = new Vector2(
                    toNode.GridPosition.X * cellSize + offset.X + nodeCenter.X,
                    toNode.GridPosition.Y * cellSize + offset.Y + nodeCenter.Y
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
            if (_draggingCard == null) return;
            
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
