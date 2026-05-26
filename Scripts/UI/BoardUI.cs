using Godot;
using OdysseyCards.Card;
using OdysseyCards.Combat;
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
    private static readonly Color _borderNormal = new(0.3f, 0.3f, 0.3f);
    private static readonly Color _borderHighlight = new(0.3f, 0.85f, 0.3f);
    private static readonly Color _textDim = new(0.5f, 0.5f, 0.5f);
    private static readonly Color _textBright = new(0.9f, 0.9f, 0.9f);

    // ===== 公开事件 =====

    /// <summary>
    /// 当玩家点击某个槽位时触发。
    /// 参数：槽位索引（0-4）、是否为玩家方。
    /// </summary>
    public event Action<int, bool>? OnSlotClicked;

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

        // 创建槽位
        for (int i = 0; i < Board.MaxSlotsPerSide; i++)
        {
            // 敌方槽位（isPlayerSide = false）
            var enemySlot = new BoardSlot(i, false, this);
            _enemySlots[i] = enemySlot;
            _enemyRow.AddChild(enemySlot);

            // 玩家槽位（isPlayerSide = true）
            var playerSlot = new BoardSlot(i, true, this);
            _playerSlots[i] = playerSlot;
            _playerRow.AddChild(playerSlot);
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
    /// 遍历所有槽位，根据战场数据更新每个槽位的显示内容。
    /// 对于每个槽位，调用 Board.GetMinionAt 获取随从并传递给对应 BoardSlot。
    /// </summary>
    public void RefreshBoard()
    {
        if (_board is null) return;

        for (int i = 0; i < Board.MaxSlotsPerSide; i++)
        {
            _playerSlots[i].UpdateDisplay(_board.GetMinionAt(i, true));
            _enemySlots[i].UpdateDisplay(_board.GetMinionAt(i, false));
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
        /// 当前槽位上的随从（null 表示空槽位）。
        /// </summary>
        public Minion? OccupyingMinion { get; private set; }

        // ===== 内部 UI 元素 =====

        private readonly BoardUI _parentBoard;
        private readonly ColorRect _background;
        private readonly ColorRect _borderRect;
        private readonly Label _indexLabel;
        private readonly Label _contentLabel;
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

            // 槽位索引标签（左上角小字）
            _indexLabel = new Label
            {
                Text = $"[{slotIndex + 1}]",
                Position = new Vector2(6, 4)
            };
            _indexLabel.AddThemeColorOverride("font_color", _textDim);
            _indexLabel.AddThemeFontSizeOverride("font_size", 11);
            AddChild(_indexLabel);

            // 主要内容标签（居中）
            _contentLabel = new Label
            {
                Text = "空",
                Position = new Vector2(4, 22),
                Size = new Vector2(SlotWidth - 8, SlotHeight - 30),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _contentLabel.AddThemeColorOverride("font_color", _textDim);
            _contentLabel.AddThemeFontSizeOverride("font_size", 13);
            AddChild(_contentLabel);

            // 悬停信号
            MouseEntered += OnMouseEnter;
            MouseExited += OnMouseExit;
        }

        // ===== 公开方法 =====

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
                _contentLabel.Text = "空";
                _contentLabel.AddThemeColorOverride("font_color", _textDim);
                _contentLabel.AddThemeFontSizeOverride("font_size", 13);
                _background.Color = _bgNormal;
                return;
            }

            // 随从名称与战斗属性
            string display = $"{minion.CardName}\n{minion.Attack}/{minion.CurrentHealth}";

            // 关键词标签
            var keywords = new List<string>(4);
            if (minion.HasTaunt) keywords.Add("嘲");
            if (minion.HasCharge) keywords.Add("冲");
            if (minion.HasWindfury) keywords.Add("风");
            if (minion.HasBattlecry) keywords.Add("吼");
            if (minion.HasDeathrattle) keywords.Add("亡");

            if (keywords.Count > 0)
            {
                display += "\n" + string.Join(" ", keywords);
            }

            _contentLabel.Text = display;
            _contentLabel.AddThemeColorOverride("font_color", _textBright);
            _contentLabel.AddThemeFontSizeOverride("font_size", 12);

            // 恢复背景色（高亮优先）
            _background.Color = IsHighlighted ? _bgHighlight : (_isHovered ? _bgHover : _bgNormal);
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
            }
            else
            {
                _borderRect.Color = _borderNormal;
                _background.Color = _isHovered ? _bgHover : _bgNormal;
            }
        }

        // ===== 输入处理 =====

        /// <summary>
        /// 处理 GUI 输入事件——检测鼠标左键点击，触发父 BoardUI 的 OnSlotClicked。
        /// </summary>
        public override void _GuiInput(InputEvent @event)
        {
            if (@event is InputEventMouseButton mouseBtn
                && mouseBtn.ButtonIndex == MouseButton.Left
                && mouseBtn.Pressed)
            {
                _parentBoard.NotifySlotClicked(SlotIndex, IsPlayerSide);
                AcceptEvent();
            }
        }

        // ===== 悬停处理 =====

        private void OnMouseEnter()
        {
            _isHovered = true;
            if (!IsHighlighted)
            {
                _background.Color = _bgHover;
            }
        }

        private void OnMouseExit()
        {
            _isHovered = false;
            if (!IsHighlighted)
            {
                _background.Color = _bgNormal;
            }
            else
            {
                _background.Color = _bgHighlight;
            }
        }
    }
}
