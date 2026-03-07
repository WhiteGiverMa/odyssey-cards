using Godot;

namespace OdysseyCards.UI
{
    public partial class PlayArea : Control
    {
        private ColorRect _highlightRect;
        private bool _isHighlightActive;
        private bool _isDraggingNoTargetCard;

        private static readonly Color HighlightColor = new Color(0.3f, 0.6f, 0.9f, 0.3f);
        private static readonly Color BorderColor = new Color(0.4f, 0.7f, 1.0f, 0.8f);

        public override void _Ready()
        {
            MouseFilter = MouseFilterEnum.Ignore;
            SetAnchorsPreset(LayoutPreset.FullRect);

            CreateHighlightRect();

            AddToGroup("PlayArea");
            GD.Print("[PlayArea] Ready, added to PlayArea group");
        }

        private void CreateHighlightRect()
        {
            _highlightRect = new ColorRect
            {
                Name = "HighlightRect",
                MouseFilter = MouseFilterEnum.Ignore,
                Color = HighlightColor,
                Visible = false
            };
            _highlightRect.SetAnchorsPreset(LayoutPreset.FullRect);
            AddChild(_highlightRect);
        }

        public void SetHighlightActive(bool active)
        {
            if (_isHighlightActive == active)
            {
                return;
            }

            _isHighlightActive = active;
            _highlightRect.Visible = active;

            if (active)
            {
                _highlightRect.Color = HighlightColor;
                GD.Print("[PlayArea] Highlight activated");
            }
            else
            {
                GD.Print("[PlayArea] Highlight deactivated");
            }
        }

        public void SetDraggingNoTargetCard(bool isDragging)
        {
            _isDraggingNoTargetCard = isDragging;
            SetHighlightActive(isDragging);
        }

        public bool ContainsPoint(Vector2 globalPos)
        {
            Rect2 globalRect = GetGlobalRect();
            return globalRect.HasPoint(globalPos);
        }

        public override void _Draw()
        {
            if (_isHighlightActive)
            {
                DrawRect(new Rect2(Vector2.Zero, Size), HighlightColor, true);
                DrawRect(new Rect2(Vector2.Zero, Size), BorderColor, false, 3f);
            }
        }

        public override void _Process(double delta)
        {
            if (_isHighlightActive)
            {
                QueueRedraw();
            }
        }
    }
}
