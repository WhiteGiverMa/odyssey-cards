using System;
using Godot;

namespace OdysseyCards.UI
{
    public partial class UIScaler : Node
    {
        public static UIScaler Instance { get; private set; }

        private const float _designWidth = 1152f;
        private const float _designHeight = 648f;
        private const float _cardWidthRatio = 180f / 260f;
        private Vector2 _currentCardSize = new(180, 260);

        public event Action OnResolutionChanged;

        public float CurrentScale { get; private set; } = 1f;
        public Vector2 CurrentCardSize => _currentCardSize;

        public override void _Ready()
        {
            Instance = this;
            GetTree().Root.SizeChanged += OnWindowSizeChanged;
            UpdateScale();
        }

        private void OnWindowSizeChanged()
        {
            UpdateScale();
        }

        private void UpdateScale()
        {
            Vector2 viewportSize = GetViewport().GetVisibleRect().Size;
            float widthRatio = viewportSize.X / _designWidth;
            float heightRatio = viewportSize.Y / _designHeight;
            CurrentScale = Mathf.Min(widthRatio, heightRatio);
            _currentCardSize = GetCardSize();
            OnResolutionChanged?.Invoke();
        }

        public float GetScaleFactor()
        {
            return CurrentScale;
        }

        public int GetScaledFontSize(int baseSize)
        {
            return Mathf.RoundToInt(baseSize * CurrentScale);
        }

        public Vector2 GetScaledSize(Vector2 baseSize)
        {
            return baseSize * CurrentScale;
        }

        public Vector2 GetCardSize()
        {
            Vector2 viewportSize = GetViewport().GetVisibleRect().Size;
            float cardHeight = viewportSize.Y * 0.35f;
            float cardWidth = cardHeight * _cardWidthRatio;
            return new Vector2(cardWidth, cardHeight);
        }

        public float GetNodeSize(float containerHeight)
        {
            return containerHeight / 8f;
        }
    }
}
