using Godot;
using OdysseyCards.Card;
using OdysseyCards.Combat;
using OdysseyCards.Core;
using OdysseyCards.Infrastructure;
using OdysseyCards.Localization;
using System;
using System.Collections.Generic;

namespace OdysseyCards.UI;

/// <summary>
/// 战场可视化组件。
/// 以炉石传说风格的 2×5 布局渲染双方随从槽位：
/// 顶部一行 5 个敌方槽位，底部一行 5 个玩家槽位。
/// 通过点击驱动的方式支持随从放置目标选择和攻击目标选择。
/// </summary>
public partial class BoardUI : Control
{
    // ===== 常量 =====

    /// <summary>
    /// 槽位最小宽度（像素）。
    /// </summary>
    private const float SlotWidth = 100f;

    /// <summary>
    /// 槽位最小高度（像素）。
    /// </summary>
    private const float SlotHeight = 140f;

    /// <summary>
    /// 双方槽位间隔（像素）。
    /// </summary>
    private const float RowSpacing = 20f;

    // ===== 颜色常量 =====

    private static readonly Color _bgNormal = new(0.15f, 0.15f, 0.15f);
    private static readonly Color _bgHover = new(0.28f, 0.28f, 0.28f);
    private static readonly Color _bgHighlight = new(0.2f, 0.6f, 0.2f, 0.6f);
    private static readonly Color _bgDimmed = new(0.1f, 0.1f, 0.1f);
    private static readonly Color _textDimmed = new(0.35f, 0.35f, 0.35f);
    private static readonly Color _borderNormal = new(0.3f, 0.3f, 0.3f);
    private static readonly Color _borderHighlight = new(0.3f, 0.85f, 0.3f);
    private static readonly Color _textDim = new(0.5f, 0.5f, 0.5f);
    private static readonly Color _textBright = new(0.9f, 0.9f, 0.9f);

    // 费用/行动花费颜色（与 CardUI 一致）
    private static readonly Color _costBlue = new("#4488cc");
    private static readonly Color _actionCostRed = new("#cc3333");
    private static readonly Color _costTextWhite = new("#f0f0e8");

    // ===== 公开事件 =====

    /// <summary>
    /// 当玩家点击某个槽位时触发。
    /// 参数：槽位索引（0-4）、是否为玩家方。
    /// </summary>
    public event Action<int, bool>? OnSlotClicked;

    /// <summary>
    /// 槽位右键点击事件。参数为槽位索引和是否玩家方。
    /// CombatUI 订阅此事件以在攻击选择模式下取消选择。
    /// </summary>
    public event Action<int, bool>? OnSlotRightClicked;

    // ===== 战场引用 =====

    private Combat.Board? _board;

    // ===== 槽位数组 =====

    /// <summary>
    /// 玩家方 5 个槽位（底部行）。
    /// </summary>
    private readonly BoardSlot[] _playerSlots = new BoardSlot[Board.MaxSlotsPerSide];

    /// <summary>
    /// 敌方 5 个槽位（顶部行）。
    /// </summary>
    private readonly BoardSlot[] _enemySlots = new BoardSlot[Board.MaxSlotsPerSide];

    /// <summary>
    /// 玩家方效果图标栏（每个槽位下方）。
    /// </summary>
    private readonly EffectBar[] _playerEffectBars = new EffectBar[Board.MaxSlotsPerSide];

    /// <summary>
    /// 敌方效果图标栏（每个槽位下方）。
    /// </summary>
    private readonly EffectBar[] _enemyEffectBars = new EffectBar[Board.MaxSlotsPerSide];

    // ===== 容器引用 =====

    private VBoxContainer _mainContainer = null!;
    private HBoxContainer _enemyRow = null!;
    private HBoxContainer _playerRow = null!;

    // ===== Godot 生命周期 =====

    /// <summary>
    /// 节点就绪时创建布局：两个水平行容器（敌方上方，玩家下方），
    /// 每行 5 个 BoardSlot，通过 VBoxContainer 垂直排列。
    /// </summary>
    public override void _Ready()
    {
        // 主垂直容器：居中
        _mainContainer = new VBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center,
            AnchorLeft = 0,
            AnchorTop = 0,
            AnchorRight = 1,
            AnchorBottom = 1,
        };
        AddChild(_mainContainer);

        // 间距空白（顶部留白）
        var topSpacer = new Control { CustomMinimumSize = new Vector2(0, RowSpacing) };
        _mainContainer.AddChild(topSpacer);

        // 敌方行（上方）
        _enemyRow = CreateSlotRow();
        _mainContainer.AddChild(_enemyRow);

        // 中间间距
        var midSpacer = new Control { CustomMinimumSize = new Vector2(0, RowSpacing * 2) };
        _mainContainer.AddChild(midSpacer);

        // 玩家行（下方）
        _playerRow = CreateSlotRow();
        _mainContainer.AddChild(_playerRow);

        // 底部留白
        var bottomSpacer = new Control { CustomMinimumSize = new Vector2(0, RowSpacing) };
        _mainContainer.AddChild(bottomSpacer);

        // 效果图层（CanvasLayer 渲染在最顶层，不受布局约束）
        var effectLayer = new CanvasLayer { Name = "EffectLayer", Layer = 10 };
        AddChild(effectLayer);

        // 创建槽位，EffectBar 挂到 CanvasLayer 上
        for (int i = 0; i < Board.MaxSlotsPerSide; i++)
        {
            // 敌方槽位
            var enemySlot = new BoardSlot(i, false, this);
            _enemySlots[i] = enemySlot;
            _enemyRow.AddChild(enemySlot);

            var enemyEffectBar = new EffectBar { Name = $"EnemyEffectBar_{i}" };
            _enemyEffectBars[i] = enemyEffectBar;
            effectLayer.AddChild(enemyEffectBar);

            // 玩家槽位
            var playerSlot = new BoardSlot(i, true, this);
            _playerSlots[i] = playerSlot;
            _playerRow.AddChild(playerSlot);

            var playerEffectBar = new EffectBar { Name = $"PlayerEffectBar_{i}" };
            _playerEffectBars[i] = playerEffectBar;
            effectLayer.AddChild(playerEffectBar);
        }
    }

    // ===== 公开方法 =====

    /// <summary>
    /// 设置战场数据引用，后续通过 <see cref="RefreshBoard"/> 刷新显示。
    /// </summary>
    /// <param name="board">战场数据对象</param>
    public void SetBoard(Combat.Board board)
    {
        _board = board;
        RefreshBoard();
    }

    /// <summary>
    /// 遍历所有槽位，更新显示内容和效果图标栏位置。
    /// EffectBar 通过 CanvasLayer 渲染，使用全局坐标定位在槽位下方。
    /// </summary>
    public void RefreshBoard()
    {
        if (_board is null) return;

        for (int i = 0; i < Board.MaxSlotsPerSide; i++)
        {
            var playerMinion = _board.GetMinionAt(i, true);
            _playerSlots[i].UpdateDisplay(playerMinion);
            PositionEffectBar(_playerSlots[i], _playerEffectBars[i], playerMinion);

            var enemyMinion = _board.GetMinionAt(i, false);
            _enemySlots[i].UpdateDisplay(enemyMinion);
            PositionEffectBar(_enemySlots[i], _enemyEffectBars[i], enemyMinion);
        }
    }

    /// <summary>
    /// 将 EffectBar 定位在 BoardSlot 正下方，并填充效果数据。
    /// </summary>
    private static void PositionEffectBar(BoardSlot slot, EffectBar bar, Minion? minion)
    {
        var effects = minion?.GetDisplayableEffects()
            ?? (IReadOnlyList<DisplayableEffect>)Array.Empty<DisplayableEffect>();
        bar.Populate(effects);

        if (bar.Visible)
        {
            var slotRect = slot.GetGlobalRect();
            // 定位在槽位正下方，水平居中，留 2px 间距
            bar.GlobalPosition = new Vector2(
                slotRect.Position.X + (slotRect.Size.X - bar.Size.X) / 2,
                slotRect.Position.Y + slotRect.Size.Y + 2
            );
        }
    }

    /// <summary>
    /// 高亮指定的槽位列表（用于展示合法目标）。
    /// </summary>
    /// <param name="slotIndices">要高亮的槽位索引列表</param>
    /// <param name="isPlayerSide">目标所属方</param>
    /// <param name="highlight">true 为高亮，false 为取消</param>
    public void HighlightSlots(List<int> slotIndices, bool isPlayerSide, bool highlight)
    {
        var slots = isPlayerSide ? _playerSlots : _enemySlots;
        foreach (int index in slotIndices)
        {
            if (index >= 0 && index < Board.MaxSlotsPerSide)
            {
                slots[index].SetHighlighted(highlight);
            }
        }
    }

    /// <summary>
    /// 清除所有槽位的高亮状态。
    /// </summary>
    public void ClearHighlights()
    {
        for (int i = 0; i < Board.MaxSlotsPerSide; i++)
        {
            _playerSlots[i].SetHighlighted(false);
            _enemySlots[i].SetHighlighted(false);
        }
    }

    /// <summary>
    /// 设置己方槽位的暗化状态——用于标识行动花费不足、无法攻击的随从。
    /// </summary>
    /// <param name="actionCostMana">当前可用法力值</param>
    public void UpdateActionCostDimming(int availableMana)
    {
        for (int i = 0; i < Board.MaxSlotsPerSide; i++)
        {
            var minion = _board?.GetMinionAt(i, true);
            bool cannotAttack = minion != null && !minion.IsDead
                && minion.ActionCost > 0 && minion.ActionCost > availableMana;
            _playerSlots[i].SetDimmed(cannotAttack);
        }
    }

    /// <summary>
    /// 检查指定全局坐标是否落在某个槽位内。
    /// 用于拖拽松手时判断落点目标。
    /// </summary>
    /// <param name="screenPos">全局坐标（GetScreenPosition 或 GetGlobalMousePosition）</param>
    /// <returns>命中的槽位索引和阵营；null 表示未命中任何槽位</returns>
    public (int slotIndex, bool isPlayerSide)? GetSlotAtPosition(Vector2 screenPos)
    {
        for (int i = 0; i < Board.MaxSlotsPerSide; i++)
        {
            if (_playerSlots[i].GetGlobalRect().HasPoint(screenPos))
                return (i, true);
            if (_enemySlots[i].GetGlobalRect().HasPoint(screenPos))
                return (i, false);
        }
        return null;
    }

    /// <summary>
    /// 获取指定槽位的屏幕中心坐标。
    /// 用于 ArrowRenderer 箭头起始/终止位置计算。
    /// </summary>
    /// <param name="slotIndex">槽位索引（0-4）</param>
    /// <param name="isPlayerSide">是否玩家方</param>
    /// <returns>槽位屏幕中心坐标；索引越界返回 Vector2.Zero</returns>
    public Vector2 GetSlotScreenCenter(int slotIndex, bool isPlayerSide)
    {
        var slots = isPlayerSide ? _playerSlots : _enemySlots;
        if (slotIndex < 0 || slotIndex >= slots.Length) return Vector2.Zero;
        var rect = slots[slotIndex].GetGlobalRect();
        return rect.Position + rect.Size / 2;
    }

    /// <summary>
    /// 设置指定槽位的意图文字——仅敌方槽位需要意图显示。
    /// </summary>
    /// <param name="slotIndex">槽位索引（0-4）</param>
    /// <param name="isPlayerSide">是否玩家方</param>
    /// <param name="text">意图描述文本，null 或空则隐藏</param>
    public void SetSlotIntentText(int slotIndex, bool isPlayerSide, string? text)
    {
        var slots = isPlayerSide ? _playerSlots : _enemySlots;
        if (slotIndex >= 0 && slotIndex < slots.Length)
            slots[slotIndex].SetIntentText(text);
    }

    // ===== 内部方法 =====

    /// <summary>
    /// 创建一行槽位容器（HBoxContainer），居中对齐。
    /// </summary>
    private static HBoxContainer CreateSlotRow()
    {
        return new HBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
    }

    /// <summary>
    /// 由 BoardSlot 调用的内部回调，触发公开的 OnSlotClicked 事件。
    /// </summary>
    internal void NotifySlotClicked(int slotIndex, bool isPlayerSide)
    {
        OnSlotClicked?.Invoke(slotIndex, isPlayerSide);
    }

    /// <summary>
    /// 由 BoardSlot 调用的内部回调，触发公开的 OnSlotRightClicked 事件。
    /// </summary>
    internal void NotifySlotRightClicked(int slotIndex, bool isPlayerSide)
    {
        OnSlotRightClicked?.Invoke(slotIndex, isPlayerSide);
    }

    // ==================================================================
    // BoardSlot —— 单个槽位可视化组件（嵌套类）
    // ==================================================================

    /// <summary>
    /// 单个战场槽位的可视化组件。
    /// 显示槽位编号、随从信息（名称、攻击/生命、关键词）或空槽位提示。
    /// 支持悬停变亮、点击触发选择，以及高亮（合法目标）状态。
    /// </summary>
    public partial class BoardSlot : Control
    {
        /// <summary>
        /// 槽位在所属行中的索引（0-4）。
        /// </summary>
        public int SlotIndex { get; }

        /// <summary>
        /// 该槽位是否属于玩家方（true = 玩家，false = 敌方）。
        /// </summary>
        public bool IsPlayerSide { get; }

        /// <summary>
        /// 当前是否处于高亮（合法目标）状态。
        /// </summary>
        public bool IsHighlighted { get; private set; }

        /// <summary>
        /// 当前是否处于暗化（行动花费不足）状态。
        /// </summary>
        public bool IsDimmed { get; private set; }

        /// <summary>
        /// 当前槽位上的随从（null 表示空槽位）。
        /// </summary>
        public Minion? OccupyingMinion { get; private set; }

        // ===== 内部 UI 元素 =====

        private readonly BoardUI _parentBoard;
        private readonly ColorRect _background;
        private readonly ColorRect _borderRect;
        private readonly Label _indexLabel;
        private readonly Label _contentLabel;
        // 左上角费用+行动花费显示
        private readonly ColorRect _costBg;
        private readonly Label _costLabel;
        private readonly ColorRect _actionCostBg;
        private readonly Label _actionCostLabel;
        private readonly Label _intentLabel;
        private bool _isHovered;

        // ===== 构造函数 =====

        /// <summary>
        /// 创建槽位组件。
        /// </summary>
        /// <param name="slotIndex">槽位索引（0-4）</param>
        /// <param name="isPlayerSide">是否玩家方</param>
        /// <param name="parentBoard">所属的 BoardUI 父节点</param>
        public BoardSlot(int slotIndex, bool isPlayerSide, BoardUI parentBoard)
        {
            SlotIndex = slotIndex;
            IsPlayerSide = isPlayerSide;
            _parentBoard = parentBoard;

            // 基础设置
            CustomMinimumSize = new Vector2(SlotWidth, SlotHeight);
            SizeFlagsHorizontal = SizeFlags.ExpandFill;
            MouseFilter = MouseFilterEnum.Stop;

            // 边框背景
            _borderRect = new ColorRect
            {
                Color = _borderNormal,
                AnchorsPreset = (int)LayoutPreset.FullRect,
                Size = CustomMinimumSize
            };
            AddChild(_borderRect);

            // 内容背景（内缩 2px 形成边框效果）
            _background = new ColorRect
            {
                Color = _bgNormal,
                Position = new Vector2(2, 2),
                Size = new Vector2(SlotWidth - 4, SlotHeight - 4)
            };
            AddChild(_background);

            // 槽位索引标签（左上角小字，费用右侧）
            _indexLabel = new Label
            {
                Text = $"[{slotIndex + 1}]",
                Position = new Vector2(22, 4)
            };
            _indexLabel.AddThemeColorOverride("font_color", _textDim);
            _indexLabel.AddThemeFontSizeOverride("font_size", 11);
            AddChild(_indexLabel);

            // 费用+行动花费组合显示（左上角）
            _costBg = new ColorRect
            {
                Color = _costBlue,
                Size = new Vector2(16, 16),
                Position = new Vector2(3, 2),
                Visible = false,
            };
            _costBg.MouseFilter = MouseFilterEnum.Ignore;
            AddChild(_costBg);

            _costLabel = new Label
            {
                Size = new Vector2(16, 16),
                Position = new Vector2(3, 2),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Visible = false,
            };
            _costLabel.AddThemeColorOverride("font_color", _costTextWhite);
            _costLabel.AddThemeFontSizeOverride("font_size", 10);
            _costLabel.MouseFilter = MouseFilterEnum.Ignore;
            AddChild(_costLabel);

            _actionCostBg = new ColorRect
            {
                Color = _actionCostRed,
                Size = new Vector2(9, 9),
                Position = new Vector2(12, 10),
                Visible = false,
            };
            _actionCostBg.MouseFilter = MouseFilterEnum.Ignore;
            AddChild(_actionCostBg);

            _actionCostLabel = new Label
            {
                Size = new Vector2(9, 9),
                Position = new Vector2(12, 10),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Visible = false,
            };
            _actionCostLabel.AddThemeColorOverride("font_color", _costTextWhite);
            _actionCostLabel.AddThemeFontSizeOverride("font_size", 7);
            _actionCostLabel.MouseFilter = MouseFilterEnum.Ignore;
            AddChild(_actionCostLabel);

            // 主要内容标签（居中）
            _contentLabel = new Label
            {
                Text = Localization.Localization.T("ui.combat.board_empty", "空"),
                Position = new Vector2(4, 22),
                Size = new Vector2(SlotWidth - 8, SlotHeight - 30),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _contentLabel.AddThemeColorOverride("font_color", _textDim);
            _contentLabel.AddThemeFontSizeOverride("font_size", 13);
            _contentLabel.MouseFilter = MouseFilterEnum.Ignore;
            AddChild(_contentLabel);

            // 意图文字标签（底部，仅敌方槽位可见）
            _intentLabel = new Label
            {
                Position = new Vector2(2, SlotHeight - 20),
                Size = new Vector2(SlotWidth - 4, 18),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Visible = false,
            };
            _intentLabel.AddThemeColorOverride("font_color", new Color(1f, 0.65f, 0.2f));
            _intentLabel.AddThemeFontSizeOverride("font_size", 10);
            _intentLabel.MouseFilter = MouseFilterEnum.Ignore;
            AddChild(_intentLabel);

            // 确保子控件不拦截鼠标事件，所有交互由 BoardSlot 本身处理
            _borderRect.MouseFilter = MouseFilterEnum.Ignore;
            _background.MouseFilter = MouseFilterEnum.Ignore;
            _indexLabel.MouseFilter = MouseFilterEnum.Ignore;

            // 悬停信号
            MouseEntered += OnMouseEnter;
            MouseExited += OnMouseExit;
        }

        // ===== 公开方法 =====

        /// <summary>
        /// 设置槽位底部意图文字（用于敌方随从意图显示）。
        /// 传入 null 或空字符串时隐藏标签。
        /// </summary>
        /// <param name="text">意图描述文本，null 或空则隐藏</param>
        public void SetIntentText(string? text)
        {
            if (string.IsNullOrEmpty(text))
            {
                _intentLabel.Visible = false;
                _intentLabel.Text = "";
            }
            else
            {
                _intentLabel.Text = text;
                _intentLabel.Visible = true;
            }
        }

        /// <summary>
        /// 更新槽位显示内容。
        /// 若 minion 不为 null，显示随从名称、攻击/生命、关键词；
        /// 否则显示"空"。
        /// </summary>
        /// <param name="minion">该槽位上的随从，null 表示空</param>
        public void UpdateDisplay(Minion? minion)
        {
            OccupyingMinion = minion;

            if (minion is null || minion.IsDead)
            {
                _contentLabel.Text = Localization.Localization.T("ui.combat.board_empty", "空");
                _contentLabel.AddThemeColorOverride("font_color", _textDim);
                _contentLabel.AddThemeFontSizeOverride("font_size", 13);
                _costBg.Visible = false;
                _costLabel.Visible = false;
                _actionCostBg.Visible = false;
                _actionCostLabel.Visible = false;
                SetIntentText(null);

                // 仅在非高亮状态恢复背景色——若槽位正被合法目标高亮则不覆盖
                if (!IsHighlighted)
                {
                    _background.Color = _bgNormal;
                }
                return;
            }

            // 左上角费用+行动花费
            _costLabel.Text = minion.Cost.ToString();
            _costBg.Visible = true;
            _costLabel.Visible = true;
            _actionCostLabel.Text = minion.ActionCost.ToString();
            _actionCostBg.Visible = true;
            _actionCostLabel.Visible = true;

            // 随从名称与战斗属性
            string defenseStr = minion.Defense != 0 ? $" {Localization.Localization.T("ui.board.defense_prefix", "防")}{minion.Defense:+0;-#}" : "";
            string display = $"{minion.GetLocalizedName()}\n{minion.Attack}/{minion.CurrentHealth}{defenseStr}";

            // 关键词标签
            var keywords = new List<string>(4);
            if (minion.HasTaunt) keywords.Add(Localization.Localization.T("ui.board.keyword_taunt", "嘲"));
            if (minion.HasCharge) keywords.Add(Localization.Localization.T("ui.board.keyword_charge", "冲"));
            if (minion.HasWindfury) keywords.Add(Localization.Localization.T("ui.board.keyword_windfury", "风"));
            if (minion.HasBattlecry) keywords.Add(Localization.Localization.T("ui.board.keyword_battlecry", "吼"));
            if (minion.HasDeathrattle) keywords.Add(Localization.Localization.T("ui.board.keyword_deathrattle", "亡"));
            if (minion.HasAmbush) keywords.Add(Localization.Localization.T("ui.board.keyword_ambush", "伏"));
            if (minion.HasImpact) keywords.Add(Localization.Localization.T("ui.board.keyword_impact", "击"));

            if (keywords.Count > 0)
            {
                display += "\n" + string.Join(" ", keywords);
            }

            _contentLabel.Text = display;
            _contentLabel.AddThemeColorOverride("font_color", _textBright);
            _contentLabel.AddThemeFontSizeOverride("font_size", 12);

            // 恢复背景色（高亮/暗化 > 悬停 > 普通）
            if (!IsHighlighted)
            {
                ApplyBackgroundColor();
                ApplyTextColor();
            }
        }

        /// <summary>
        /// 设置高亮状态。高亮时边框和背景变色（绿/黄色），
        /// 用于标识该槽位为当前操作的合法目标。
        /// </summary>
        /// <param name="highlighted">是否高亮</param>
        public void SetHighlighted(bool highlighted)
        {
            IsHighlighted = highlighted;

            if (highlighted)
            {
                _borderRect.Color = _borderHighlight;
                _background.Color = _bgHighlight;
                _contentLabel.AddThemeColorOverride("font_color", _textBright);
            }
            else
            {
                _borderRect.Color = _borderNormal;
                ApplyBackgroundColor();
                ApplyTextColor();
            }
        }

        /// <summary>
        /// 设置暗化状态——行动花费不足时，槽位显示为灰色。
        /// </summary>
        /// <param name="dimmed">是否暗化</param>
        public void SetDimmed(bool dimmed)
        {
            IsDimmed = dimmed;

            if (!IsHighlighted)
            {
                ApplyBackgroundColor();
                ApplyTextColor();
            }
        }

        /// <summary>
        /// 根据当前状态应用正确的背景色。
        /// </summary>
        private void ApplyBackgroundColor()
        {
            if (IsDimmed)
                _background.Color = _bgDimmed;
            else if (_isHovered)
                _background.Color = _bgHover;
            else
                _background.Color = _bgNormal;
        }

        /// <summary>
        /// 根据当前状态应用正确的文字颜色。
        /// </summary>
        private void ApplyTextColor()
        {
            if (IsDimmed)
                _contentLabel.AddThemeColorOverride("font_color", _textDimmed);
            else if (OccupyingMinion != null && !OccupyingMinion.IsDead)
                _contentLabel.AddThemeColorOverride("font_color", _textBright);
            else
                _contentLabel.AddThemeColorOverride("font_color", _textDim);
        }

        // ===== 输入处理 =====

        /// <summary>
        /// 处理 GUI 输入事件——鼠标/触控点击触发攻击/放置，右键取消选择。
        /// 移动端不处理右键（无触控等效操作）。
        /// </summary>
        public override void _GuiInput(InputEvent @event)
        {
            // 触控事件（移动端）
            if (@event is InputEventScreenTouch touch && touch.Pressed)
            {
                _parentBoard.NotifySlotClicked(SlotIndex, IsPlayerSide);
                AcceptEvent();
                return;
            }

            // 鼠标事件（桌面端 / 移动端外接鼠标）
            if (@event is InputEventMouseButton mouseBtn && mouseBtn.Pressed)
            {
                if (mouseBtn.ButtonIndex == MouseButton.Left)
                {
                    _parentBoard.NotifySlotClicked(SlotIndex, IsPlayerSide);
                    AcceptEvent();
                }
                else if (mouseBtn.ButtonIndex == MouseButton.Right
                    && !MobileInputHelper.IsMobile) // 移动端无右键
                {
                    _parentBoard.NotifySlotRightClicked(SlotIndex, IsPlayerSide);
                    AcceptEvent();
                }
            }
        }

        // ===== 悬停处理 =====

        private void OnMouseEnter()
        {
            _isHovered = true;
            if (!IsHighlighted)
            {
                ApplyBackgroundColor();
            }
        }

        private void OnMouseExit()
        {
            _isHovered = false;
            if (!IsHighlighted)
            {
                ApplyBackgroundColor();
            }
            else
            {
                _background.Color = _bgHighlight;
            }
        }
    }
}
