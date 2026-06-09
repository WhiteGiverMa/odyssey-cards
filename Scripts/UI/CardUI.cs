#nullable enable
using System;
using Godot;
using OdysseyCards.Core;
using OdysseyCards.Infrastructure;
using Loc = OdysseyCards.Localization.Localization;

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
	public const float DESIGN_WIDTH = 120f;
	public const float DESIGN_HEIGHT = 180f;
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
    private static readonly Color ClrAmbush = new("#cc6644");
    private static readonly Color ClrImpact = new("#cccc66");
    private static readonly Color ClrActionCost = new("#cc3333");

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
	/// <summary>
	/// 卡牌被点击（鼠标左键按下）时触发。
	/// </summary>
	public event Action<CardUI>? OnCardClicked;

    /// <summary>
    /// 卡牌被右键点击（取消选中）时触发。
    /// </summary>
	public event Action<CardUI>? OnCardRightClicked;

    /// <summary>
    /// 移动端拖拽开始时触发（手指移动超过 DragThreshold 时）。
    /// HandUI 监听此事件以自动进入选择模式（跳过二次点击）。
    /// </summary>
    public event Action<CardUI>? OnMobileDragBegan;

    /// <summary>
    /// 纯展示模式：禁用所有战斗交互（拖拽、选中、拾起）。
    /// 设置为 true 后，卡牌只作为视觉元素显示，不响应任何鼠标输入。
    /// 外部可通过包裹的按钮或其他控件自行处理点击。
    /// </summary>
    public bool DisplayOnly { get; set; }

    /// <summary>
    /// 阻止拖拽：不进入 StartDrag 流程，但仍触发 OnCardClicked 用于手牌选择模式。
    /// 与 DisplayOnly 不同——DisplayOnly 完全禁用交互，PreventDrag 仅阻止拖拽副作用。
    /// </summary>
    public bool PreventDrag { get; set; }

    /// <summary>
    /// 当前是否处于拖拽状态。HandUI 通过此属性在拖拽期间抑制悬停效果。
    /// </summary>
    public bool IsDragging => _isDragging;

    /// <summary>
    /// 最后一次左键点击的全局坐标（来自 InputEventMouseButton）。
    /// 拖拽流程使用此坐标而非 GetGlobalMousePosition()，确保合成点击和帧时序边界下的位置一致性。
    /// </summary>
    public Vector2 LastClickGlobalPosition { get; private set; }

    /// <summary>
    /// 卡牌在拖拽中左键松开时触发。参数为卡牌 UI 和松开位置的全局坐标。
    /// 接收方根据松开位置判断：有效目标→打出，无效→取消（等效右键）。
    /// </summary>
    public event Action<CardUI, Vector2>? OnCardDropped;

    /// <summary>
    /// 卡牌在拖拽中逐帧触发。参数为卡牌 UI 和当前全局坐标。
    /// 接收方（CombatUI）根据位置更新播放区域视觉反馈。
    /// </summary>
    public event Action<CardUI, Vector2>? OnDragMove;

	// ============================================================
	// 私有 UI 节点
	// ============================================================
	private Panel _bgPanel = null!;
	private ColorRect _headerRect = null!;
	private ColorRect _manaCircle = null!;
	private Label _manaLabel = null!;
	private ColorRect _actionCostBg = null!;
	private Label _actionCostLabel = null!;
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
    private bool _isDragging;
    private Vector2 _dragOffset;
    private Vector2 _dragStartScreenPos;
    private bool _hasDragged;
    /// <summary>点击选中模式：用户快速点击（松手无拖拽位移）后进入，卡片跟随鼠标但不响应松手掉落。</summary>
    private bool _clickSelectMode;
    /// <summary>点击选中跟随鼠标时，是否仍向外广播移动事件（无目标牌需要用它更新播放区高亮）。</summary>
    private bool _emitMoveWhileClickFollowing;
    private const float DragThresholdDesktop = 10f;
    private const float DragThresholdMobile = 20f;
    private float DragThreshold => MobileInputRouter.IsMobile ? DragThresholdMobile : DragThresholdDesktop;

    /// <summary>
    /// 上一帧移动端主触控是否仍处于按下状态。
    /// 用于从 Router 的当前态推导“本帧刚松手”，避免全局共享 release 标记竞争消费。
    /// </summary>
    private bool _wasMobileTouchActive;
    /// <summary>上一帧左键是否按下——用于检测松手事件（clickSelectMode 中松手通知 CombatUI）。</summary>
    private bool _wasLeftDownLastFrame;
    /// <summary>上一帧右键是否按下——用于桌面端拖拽中右键取消（MouseFilter=Ignore 时 GuiInput 不可达）。</summary>
    private bool _wasRightDownLastFrame;
    /// <summary>卡牌从拾取位置移动超过 5px 后置 true——区分点击选中（不触发掉落）与拖拽松手（触发掉落）。</summary>
    private bool _hasMovedFromOrigin;
    /// <summary>BeginPointerFollowFrom 调用时的卡牌位置——用于移动追踪比较基准。</summary>
    private Vector2 _pointerFollowStartPos;
    private Tween? _hoverTween;
	private bool _isHoverEffectActive;
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
		MouseFilter = DisplayOnly ? MouseFilterEnum.Ignore : MouseFilterEnum.Stop;

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

		if (!DisplayOnly)
		{
			GuiInput += OnGuiInputHandler;
		}

		_built = true;

		// 如果卡牌数据在 _Ready 之前已被设定（程序化创建时的常见情况），
		// 此时 UI 刚构建完，立即回填数据。
		if (Card != null)
		{
			var pendingCard = Card;
			Card = null;
			SetCard(pendingCard);
		}

		// 所有子控件的鼠标事件穿透到 CardUI，确保整张卡牌可点击
		foreach (var child in GetChildren())
		{
			if (child is Control ctrl)
				ctrl.MouseFilter = MouseFilterEnum.Ignore;
		}
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
	/// 构建左上角法力水晶（蓝色矩形 + 白色数字）和右下角行动花费（红色小矩形 + 白色数字）。
	/// </summary>
	private void BuildManaCrystal(float s)
	{
		float d = MANA_DIAMETER * s;
		float m = 4f * s;

		// 法力水晶（蓝色底）
		_manaCircle = new ColorRect
		{
			Color = ClrMana,
			Size = new Vector2(d, d),
			Position = new Vector2(m, m),
		};
		AddChild(_manaCircle);

		// 法力消耗数字
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

		// 行动花费（红色小矩形，位于法力水晶右下角）
		float acSize = d * 0.5f;
		float acX = m + d * 0.55f;
		float acY = m + d * 0.5f;

		_actionCostBg = new ColorRect
		{
			Color = ClrActionCost,
			Size = new Vector2(acSize, acSize),
			Position = new Vector2(acX, acY),
			Visible = false, // 默认隐藏，ShowMinionLayout 中显示
		};
		AddChild(_actionCostBg);

		_actionCostLabel = new Label
		{
			Size = new Vector2(acSize, acSize),
			Position = new Vector2(acX, acY),
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			Visible = false,
		};
		_actionCostLabel.AddThemeColorOverride("font_color", ClrTextWhite);
		_actionCostLabel.AddThemeFontSizeOverride("font_size", (int)(9 * s));
		AddChild(_actionCostLabel);
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
			Text = Loc.T("ui.card.type_spell", "法术"),
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
			AutowrapMode = TextServer.AutowrapMode.Word,
			ClipText = true,
		};
		_descLabel.AddThemeColorOverride("font_color", ClrDescText);
		_descLabel.AddThemeFontSizeOverride("font_size", Mathf.RoundToInt(7 * s));
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
		_manaLabel.Text = card.GetEffectiveCost().ToString();

		// 行动花费（随从始终显示，法术隐藏）
		_actionCostLabel.Text = card.ActionCost.ToString();

		// 卡牌名称（本地化）
		_nameLabel.Text = card.GetLocalizedName();

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

		// 描述文字（本地化）
		_descLabel.Text = card.GetLocalizedDescription();

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
	/// 处理 GUI 输入事件：仅左键按下开始拖拽。
	/// 拖拽中 MouseFilter 设为 Ignore，使后续点击穿透到下层目标。
	/// 右键取消由 <see cref="_Process"/> 轮询处理。
	/// </summary>
	private void OnGuiInputHandler(InputEvent @event)
	{
		if (DisplayOnly) return;

		if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
		{
			if (PreventDrag)
			{
				// 手牌选择模式：不进入拖拽态，仅触发 OnCardClicked 用于选择切换
				OnCardClicked?.Invoke(this);
				AcceptEvent();
				return;
			}

			LastClickGlobalPosition = mb.GlobalPosition;

			// 先通知 HandUI/CombatUI 完成选中与表现切换。
			// 后续是进入无目标跟随、还是进入目标展示态，由 CombatUI 统一决定。
			// 这里不能再无条件 StartDragState，否则会把目标牌重新打回“跟随鼠标”的拖拽态。
			OnCardClicked?.Invoke(this);
			AcceptEvent();
		}
	}

	/// <summary>
	/// 进入基础拖拽状态：计算偏移量、设为鼠标穿透、清除旧偏移。
	/// 仅保留给未来显式调用；当前战斗手牌路径统一由 CombatUI 决定是否进入拖拽/展示态。
	/// </summary>
    private void StartDragState()
    {
        _isDragging = true;
        _hasDragged = false;
        _dragOffset = LastClickGlobalPosition - GlobalPosition;
        _dragStartScreenPos = LastClickGlobalPosition;
        MouseFilter = MouseFilterEnum.Ignore;
        _isHoverEffectActive = false;
        KillHoverTween();
        FlashHighlight();
    }

    /// <summary>
    /// 进入无目标卡牌的指针跟随表现。
    /// 鼠标按住拖拽时直接进入拖拽跟随态；键盘/点击时进入点击跟随态。
    /// </summary>
    /// <param name="globalAnchor">指针与卡牌之间保持不变的锚点</param>
    /// <param name="startAsClickFollow">true=直接进入点击跟随态（键盘/松开后）；false=进入拖拽态（鼠标按住中）</param>
    public void BeginPointerFollowFrom(Vector2 globalAnchor, bool startAsClickFollow)
    {
        _isDragging = true;
        _dragOffset = globalAnchor - GlobalPosition;
        _dragStartScreenPos = globalAnchor;
        MouseFilter = MouseFilterEnum.Ignore;
        _isHoverEffectActive = false;
        KillHoverTween();
        FlashHighlight();

        if (startAsClickFollow)
        {
            // 点击/键盘路径：进入点击选中跟随态，不判定拖拽距离
            _hasDragged = false;
            _clickSelectMode = true;
            _emitMoveWhileClickFollowing = true;
            _pointerFollowStartPos = GlobalPosition;
            _hasMovedFromOrigin = false;
        }
        else
        {
            // 鼠标按住拖拽路径：直接标记已拖拽，跳过距离阈值
            _hasDragged = true;
            _clickSelectMode = false;
            _emitMoveWhileClickFollowing = false;
            _hasMovedFromOrigin = true; // 拖拽路径始终视为已移动
        }
    }

    /// <summary>
    /// 进入目标选择展示态：卡牌不再跟随鼠标，移动到统一的展示位置。
    /// </summary>
    public void PresentForTargeting(Vector2 globalCenter, float targetScale)
    {
        CancelDragSilent();
        MouseFilter = MouseFilterEnum.Ignore;
        ZIndex = 10;

        Vector2 targetSize = Size * targetScale;
        Vector2 targetTopLeft = globalCenter - targetSize * 0.5f;
        Scale = Vector2.One * targetScale;

        var tween = CreateTween();
        tween.SetTrans(Tween.TransitionType.Cubic);
        tween.SetEase(Tween.EaseType.Out);
        tween.TweenProperty(this, "global_position", targetTopLeft, 0.12f);
    }

    /// <summary>
    /// 退出拖拽状态（静默，不触发事件）。
    /// 用于切换卡牌时直接清理旧卡，避免触发 RefreshHand 链条。
    /// </summary>
    public void CancelDragSilent()
    {
        _isDragging = false;
        _hasDragged = false;
        _clickSelectMode = false;
        _emitMoveWhileClickFollowing = false;
        _isHoverEffectActive = false;
        KillHoverTween();
        MouseFilter = MouseFilterEnum.Stop;
        OffsetTop = 0;
    }

    /// <summary>
    /// 退出拖拽状态：恢复鼠标响应、通知取消。
    /// </summary>
    public void CancelDrag()
    {
        if (!_isDragging) return;
        CancelDragSilent();
        OnCardRightClicked?.Invoke(this);
    }

	/// <summary>
	/// 拖拽时每帧跟随鼠标/触控移动，并轮询取消和松开事件。
	/// 由于拖拽中 MouseFilter=Ignore，GuiInput 不会收到事件，故在此轮询。
	///
	/// 桌面端（交互等效性，参考杀戮尖塔2 NMouseCardPlay 的区域+按键状态模型）：
	///   快速点击选中 → 卡片保持原位 → 点击有效目标打出（clickSelectMode）
	///   按住并移动超过阈值 → 卡片跟随鼠标 → 松手打出/取消（drag-drop）
	///   右键取消 ≡ 拖拽中松开在无效区域
	///
	/// 移动端（MobileInputHelper.IsMobile）：
	///   仅拖拽模式，无 clickSelectMode
	///   手指移动超过阈值（20f）→ 卡片跟随手指 → 松手打出/取消
	///   无右键取消（拖到无效区域 = 取消）
	/// </summary>
	public override void _Process(double delta)
	{
		if (SceneLifecycleGuard.ShouldSkip(this)) return;
		if (DisplayOnly || !_isDragging) return;

		if (MobileInputRouter.IsMobile)
		{
			MobileDragProcess();
			return;
		}

		// ==================== 桌面端鼠标交互（STS2 对齐） ====================
		// 核心原则：不使用像素距离阈值区分点击/拖拽。
		// 用 _hasMovedFromOrigin 区分「点击选中」（卡牌未移动→不触发掉落）与「拖拽松手」（卡牌移动过→触发掉落）。
		// 每次松手都通知 CombatUI，由它根据落点决定打出/取消/忽略（对齐 STS2）。

		bool leftDown = Input.IsMouseButtonPressed(MouseButton.Left);
		bool leftReleasedThisFrame = _wasLeftDownLastFrame && !leftDown;
		_wasLeftDownLastFrame = leftDown;

		// 右键取消（桌面端安全网：MouseFilter=Ignore 时 GuiInput 不可达，轮询处理）
		bool rightDown = Input.IsMouseButtonPressed(MouseButton.Right);
		if (!_wasRightDownLastFrame && rightDown)
		{
			CancelDrag();
			_wasRightDownLastFrame = true;
			return;
		}
		_wasRightDownLastFrame = rightDown;

		Vector2 mousePosition = GetGlobalMousePosition();

		// 卡牌跟随鼠标 + 移动追踪
		if (_clickSelectMode || _hasDragged)
		{
			Vector2 newPos = mousePosition - _dragOffset;
			if (!_hasMovedFromOrigin && newPos.DistanceSquaredTo(_pointerFollowStartPos) > 25f) // 5px² 阈值
			{
				_hasMovedFromOrigin = true;
			}
			GlobalPosition = newPos;
			OnDragMove?.Invoke(this, mousePosition);
		}

		// 左键松开处理
		if (!leftDown)
		{
			if (_hasDragged)
			{
				// 拖拽松手 → 转入 clickSelectMode 并通知 CombatUI
				Vector2 dropPos = mousePosition;
				_hasDragged = false;
				_clickSelectMode = true;
				_emitMoveWhileClickFollowing = true;
				OnCardDropped?.Invoke(this, dropPos);
			}
			else if (!_clickSelectMode)
			{
				// 首次松手 → 进入点击选中模式
				_clickSelectMode = true;
				_emitMoveWhileClickFollowing = true;
			}
			else if (leftReleasedThisFrame && _hasMovedFromOrigin)
			{
				// clickSelectMode 中松手（卡牌已移动）→ 通知 CombatUI 根据落点处理
				OnCardDropped?.Invoke(this, mousePosition);
			}
		}
	}

	/// <summary>
	/// 移动端触控拖拽逻辑。
	/// 手指按下后超过阈值（20f）即触发拖拽，松手即掉落或取消。
	/// 无 clickSelectMode，无右键取消。
	/// </summary>
	private void MobileDragProcess()
	{
		var router = MobileInputRouter.Instance;
		if (router.IsTouchActive)
		{
			Vector2 touchPos = router.TouchPosition;

			// 跟踪拖拽距离
			if (!_hasDragged)
			{
				float dist = touchPos.DistanceTo(_dragStartScreenPos);
				if (dist > DragThreshold)
				{
					_hasDragged = true;
					OnMobileDragBegan?.Invoke(this);
				}
			}

			if (_hasDragged)
			{
				// 拖拽模式：卡牌跟随手指，逐帧通知位置
				GlobalPosition = touchPos - _dragOffset;
				OnDragMove?.Invoke(this, touchPos);
			}
		}

		// 手指松开处理
		if (_wasMobileTouchActive && !router.IsTouchActive)
		{
			Vector2 dropScreenPos = router.TouchReleasePosition;
			bool wasDragging = _hasDragged;
			_isDragging = false;
			_hasDragged = false;
			_clickSelectMode = false;
			_emitMoveWhileClickFollowing = false;
			_isHoverEffectActive = false;
			KillHoverTween();
			MouseFilter = MouseFilterEnum.Stop;
			OffsetTop = 0;

			if (wasDragging)
			{
				// 拖拽后松手 → 触发 OnCardDropped
				OnCardDropped?.Invoke(this, dropScreenPos);
			}
			else
			{
				// 快速点击 → 取消（移动端无 clickSelectMode，直接取消拖拽）
				OnCardRightClicked?.Invoke(this);
			}
		}

		_wasMobileTouchActive = router.IsTouchActive;
	}

	/// <summary>
	/// 选中卡牌：切换高亮状态，上移产生抬起效果。
	/// 使用 OffsetTop 而非直接改 Position，避免与 HBoxContainer 布局冲突。
	/// 拖拽中跳过——位置由 _Process 控制，不应额外偏移。
	/// </summary>
	public void Select()
	{
		if (_isDragging) return;
		IsSelected = true;
		_isHoverEffectActive = false;
		KillHoverTween();
		float s = UIScaler.Instance?.GetScaleFactor() ?? 1.0f;
		OffsetTop = -HOVER_LIFT * s;
		ZIndex = 1;
	}

    /// <summary>
    /// 取消选中：清除高亮状态，恢复原位置。
    /// </summary>
    public void Deselect()
    {
        IsSelected = false;
        OffsetTop = 0;
        ZIndex = 0;
    }

    /// <summary>
    /// 设置手牌选择模式的选中/取消高亮。
    /// 选中时：金色调色 + 上移抬起效果。
    /// 取消时：恢复原始颜色和位置。
    /// </summary>
    /// <param name="selected">true=选中高亮，false=取消</param>
    public void SetHandSelectionHighlight(bool selected)
    {
        if (selected)
        {
            _isHoverEffectActive = false;
            KillHoverTween();
            Modulate = new Color(1f, 0.85f, 0.3f, 1f); // golden
            OffsetTop = -15f; // slight lift
            ZIndex = 1;
        }
        else
        {
            Modulate = _canPlay ? Colors.White : ClrCannotPlay;
            OffsetTop = 0f;
            ZIndex = 0;
        }
    }

    /// <summary>
    /// 设置播放区域高亮状态——卡牌拖入播放区域时显示绿色边框反馈。
    /// </summary>
    /// <param name="active">true=在播放区域内，false=离开</param>
    public void SetPlayZoneHighlight(bool active)
    {
        if (_bgPanel == null) return;

        var style = new StyleBoxFlat
        {
            BgColor = ClrBg,
            CornerRadiusTopLeft = (int)(6 * (UIScaler.Instance?.GetScaleFactor() ?? 1f)),
            CornerRadiusTopRight = (int)(6 * (UIScaler.Instance?.GetScaleFactor() ?? 1f)),
            CornerRadiusBottomLeft = (int)(6 * (UIScaler.Instance?.GetScaleFactor() ?? 1f)),
            CornerRadiusBottomRight = (int)(6 * (UIScaler.Instance?.GetScaleFactor() ?? 1f)),
            BorderWidthBottom = active ? 3 : 1,
            BorderWidthLeft = active ? 3 : 1,
            BorderWidthRight = active ? 3 : 1,
            BorderWidthTop = active ? 3 : 1,
            BorderColor = active ? new Color(0.3f, 0.9f, 0.3f, 0.8f) : ClrBorder,
        };
        _bgPanel.AddThemeStyleboxOverride("panel", style);
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

		// 行动花费：随从始终显示红底数字
		_actionCostBg.Visible = true;
		_actionCostLabel.Visible = true;
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
		_actionCostBg.Visible = false;
		_actionCostLabel.Visible = false;
		_spellTypeLabel.Text = card.Type switch
		{
			CardType.Spell => "法术",
			CardType.Domain => "领域",
			_ => "法术"
		};
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
				MouseFilter = MouseFilterEnum.Ignore,
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
        Keyword.Charge => ("闪击", ClrCharge),
			Keyword.Taunt => ("嘲讽", ClrTaunt),
			Keyword.Battlecry => ("战吼", ClrBattlecry),
			Keyword.Deathrattle => ("亡语", ClrDeathrattle),
        Keyword.Windfury => ("风怒", ClrWindfury),
        Keyword.Ambush => ("伏击", ClrAmbush),
        Keyword.Impact => ("冲击", ClrImpact),
        _ => (null, Colors.White),
		};
	}

	// ============================================================
	// 交互事件
	// ============================================================

	/// <summary>
	/// 悬停入效果：仅设置 ZIndex。上浮和缩放由 HandUI.RefreshLayout 统一控制。
	/// </summary>
	public void ApplyHoverEffect()
	{
		if (_isDragging || IsSelected || _isHoverEffectActive) return;
		_isHoverEffectActive = true;
		KillHoverTween();
		ZIndex = 2;
	}

	/// <summary>
	/// 悬停出效果：仅恢复 ZIndex。位置恢复由 RefreshLayout 处理。
	/// </summary>
	public void RemoveHoverEffect()
	{
		if (!_isHoverEffectActive) return;
		_isHoverEffectActive = false;
		KillHoverTween();
		OffsetTop = 0f;
		ZIndex = 0;
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
