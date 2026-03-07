using System;
using Godot;
using OdysseyCards.Core;

namespace OdysseyCards.UI
{
    public partial class CardUI : Control
    {
        private const float DragThreshold = 10f;
        private const float DragAlpha = 0.7f;
        private bool _isHovered;
        private bool _isDragActive;
        private Vector2 _dragStartPosition;
        private Vector2 _originalPosition;
        private Vector2 _dragOffset;
        private Tween _returnTween;

        private ColorRect _background;
        private ColorRect _headerBg;
        private Label _nameLabel;
        private Label _costLabel;
        private Label _descLabel;
        private Label _typeLabel;
        private Label _statsLabel;
        private Label _rarityLabel;
        private ColorRect _selectionBorder;
        private ColorRect _artworkPlaceholder;
        private TextureRect _artworkRect;
        private ColorRect _descBg;

        public event Action<CardUI> OnCardSelected;
        public event Action<CardUI> OnCardDeselected;
        public event Action<CardUI, Character.Character> OnCardDraggedToTarget;
        public event Action<CardUI, Vector2> OnDragStarted;
        public event Action<CardUI, Vector2> OnDragEnded;
        public event Action<CardUI, int> OnDroppedOnNode;
        public event Action<CardUI> OnReturnToHandRequested;
        public event Action<CardUI> OnPlayWithoutTarget;

        public bool IsDragging { get; private set; }
        public bool IsSelected { get; private set; }
        public Vector2 OriginalPosition => _originalPosition;
        public Card.Card Card { get; private set; }

        public override void _Ready()
        {
            GD.Print($"[CardUI] _Ready called, UIScaler.Instance is null: {UIScaler.Instance == null}");
            ApplyScaledSize();
            MouseFilter = MouseFilterEnum.Stop;
            MouseEntered += OnMouseEntered;
            MouseExited += OnMouseExited;
            Resized += OnResized;

            if (UIScaler.Instance != null)
            {
                UIScaler.Instance.OnResolutionChanged += ApplyScaledSize;
            }

            Localization.Localization.OnLanguageChanged += OnLanguageChanged;

            CreateAllElements();

            GD.Print($"[CardUI] Elements created, Size: {Size}, CustomMinimumSize: {CustomMinimumSize}");

            if (Card != null)
            {
                UpdateDisplay();
            }
        }

        private void CreateAllElements()
        {
            _selectionBorder = new ColorRect
            {
                Color = new Color(1f, 0.9f, 0.3f, 0f),
                MouseFilter = MouseFilterEnum.Ignore,
                ZIndex = 10
            };
            AddChild(_selectionBorder);

            _background = new ColorRect
            {
                Color = new Color(0.15f, 0.15f, 0.18f),
                MouseFilter = MouseFilterEnum.Ignore
            };
            AddChild(_background);

            _headerBg = new ColorRect
            {
                Color = new Color(0.2f, 0.2f, 0.25f),
                MouseFilter = MouseFilterEnum.Ignore
            };
            AddChild(_headerBg);

            _costLabel = new Label
            {
                Name = "CostLabel",
                Text = "0K",
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                MouseFilter = MouseFilterEnum.Ignore
            };
            _costLabel.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.2f));
            _costLabel.AddThemeFontSizeOverride("font_size", 18);
            AddChild(_costLabel);

            _nameLabel = new Label
            {
                Name = "NameLabel",
                Text = "Card Name",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                MouseFilter = MouseFilterEnum.Ignore
            };
            _nameLabel.AddThemeColorOverride("font_color", Colors.White);
            _nameLabel.AddThemeFontSizeOverride("font_size", 16);
            AddChild(_nameLabel);

            _artworkPlaceholder = new ColorRect
            {
                Color = new Color(0.25f, 0.25f, 0.3f),
                MouseFilter = MouseFilterEnum.Ignore
            };
            AddChild(_artworkPlaceholder);

            _artworkRect = new TextureRect
            {
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
                ClipContents = true,
                MouseFilter = MouseFilterEnum.Ignore
            };
            AddChild(_artworkRect);

            _typeLabel = new Label
            {
                Name = "TypeLabel",
                Text = "UNIT",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                MouseFilter = MouseFilterEnum.Ignore
            };
            _typeLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.75f));
            _typeLabel.AddThemeFontSizeOverride("font_size", 12);
            AddChild(_typeLabel);

            _statsLabel = new Label
            {
                Name = "StatsLabel",
                Text = "",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                MouseFilter = MouseFilterEnum.Ignore
            };
            _statsLabel.AddThemeColorOverride("font_color", Colors.White);
            _statsLabel.AddThemeFontSizeOverride("font_size", 14);
            AddChild(_statsLabel);

            _descBg = new ColorRect
            {
                Color = new Color(0.18f, 0.18f, 0.22f),
                MouseFilter = MouseFilterEnum.Ignore
            };
            AddChild(_descBg);

            _descLabel = new Label
            {
                Name = "DescLabel",
                Text = "Card description",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                MouseFilter = MouseFilterEnum.Ignore,
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
            _descLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.9f, 0.9f));
            _descLabel.AddThemeFontSizeOverride("font_size", 12);
            AddChild(_descLabel);

            _rarityLabel = new Label
            {
                Name = "RarityLabel",
                Text = "●",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                MouseFilter = MouseFilterEnum.Ignore
            };
            _rarityLabel.AddThemeFontSizeOverride("font_size", 10);
            AddChild(_rarityLabel);

            UpdateLayout();
        }

        private void OnResized()
        {
            UpdateLayout();
        }

        private void UpdateLayout()
        {
            float w = Size.X;
            float h = Size.Y;

            if (w <= 0 || h <= 0 || _selectionBorder == null)
            {
                return;
            }

            float margin = w * 0.05f;
            float headerHeight = h * 0.1f;
            float artworkTop = headerHeight + (margin * 0.2f);
            float artworkHeight = h * 0.43f;
            float typeTop = artworkTop + artworkHeight + (margin * 0.3f);
            float typeHeight = h * 0.06f;
            float statsTop = typeTop + typeHeight;
            float statsHeight = h * 0.06f;
            float descTop = statsTop + statsHeight + (margin * 0.2f);
            float descHeight = h * 0.23f;
            float rarityHeight = h * 0.05f;

            _selectionBorder.Position = new Vector2(-4, -4);
            _selectionBorder.Size = new Vector2(w + 8, h + 8);

            _background.Position = Vector2.Zero;
            _background.Size = new Vector2(w, h);

            _headerBg.Position = new Vector2(0, 0);
            _headerBg.Size = new Vector2(w, headerHeight);

            _costLabel.Position = new Vector2(margin, 0);
            _costLabel.Size = new Vector2(w * 0.25f, headerHeight);

            _nameLabel.Position = new Vector2(w * 0.3f, 0);
            _nameLabel.Size = new Vector2(w * 0.65f, headerHeight);

            _artworkPlaceholder.Position = new Vector2(margin, artworkTop);
            _artworkPlaceholder.Size = new Vector2(w - (margin * 2), artworkHeight);

            _artworkRect.Position = new Vector2(margin, artworkTop);
            _artworkRect.Size = new Vector2(w - (margin * 2), artworkHeight);

            _typeLabel.Position = new Vector2(margin, typeTop);
            _typeLabel.Size = new Vector2(w - (margin * 2), typeHeight);

            _statsLabel.Position = new Vector2(margin, statsTop);
            _statsLabel.Size = new Vector2(w - (margin * 2), statsHeight);

            _descBg.Position = new Vector2(margin, descTop);
            _descBg.Size = new Vector2(w - (margin * 2), descHeight);

            _descLabel.Position = new Vector2(margin + 4, descTop + 4);
            _descLabel.Size = new Vector2(w - (margin * 2) - 8, descHeight - rarityHeight - 8);

            _rarityLabel.Position = new Vector2(margin, descTop + descHeight - rarityHeight);
            _rarityLabel.Size = new Vector2(w - (margin * 2), rarityHeight);
        }

        private void ApplyScaledSize()
        {
            if (UIScaler.Instance != null)
            {
                CustomMinimumSize = UIScaler.Instance.GetCardSize();
                Size = UIScaler.Instance.GetCardSize();
            }
            else
            {
                CustomMinimumSize = new Vector2(180, 252);
                Size = new Vector2(180, 252);
            }
            UpdateLayout();
        }

        public void SetCard(Card.Card card)
        {
            Card = card;
            UpdateDisplay();
        }

        public void SetSelected(bool selected)
        {
            IsSelected = selected;
            UpdateSelectionVisual();
        }

        public void ResetDragState()
        {
            _dragStartPosition = Vector2.Zero;
            IsDragging = false;
            _isDragActive = false;
        }

        public void StartDrag()
        {
            if (IsDragging)
            {
                return;
            }

            IsDragging = true;
            _isDragActive = true;
            _originalPosition = GlobalPosition;
            _dragOffset = GetGlobalMousePosition() - GlobalPosition;

            Modulate = new Color(1f, 1f, 1f, DragAlpha);
            ZIndex = 100;
            MoveToFront();

            OnDragStarted?.Invoke(this, GlobalPosition);
            GD.Print($"[CardUI] StartDrag: {Card?.CardName}");
        }

        public void EndDrag(bool success)
        {
            if (!IsDragging)
            {
                return;
            }

            IsDragging = false;
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

        public void ReturnToOriginalPosition()
        {
            OnReturnToHandRequested?.Invoke(this);

            Modulate = new Color(1f, 1f, 1f, 1f);
            ZIndex = 0;
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

        public override void _Process(double delta)
        {
            if (IsDragging && _isDragActive)
            {
                UpdateDragPosition();
            }
        }

        public override void _Input(InputEvent @event)
        {
            if (@event is InputEventMouseButton mouseEvent && !mouseEvent.Pressed)
            {
                if (mouseEvent.ButtonIndex == MouseButton.Left && _isDragActive)
                {
                    HandleLeftRelease();
                }
            }
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (@event is InputEventKey keyEvent && keyEvent.Pressed && keyEvent.Keycode == Key.Escape)
            {
                if (IsDragging || IsSelected)
                {
                    CancelDragOrSelection();
                    GetViewport().SetInputAsHandled();
                }
            }
        }

        private void HandleMouseButtonInput(InputEventMouseButton mouseEvent)
        {
            if (mouseEvent.ButtonIndex == MouseButton.Right && !mouseEvent.Pressed)
            {
                HandleRightClick();
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
            }
        }

        private void HandleRightClick()
        {
            if (IsDragging || IsSelected)
            {
                CancelDragOrSelection();
                GD.Print("[CardUI] Right click - cancelled drag/selection");
            }
        }

        private void CancelDragOrSelection()
        {
            if (IsDragging)
            {
                EndDrag(false);
            }

            IsSelected = false;
            _isDragActive = false;
            _dragStartPosition = Vector2.Zero;
            UpdateSelectionVisual();
            OnCardDeselected?.Invoke(this);
        }

        private void HandleLeftPress(Vector2 position)
        {
            GD.Print($"[CardUI] HandleLeftPress: {Card?.CardName}, position: {position}");
            _dragStartPosition = position;
            _isDragActive = true;

            if (_returnTween?.IsValid() == true)
            {
                _returnTween.Kill();
                Modulate = new Color(1f, 1f, 1f, 1f);
            }
        }

        private void HandleLeftRelease()
        {
            GD.Print($"[CardUI] HandleLeftRelease: {Card?.CardName}, IsDragging: {IsDragging}, isDragActive: {_isDragActive}");

            if (IsDragging)
            {
                ProcessDragEnd();
            }
            else if (_isDragActive)
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

            if (dragDistance > DragThreshold && !IsDragging)
            {
                StartDrag();
            }

            if (IsDragging && _isDragActive)
            {
                UpdateDragPosition();
            }
        }

        private void UpdateDragPosition()
        {
            Vector2 globalMouse = GetGlobalMousePosition();
            GlobalPosition = globalMouse - (GetRect().Size / 2);
        }

        private void HandleClick()
        {
            GD.Print($"[CardUI] Clicked card: {Card?.CardName}, was selected: {IsSelected}");

            if (IsSelected)
            {
                IsSelected = false;
                UpdateSelectionVisual();
                OnCardDeselected?.Invoke(this);
            }
            else
            {
                IsSelected = true;
                UpdateSelectionVisual();
                OnCardSelected?.Invoke(this);
            }
        }

        private void ProcessDragEnd()
        {
            Vector2 globalMousePos = GetGlobalMousePosition();
            GD.Print($"[CardUI] ProcessDragEnd: {Card?.CardName}, mousePos: {globalMousePos}");

            int nodeId = DetectNodeTarget(globalMousePos);
            if (nodeId >= 0)
            {
                GD.Print($"[CardUI] Dropped on node: {nodeId}");
                OnDroppedOnNode?.Invoke(this, nodeId);
                EndDrag(true);
                return;
            }

            Character.Character targetChar = DetectCharacterTarget(globalMousePos);
            if (targetChar != null)
            {
                GD.Print($"[CardUI] Dropped on character: {targetChar.CharacterName}");
                OnCardDraggedToTarget?.Invoke(this, targetChar);
                EndDrag(true);
                return;
            }

            if (Card is Card.Order order && !order.RequiresTarget)
            {
                GD.Print($"[CardUI] Order without target, playing in air zone");
                OnPlayWithoutTarget?.Invoke(this);
                EndDrag(true);
                return;
            }

            GD.Print("[CardUI] Dropped on invalid target, returning to original position");
            EndDrag(false);
        }

        private int DetectNodeTarget(Vector2 globalPos)
        {
            foreach (Node node in (Godot.Collections.Array<Node>)GetTree().GetNodesInGroup("MapNode"))
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

        private Character.Character DetectCharacterTarget(Vector2 globalPos)
        {
            if (Card is not Card.Order order)
            {
                return null;
            }

            if (!order.RequiresTarget)
            {
                return null;
            }

            var player = GetTree().GetFirstNodeInGroup("Player") as Character.Player;
            if (player == null)
            {
                return null;
            }

            foreach (Node node in (Godot.Collections.Array<Node>)GetTree().GetNodesInGroup("Enemy"))
            {
                if (node is Control container)
                {
                    Rect2 globalRect = container.GetGlobalRect();
                    if (globalRect.HasPoint(globalPos))
                    {
                        if (container.HasMeta("EnemyObject"))
                        {
                            Character.Character targetChar = container.GetMeta("EnemyObject").As<Character.Character>();
                            if (order.IsValidTarget(targetChar, player))
                            {
                                return targetChar;
                            }
                        }
                    }
                }
            }

            foreach (Node node in (Godot.Collections.Array<Node>)GetTree().GetNodesInGroup("Ally"))
            {
                if (node is Control container)
                {
                    Rect2 globalRect = container.GetGlobalRect();
                    if (globalRect.HasPoint(globalPos))
                    {
                        if (container.HasMeta("CharacterObject"))
                        {
                            Character.Character targetChar = container.GetMeta("CharacterObject").As<Character.Character>();
                            if (order.IsValidTarget(targetChar, player))
                            {
                                return targetChar;
                            }
                        }
                    }
                }
            }

            return null;
        }

        private void UpdateSelectionVisual()
        {
            if (_selectionBorder != null)
            {
                _selectionBorder.Color = IsSelected
                    ? new Color(1f, 0.9f, 0.3f, 1f)
                    : new Color(1f, 0.9f, 0.3f, 0f);
            }

            if (_background != null)
            {
                if (IsSelected)
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
            if (Card == null)
            {
                return;
            }

            if (_nameLabel != null)
            {
                _nameLabel.Text = Card.CardName;
            }

            if (_costLabel != null)
            {
                _costLabel.Text = Card switch
                {
                    Card.Unit unit => $"{unit.DeployCost}K",
                    Card.Order order => $"{order.Cost}K",
                    _ => "0K"
                };
            }

            if (_descLabel != null)
            {
                _descLabel.Text = Card.Description;
            }

            if (_typeLabel != null)
            {
                _typeLabel.Text = Card.Type.ToString().ToUpper();
            }

            if (_statsLabel != null)
            {
                if (Card is Card.Unit unit)
                {
                    _statsLabel.Text = $"⚔ {unit.Attack}    ❤ {unit.MaxHealth}    ↕ {unit.Range}";
                    _statsLabel.Visible = true;
                }
                else
                {
                    _statsLabel.Visible = false;
                }
            }

            if (_rarityLabel != null)
            {
                _rarityLabel.Text = GetRarityText(Card.Rarity);
                _rarityLabel.AddThemeColorOverride("font_color", GetRarityColor(Card.Rarity));
            }

            if (_artworkRect != null)
            {
                _artworkRect.Texture = Card.Artwork;
                _artworkRect.Visible = Card.Artwork != null;
            }

            if (_artworkPlaceholder != null)
            {
                _artworkPlaceholder.Visible = Card.Artwork == null;
            }

            if (_background != null)
            {
                _background.Color = Card.Type switch
                {
                    CardType.Unit => new Color(0.15f, 0.35f, 0.25f),
                    CardType.Order => new Color(0.35f, 0.2f, 0.15f),
                    _ => new Color(0.2f, 0.2f, 0.25f)
                };
            }

            if (_headerBg != null)
            {
                _headerBg.Color = Card.Type switch
                {
                    CardType.Unit => new Color(0.2f, 0.45f, 0.3f),
                    CardType.Order => new Color(0.45f, 0.25f, 0.15f),
                    _ => new Color(0.25f, 0.25f, 0.3f)
                };
            }
        }

        private static string GetRarityText(CardRarity rarity)
        {
            return rarity switch
            {
                CardRarity.Common => "●",
                CardRarity.Uncommon => "◆",
                CardRarity.Rare => "★",
                CardRarity.Legendary => "✦",
                _ => "●"
            };
        }

        private static Color GetRarityColor(CardRarity rarity)
        {
            return rarity switch
            {
                CardRarity.Common => new Color(0.7f, 0.7f, 0.7f),
                CardRarity.Uncommon => new Color(0.3f, 0.8f, 0.3f),
                CardRarity.Rare => new Color(0.3f, 0.5f, 1f),
                CardRarity.Legendary => new Color(1f, 0.7f, 0.2f),
                _ => new Color(0.7f, 0.7f, 0.7f)
            };
        }

        public void OnMouseEntered()
        {
            _isHovered = true;
            if (_background != null && !IsSelected)
            {
                _background.Color = Card?.Type switch
                {
                    CardType.Unit => new Color(0.2f, 0.45f, 0.3f),
                    CardType.Order => new Color(0.45f, 0.25f, 0.15f),
                    _ => new Color(0.25f, 0.25f, 0.3f)
                };
            }
        }

        public void OnMouseExited()
        {
            _isHovered = false;
            if (_background != null && !IsSelected)
            {
                UpdateDisplay();
            }
        }

        private void OnLanguageChanged(string newLanguage)
        {
            UpdateLocalizedText();
        }

        public void UpdateLocalizedText()
        {
            if (Card == null)
            {
                return;
            }

            if (_nameLabel != null)
            {
                _nameLabel.Text = Card.CardName;
            }

            if (_descLabel != null)
            {
                _descLabel.Text = Card.Description;
            }
        }

        public override void _ExitTree()
        {
            Localization.Localization.OnLanguageChanged -= OnLanguageChanged;

            if (UIScaler.Instance != null)
            {
                UIScaler.Instance.OnResolutionChanged -= ApplyScaledSize;
            }
        }
    }
}
