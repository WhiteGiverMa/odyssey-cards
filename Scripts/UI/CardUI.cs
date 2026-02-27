using System;
using Godot;

namespace OdysseyCards.UI;

public partial class CardUI : Control
{
    private Card.Card _card;
    private bool _isHovered = false;
    private bool _isSelected = false;
    private bool _isDragging = false;
    private Vector2 _dragStartPosition;
    private Vector2 _originalPosition;
    private Control _dragPreview;

    private ColorRect _background;
    private Label _nameLabel;
    private Label _costLabel;
    private Label _descLabel;
    private Label _typeLabel;

    public event Action<CardUI> OnCardSelected;
    public event Action<CardUI> OnCardDeselected;
    public event Action<CardUI, Character.Character> OnCardDraggedToTarget;

    public bool IsSelected => _isSelected;
    public Card.Card Card => _card;

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(180, 260);
        MouseFilter = MouseFilterEnum.Stop;
        MouseEntered += OnMouseEntered;
        MouseExited += OnMouseExited;

        _background = new ColorRect
        {
            Color = new Color(0.2f, 0.2f, 0.25f),
            SizeFlagsHorizontal = SizeFlags.Expand | SizeFlags.Fill,
            SizeFlagsVertical = SizeFlags.Expand | SizeFlags.Fill,
            MouseFilter = MouseFilterEnum.Ignore
        };
        _background.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_background);

        _costLabel = new Label
        {
            Name = "CostLabel",
            Text = "0",
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            MouseFilter = MouseFilterEnum.Ignore
        };
        _costLabel.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _costLabel.OffsetLeft = 8;
        _costLabel.OffsetTop = 8;
        _costLabel.OffsetRight = -8;
        _costLabel.OffsetBottom = 40;
        AddChild(_costLabel);

        _nameLabel = new Label
        {
            Name = "NameLabel",
            Text = "Card Name",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            MouseFilter = MouseFilterEnum.Ignore
        };
        _nameLabel.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _nameLabel.OffsetLeft = 8;
        _nameLabel.OffsetTop = 45;
        _nameLabel.OffsetRight = -8;
        _nameLabel.OffsetBottom = 80;
        AddChild(_nameLabel);

        _typeLabel = new Label
        {
            Name = "TypeLabel",
            Text = "ATTACK",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            MouseFilter = MouseFilterEnum.Ignore
        };
        _typeLabel.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _typeLabel.OffsetLeft = 8;
        _typeLabel.OffsetTop = 85;
        _typeLabel.OffsetRight = -8;
        _typeLabel.OffsetBottom = 110;
        AddChild(_typeLabel);

        var centerBg = new ColorRect
        {
            Color = new Color(0.25f, 0.25f, 0.3f),
            MouseFilter = MouseFilterEnum.Ignore
        };
        centerBg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        centerBg.OffsetLeft = 10;
        centerBg.OffsetTop = 120;
        centerBg.OffsetRight = -10;
        centerBg.OffsetBottom = -10;
        AddChild(centerBg);

        _descLabel = new Label
        {
            Name = "DescLabel",
            Text = "Card description",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore
        };
        _descLabel.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _descLabel.OffsetLeft = 10;
        _descLabel.OffsetTop = 10;
        _descLabel.OffsetRight = -10;
        _descLabel.OffsetBottom = -10;
        centerBg.AddChild(_descLabel);
    }

    public void ResetDragState()
    {
        _dragStartPosition = Vector2.Zero;
        _isDragging = false;
    }

    public void SetCard(Card.Card card)
    {
        _card = card;
        UpdateDisplay();
    }

    public void SetSelected(bool selected)
    {
        _isSelected = selected;
        UpdateSelectionVisual();
    }

    private void UpdateSelectionVisual()
    {
        if (_background != null)
        {
            if (_isSelected)
            {
                _background.Color = new Color(1f, 0.9f, 0.3f);
            }
            else
            {
                UpdateDisplay();
            }
        }
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
                    _dragStartPosition = mouseEvent.Position;
                    _isDragging = false;
                }
                else
                {
                    if (_isDragging)
                    {
                        HandleDragEnd(mouseEvent.Position);
                    }
                    else
                    {
                        HandleClick();
                    }
                    _isDragging = false;
                }
                AcceptEvent();
            }
        }
        else if (@event is InputEventMouseMotion motionEvent)
        {
            if (_dragStartPosition != Vector2.Zero)
            {
                var dragDistance = (motionEvent.Position - _dragStartPosition).Length();
                if (dragDistance > 10 && !_isDragging)
                {
                    _isDragging = true;
                    _originalPosition = GlobalPosition;
                    StartDragPreview();
                }
                
                if (_isDragging)
                {
                    GlobalPosition = motionEvent.Position - GetRect().Size / 2;
                }
            }
        }
    }

    private void HandleClick()
    {
        GD.Print($"[CardUI] Clicked card: {_card?.Data.CardName}, was selected: {_isSelected}");
        
        if (_isSelected)
        {
            _isSelected = false;
            UpdateSelectionVisual();
            OnCardDeselected?.Invoke(this);
        }
        else
        {
            _isSelected = true;
            UpdateSelectionVisual();
            OnCardSelected?.Invoke(this);
        }
    }

    private void StartDragPreview()
    {
        GD.Print("[CardUI] Started dragging");
    }

    private void HandleDragEnd(Vector2 endPosition)
    {
        GD.Print($"[CardUI] Drag ended at: {endPosition}");
        
        GlobalPosition = _originalPosition;
        
        var enemies = GetTree().GetNodesInGroup("Enemy");
        foreach (var node in enemies)
        {
            if (node is VBoxContainer container)
            {
                var rect = container.GetGlobalRect();
                if (rect.HasPoint(endPosition))
                {
                    if (_card != null && _card.Data.Target == Core.CardTarget.SingleEnemy)
                    {
                        var meta = container.GetMeta("EnemyObject");
                        if (meta.VariantType != Variant.Type.Nil)
                        {
                            var enemy = meta.As<Character.Enemy>();
                            GD.Print($"[CardUI] Dropped on enemy: {enemy.CharacterName}");
                            OnCardDraggedToTarget?.Invoke(this, enemy);
                            return;
                        }
                    }
                }
            }
        }
        
        GD.Print("[CardUI] Dropped on invalid target");
    }

    public void OnMouseEntered()
    {
        _isHovered = true;
        if (_background != null && !_isSelected)
        {
            var cardColor = _card?.Data.Type switch
            {
                Core.CardType.Attack => new Color(0.95f, 0.35f, 0.35f),
                Core.CardType.Skill => new Color(0.35f, 0.55f, 0.95f),
                Core.CardType.Power => new Color(0.6f, 0.35f, 0.8f),
                _ => new Color(0.5f, 0.5f, 0.55f)
            };
            _background.Color = cardColor;
        }
    }

    public void OnMouseExited()
    {
        _isHovered = false;
        if (_background != null && !_isSelected)
        {
            UpdateDisplay();
        }
    }
}
