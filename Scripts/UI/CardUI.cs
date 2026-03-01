using System;
using Godot;
using OdysseyCards.Core;

namespace OdysseyCards.UI;

/// <summary>
/// UI component for displaying and interacting with cards in the player's hand.
/// Handles selection, dragging, and dropping cards onto targets.
/// </summary>
public partial class CardUI : Control
{
    private const float DragThreshold = 10f;
    private const float DragAlpha = 0.7f;

    private Card.Card _card;
    private bool _isHovered;
    private bool _isSelected;
    private bool _isDragging;
    private bool _isDragActive;
    private Vector2 _dragStartPosition;
    private Vector2 _originalPosition;
    private Vector2 _dragOffset;
    private Tween _returnTween;

    private ColorRect _background;
    private Label _nameLabel;
    private Label _costLabel;
    private Label _descLabel;
    private Label _typeLabel;
    private ColorRect _selectionBorder;

    /// <summary>
    /// Fired when this card is selected.
    /// </summary>
    public event Action<CardUI> OnCardSelected;

    /// <summary>
    /// Fired when this card is deselected.
    /// </summary>
    public event Action<CardUI> OnCardDeselected;

    /// <summary>
    /// Fired when a card is dragged to a target character.
    /// </summary>
    public event Action<CardUI, Character.Character> OnCardDraggedToTarget;

    /// <summary>
    /// Fired when dragging starts.
    /// </summary>
    public event Action<CardUI, Vector2> OnDragStarted;

    /// <summary>
    /// Fired when dragging ends.
    /// </summary>
    public event Action<CardUI, Vector2> OnDragEnded;

    /// <summary>
    /// Fired when a card is dropped on a map node.
    /// </summary>
    public event Action<CardUI, int> OnDroppedOnNode;

    /// <summary>
    /// Fired when card needs to return to hand.
    /// </summary>
    public event Action<CardUI> OnReturnToHandRequested;

    /// <summary>
    /// Whether this card is currently being dragged.
    /// </summary>
    public bool IsDragging => _isDragging;

    /// <summary>
    /// Whether this card is selected.
    /// </summary>
    public bool IsSelected => _isSelected;

    /// <summary>
    /// The original position before dragging.
    /// </summary>
    public Vector2 OriginalPosition => _originalPosition;

    /// <summary>
    /// The card data this UI represents.
    /// </summary>
    public Card.Card Card => _card;

    public override void _Ready()
    {
        ApplyScaledSize();
        MouseFilter = MouseFilterEnum.Stop;
        MouseEntered += OnMouseEntered;
        MouseExited += OnMouseExited;

        if (UIScaler.Instance != null)
        {
            UIScaler.Instance.OnResolutionChanged += ApplyScaledSize;
        }

        CreateSelectionBorder();
        CreateBackground();
        CreateLabels();
    }

    private void ApplyScaledSize()
    {
        if (UIScaler.Instance != null)
        {
            CustomMinimumSize = UIScaler.Instance.GetCardSize();
        }
        else
        {
            CustomMinimumSize = new Vector2(180, 260);
        }
    }

    private void CreateSelectionBorder()
    {
        _selectionBorder = new ColorRect
        {
            Color = new Color(1f, 0.9f, 0.3f, 0f),
            SizeFlagsHorizontal = SizeFlags.Expand | SizeFlags.Fill,
            SizeFlagsVertical = SizeFlags.Expand | SizeFlags.Fill,
            MouseFilter = MouseFilterEnum.Ignore,
            ZIndex = 10
        };
        _selectionBorder.SetAnchorsPreset(LayoutPreset.FullRect);
        _selectionBorder.OffsetLeft = -4;
        _selectionBorder.OffsetTop = -4;
        _selectionBorder.OffsetRight = 4;
        _selectionBorder.OffsetBottom = 4;
        AddChild(_selectionBorder);
    }

    private void CreateBackground()
    {
        _background = new ColorRect
        {
            Color = new Color(0.2f, 0.2f, 0.25f),
            SizeFlagsHorizontal = SizeFlags.Expand | SizeFlags.Fill,
            SizeFlagsVertical = SizeFlags.Expand | SizeFlags.Fill,
            MouseFilter = MouseFilterEnum.Ignore
        };
        _background.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(_background);
    }

    private void CreateLabels()
    {
        _costLabel = new Label
        {
            Name = "CostLabel",
            Text = "0",
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            MouseFilter = MouseFilterEnum.Ignore
        };
        _costLabel.SetAnchorsPreset(LayoutPreset.FullRect);
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
        _nameLabel.SetAnchorsPreset(LayoutPreset.FullRect);
        _nameLabel.OffsetLeft = 8;
        _nameLabel.OffsetTop = 45;
        _nameLabel.OffsetRight = -8;
        _nameLabel.OffsetBottom = 80;
        AddChild(_nameLabel);

        _typeLabel = new Label
        {
            Name = "TypeLabel",
            Text = "UNIT",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            MouseFilter = MouseFilterEnum.Ignore
        };
        _typeLabel.SetAnchorsPreset(LayoutPreset.FullRect);
        _typeLabel.OffsetLeft = 8;
        _typeLabel.OffsetTop = 85;
        _typeLabel.OffsetRight = -8;
        _typeLabel.OffsetBottom = 110;
        AddChild(_typeLabel);

        ColorRect centerBg = new()
        {
            Color = new Color(0.25f, 0.25f, 0.3f),
            MouseFilter = MouseFilterEnum.Ignore
        };
        centerBg.SetAnchorsPreset(LayoutPreset.FullRect);
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
        _descLabel.SetAnchorsPreset(LayoutPreset.FullRect);
        _descLabel.OffsetLeft = 10;
        _descLabel.OffsetTop = 10;
        _descLabel.OffsetRight = -10;
        _descLabel.OffsetBottom = -10;
        centerBg.AddChild(_descLabel);
    }

    /// <summary>
    /// Sets the card data to display.
    /// </summary>
    /// <param name="card">The card to display.</param>
    public void SetCard(Card.Card card)
    {
        _card = card;
        UpdateDisplay();
    }

    /// <summary>
    /// Sets the selection state of this card.
    /// </summary>
    /// <param name="selected">Whether the card is selected.</param>
    public void SetSelected(bool selected)
    {
        _isSelected = selected;
        UpdateSelectionVisual();
    }

    /// <summary>
    /// Resets the drag state.
    /// </summary>
    public void ResetDragState()
    {
        _dragStartPosition = Vector2.Zero;
        _isDragging = false;
        _isDragActive = false;
    }

    /// <summary>
    /// Starts dragging this card.
    /// </summary>
    public void StartDrag()
    {
        if (_isDragging)
        {
            return;
        }

        _isDragging = true;
        _isDragActive = true;
        _originalPosition = GlobalPosition;
        _dragOffset = GetGlobalMousePosition() - GlobalPosition;

        Modulate = new Color(1f, 1f, 1f, DragAlpha);
        ZIndex = 100;
        MoveToFront();

        OnDragStarted?.Invoke(this, GlobalPosition);
        GD.Print($"[CardUI] StartDrag: {_card?.CardName}");
    }

    /// <summary>
    /// Ends dragging this card.
    /// </summary>
    /// <param name="success">Whether the drop was successful.</param>
    public void EndDrag(bool success)
    {
        if (!_isDragging)
        {
            return;
        }

        _isDragging = false;
        _isDragActive = false;
        ZIndex = 0;

        Vector2 endPosition = GlobalPosition;
        OnDragEnded?.Invoke(this, endPosition);

        if (!success)
        {
            ReturnToOriginalPosition();
        }

        GD.Print($"[CardUI] EndDrag: success={success}");
    }

    /// <summary>
    /// Returns the card to its original position with animation.
    /// </summary>
    public void ReturnToOriginalPosition()
    {
        OnReturnToHandRequested?.Invoke(this);

        Modulate = new Color(1f, 1f, 1f, 1f);
        ZIndex = 0;

        GD.Print($"[CardUI] ReturnToOriginalPosition: {_originalPosition}");
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseEvent)
        {
            HandleMouseButtonInput(mouseEvent);
        }
        else if (@event is InputEventMouseMotion motionEvent)
        {
            HandleMouseMotionInput(motionEvent);
        }
    }

    private void HandleMouseButtonInput(InputEventMouseButton mouseEvent)
    {
        if (mouseEvent.ButtonIndex == MouseButton.Right && !mouseEvent.Pressed)
        {
            HandleRightClick();
            AcceptEvent();
            return;
        }

        if (mouseEvent.ButtonIndex == MouseButton.Left)
        {
            if (mouseEvent.Pressed)
            {
                HandleLeftPress(mouseEvent.Position);
            }
            else
            {
                HandleLeftRelease();
            }
            AcceptEvent();
        }
    }

    private void HandleRightClick()
    {
        if (_isSelected)
        {
            _isSelected = false;
            _isDragging = false;
            _isDragActive = false;
            _dragStartPosition = Vector2.Zero;
            UpdateSelectionVisual();
            OnCardDeselected?.Invoke(this);
            GD.Print("[CardUI] Right click - deselected");
        }
    }

    private void HandleLeftPress(Vector2 position)
    {
        _dragStartPosition = position;
        _isDragActive = false;

        if (_returnTween != null && _returnTween.IsValid())
        {
            _returnTween.Kill();
            Modulate = new Color(1f, 1f, 1f, 1f);
        }
    }

    private void HandleLeftRelease()
    {
        if (_isDragging)
        {
            ProcessDragEnd();
        }
        else
        {
            HandleClick();
        }

        _isDragActive = false;
        _dragStartPosition = Vector2.Zero;
    }

    private void HandleMouseMotionInput(InputEventMouseMotion motionEvent)
    {
        if (_dragStartPosition == Vector2.Zero)
        {
            return;
        }

        float dragDistance = (motionEvent.Position - _dragStartPosition).Length();

        if (dragDistance > DragThreshold && !_isDragging)
        {
            StartDrag();
        }

        if (_isDragging && _isDragActive)
        {
            UpdateDragPosition();
        }
    }

    private void UpdateDragPosition()
    {
        Vector2 globalMouse = GetGlobalMousePosition();
        GlobalPosition = globalMouse - GetRect().Size / 2;
    }

    private void HandleClick()
    {
        GD.Print($"[CardUI] Clicked card: {_card?.CardName}, was selected: {_isSelected}");

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

    private void ProcessDragEnd()
    {
        Vector2 globalMousePos = GetGlobalMousePosition();

        int nodeId = DetectNodeTarget(globalMousePos);
        if (nodeId >= 0)
        {
            GD.Print($"[CardUI] Dropped on node: {nodeId}");
            OnDroppedOnNode?.Invoke(this, nodeId);
            EndDrag(true);
            return;
        }

        Character.Character enemy = DetectEnemyTarget(globalMousePos);
        if (enemy != null)
        {
            GD.Print($"[CardUI] Dropped on enemy: {enemy.CharacterName}");
            OnCardDraggedToTarget?.Invoke(this, enemy);
            EndDrag(true);
            return;
        }

        int hqNodeId = DetectHeadquartersTarget(globalMousePos);
        if (hqNodeId >= 0)
        {
            GD.Print($"[CardUI] Dropped on headquarters node: {hqNodeId}");
            OnDroppedOnNode?.Invoke(this, hqNodeId);
            EndDrag(true);
            return;
        }

        GD.Print("[CardUI] Dropped on invalid target, returning to original position");
        EndDrag(false);
    }

    private int DetectNodeTarget(Vector2 globalPos)
    {
        Godot.Collections.Array<Node> nodes = GetTree().GetNodesInGroup("MapNode");
        foreach (Node node in nodes)
        {
            if (node is Control nodeControl)
            {
                Rect2 globalRect = nodeControl.GetGlobalRect();
                if (globalRect.HasPoint(globalPos))
                {
                    if (nodeControl.HasMeta("NodeId"))
                    {
                        return nodeControl.GetMeta("NodeId").AsInt32();
                    }
                }
            }
        }
        return -1;
    }

    private Character.Character DetectEnemyTarget(Vector2 globalPos)
    {
        if (_card is not Card.Order order)
        {
            return null;
        }

        if (order.Target != CardTarget.SingleEnemy)
        {
            return null;
        }

        Godot.Collections.Array<Node> enemies = GetTree().GetNodesInGroup("Enemy");
        foreach (Node node in enemies)
        {
            if (node is Control container)
            {
                Rect2 globalRect = container.GetGlobalRect();
                if (globalRect.HasPoint(globalPos))
                {
                    if (container.HasMeta("EnemyObject"))
                    {
                        Character.Character enemy = container.GetMeta("EnemyObject").As<Character.Character>();
                        return enemy;
                    }
                }
            }
        }
        return null;
    }

    private int DetectHeadquartersTarget(Vector2 globalPos)
    {
        Godot.Collections.Array<Node> hqNodes = GetTree().GetNodesInGroup("Headquarters");
        foreach (Node node in hqNodes)
        {
            if (node is Control hqControl)
            {
                Rect2 globalRect = hqControl.GetGlobalRect();
                if (globalRect.HasPoint(globalPos))
                {
                    if (hqControl.HasMeta("NodeId"))
                    {
                        return hqControl.GetMeta("NodeId").AsInt32();
                    }
                }
            }
        }
        return -1;
    }

    private void UpdateSelectionVisual()
    {
        if (_selectionBorder != null)
        {
            _selectionBorder.Color = _isSelected
                ? new Color(1f, 0.9f, 0.3f, 1f)
                : new Color(1f, 0.9f, 0.3f, 0f);
        }

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
        {
            return;
        }

        if (_nameLabel != null)
        {
            _nameLabel.Text = _card.CardName;
        }

        if (_costLabel != null)
        {
            _costLabel.Text = _card switch
            {
                Card.Unit unit => $"{unit.DeployCost}K",
                Card.Order order => $"{order.Cost}K",
                _ => "0K"
            };
        }

        if (_descLabel != null)
        {
            _descLabel.Text = _card.Description;
        }

        if (_typeLabel != null)
        {
            _typeLabel.Text = _card.Type.ToString().ToUpper();
        }

        if (_background != null)
        {
            _background.Color = _card.Type switch
            {
                CardType.Unit => new Color(0.25f, 0.55f, 0.35f),
                CardType.Order => new Color(0.55f, 0.35f, 0.25f),
                _ => new Color(0.4f, 0.4f, 0.45f)
            };
        }
    }

    /// <summary>
    /// Called when the mouse enters this card.
    /// </summary>
    public void OnMouseEntered()
    {
        _isHovered = true;
        if (_background != null && !_isSelected)
        {
            _background.Color = _card?.Type switch
            {
                CardType.Unit => new Color(0.35f, 0.65f, 0.45f),
                CardType.Order => new Color(0.65f, 0.45f, 0.35f),
                _ => new Color(0.5f, 0.5f, 0.55f)
            };
        }
    }

    /// <summary>
    /// Called when the mouse exits this card.
    /// </summary>
    public void OnMouseExited()
    {
        _isHovered = false;
        if (_background != null && !_isSelected)
        {
            UpdateDisplay();
        }
    }
}
