using System;
using Godot;
using OdysseyCards.Domain.Combat.Engine;
using OdysseyCards.Map;

namespace OdysseyCards.UI
{
    public partial class MapNodeUI : Control
    {
        private MapNode _node;
        private Label _distanceLabel;
        private Panel _panel;
        private Button _button;
        private bool _isDragHovering;
        private bool _isDropValid;
        private float _nodeSize = 60f;
        private UnitSnapshot _unitOnNode;
        private Label _unitLabel;
        private Label _unitHealthLabel;

        public MapNode MapNodeData => _node;
        public bool CanDropOnNode { get; set; } = true;

        public event Action<CardDroppedEventArgs> OnCardDropped;

        public static readonly Color PlayerColor = new Color(0.2f, 0.6f, 0.2f);
        public static readonly Color EnemyColor = new Color(0.6f, 0.2f, 0.2f);
        public static readonly Color NeutralColor = new Color(0.3f, 0.3f, 0.3f);
        public static readonly Color DeploymentColor = new Color(0.8f, 0.6f, 0.2f);
        public static readonly Color ValidDropColor = new Color(0.2f, 0.8f, 0.4f);
        public static readonly Color InvalidDropColor = new Color(0.8f, 0.2f, 0.2f);
        public static readonly Color PlayerUnitColor = new Color(0.3f, 0.8f, 0.3f);
        public static readonly Color EnemyUnitColor = new Color(0.8f, 0.3f, 0.3f);

        public MapNodeUI()
        {
            CustomMinimumSize = new Vector2(_nodeSize, _nodeSize);
            MouseFilter = MouseFilterEnum.Stop;

            _panel = new Panel();
            _panel.CustomMinimumSize = new Vector2(_nodeSize - 10, _nodeSize - 10);
            _panel.Position = new Vector2(5, 5);
            AddChild(_panel);

            var styleBox = new StyleBoxFlat();
            styleBox.BgColor = NeutralColor;
            styleBox.BorderColor = Colors.White;
            styleBox.SetBorderWidthAll(2);
            styleBox.SetCornerRadiusAll(8);
            _panel.AddThemeStyleboxOverride("panel", styleBox);

            _distanceLabel = new Label();
            _distanceLabel.Position = new Vector2(8, 8);
            _distanceLabel.AddThemeColorOverride("font_color", Colors.White);
            _distanceLabel.AddThemeFontSizeOverride("font_size", (int)(_nodeSize * 0.12f));
            AddChild(_distanceLabel);

            _unitLabel = new Label();
            _unitLabel.Position = new Vector2(5, _nodeSize * 0.35f);
            _unitLabel.AddThemeColorOverride("font_color", Colors.White);
            _unitLabel.AddThemeFontSizeOverride("font_size", (int)(_nodeSize * 0.15f));
            _unitLabel.HorizontalAlignment = HorizontalAlignment.Center;
            AddChild(_unitLabel);

            _unitHealthLabel = new Label();
            _unitHealthLabel.Position = new Vector2(5, _nodeSize * 0.55f);
            _unitHealthLabel.AddThemeColorOverride("font_color", Colors.Yellow);
            _unitHealthLabel.AddThemeFontSizeOverride("font_size", (int)(_nodeSize * 0.12f));
            _unitHealthLabel.HorizontalAlignment = HorizontalAlignment.Center;
            AddChild(_unitHealthLabel);

            _button = new Button();
            _button.CustomMinimumSize = new Vector2(_nodeSize - 10, _nodeSize - 10);
            _button.Position = new Vector2(5, 5);
            _button.Modulate = new Color(1, 1, 1, 0);
            AddChild(_button);
        }

        public override void _Ready()
        {
        }

        public void SetSize(float size)
        {
            _nodeSize = size;
            CustomMinimumSize = new Vector2(size, size);
            _panel.CustomMinimumSize = new Vector2(size - 10, size - 10);
            _panel.Position = new Vector2(5, 5);
            _button.CustomMinimumSize = new Vector2(size - 10, size - 10);
            _button.Position = new Vector2(5, 5);
            _distanceLabel.AddThemeFontSizeOverride("font_size", (int)(size * 0.12f));
            _unitLabel.AddThemeFontSizeOverride("font_size", (int)(size * 0.15f));
            _unitLabel.Position = new Vector2(5, size * 0.35f);
            _unitHealthLabel.AddThemeFontSizeOverride("font_size", (int)(size * 0.12f));
            _unitHealthLabel.Position = new Vector2(5, size * 0.55f);
        }

        public void SetUnit(UnitSnapshot unit)
        {
            _unitOnNode = unit;
            UpdateUnitVisuals();
        }

        private void UpdateUnitVisuals()
        {
            if (_unitLabel == null || _unitHealthLabel == null)
                return;

            if (_unitOnNode != null)
            {
                _unitLabel.Text = _unitOnNode.Name.Replace("Unit_", "U");
                _unitHealthLabel.Text = $"❤{_unitOnNode.CurrentHealth}/{_unitOnNode.MaxHealth}";
                _unitLabel.Visible = true;
                _unitHealthLabel.Visible = true;

                var styleBox = _panel.GetThemeStylebox("panel") as StyleBoxFlat;
                if (styleBox != null)
                {
                    styleBox.BgColor = _unitOnNode.OwnerId == 1 ? PlayerUnitColor : EnemyUnitColor;
                    styleBox.SetBorderWidthAll(3);
                }
            }
            else
            {
                _unitLabel.Visible = false;
                _unitHealthLabel.Visible = false;
            }
        }

        public override bool _CanDropData(Vector2 atPosition, Variant data)
        {
            if (!CanDropOnNode || _node == null)
                return false;

            if (data.VariantType != Variant.Type.Dictionary)
                return false;

            var dict = data.AsGodotDictionary();
            if (!dict.ContainsKey("card_id"))
                return false;

            return true;
        }

        public override void _DropData(Vector2 atPosition, Variant data)
        {
            if (data.VariantType != Variant.Type.Dictionary)
                return;

            var dict = data.AsGodotDictionary();
            if (!dict.ContainsKey("card_id"))
                return;

            var args = new CardDroppedEventArgs
            {
                CardId = dict["card_id"].AsString(),
                SourcePath = dict.ContainsKey("source_path") ? dict["source_path"].AsString() : "",
                TargetNode = _node,
                DropPosition = atPosition
            };

            OnCardDropped?.Invoke(args);
            SetDropHighlight(false, true);
        }

        public override void _Notification(int what)
        {
            if (what == NotificationDragBegin)
            {
                _isDragHovering = false;
            }
            else if (what == NotificationDragEnd)
            {
                if (_isDragHovering)
                {
                    SetDropHighlight(false, true);
                    _isDragHovering = false;
                }
            }
        }

        public override void _Input(InputEvent @event)
        {
            if (!CanDropOnNode)
                return;

            if (@event is InputEventMouseMotion mouseMotion)
            {
                bool isCurrentlyDragging = Input.IsMouseButtonPressed(MouseButton.Left);

                if (isCurrentlyDragging)
                {
                    bool isInside = GetRect().HasPoint(mouseMotion.Position - GlobalPosition);

                    if (isInside && !_isDragHovering)
                    {
                        _isDragHovering = true;
                        bool canDrop = _CanDropData(mouseMotion.Position - GlobalPosition, new Variant());
                        SetDropHighlight(true, canDrop);
                    }
                    else if (!isInside && _isDragHovering)
                    {
                        _isDragHovering = false;
                        SetDropHighlight(false, true);
                    }
                }
            }
        }

        public void SetNode(MapNode node)
        {
            _node = node;
            AddToGroup("MapNode");
            SetMeta("NodeId", node.Id);
            UpdateVisuals();
        }

        public void UpdateVisuals()
        {
            if (_node == null)
                return;

            var styleBox = _panel.GetThemeStylebox("panel") as StyleBoxFlat;
            if (styleBox == null)
                return;

            if (_node.IsPlayerDeploymentPoint)
            {
                styleBox.BgColor = PlayerColor;
                styleBox.SetBorderWidthAll(3);
            }
            else if (_node.IsEnemyDeploymentPoint)
            {
                styleBox.BgColor = EnemyColor;
                styleBox.SetBorderWidthAll(3);
            }
            else
            {
                styleBox.BgColor = NeutralColor;
                styleBox.SetBorderWidthAll(2);
            }

            _distanceLabel.Text = $"P{_node.DistanceToPlayerHQ}\nE{_node.DistanceToEnemyHQ}";
        }

        public void SetHighlight(bool highlight, Color? color = null)
        {
            if (_isDragHovering)
                return;

            var styleBox = _panel.GetThemeStylebox("panel") as StyleBoxFlat;
            if (styleBox == null)
                return;

            if (highlight)
            {
                styleBox.BorderColor = color ?? Colors.Yellow;
                styleBox.SetBorderWidthAll(3);
            }
            else
            {
                styleBox.BorderColor = Colors.White;
                styleBox.SetBorderWidthAll(2);
            }
        }

        public void SetDropHighlight(bool highlight, bool isValid)
        {
            var styleBox = _panel.GetThemeStylebox("panel") as StyleBoxFlat;
            if (styleBox == null)
                return;

            if (highlight)
            {
                styleBox.BorderColor = isValid ? ValidDropColor : InvalidDropColor;
                styleBox.SetBorderWidthAll(4);
            }
            else
            {
                styleBox.BorderColor = Colors.White;
                styleBox.SetBorderWidthAll(2);
            }
        }

        public void ConnectButtonPressed(Callable callable)
        {
            _button.Pressed += () => callable.Call();
        }
    }

    public class CardDroppedEventArgs : EventArgs
    {
        public string CardId { get; set; }
        public string SourcePath { get; set; }
        public MapNode TargetNode { get; set; }
        public Vector2 DropPosition { get; set; }
    }
}
