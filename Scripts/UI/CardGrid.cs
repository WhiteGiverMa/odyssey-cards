using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using OdysseyCards.Core;
using OdysseyCards.Infrastructure;
using Loc = OdysseyCards.Localization.Localization;

namespace OdysseyCards.UI
{
    /// <summary>
    /// 可复用的卡牌网格组件。
    /// 在 ScrollContainer 中用 FlowContainer 排列 CardUI 实例。
    /// 支持按类型/费用/稀有度过滤，翻页，以及选择事件。
    ///
    /// 使用场景：
    /// 1. 收藏浏览 — 显示所有已解锁卡牌
    /// 2. 牌组编辑右侧面板 — 显示可加入牌组的卡牌
    /// 3. 战后奖励 — 显示 3 个奖励选项（每选项可能有多张复制）
    /// </summary>
    public partial class CardGrid : Control
    {
        // ===== 常量 =====

        private const float CardWidth = 120f;
        private const float CardHeight = 180f;
        private const float GridSpacing = 12f;
        private const int CardsPerPage = 20;

        // ===== 子控件 =====

        private ScrollContainer _scrollContainer = null!;
        private FlowContainer _flowContainer = null!;
        private HBoxContainer _filterBar = null!;
        private HBoxContainer _pageBar = null!;
        private Label _pageLabel = null!;

        // ===== 状态 =====

        private readonly List<CardUI> _cardUIs = new();
        private List<CardData> _allCards = new();
        private List<CardData> _filteredCards = new();
        private int _currentPage;
        private int _totalPages;

        /// <summary>
        /// 过滤条件：null 表示不过滤。
        /// </summary>
        private CardType? _filterType;
        private int? _filterCostMin;
        private int? _filterCostMax;
        private CardRarity? _filterRarity;

        // ===== 拖拽状态 =====

        private Control _dragLayer = null!;
        private CardUI? _dragClone;
        private CardData? _draggingCard;
        private Vector2 _dragStartPos;
        private bool _isDragging;
        private const float DragThreshold = 8f;

        // ===== 事件 =====

        /// <summary>
        /// 卡牌被点击时触发（无拖拽位移的快速点击）。
        /// </summary>
        public event Action<CardData>? OnCardClicked;

        /// <summary>
        /// 卡牌被右键点击时触发。
        /// </summary>
        public event Action<CardData>? OnCardRightClicked;

        /// <summary>
        /// 卡牌拖拽完成时触发。参数为卡牌数据和松手时的屏幕坐标。
        /// 接收方判断坐标是否在有效放置区域内。
        /// </summary>
        public event Action<CardData, Vector2>? OnCardDragCompleted;

        /// <summary>
        /// 是否显示过滤栏。
        /// </summary>
        public bool ShowFilterBar { get; set; } = true;

        /// <summary>
        /// 是否显示翻页。
        /// </summary>
        public bool ShowPagination { get; set; } = true;

        /// <summary>
        /// 是否启用点击选择。
        /// </summary>
        public bool Clickable { get; set; } = true;

        /// <summary>
        /// 布局是否已构建。构造函数中构建一次，后续 SetCards 调用复用。
        /// </summary>
        private bool _layoutBuilt;

        // ===== 初始化 =====

        public CardGrid()
        {
            BuildLayout();
            _layoutBuilt = true;
        }

        public override void _Ready()
        {
            // 布局已在构造函数中构建，无需重复。
            // 如果 _Ready 在构造函数之前被调用（不应发生），则兜底构建。
            EnsureLayout();
            GameManager.Instance.LanguageChanged += OnLanguageChanged;
        }

        public override void _ExitTree()
        {
            base._ExitTree();
            if (GameManager.Instance != null)
                GameManager.Instance.LanguageChanged -= OnLanguageChanged;
        }

        /// <summary>
        /// 语言切换时刷新过滤栏按钮文本。
        /// </summary>
        private void OnLanguageChanged(string lang)
        {
            if (!IsInsideTree()) return;
            Refresh();
        }

        /// <summary>
        /// 确保布局已构建（防止 SetCards 在构造函数之前被调用）。
        /// </summary>
        private void EnsureLayout()
        {
            if (!_layoutBuilt)
            {
                BuildLayout();
                _layoutBuilt = true;
            }
        }

        // ===== 布局构建 =====

        private void BuildLayout()
        {
            float s = UIScaler.Instance?.GetScaleFactor() ?? 1f;

            // 全填充父容器
            SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            MouseFilter = MouseFilterEnum.Pass;

            var root = new VBoxContainer
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ExpandFill,
            };
            root.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            AddChild(root);

            // 过滤栏
            _filterBar = new HBoxContainer
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
            };
            _filterBar.AddThemeConstantOverride("separation", Mathf.RoundToInt(8 * s));
            _filterBar.Visible = ShowFilterBar;

            // 类型过滤按钮
            _filterBar.AddChild(CreateFilterButton(Loc.T("ui.card_grid.filter_all", "全部"), () => SetTypeFilter(null)));
            _filterBar.AddChild(CreateFilterButton(Loc.T("ui.card_grid.filter_minion", "随从"), () => SetTypeFilter(CardType.Minion)));
            _filterBar.AddChild(CreateFilterButton(Loc.T("ui.card_grid.filter_spell", "法术"), () => SetTypeFilter(CardType.Spell)));
            _filterBar.AddChild(CreateFilterButton(Loc.T("ui.card_grid.filter_domain", "领域"), () => SetTypeFilter(CardType.Domain)));

            root.AddChild(_filterBar);

            // 翻页栏
            _pageBar = new HBoxContainer
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
            };
            _pageBar.AddThemeConstantOverride("separation", Mathf.RoundToInt(8 * s));

            var prevBtn = new Button
            {
                Text = "←",
                CustomMinimumSize = new Vector2(44 * s, 44 * s),
            };
            prevBtn.Pressed += GoToPreviousPage;
            _pageBar.AddChild(prevBtn);

            _pageLabel = new Label
            {
                Text = "1/1",
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            _pageLabel.AddThemeFontSizeOverride("font_size", Mathf.RoundToInt(14 * s));
            _pageBar.AddChild(_pageLabel);

            var nextBtn = new Button
            {
                Text = "→",
                CustomMinimumSize = new Vector2(44 * s, 44 * s),
            };
            nextBtn.Pressed += GoToNextPage;
            _pageBar.AddChild(nextBtn);

            _pageBar.Visible = ShowPagination;
            root.AddChild(_pageBar);

            // 滚动卡片网格
            _scrollContainer = new ScrollContainer
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ExpandFill,
            };
            root.AddChild(_scrollContainer);

            _flowContainer = new FlowContainer
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
            };
            _flowContainer.AddThemeConstantOverride("separation", Mathf.RoundToInt(GridSpacing * s));
            _scrollContainer.AddChild(_flowContainer);

            // 拖拽层（浮动在所有内容上方）
            _dragLayer = new Control
            {
                Name = "CardGridDragLayer",
                MouseFilter = MouseFilterEnum.Ignore,
                ZIndex = 100,
            };
            _dragLayer.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            AddChild(_dragLayer);
        }

        private Button CreateFilterButton(string text, Action onClick)
        {
            float s = UIScaler.Instance?.GetScaleFactor() ?? 1f;
            var btn = new Button
            {
                Text = text,
                CustomMinimumSize = new Vector2(60 * s, 30 * s),
            };
            btn.AddThemeFontSizeOverride("font_size", Mathf.RoundToInt(14 * s));
            btn.Pressed += onClick;
            return btn;
        }

        // ===== 公共 API =====

        /// <summary>
        /// 设置要展示的卡牌列表。
        /// </summary>
        public void SetCards(List<CardData> cards)
        {
            EnsureLayout();
            _allCards = new List<CardData>(cards);
            ApplyFilter();
        }

        /// <summary>
        /// 设置类型过滤。
        /// </summary>
        public void SetTypeFilter(CardType? type)
        {
            _filterType = type;
            ApplyFilter();
        }

        /// <summary>
        /// 设置费用过滤。
        /// </summary>
        public void SetCostFilter(int? min, int? max)
        {
            _filterCostMin = min;
            _filterCostMax = max;
            ApplyFilter();
        }

        /// <summary>
        /// 设置稀有度过滤。
        /// </summary>
        public void SetRarityFilter(CardRarity? rarity)
        {
            _filterRarity = rarity;
            ApplyFilter();
        }

        /// <summary>
        /// 清除所有过滤条件。
        /// </summary>
        public void ClearFilters()
        {
            _filterType = null;
            _filterCostMin = null;
            _filterCostMax = null;
            _filterRarity = null;
            ApplyFilter();
        }

        /// <summary>
        /// 刷新显示（语言切换或分辨率变化后调用）。
        /// </summary>
        public void Refresh()
        {
            EnsureLayout();
            RenderCurrentPage();
        }

        // ===== 过滤与分页 =====

        private void ApplyFilter()
        {
            _filteredCards = _allCards.Where(c =>
            {
                if (_filterType.HasValue && c.Type != _filterType.Value)
                {
                    return false;
                }

                if (_filterCostMin.HasValue && c.Cost < _filterCostMin.Value)
                {
                    return false;
                }

                if (_filterCostMax.HasValue && c.Cost > _filterCostMax.Value)
                {
                    return false;
                }

                if (_filterRarity.HasValue && c.Rarity != _filterRarity.Value)
                {
                    return false;
                }

                return true;
            }).ToList();

            _totalPages = Mathf.Max(1, Mathf.CeilToInt((float)_filteredCards.Count / CardsPerPage));
            _currentPage = 0;
            RenderCurrentPage();
        }

        private void RenderCurrentPage()
        {
            // 清除所有现有 wrapper（每个 wrapper 包含一个 CardUI）
            foreach (Node? child in _flowContainer.GetChildren())
            {
                child.QueueFree();
            }
            _cardUIs.Clear();

            float s = UIScaler.Instance?.GetScaleFactor() ?? 1f;
            var cardSize = new Vector2(CardWidth * s, CardHeight * s);

            int start = _currentPage * CardsPerPage;
            int end = Mathf.Min(start + CardsPerPage, _filteredCards.Count);

            for (int i = start; i < end; i++)
            {
                CardData cardData = _filteredCards[i];
                var card = new Card.Card(cardData);
                var cardUI = new CardUI
                {
                    DisplayOnly = true,  // 禁用战斗交互（拖拽/选中）
                };
                cardUI.SetCard(card);
                cardUI.CustomMinimumSize = cardSize;
                cardUI.Size = cardSize;

                // 包裹在透明按钮中：点击 = 加入牌组
                var wrapper = new Control
                {
                    CustomMinimumSize = cardSize,
                    Size = cardSize,
                    MouseFilter = MouseFilterEnum.Stop,
                };

                int capturedIndex = i;  // 闭包捕获
                bool dragStarted = false;

                // 移动端触控状态（区分轻触添加 vs 滚动浏览）
                Vector2 touchStartPos = Vector2.Zero;
                bool touchMoved = false;
                const float ScrollThreshold = 10f;

                wrapper.GuiInput += (InputEvent @event) =>
                {
                    if (MobileInputHelper.IsMobile)
                    {
                        // 移动端：区分 tap（添加卡牌）与 scroll（浏览卡库）
                        if (@event is InputEventScreenTouch touch)
                        {
                            if (touch.Pressed)
                            {
                                touchStartPos = touch.Position;
                                touchMoved = false;
                            }
                            else
                            {
                                if (!touchMoved)
                                {
                                    CardData clickedCard = _filteredCards[capturedIndex];
                                    OnCardClicked?.Invoke(clickedCard);
                                }
                            }
                            wrapper.AcceptEvent();
                            return;
                        }
                        else if (@event is InputEventScreenDrag drag)
                        {
                            float dist = drag.Position.DistanceTo(touchStartPos);
                            if (dist > ScrollThreshold)
                            {
                                touchMoved = true;
                                // 不消费事件——让父级 ScrollContainer 处理滚动
                                return;
                            }
                            wrapper.AcceptEvent();
                            return;
                        }
                        return;
                    }

                    if (@event is InputEventMouseButton mb
                        && mb.ButtonIndex == MouseButton.Left)
                    {
                        if (mb.Pressed)
                        {
                            // 鼠标按下：记录拖拽起点
                            CardData clickedCard = _filteredCards[capturedIndex];
                            _draggingCard = clickedCard;
                            _dragStartPos = GetGlobalMousePosition();
                            _isDragging = false;
                            dragStarted = true;
                            wrapper.AcceptEvent();
                        }
                        else
                        {
                            // 鼠标松开
                            if (!_isDragging && dragStarted)
                            {
                                // 无位移 → 点击
                                CardData clickedCard = _filteredCards[capturedIndex];
                                OnCardClicked?.Invoke(clickedCard);
                            }
                            else if (_isDragging && _dragClone != null)
                            {
                                // 拖拽松手 → 触发拖拽完成事件
                                Vector2 dropPos = GetGlobalMousePosition();
                                CardData card = _draggingCard!;
                                OnCardDragCompleted?.Invoke(card, dropPos);
                                CleanupDrag();
                            }
                            else
                            {
                                CleanupDrag();
                            }
                            _draggingCard = null;
                            dragStarted = false;
                        }
                    }
                    else if (@event is InputEventMouseButton rmb
                        && rmb.Pressed
                        && rmb.ButtonIndex == MouseButton.Right)
                    {
                        CardData clickedCard = _filteredCards[capturedIndex];
                        OnCardRightClicked?.Invoke(clickedCard);
                        wrapper.AcceptEvent();
                    }
                    else if (@event is InputEventMouseMotion mm
                        && dragStarted
                        && _draggingCard != null)
                    {
                        // 检测拖拽阈值
                        float dist = GetGlobalMousePosition().DistanceTo(_dragStartPos);
                        if (dist > DragThreshold && !_isDragging)
                        {
                            _isDragging = true;
                            StartDragClone(_draggingCard, _dragStartPos);
                        }

                        // 拖拽中：移动克隆体
                        if (_isDragging && _dragClone != null)
                        {
                            _dragClone.GlobalPosition = GetGlobalMousePosition() - (_dragClone.Size / 2);
                        }
                    }
                };

                wrapper.AddChild(cardUI);
                _cardUIs.Add(cardUI);
                _flowContainer.AddChild(wrapper);
            }

            _pageLabel.Text = $"{_currentPage + 1}/{_totalPages}";
            UpdatePageButtons();
        }

        private void UpdatePageButtons()
        {
            foreach (Node? child in _pageBar.GetChildren())
            {
                if (child is Button btn)
                {
                    if (btn.Text == "←")
                    {
                        btn.Disabled = _currentPage <= 0;
                    }
                    else if (btn.Text == "→")
                    {
                        btn.Disabled = _currentPage >= _totalPages - 1;
                    }
                }
            }
        }

        public void GoToPreviousPage()
        {
            if (_currentPage > 0)
            {
                _currentPage--;
                RenderCurrentPage();
            }
        }

        public void GoToNextPage()
        {
            if (_currentPage < _totalPages - 1)
            {
                _currentPage++;
                RenderCurrentPage();
            }
        }

        /// <summary>
        /// 当前页展示的卡牌数量。
        /// </summary>
        public int CurrentPageCardCount
        {
            get
            {
                int start = _currentPage * CardsPerPage;
                if (start >= _filteredCards.Count) return 0;
                return Mathf.Min(CardsPerPage, _filteredCards.Count - start);
            }
        }

        /// <summary>
        /// 获取当前页中指定索引的卡牌数据。索引 0 对应当前页第一张卡牌。
        /// </summary>
        public CardData? GetCardDataAt(int index)
        {
            int start = _currentPage * CardsPerPage;
            int globalIndex = start + index;
            if (globalIndex < 0 || globalIndex >= _filteredCards.Count) return null;
            return _filteredCards[globalIndex];
        }

        /// <summary>
        /// 设置当前页中指定索引卡牌的高亮状态，同时清除其余卡牌的高亮。
        /// 用于键盘焦点指示器。仅当 HotkeyManager 报告最近有键盘操作时显示。
        /// </summary>
        public void SetCardHighlight(int index)
        {
            var children = _flowContainer.GetChildren();
            for (int i = 0; i < children.Count; i++)
            {
                if (children[i] is Control ctrl)
                {
                    ctrl.SelfModulate = (i == index) ? new Color(1.2f, 1.2f, 0.85f, 1) : Colors.White;
                }
            }
        }

        /// <summary>
        /// 清除当前页所有卡牌的高亮。
        /// </summary>
        public void ClearCardHighlights()
        {
            SetCardHighlight(-1);
        }

        /// <summary>
        /// 估算当前 FlowContainer 每行列数。
        /// 用于键盘方向键上/下导航。
        /// </summary>
        public int EstimatedColumns
        {
            get
            {
                float s = UIScaler.Instance?.GetScaleFactor() ?? 1f;
                float cardSlotWidth = (CardWidth + GridSpacing) * s;
                float containerWidth = _flowContainer.Size.X;
                if (containerWidth <= 0) containerWidth = Size.X; // 回退到 CardGrid 自身宽度
                return Mathf.Max(1, Mathf.FloorToInt(containerWidth / cardSlotWidth));
            }
        }

        // ===== 拖拽支持 =====

        /// <summary>
        /// 创建拖拽克隆体（半透明副本，跟随鼠标）。
        /// </summary>
        private void StartDragClone(CardData cardData, Vector2 startScreenPos)
        {
            float s = UIScaler.Instance?.GetScaleFactor() ?? 1f;
            var cardSize = new Vector2(CardWidth * s, CardHeight * s);

            var card = new Card.Card(cardData);
            _dragClone = new CardUI
            {
                DisplayOnly = true,
                Modulate = new Color(1, 1, 1, 0.75f),  // 半透明
                CustomMinimumSize = cardSize,
                Size = cardSize,
            };
            _dragClone.SetCard(card);
            _dragClone.GlobalPosition = startScreenPos - (cardSize / 2);

            _dragLayer.AddChild(_dragClone);
        }

        /// <summary>
        /// 清理拖拽状态和克隆体。
        /// </summary>
        private void CleanupDrag()
        {
            _isDragging = false;
            if (_dragClone != null)
            {
                _dragClone.QueueFree();
                _dragClone = null;
            }
        }
    }
}
