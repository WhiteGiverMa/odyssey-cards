using System;
using Godot;

namespace OdysseyCards.UI;

public partial class CardUI : Control
{
    private Card.Card _card;
    private bool _isHovered = false;
    private bool _isSelected = false;

    private ColorRect _background;
    private Label _nameLabel;
    private Label _costLabel;
    private Label _descLabel;
    private Label _typeLabel;

    public event Action<Card.Card> OnCardSelected;
    public event Action<Card.Card> OnCardPlayed;

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(180, 260);
        MouseEntered += OnMouseEntered;
        MouseExited += OnMouseExited;

        _background = new ColorRect
        {
            Color = new Color(0.2f, 0.2f, 0.25f),
            AnchorRight = 1f,
            AnchorBottom = 1f
        };
        AddChild(_background);

        var topPanel = new ColorRect
        {
            Color = new Color(0.15f, 0.15f, 0.2f),
            AnchorRight = 1f,
            OffsetBottom = -180f
        };
        AddChild(topPanel);

        var costLabelSettings = new LabelSettings
        {
            FontColor = new Color(0.9f, 0.8f, 0.2f),
            FontSize = 24
        };
        _costLabel = new Label
        {
            AnchorRight = 1f,
            OffsetLeft = 8f,
            OffsetTop = 8f,
            OffsetRight = -8f,
            OffsetBottom = 40f,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            LabelSettings = costLabelSettings
        };
        topPanel.AddChild(_costLabel);

        var nameLabelSettings = new LabelSettings
        {
            FontColor = new Color(1f, 1f, 1f),
            FontSize = 18
        };
        _nameLabel = new Label
        {
            AnchorRight = 1f,
            OffsetLeft = 8f,
            OffsetTop = 45f,
            OffsetRight = -8f,
            OffsetBottom = 80f,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            LabelSettings = nameLabelSettings
        };
        topPanel.AddChild(_nameLabel);

        var typeLabelSettings = new LabelSettings
        {
            FontColor = new Color(0.7f, 0.7f, 0.7f),
            FontSize = 12
        };
        _typeLabel = new Label
        {
            AnchorRight = 1f,
            OffsetLeft = 8f,
            OffsetTop = 85f,
            OffsetRight = -8f,
            OffsetBottom = 110f,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            LabelSettings = typeLabelSettings
        };
        topPanel.AddChild(_typeLabel);

        var centerPanel = new ColorRect
        {
            Color = new Color(0.3f, 0.3f, 0.35f),
            AnchorTop = 0.4f,
            AnchorRight = 1f,
            AnchorBottom = 0.7f,
            OffsetLeft = 10f,
            OffsetTop = -30f,
            OffsetRight = -10f,
            OffsetBottom = 30f
        };
        AddChild(centerPanel);

        var descLabelSettings = new LabelSettings
        {
            FontColor = new Color(0.9f, 0.9f, 0.9f),
            FontSize = 14
        };
        _descLabel = new Label
        {
            AnchorRight = 1f,
            AnchorBottom = 1f,
            OffsetLeft = 10f,
            OffsetTop = 10f,
            OffsetRight = -10f,
            OffsetBottom = -10f,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            LabelSettings = descLabelSettings
        };
        centerPanel.AddChild(_descLabel);
    }

    public void SetCard(Card.Card card)
    {
        _card = card;
        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        if (_card == null)
            return;

        if (_nameLabel != null)
            _nameLabel.Text = _card.Data.CardName;
        if (_costLabel != null)
            _costLabel.Text = $"⚡ {_card.CurrentCost}";
        if (_descLabel != null)
            _descLabel.Text = _card.Data.Description;
        if (_typeLabel != null)
            _typeLabel.Text = _card.Data.Type.ToString().ToUpper();

        var cardColor = _card.Data.Type switch
        {
            Core.CardType.Attack => new Color(0.85f, 0.25f, 0.25f),
            Core.CardType.Skill => new Color(0.25f, 0.45f, 0.85f),
            Core.CardType.Power => new Color(0.5f, 0.25f, 0.7f),
            _ => new Color(0.4f, 0.4f, 0.45f)
        };

        if (_background != null)
            _background.Color = cardColor;
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseEvent)
        {
            if (mouseEvent.ButtonIndex == MouseButton.Left)
            {
                if (mouseEvent.Pressed)
                {
                    _isSelected = !_isSelected;
                    OnCardSelected?.Invoke(_card);
                }
            }
        }
    }

    public void OnMouseEntered()
    {
        _isHovered = true;
    }

    public void OnMouseExited()
    {
        _isHovered = false;
    }
}
