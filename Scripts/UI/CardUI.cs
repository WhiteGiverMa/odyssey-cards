using System;
using Godot;
using OdysseyCards.Card;
using OdysseyCards.Core;

namespace OdysseyCards.UI;

/// <summary>
/// 卡牌视觉组件（炉石传说风格重构版）。
/// 在一个 Control 上程序化渲染完整卡牌：法力水晶、名称、卡图区域、攻防属性、
/// 关键词徽章和描述文本。支持点击选择（无拖拽）与悬停动效，适配手牌布局。
/// </summary>
public partial class CardUI : Control
{
    // ============================================================
    // 尺寸常量（设计基准 120×180，运行时通过 UIScaler 缩放）
    // ============================================================
    private const float DESIGN_WIDTH = 120f;
    private const float DESIGN_HEIGHT = 180f;
    private const float MANA_DIAMETER = 24f;
    private const float HEADER_H = 28f;
    private const float ARTWORK_H = 80f;
    private const float STATS_W = 32f;
    private const float STATS_H = 22f;
    private const float KEYWORD_H = 18f;
    private const float HOVER_LIFT = 10f;

    // ============================================================
    // 颜色定义
    // ============================================================
    private static readonly Color ClrBg = new("#3a3a30");
    private static readonly Color ClrHeader = new("#2a2a20");
    private static readonly Color ClrMana = new("#4488cc");
    private static readonly Color ClrTextWhite = new("#f0f0e8");
    private static readonly Color ClrArtworkPlaceholder = new("#555548");
    private static readonly Color ClrDescBg = new("#4a4a40");
    private static readonly Color ClrDescText = new("#ccccaa");
    private static readonly Color ClrStatsBg = new("#1a1a10");
    private static readonly Color ClrCannotPlay = new("#666666");
    private static readonly Color ClrBorder = new("#555540");

    // 关键词颜色
    private static readonly Color ClrCharge = new("#cc4444");
    private static readonly Color ClrTaunt = new("#cc8844");
    private static readonly Color ClrBattlecry = new("#cccc44");
    private static readonly Color ClrDeathrattle = new("#8844cc");
    private static readonly Color ClrWindfury = new("#44cccc");

    // ============================================================
    // 公共属性与事件
    // ============================================================

    /// <summary>
    /// 关联的运行时卡牌实例。
    /// </summary>
    public Card.Card? Card { get; private set; }

    /// <summary>
    /// 当前是否处于选中状态（高亮抬起）。
    /// </summary>
    public bool IsSelected { get; private set; }

    /// <summary>
    /// 当前是否有足够法力值打出此牌。
    /// </summary>
    public bool CanPlay { get; private set; } = true;

    /// <summary>
    /// 卡牌被选中/取消选中时触发。
    /// </summary>
    public event Action<CardUI>? OnCardSelected;

    /// <summary>
    /// 卡牌被点击（鼠标左键按下）时触发。
    /// </summary>
    public event Action<CardUI>? OnCardClicked;

    // ============================================================
    // 私有 UI 节点
    // ============================================================
    private Panel _bgPanel = null!;
    private ColorRect _headerRect = null!;
    private ColorRect _manaCircle = null!;
    private Label _manaLabel = null!;
    private Label _nameLabel = null!;
    private ColorRect _artworkRect = null!;
    private Label _artworkLabel = null!;

    // 随从属性（攻击力 / 生命值）
    private ColorRect _attackBg = null!;
    private ColorRect _healthBg = null!;
    private Label _attackLabel = null!;
    private Label _healthLabel = null!;

    // 法术类型标签
    private Label _spellTypeLabel = null!;

    // 描述文字
    private ColorRect _descBg = null!;
    private Label _descLabel = null!;

    // 关键词容器
    private HBoxContainer _keywordContainer = null!;

    // ============================================================
    // 内部状态
    // ============================================================
    private bool _canPlay = true;
    private Tween? _hoverTween;
    private Vector2 _originalPos;
    private bool _hovering;
    private bool _built;

    // ============================================================
    // Godot 生命周期
    // ============================================================

    /// <summary>
    /// 程序化构建卡牌的所有子 UI 节点。
    /// 在进入场景树时调用，所有 Control / Label / ColorRect 由此创建。
    /// </summary>
    public override void _Ready()
    {
        float s = UIScaler.Instance?.GetScaleFactor() ?? 1.0f;
        Vector2 cardSize = new(DESIGN_WIDTH * s, DESIGN_HEIGHT * s);

        CustomMinimumSize = cardSize;
        Size = cardSize;
        MouseFilter = MouseFilterEnum.Stop;

        BuildBackground(s, cardSize);
        BuildHeader(s, cardSize);
        BuildManaCrystal(s);
        BuildCardName(s, cardSize);
        BuildArtworkArea(s, cardSize);
        BuildAttackStat(s);
        BuildHealthStat(s, cardSize);
        BuildSpellTypeLabel(s, cardSize);
        BuildDescriptionArea(s, cardSize);
        BuildKeywordContainer(s, cardSize);

        MouseEntered += OnMouseEnteredHandler;
        MouseExited += OnMouseExitedHandler;
        GuiInput += OnGuiInputHandler;

        _built = true;
    }

    // ============================================================
    // UI 构建子方法
    // ============================================================

    /// <summary>
    /// 构建卡牌背景面板（带圆角和边框）。
    /// </summary>
    private void BuildBackground(float s, Vector2 size)
    {
        _bgPanel = new Panel { Size = size, Position = Vector2.Zero };

        var style = new StyleBoxFlat
        {
            BgColor = ClrBg,
            CornerRadiusTopLeft = (int)(6 * s),
            CornerRadiusTopRight = (int)(6 * s),
            CornerRadiusBottomLeft = (int)(6 * s),
            CornerRadiusBottomRight = (int)(6 * s),
            BorderWidthBottom = (int)(1 * s),
            BorderWidthLeft = (int)(1 * s),
            BorderWidthRight = (int)(1 * s),
            BorderWidthTop = (int)(1 * s),
            BorderColor = ClrBorder,
        };
        _bgPanel.AddThemeStyleboxOverride("panel", style);
        AddChild(_bgPanel);
    }

    /// <summary>
    /// 构建顶部深色头部条。
    /// </summary>
    private void BuildHeader(float s, Vector2 size)
    {
        _headerRect = new ColorRect
        {
            Color = ClrHeader,
            Size = new Vector2(size.X, HEADER_H * s),
            Position = Vector2.Zero,
        };
        AddChild(_headerRect);
    }

    /// <summary>
    /// 构建左上角法力水晶（蓝色矩形 + 白色数字）。
    /// </summary>
    private void BuildManaCrystal(float s)
    {
        float d = MANA_DIAMETER * s;
        float m = 4f * s;

        _manaCircle = new ColorRect
        {
            Color = ClrMana,
            Size = new Vector2(d, d),
            Position = new Vector2(m, m),
        };
        AddChild(_manaCircle);

        _manaLabel = new Label
        {
            Size = new Vector2(d, d),
            Position = new Vector2(m, m),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _manaLabel.AddThemeColorOverride("font_color", ClrTextWhite);
        _manaLabel.AddThemeFontSizeOverride("font_size", (int)(14 * s));
        AddChild(_manaLabel);
    }

    /// <summary>
    /// 构建卡牌名称标签（居中于头部区域，白色）。
    /// </summary>
    private void BuildCardName(float s, Vector2 size)
    {
        float x = MANA_DIAMETER * s + 8f * s;
        float w = size.X - x - 4f * s;
        float h = HEADER_H * s;

        _nameLabel = new Label
        {
            Size = new Vector2(w, h),
            Position = new Vector2(x, 0),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            ClipText = true,
        };
        _nameLabel.AddThemeColorOverride("font_color", ClrTextWhite);
        _nameLabel.AddThemeFontSizeOverride("font_size", (int)(11 * s));
        AddChild(_nameLabel);
    }

    /// <summary>
    /// 构建中央卡图占位区域（灰色矩形 + 水印文字）。
    /// </summary>
    private void BuildArtworkArea(float s, Vector2 size)
    {
        float y = HEADER_H * s;
        float h = ARTWORK_H * s;

        _artworkRect = new ColorRect
        {
            Color = ClrArtworkPlaceholder,
            Size = new Vector2(size.X, h),
            Position = new Vector2(0, y),
        };
        AddChild(_artworkRect);

        _artworkLabel = new Label
        {
            Size = new Vector2(size.X, h),
            Position = new Vector2(0, y),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Text = "Odyssey",
        };
        _artworkLabel.AddThemeColorOverride("font_color", new Color("#888870"));
        _artworkLabel.AddThemeFontSizeOverride("font_size", (int)(9 * s));
        AddChild(_artworkLabel);
    }

    /// <summary>
    /// 构建攻击力指示器（卡图区域左下角，白色数字 + 深色底）。
    /// </summary>
    private void BuildAttackStat(float s)
    {
        float w = STATS_W * s;
        float h = STATS_H * s;
        float x = 2f * s;
        float y = (HEADER_H + ARTWORK_H) * s - h - 2f * s;

        _attackBg = new ColorRect
        {
            Color = ClrStatsBg,
            Size = new Vector2(w, h),
            Position = new Vector2(x, y),
        };
        AddChild(_attackBg);

        _attackLabel = new Label
        {
            Size = new Vector2(w, h),
            Position = new Vector2(x, y),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _attackLabel.AddThemeColorOverride("font_color", ClrTextWhite);
        _attackLabel.AddThemeFontSizeOverride("font_size", (int)(12 * s));
        AddChild(_attackLabel);
    }

    /// <summary>
    /// 构建生命值指示器（卡图区域右下角，白色数字 + 深色底）。
    /// </summary>
    private void BuildHealthStat(float s, Vector2 size)
    {
        float w = STATS_W * s;
        float h = STATS_H * s;
        float x = size.X - w - 2f * s;
        float y = (HEADER_H + ARTWORK_H) * s - h - 2f * s;

        _healthBg = new ColorRect
        {
            Color = ClrStatsBg,
            Size = new Vector2(w, h),
            Position = new Vector2(x, y),
        };
        AddChild(_healthBg);

        _healthLabel = new Label
        {
            Size = new Vector2(w, h),
            Position = new Vector2(x, y),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _healthLabel.AddThemeColorOverride("font_color", ClrTextWhite);
        _healthLabel.AddThemeFontSizeOverride("font_size", (int)(12 * s));
        AddChild(_healthLabel);
    }

    /// <summary>
    /// 构建法术类型标签（覆盖卡图区域，显示"法术"字样）。
    /// </summary>
    private void BuildSpellTypeLabel(float s, Vector2 size)
    {
        float y = HEADER_H * s;
        float h = ARTWORK_H * s;

        _spellTypeLabel = new Label
        {
            Size = new Vector2(size.X, h),
            Position = new Vector2(0, y),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Text = "法术",
            Visible = false,
        };
        _spellTypeLabel.AddThemeColorOverride("font_color", new Color("#ccccaa"));
        _spellTypeLabel.AddThemeFontSizeOverride("font_size", (int)(14 * s));
        AddChild(_spellTypeLabel);
    }

    /// <summary>
    /// 构建描述文字区域（卡图下方，深色底 + 浅色小字）。
    /// </summary>
    private void BuildDescriptionArea(float s, Vector2 size)
    {
        float y = (HEADER_H + ARTWORK_H) * s;
        float h = DESIGN_HEIGHT * s - y - KEYWORD_H * s;

        _descBg = new ColorRect
        {
            Color = ClrDescBg,
            Size = new Vector2(size.X, h),
            Position = new Vector2(0, y),
        };
        AddChild(_descBg);

        _descLabel = new Label
        {
            Size = new Vector2(size.X - 8f * s, h - 4f * s),
            Position = new Vector2(4f * s, y + 2f * s),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            ClipText = true,
        };
        _descLabel.AddThemeColorOverride("font_color", ClrDescText);
        _descLabel.AddThemeFontSizeOverride("font_size", (int)(8 * s));
        AddChild(_descLabel);
    }

    /// <summary>
    /// 构建关键词徽章横排容器（卡牌底部）。
    /// </summary>
    private void BuildKeywordContainer(float s, Vector2 size)
    {
        float y = DESIGN_HEIGHT * s - KEYWORD_H * s;
        float h = KEYWORD_H * s;

        _keywordContainer = new HBoxContainer
        {
            Size = new Vector2(size.X - 4f * s, h),
            Position = new Vector2(2f * s, y),
        };
        _keywordContainer.AddThemeConstantOverride("separation", (int)(2 * s));
        AddChild(_keywordContainer);
    }

    // ============================================================
    // 公共方法
    // ============================================================

    /// <summary>
    /// 将卡牌数据绑定到 UI，刷新所有标签。
    /// </summary>
    /// <param name="card">运行时卡牌实例（Minion 或 Spell）</param>
    public void SetCard(Card.Card card)
    {
        Card = card;
        if (card == null || !_built)
        {
            return;
        }

        float s = UIScaler.Instance?.GetScaleFactor() ?? 1.0f;

        // 法力消耗
        _manaLabel.Text = card.Cost.ToString();

        // 卡牌名称
        _nameLabel.Text = card.CardName;

        // 卡图区域：有真实资源时隐藏占位文字
        _artworkLabel.Visible = card.Data.Artwork == null;

        // 根据类型切换显示模式
        if (card.Type == CardType.Minion)
        {
            ShowMinionLayout(card, s);
        }
        else
        {
            ShowSpellLayout(card, s);
        }

        // 描述文字
        _descLabel.Text = card.Data.Description ?? string.Empty;

        // 关键词（仅随从）
        RebuildKeywordLabels(card, s);
    }

    /// <summary>
    /// 设置卡牌是否可打出。不可打出时整体灰化。
    /// </summary>
    /// <param name="canPlay">是否有足够法力值打出</param>
    public void SetCanPlay(bool canPlay)
    {
        CanPlay = canPlay;
        _canPlay = canPlay;
        Modulate = canPlay ? Colors.White : ClrCannotPlay;
    }

    /// <summary>
    /// 选中卡牌：切换高亮状态，上移产生抬起效果。
    /// </summary>
    public void Select()
    {
        IsSelected = true;
        Position = new Vector2(Position.X, Position.Y - HOVER_LIFT);
    }

    /// <summary>
    /// 取消选中：清除高亮状态，恢复原位置。
    /// </summary>
    public void Deselect()
    {
        IsSelected = false;
        Position = new Vector2(Position.X, Position.Y + HOVER_LIFT);
    }

    // ============================================================
    // 布局切换
    // ============================================================

    /// <summary>
    /// 随从布局：显示攻防属性，隐藏法术标签。
    /// </summary>
    private void ShowMinionLayout(Card.Card card, float s)
    {
        _attackLabel.Text = card.Data.Attack.ToString();
        _healthLabel.Text = card.Data.Health.ToString();

        _attackBg.Visible = true;
        _healthBg.Visible = true;
        _attackLabel.Visible = true;
        _healthLabel.Visible = true;
        _spellTypeLabel.Visible = false;
    }

    /// <summary>
    /// 法术布局：隐藏攻防属性，显示"法术"标签。
    /// </summary>
    private void ShowSpellLayout(Card.Card card, float s)
    {
        _attackBg.Visible = false;
        _healthBg.Visible = false;
        _attackLabel.Visible = false;
        _healthLabel.Visible = false;
        _spellTypeLabel.Visible = true;
    }

    // ============================================================
    // 关键词渲染
    // ============================================================

    /// <summary>
    /// 根据卡牌数据重建关键词徽章列表。
    /// 仅随从牌会显示关键词，法术牌清空关键词区域。
    /// </summary>
    private void RebuildKeywordLabels(Card.Card card, float s)
    {
        // 清空旧徽章
        foreach (Node child in _keywordContainer.GetChildren())
        {
            child.QueueFree();
        }

        if (card.Type != CardType.Minion)
        {
            return;
        }

        var keywords = card.Data.Keywords;
        if (keywords == null || keywords.Count == 0)
        {
            return;
        }

        foreach (Keyword kw in keywords)
        {
            (string? text, Color color) = ResolveKeyword(kw);
            if (string.IsNullOrEmpty(text))
            {
                continue;
            }

            var kwLabel = new Label
            {
                Text = text,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            kwLabel.AddThemeColorOverride("font_color", color);
            kwLabel.AddThemeFontSizeOverride("font_size", (int)(7 * s));
            _keywordContainer.AddChild(kwLabel);
        }
    }

    /// <summary>
    /// 将 Keyword 枚举映射为中文显示文本和主题颜色。
    /// </summary>
    private static (string? text, Color color) ResolveKeyword(Keyword keyword)
    {
        return keyword switch
        {
            Keyword.Charge => ("冲锋", ClrCharge),
            Keyword.Taunt => ("嘲讽", ClrTaunt),
            Keyword.Battlecry => ("战吼", ClrBattlecry),
            Keyword.Deathrattle => ("亡语", ClrDeathrattle),
            Keyword.Windfury => ("风怒", ClrWindfury),
            _ => (null, Colors.White),
        };
    }

    // ============================================================
    // 交互事件
    // ============================================================

    /// <summary>
    /// 鼠标进入卡牌区域：放大至 1.05 倍、上移、提升层级。
    /// </summary>
    private void OnMouseEnteredHandler()
    {
        _hovering = true;
        float s = UIScaler.Instance?.GetScaleFactor() ?? 1.0f;
        _originalPos = Position;

        KillHoverTween();

        _hoverTween = CreateTween().SetParallel(true);
        _hoverTween.TweenProperty(this, "scale", new Vector2(1.05f, 1.05f), 0.15f);
        _hoverTween.TweenProperty(this, "position",
            _originalPos - new Vector2(0, HOVER_LIFT * s), 0.15f);
        ZIndex = 1;
    }

    /// <summary>
    /// 鼠标离开卡牌区域：恢复原始大小、位置和层级。
    /// </summary>
    private void OnMouseExitedHandler()
    {
        _hovering = false;

        KillHoverTween();

        _hoverTween = CreateTween().SetParallel(true);
        _hoverTween.TweenProperty(this, "scale", Vector2.One, 0.15f);
        _hoverTween.TweenProperty(this, "position", _originalPos, 0.15f);
        ZIndex = 0;
    }

    /// <summary>
    /// 处理 GUI 输入事件（仅响应鼠标左键点击）。
    /// 点击时触发 <see cref="OnCardClicked"/> 事件并播放高亮闪烁。
    /// </summary>
    private void OnGuiInputHandler(InputEvent @event)
    {
        if (@event is InputEventMouseButton mb
            && mb.ButtonIndex == MouseButton.Left
            && mb.Pressed)
        {
            OnCardClicked?.Invoke(this);
            OnCardSelected?.Invoke(this);
            FlashHighlight();
            AcceptEvent();
        }
    }

    /// <summary>
    /// 点击反馈：短暂闪白后恢复至当前调制色。
    /// </summary>
    private void FlashHighlight()
    {
        var flash = new Color(1.3f, 1.3f, 1.3f, 1f);
        Color target = _canPlay ? Colors.White : ClrCannotPlay;

        var t = CreateTween();
        t.TweenProperty(this, "modulate", flash, 0.08f);
        t.TweenProperty(this, "modulate", target, 0.15f);
    }

    /// <summary>
    /// 安全终止并释放悬停 Tween。
    /// </summary>
    private void KillHoverTween()
    {
        if (_hoverTween != null && _hoverTween.IsValid())
        {
            _hoverTween.Kill();
        }
    }
}
