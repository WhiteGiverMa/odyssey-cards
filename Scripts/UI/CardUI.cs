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
/// 稀有度标识和描述文本。支持点击选择（无拖拽）与悬停动效，适配手牌布局。
/// </summary>
[Tool]
public partial class CardUI : Control
{
	// ============================================================
	// 尺寸常量（设计基准 140×196 = 5:7，运行时通过 UIScaler 缩放）
	// ============================================================
	public const float DESIGN_WIDTH = HandLayout.CardWidth;
	public const float DESIGN_HEIGHT = HandLayout.CardHeight;
	private const float MANA_DIAMETER = 24f;
	private const float HEADER_H = 30f;
	private const float ARTWORK_H = 88f;
	private const float STATS_W = 34f;
	private const float STATS_H = 24f;
	private const float RARITY_H = 22f;
	private const float MIN_ARTWORK_H = 48f;
	private const float DESC_SIDE_PADDING = 4f;
	private const float DESC_TOP_BOTTOM_PADDING = 2f;
	private const int DESC_BASE_FONT_SIZE = 7;
	private const int DESC_MIN_FONT_SIZE = 5;
	private const int NAME_BASE_FONT_SIZE = 11;
	private const int NAME_MIN_FONT_SIZE = 7;

	// ============================================================
	// 颜色定义
	// ============================================================
	private static readonly Color ClrBg = new("#3a3a30");
	private static readonly Color ClrHeader = new("#2a2a20");
	private static readonly Color ClrMana = new("#4488cc");
	private static readonly Color ClrTextWhite = new("#f0f0e8");
	private static readonly Color ClrDescBg = new("#4a4a40");
	private static readonly Color ClrDescText = new("#ccccaa");
	private static readonly Color ClrStatsBg = new("#1a1a10");
	private static readonly Color ClrCannotPlay = new("#666666");
	private static readonly Color ClrBorder = new("#555540");
	private static readonly Color ClrActionCost = new("#cc3333");
	/// <summary>反制牌待命徽章——青金色，与「已激活」提示形成强视觉标识。</summary>
	private static readonly Color ClrArmedBadge = new("#0e5f6e");

	// 稀有度标识背景
	private static readonly Color ClrRarityBg = new("#1a1a10");

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
	/// 覆盖内部渲染 scale。设为 &gt;0 时 CardUI 用此值替代系统 UIScaler.uiScale
	/// 计算所有控件尺寸和字体大小。用于预览特写等需要非标准尺寸渲染的场景。
	/// 设为 0（默认）时使用系统 uiScale。
	/// </summary>
	public float RenderScaleOverride { get; set; }

	/// <summary>
	/// 阻止拖拽：不进入 StartDrag 流程，但仍触发 OnCardClicked 用于手牌选择模式。
	/// 与 DisplayOnly 不同——DisplayOnly 完全禁用交互，PreventDrag 仅阻止拖拽副作用。
	/// </summary>
	public bool PreventDrag { get; set; }

	/// <summary>
	/// 当前是否处于指针跟随交互；移动端未跨过阈值的触碰仅用于展开预览。
	/// </summary>
	public bool IsDragging => _isDragging;
	internal bool IsPointerDragging => _dragFsm.HasDragged;
	internal bool IsClickFollowing => _isDragging && _pickupReleased && !_dragFsm.HasDragged;

	internal float FaceRenderScale => _uiScale;
	internal float Elevation { get; private set; }

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

	/// <summary>指针未超过拖拽阈值便松手，继续以第二击选定目标。</summary>
	internal event Action<CardUI>? OnTargetingClickReleased;

	// ============================================================
	// 私有 UI 节点
	// ============================================================
	private Panel _bgPanel = null!;
	private Panel _shadowPanel = null!;
	private StyleBoxFlat _shadowStyle = null!;
	private StyleBoxFlat _faceStyle = null!;
	private ColorRect _headerRect = null!;
	private ColorRect _manaCircle = null!;
	private Label _manaLabel = null!;
	private ColorRect _actionCostBg = null!;
	private Label _actionCostLabel = null!;
	private Label _nameLabel = null!;
	private CardArtworkView _artworkView = null!;
	private TacticalBadgeOverlay _tacticalBadge = null!;

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

	// 稀有度标识（底部居中彩色数字）
	private ColorRect _rarityBg = null!;
	private Label _rarityLabel = null!;

	// 反制牌「已激活」徽章
	private ColorRect _armedBadgeBg = null!;
	private Label _armedBadgeLabel = null!;

	// ============================================================
	// 内部状态
	// ============================================================
	private readonly InteractionFsm _dragFsm = new();
	private bool _handSelectionHighlighted;
	private bool _trackingMobilePress;
	private bool _isDragging;
	private Vector2 _localGrabOffset;
	private Vector2 _dragAnchor;
	private Vector2 _lastPointer;
	private float _dragDelta;
	private bool _pointerWasDown;
	private bool _pickupReleased;
	private bool _parkOnClickRelease;
	private bool _playZoneActive;
	private float _playZoneGlow;
	private int? _runtimeCost;
	private (Vector2 Center, float Scale)? _targetingPose;
	private bool _built;
	private float _uiScale = 1.0f;
	private Vector2 _cardSize;

	// ============================================================
	// Godot 生命周期
	// ============================================================

	/// <summary>
	/// 程序化构建卡牌的所有子 UI 节点。
	/// 在进入场景树时调用，所有 Control / Label / ColorRect 由此创建。
	/// </summary>
	public override void _Ready()
	{
		float s = RenderScaleOverride > 0f ? RenderScaleOverride
			: UIScaler.Instance?.GetScaleFactor() ?? 1.0f;
		Vector2 cardSize = new(DESIGN_WIDTH * s, DESIGN_HEIGHT * s);
		_uiScale = s;
		_cardSize = cardSize;

		CustomMinimumSize = cardSize;
		Size = cardSize;
		PivotOffset = _cardSize * 0.5f;
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
		BuildRarityIndicator(s, cardSize);
		BuildArmedBadge(s, cardSize);
		BuildTacticalBadge(s, cardSize);

		if (!DisplayOnly)
		{
			GuiInput += OnGuiInputHandler;
		}
		_dragFsm.OnDragMove += OnFsmDragMove;
		_dragFsm.OnDrop += OnFsmDrop;
		_dragFsm.OnCancel += OnFsmCancel;

		_built = true;

		// 如果卡牌数据在 _Ready 之前已被设定（程序化创建时的常见情况），
		// 此时 UI 刚构建完，立即回填数据。
		if (Card != null)
		{
			SetCard(Card);
		}
#if TOOLS
		// 编辑器预览模式：创建模拟卡牌数据，使卡牌在 Godot 编辑器中可视化。
		// #if TOOLS 确保此代码完全从发布版剥离，零运行时开销。
		else if (Engine.IsEditorHint())
		{
			PopulateEditorPreview();
		}
#endif

		// 所有子控件的鼠标事件穿透到 CardUI，确保整张卡牌可点击
		foreach (var child in GetChildren())
		{
			if (child is Control ctrl)
				ctrl.MouseFilter = MouseFilterEnum.Ignore;
		}
	}

	public override void _EnterTree()
	{
		if (UIScaler.Instance is UIScaler scaler)
		{
			scaler.OnCardDescriptionSettingsChanged += OnCardDescriptionSettingsChanged;
			scaler.OnRarityColorSchemeChanged += OnRarityColorSchemeChanged;
			scaler.OnEmojiIconsChanged += OnEmojiIconsChanged;
		}
	}

	public override void _ExitTree()
	{
		// Reparent 也会出入树；保留同一张卡的牌面、样式和本地 FSM，只解绑外部事件。
		if (UIScaler.Instance is UIScaler scaler)
		{
			scaler.OnCardDescriptionSettingsChanged -= OnCardDescriptionSettingsChanged;
			scaler.OnRarityColorSchemeChanged -= OnRarityColorSchemeChanged;
			scaler.OnEmojiIconsChanged -= OnEmojiIconsChanged;
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
		int radius = Mathf.Max(1, Mathf.RoundToInt(6f * s));
		_shadowPanel = new Panel { Size = size, MouseFilter = MouseFilterEnum.Ignore, Visible = false };
		_shadowStyle = new StyleBoxFlat
		{
			BgColor = Colors.Transparent,
			CornerRadiusTopLeft = radius,
			CornerRadiusTopRight = radius,
			CornerRadiusBottomLeft = radius,
			CornerRadiusBottomRight = radius,
		};
		_shadowPanel.AddThemeStyleboxOverride("panel", _shadowStyle);
		AddChild(_shadowPanel);

		_bgPanel = new Panel { Size = size, MouseFilter = MouseFilterEnum.Ignore };
		_faceStyle = new StyleBoxFlat
		{
			BgColor = ClrBg,
			CornerRadiusTopLeft = radius,
			CornerRadiusTopRight = radius,
			CornerRadiusBottomLeft = radius,
			CornerRadiusBottomRight = radius,
			BorderWidthBottom = (int)(1 * s),
			BorderWidthLeft = (int)(1 * s),
			BorderWidthRight = (int)(1 * s),
			BorderWidthTop = (int)(1 * s),
			BorderColor = ClrBorder,
		};
		_bgPanel.AddThemeStyleboxOverride("panel", _faceStyle);
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
			HorizontalAlignment = GetNameAlignment(),
			VerticalAlignment = VerticalAlignment.Center,
		};
		_nameLabel.AddThemeColorOverride("font_color", ClrTextWhite);
		_nameLabel.AddThemeFontSizeOverride("font_size", Mathf.RoundToInt(NAME_BASE_FONT_SIZE * s));
		AddChild(_nameLabel);
	}

	/// <summary>
	/// 构建中央卡图区域（程序化生成卡面，0 美术资产）。
	/// </summary>
	private void BuildArtworkArea(float s, Vector2 size)
	{
		float y = HEADER_H * s;
		float h = ARTWORK_H * s;

		_artworkView = new CardArtworkView
		{
			Size = new Vector2(size.X, h),
			Position = new Vector2(0, y),
			MouseFilter = MouseFilterEnum.Ignore,
		};
		AddChild(_artworkView);
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
		_spellTypeLabel.AddThemeColorOverride("font_color", new Color(0.95f, 0.95f, 0.9f, 0.85f));
		_spellTypeLabel.AddThemeFontSizeOverride("font_size", (int)(9 * s));
		AddChild(_spellTypeLabel);
	}

	/// <summary>
	/// 构建描述文字区域（卡图下方，深色底 + 浅色小字）。
	/// </summary>
	private void BuildDescriptionArea(float s, Vector2 size)
	{
		float y = (HEADER_H + ARTWORK_H) * s;
		float h = DESIGN_HEIGHT * s - y - RARITY_H * s;

		_descBg = new ColorRect
		{
			Color = ClrDescBg,
			Size = new Vector2(size.X, h),
			Position = new Vector2(0, y),
		};
		AddChild(_descBg);

		_descLabel = new Label
		{
			Size = new Vector2(size.X - DESC_SIDE_PADDING * 2f * s, h - DESC_TOP_BOTTOM_PADDING * 2f * s),
			Position = new Vector2(DESC_SIDE_PADDING * s, y + DESC_TOP_BOTTOM_PADDING * s),
			HorizontalAlignment = GetDescriptionAlignment(),
			VerticalAlignment = VerticalAlignment.Top,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			ClipText = true,
		};
		_descLabel.AddThemeColorOverride("font_color", ClrDescText);
		_descLabel.AddThemeFontSizeOverride("font_size", Mathf.RoundToInt(DESC_BASE_FONT_SIZE * s));
		AddChild(_descLabel);
	}

	/// <summary>
	/// 构建稀有度标识区域（卡牌底部居中彩色数字）。
	/// 显示稀有度数字 0-6，颜色由当前选择的方案决定。
	/// </summary>
	private void BuildRarityIndicator(float s, Vector2 size)
	{
		float y = DESIGN_HEIGHT * s - RARITY_H * s;
		float h = RARITY_H * s;
		float margin = 4f * s;
		float w = size.X - margin * 2f;

		_rarityBg = new ColorRect
		{
			Color = ClrRarityBg,
			Size = new Vector2(w, h),
			Position = new Vector2(margin, y),
		};
		AddChild(_rarityBg);

		_rarityLabel = new Label
		{
			Size = new Vector2(w, h),
			Position = new Vector2(margin, y),
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			Text = "",
			MouseFilter = MouseFilterEnum.Ignore,
		};
		_rarityLabel.AddThemeFontSizeOverride("font_size", Mathf.RoundToInt(10 * s));
		AddChild(_rarityLabel);
	}

	/// <summary>构建反制牌待命徽章（默认隐藏）。</summary>
	private void BuildArmedBadge(float s, Vector2 size)
	{
		float w = 54f * s;
		float h = HEADER_H * s;
		_armedBadgeBg = new ColorRect
		{
			Color = ClrArmedBadge,
			Size = new Vector2(w, h),
			Position = new Vector2(size.X - w, 0),
			Visible = false,
		};
		AddChild(_armedBadgeBg);

		_armedBadgeLabel = new Label
		{
			Size = new Vector2(w, h),
			Position = new Vector2(size.X - w, 0),
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			Text = UiSymbolMode.Prefix(Loc.T("ui.combat.counter_armed", "已激活"), "⚡"),
			Visible = false,
		};
		_armedBadgeLabel.AddThemeColorOverride("font_color", new Color(0.55f, 0.95f, 1f));
		_armedBadgeLabel.AddThemeFontSizeOverride("font_size", Mathf.RoundToInt(7 * s));
		AddChild(_armedBadgeLabel);
	}

	/// <summary>构建卡面右上角的战术兵种/嘲讽/守护矢量微标覆盖层。</summary>
	private void BuildTacticalBadge(float s, Vector2 size)
	{
		_tacticalBadge = new TacticalBadgeOverlay(s)
		{
			Name = "TacticalBadge",
			MouseFilter = MouseFilterEnum.Ignore,
			Size = size,
			Position = Vector2.Zero,
		};
		AddChild(_tacticalBadge);
	}

	// ============================================================
	// 公共方法
	// ============================================================

	/// <summary>
	/// 将卡牌数据绑定到 UI，刷新所有标签。
	/// </summary>
	/// <param name="card">运行时卡牌实例（Minion 或 Spell）</param>
	public void SetCard(Card.Card? card)
	{
		if (!ReferenceEquals(Card, card))
		{
			_runtimeCost = null;
		}
		Card = card;
		if (card == null || !_built)
		{
			return;
		}

		// 卡图区域：程序化生成（真实 Artwork 存在时优先）
		_artworkView.Setup(card.Data);
		_tacticalBadge.Bind(card);
		if (card.Type == CardType.Minion)
		{
			ShowMinionLayout();
		}
		else
		{
			ShowSpellLayout();
		}
		UpdateRarityIndicator(card, _uiScale);
		RefreshRuntimeDisplay();
	}

	/// <summary>原地重取费用、描述、实际攻防与待命状态；只在文字长度变化时重测布局。</summary>
	internal void RefreshRuntimeDisplay()
	{
		if (!_built || Card is not { } card)
		{
			return;
		}
		UpdateLabelText(_manaLabel, (_runtimeCost ?? card.GetEffectiveCost()).ToString());
		string name = card.GetLocalizedName();
		if (UpdateLabelText(_nameLabel, name))
		{
			ApplyNameFontSize(_uiScale);
		}

		string description = card.GetLocalizedDescription();
		if (card.Type == CardType.Minion)
		{
			UpdateLabelText(_actionCostLabel, card.ActionCost.ToString());
			UpdateLabelText(_attackLabel, (card.RuntimeAttackSnapshot ?? card.Data.Attack).ToString());
			UpdateLabelText(_healthLabel, (card.RuntimeHealthSnapshot ?? card.Data.Health).ToString());
			description = $"{BoardUI.GetUnitClassName(card.Data.UnitClass)}\n{description}";
		}
		else
		{
			UpdateLabelText(_spellTypeLabel, card.Type switch
			{
				CardType.Domain => Loc.T("card_type.domain", "领域"),
				CardType.Status => Loc.T("card_type.status", "状态"),
				CardType.Countermeasure => Loc.T("card_type.countermeasure", "反制"),
				_ => Loc.T("card_type.spell", "法术"),
			});
		}
		if (UpdateLabelText(_descLabel, description))
		{
			ApplyResponsiveContentLayout();
		}

		bool armed = card.Type == CardType.Countermeasure && card.IsCountermeasureArmed;
		if (_armedBadgeBg.Visible != armed)
		{
			_armedBadgeBg.Visible = armed;
			_armedBadgeLabel.Visible = armed;
		}
		if (armed)
		{
			UpdateLabelText(_armedBadgeLabel, UiSymbolMode.Prefix(Loc.T("ui.combat.counter_armed", "已激活"), "⚡"));
		}
	}

	internal void SetRuntimeCost(int cost)
	{
		if (_runtimeCost != cost)
		{
			_runtimeCost = cost;
			if (_built)
			{
				UpdateLabelText(_manaLabel, cost.ToString());
			}
		}
	}

	private static bool UpdateLabelText(Label label, string text)
	{
		if (label.Text == text)
		{
			return false;
		}
		label.Text = text;
		return true;
	}

#if TOOLS
	/// <summary>
	/// 编辑器预览：用模拟数据填充卡牌 UI，使卡牌在 Godot 编辑器中可视化。
	/// 绕过 Localization 调用（编辑器下 YAML 可能未加载），直接用硬编码数据。
	/// 可在 CardUI 的 _Ready 中被调用，也可在 BoardUI 等预览场景中手动调用。
	/// </summary>
	private void PopulateEditorPreview()
	{
		float s = UIScaler.Instance?.GetScaleFactor() ?? 1.0f;

		// 法力消耗
		_manaLabel.Text = "3";

		// 名字（绕过本地化）
		_nameLabel.Text = "预览卡牌";

		// 攻击力 / 生命值（随从布局）
		if (_attackLabel != null)
		{
			_attackLabel.Text = "5";
			_attackLabel.Visible = true;
		}
		if (_healthLabel != null)
		{
			_healthLabel.Text = "4";
			_healthLabel.Visible = true;
		}

		// 法术类型标签隐藏（预览默认展示随从样式）
		_spellTypeLabel?.Hide();

		// 描述
		_descLabel.Text = "编辑器预览\n调整布局用";

		// 稀有度标识预览
		if (_rarityLabel != null)
		{
			_rarityLabel.Text = "4";
			int scheme = UIScaler.Instance?.RarityColorSchemeIndex ?? 0;
			_rarityLabel.AddThemeColorOverride("font_color",
				Core.RarityColorScheme.GetColor(scheme, CardRarity.Common));
		}

		ApplyResponsiveContentLayout();
	}
#endif

	/// <summary>仅更新费用可用态；选牌高亮与牌面位置不受影响。</summary>
	public void SetCanPlay(bool canPlay)
	{
		if (CanPlay != canPlay)
		{
			CanPlay = canPlay;
			UpdateInteractionTint();
		}
	}

	private void UpdateInteractionTint()
	{
		Modulate = _handSelectionHighlighted ? new Color(1f, 0.85f, 0.3f) : CanPlay ? Colors.White : ClrCannotPlay;
	}

	internal void SetKeyboardFocusTint(bool active)
	{
		if (_built)
		{
			_bgPanel.SelfModulate = active ? new Color(0.72f, 0.85f, 1.4f) : Colors.White;
		}
	}

	/// <summary>卡面真实屏幕中心，不依赖旋转后的包围矩形。</summary>
	public Vector2 ScreenCenter => GetGlobalTransformWithCanvas() * PivotOffset;

	/// <summary>以卡面中心而不是缩放后的左上角定位，缩放和旋转不引入偏移。</summary>
	internal void SetScreenCenter(Vector2 center)
	{
		Position = (GetParentScreenTransform().AffineInverse() * center) - PivotOffset;
	}

	internal void ReparentKeepingScreenPose(Control destination)
	{
		Transform2D screenTransform = GetGlobalTransformWithCanvas();
		Reparent(destination, keepGlobalTransform: false);
		Transform2D local = GetParentScreenTransform().AffineInverse() * screenTransform;
		Rotation = local.Rotation;
		Scale = local.Scale;
		Position = (local * PivotOffset) - PivotOffset;
	}

	private Transform2D GetParentScreenTransform()
	{
		return GetParent() is CanvasItem parent ? parent.GetGlobalTransformWithCanvas() : GetCanvasTransform();
	}

	private void OnGuiInputHandler(InputEvent @event)
	{
		if (DisplayOnly || Engine.IsEditorHint() || SceneLifecycleGuard.ShouldSkip(this)
			|| @event is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mouse)
		{
			return;
		}
		LastClickGlobalPosition = mouse.GlobalPosition;
		if (!PreventDrag && MobileInputRouter.IsMobile && OnMobileDragBegan != null)
		{
			_trackingMobilePress = true;
		}
		AcceptEvent();
		OnCardClicked?.Invoke(this);
	}

	/// <summary>记下抓点在未缩放卡面上的局部偏移，再由 FSM 管理原始指针与松手判定。</summary>
	public void BeginPointerFollowFrom(Vector2 globalAnchor, bool startAsClickFollow, bool parkOnClickRelease = false)
	{
		_targetingPose = null;
		_trackingMobilePress = false;
		_isDragging = true;
		_parkOnClickRelease = parkOnClickRelease;
		_pointerWasDown = Input.IsMouseButtonPressed(MouseButton.Left);
		_pickupReleased = !_pointerWasDown;
		_dragAnchor = globalAnchor;
		_lastPointer = globalAnchor;
		_localGrabOffset = (GetGlobalTransformWithCanvas().AffineInverse() * globalAnchor) - PivotOffset;
		MouseFilter = MouseFilterEnum.Ignore;
		_dragFsm.PickUpCard(globalAnchor, isClickSelect: startAsClickFollow, isMobile: MobileInputRouter.IsMobile);
	}

	/// <summary>点击选中后固定展示，保持中心补间；右键或换牌可以从任意中间姿态归还。</summary>
	public void PresentForTargeting(Vector2 globalCenter, float targetScale)
	{
		CancelDragSilent();
		MouseFilter = MouseFilterEnum.Ignore;
		_targetingPose = (globalCenter, targetScale);
	}

	/// <summary>停止指针和目标展示，不改变当前位置、缩放、角度或底影。</summary>
	public void CancelDragSilent()
	{
		_dragFsm.ForceReset();
		_trackingMobilePress = false;
		_isDragging = false;
		_targetingPose = null;
		SetPlayZoneHighlight(false);
		MouseFilter = DisplayOnly ? MouseFilterEnum.Ignore : MouseFilterEnum.Stop;
	}

	public override void _Process(double delta)
	{
		if (DisplayOnly || SceneLifecycleGuard.ShouldSkip(this))
		{
			return;
		}
		float seconds = (float)delta;
		UpdatePlayZoneGlow(seconds);
		if (_targetingPose is { } targeting)
		{
			AnimateTargeting(targeting.Center, targeting.Scale, seconds);
			return;
		}
		if (!_trackingMobilePress && !_isDragging)
		{
			return;
		}

		Vector2 pointer = EvalGateway.GetEffectiveMousePosition(this);
		bool down = Input.IsMouseButtonPressed(MouseButton.Left);
		if (_trackingMobilePress)
		{
			if (!down || PreventDrag || !IsVisibleInTree())
			{
				_trackingMobilePress = false;
			}
			else if (pointer.DistanceTo(LastClickGlobalPosition) > InteractionFsm.PointerDragThreshold)
			{
				_trackingMobilePress = false;
				OnMobileDragBegan?.Invoke(this);
			}
		}
		if (!_isDragging)
		{
			return;
		}
		_dragDelta = seconds;
		bool firstRelease = !_pickupReleased && _pointerWasDown && !down;
		_pointerWasDown = down;
		_dragFsm.Tick(pointer, down, Input.IsMouseButtonPressed(MouseButton.Right), GetViewportRect().Size.Y, _dragAnchor.Y);
		if (_isDragging && !down)
		{
			if (firstRelease)
			{
				_pickupReleased = true;
				if (_parkOnClickRelease)
				{
					OnTargetingClickReleased?.Invoke(this);
				}
			}
			if (_isDragging && _dragFsm.CurrentPhase == InteractionPhase.Idle)
			{
				// 轻微位移产生的非拖拽松手不算确认，续接点击跟随。
				_dragFsm.PickUpCard(_dragAnchor, isClickSelect: true, isMobile: false);
			}
		}
	}

	private void AnimateTargeting(Vector2 center, float scale, float delta)
	{
		const float rate = 24f;
		Vector2 current = ScreenCenter;
		SetScreenCenter(current.DistanceSquaredTo(center) <= 0.04f ? center : current.Lerp(center, HandMotionMath.DampingWeight(delta, rate)));
		Scale = Vector2.One * HandMotionMath.Damp(Scale.X, scale, delta, rate);
		RotationDegrees = HandMotionMath.Damp(RotationDegrees, 0f, delta, rate);
		SetElevation(HandMotionMath.Damp(Elevation, 0.7f, delta, rate));
	}

	private void OnFsmDragMove(Vector2 pos, bool inPlay, bool inCancel)
	{
		OnDragMove?.Invoke(this, pos);
		if (!_isDragging)
		{
			return; // 出牌区检查可能同步触发归还。
		}
		float delta = _dragDelta;
		float uiScale = Mathf.Max(UIScaler.Instance?.CurrentScale ?? 1f, 0.01f);
		float velocity = delta > 0f ? (pos.X - _lastPointer.X) / (delta * uiScale) : 0f;
		_lastPointer = pos;
		if (!_pickupReleased && !_dragFsm.HasDragged)
		{
			return;
		}

		float scaleRatio = uiScale / _uiScale;
		float targetScale = (_playZoneActive ? 0.94f : 1f) * scaleRatio;
		float scale = HandMotionMath.Damp(Scale.X, targetScale, delta, 27f);
		float angle = HandMotionMath.Damp(RotationDegrees, HandMotionMath.DragTiltTarget(velocity), delta, HandMotionMath.DragTiltRate);
		Vector2 grabOffset = (_localGrabOffset * scale).Rotated(Mathf.DegToRad(angle));
		Vector2 lifted = new(0f, -7f * _playZoneGlow * uiScale);
		Vector2 localPointer = GetParentScreenTransform().AffineInverse() * pos;
		Vector2 target = localPointer - grabOffset + lifted;
		Vector2 current = Position + PivotOffset;
		Position = current.Lerp(target, HandMotionMath.DampingWeight(delta, HandMotionMath.DragPositionRate)) - PivotOffset;
		Scale = Vector2.One * scale;
		RotationDegrees = angle;
		SetElevation(HandMotionMath.Damp(Elevation, _playZoneActive ? 1f : 0.78f, delta, 24f));
	}

	private void OnFsmDrop(Vector2 pos, bool wasDrag)
	{
		if (!_isDragging || (!wasDrag && (!_pickupReleased || _parkOnClickRelease)))
		{
			return;
		}
		CancelDragSilent();
		OnCardDropped?.Invoke(this, pos);
	}

	private void OnFsmCancel()
	{
		if (_isDragging)
		{
			CancelDragSilent();
			OnCardRightClicked?.Invoke(this);
		}
	}

	/// <summary>只为手牌选择标记调色；槽位与牌面位姿由容器负责。</summary>
	public void SetHandSelectionHighlight(bool selected)
	{
		if (_handSelectionHighlighted != selected)
		{
			_handSelectionHighlighted = selected;
			UpdateInteractionTint();
		}
	}

	/// <summary>随离桌高度调节柔和阴影。阴影在背景之下且不参与输入。</summary>
	public void SetElevation(float amount)
	{
		float next = Mathf.Clamp(amount, 0f, 1f);
		if (!_built || Mathf.IsEqualApprox(Elevation, next))
		{
			return;
		}
		Elevation = next;
		UpdateShadow();
	}

	public void SetPlayZoneHighlight(bool active)
	{
		_playZoneActive = active;
	}

	private void UpdatePlayZoneGlow(float delta)
	{
		float target = _playZoneActive ? 1f : 0f;
		if (_playZoneGlow == target)
		{
			return;
		}
		_playZoneGlow = Mathf.Abs(_playZoneGlow - target) < 0.008f
			? target : HandMotionMath.Damp(_playZoneGlow, target, delta, 28f);
		_faceStyle.BorderColor = ClrBorder.Lerp(new Color(0.45f, 0.85f, 0.64f, 0.95f), _playZoneGlow);
		UpdateShadow();
	}

	private void UpdateShadow()
	{
		_shadowPanel.Visible = Elevation > 0.01f || _playZoneGlow > 0.01f;
		if (!_shadowPanel.Visible)
		{
			return;
		}
		_shadowStyle.ShadowSize = Mathf.Max(1, Mathf.RoundToInt((4f + 11f * Elevation) * _uiScale));
		_shadowStyle.ShadowOffset = new Vector2(0f, (2f + 7f * Elevation) * _uiScale);
		_shadowStyle.ShadowColor = new Color(0.03f, 0.045f, 0.08f, 0.32f * Elevation)
			.Lerp(new Color(0.19f, 0.68f, 0.48f, 0.34f), _playZoneGlow * 0.65f);
	}

	// ============================================================
	// 布局切换
	// ============================================================

	/// <summary>
	/// 随从布局：显示攻防属性，隐藏法术标签。
	/// </summary>
	private void ShowMinionLayout()
	{
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
	/// 非随从布局：隐藏攻防属性，显示卡牌类型标签（法术/领域/状态）。
	/// </summary>
	private void ShowSpellLayout()
	{
		_attackBg.Visible = false;
		_healthBg.Visible = false;
		_attackLabel.Visible = false;
		_healthLabel.Visible = false;
		_spellTypeLabel.Visible = true;
		_actionCostBg.Visible = false;
		_actionCostLabel.Visible = false;
	}

	/// <summary>
	/// 按描述文本长度、稀有度标识占位和牌面可用空间重新分配卡图/描述/稀有度区域。
	/// 目标是让长描述优先获得垂直空间，其次再缩小字号，而不是依赖固定高度硬裁切。
	/// </summary>
	private void ApplyResponsiveContentLayout()
	{
		if (!_built)
		{
			return;
		}

		float s = _uiScale > 0f ? _uiScale : (UIScaler.Instance?.GetScaleFactor() ?? 1.0f);
		Vector2 size = _cardSize == Vector2.Zero ? Size : _cardSize;
		float headerHeight = HEADER_H * s;
		float rarityHeight = GetRarityAreaHeight(s);
		float maxArtworkHeight = ARTWORK_H * s;
		float minArtworkHeight = MIN_ARTWORK_H * s;
		float artworkStep = Mathf.Max(2f, 4f * s);
		float descWidth = Mathf.Max(8f, size.X - DESC_SIDE_PADDING * 2f * s);
		int baseFontSize = Mathf.Max(1, Mathf.RoundToInt(DESC_BASE_FONT_SIZE * s));
		int minFontSize = Mathf.Max(1, Mathf.RoundToInt(DESC_MIN_FONT_SIZE * s));
		float selectedArtworkHeight = minArtworkHeight;
		int selectedFontSize = minFontSize;
		bool foundFit = false;

		for (float artworkHeight = maxArtworkHeight; artworkHeight >= minArtworkHeight; artworkHeight -= artworkStep)
		{
			float descAreaHeight = Mathf.Max(0f, size.Y - headerHeight - artworkHeight - rarityHeight);
			float descInnerHeight = Mathf.Max(0f, descAreaHeight - DESC_TOP_BOTTOM_PADDING * 2f * s);

			for (int fontSize = baseFontSize; fontSize >= minFontSize; fontSize--)
			{
				float textHeight = MeasureWrappedDescriptionHeight(descWidth, fontSize);
				if (textHeight <= descInnerHeight)
				{
					selectedArtworkHeight = artworkHeight;
					selectedFontSize = fontSize;
					foundFit = true;
					break;
				}
			}

			if (foundFit)
			{
				break;
			}

			selectedArtworkHeight = artworkHeight;
		}

		float descTop = headerHeight + selectedArtworkHeight;
		float descHeight = Mathf.Max(0f, size.Y - descTop - rarityHeight);
		float rarityTop = descTop + descHeight;

		ApplyArtworkLayout(headerHeight, selectedArtworkHeight, size, s);
		ApplyDescriptionLayout(descTop, descHeight, descWidth, selectedFontSize, s);
		ApplyRarityLayout(rarityTop, rarityHeight, size, s);
	}

	/// <summary>
	/// 更新卡图区、法术标签和攻防角标的位置，使其与响应式高度一致。
	/// </summary>
	private void ApplyArtworkLayout(float artworkTop, float artworkHeight, Vector2 size, float s)
	{
		_artworkView.Position = new Vector2(0, artworkTop);
		_artworkView.Size = new Vector2(size.X, artworkHeight);

		_spellTypeLabel.Position = new Vector2(0, artworkTop + artworkHeight - 15f * s);
		_spellTypeLabel.Size = new Vector2(size.X, 15f * s);

		float statWidth = STATS_W * s;
		float statHeight = STATS_H * s;
		float statY = artworkTop + artworkHeight - statHeight - 2f * s;

		_attackBg.Position = new Vector2(2f * s, statY);
		_attackLabel.Position = _attackBg.Position;
		_attackBg.Size = new Vector2(statWidth, statHeight);
		_attackLabel.Size = _attackBg.Size;

		float healthX = size.X - statWidth - 2f * s;
		_healthBg.Position = new Vector2(healthX, statY);
		_healthLabel.Position = _healthBg.Position;
		_healthBg.Size = new Vector2(statWidth, statHeight);
		_healthLabel.Size = _healthBg.Size;
	}

	/// <summary>
	/// 更新描述区位置与字号；多行文本始终从上往下排，避免垂直居中导致的假性溢出。
	/// </summary>
	private void ApplyDescriptionLayout(float descTop, float descHeight, float descWidth, int fontSize, float s)
	{
		_descBg.Position = new Vector2(0, descTop);
		_descBg.Size = new Vector2(_cardSize.X, descHeight);

		_descLabel.Position = new Vector2(DESC_SIDE_PADDING * s, descTop + DESC_TOP_BOTTOM_PADDING * s);
		_descLabel.Size = new Vector2(descWidth, Mathf.Max(0f, descHeight - DESC_TOP_BOTTOM_PADDING * 2f * s));
		_descLabel.HorizontalAlignment = GetDescriptionAlignment();
		_descLabel.AddThemeFontSizeOverride("font_size", fontSize);
	}

	/// <summary>
	/// 更新稀有度标识区域位置；稀有度标识始终可见。
	/// </summary>
	private void ApplyRarityLayout(float rarityTop, float rarityHeight, Vector2 size, float s)
	{
		float margin = 4f * s;
		float w = size.X - margin * 2f;

		_rarityBg.Position = new Vector2(margin, rarityTop);
		_rarityBg.Size = new Vector2(w, rarityHeight);

		_rarityLabel.Position = new Vector2(margin, rarityTop);
		_rarityLabel.Size = new Vector2(w, rarityHeight);
	}

	/// <summary>
	/// 稀有度标识区当前高度。始终占固定高度。
	/// </summary>
	private static float GetRarityAreaHeight(float s)
	{
		return RARITY_H * s;
	}

	/// <summary>
	/// 用当前 Label 字体测量固定宽度下的换行后高度。
	/// </summary>
	private float MeasureWrappedDescriptionHeight(float width, int fontSize)
	{
		if (string.IsNullOrEmpty(_descLabel.Text))
		{
			return 0f;
		}

		Font font = _descLabel.GetThemeFont("font") ?? ThemeDB.FallbackFont;
		Vector2 textSize = font.GetMultilineStringSize(
			_descLabel.Text,
			GetDescriptionAlignment(),
			width,
			fontSize);
		return textSize.Y;
	}

	/// <summary>
	/// 从 UIScaler 读取当前卡牌描述对齐方式。
	/// </summary>
	private static HorizontalAlignment GetDescriptionAlignment()
	{
		return UIScaler.Instance?.CardDescriptionCentered == true
			? HorizontalAlignment.Center
			: HorizontalAlignment.Left;
	}

	/// <summary>
	/// 卡牌名称对齐方式——复用「卡牌描述对齐」设置项，
	/// 白名单模式：居中对齐时名称也居中，左对齐时名称左对齐。
	/// </summary>
	private static HorizontalAlignment GetNameAlignment()
	{
		return UIScaler.Instance?.CardDescriptionCentered == true
			? HorizontalAlignment.Center
			: HorizontalAlignment.Left;
	}

	/// <summary>
	/// 测量卡牌名称文本宽度，必要时缩小字号以适配名称区域宽度。
	/// 从 NAME_BASE_FONT_SIZE 逐步降至 NAME_MIN_FONT_SIZE。
	/// </summary>
	private void ApplyNameFontSize(float s)
	{
		if (string.IsNullOrEmpty(_nameLabel.Text))
			return;

		float x = MANA_DIAMETER * s + 8f * s;
		float availableWidth = _cardSize.X - x - 4f * s;

		int baseFontSize = Mathf.RoundToInt(NAME_BASE_FONT_SIZE * s);
		int minFontSize = Mathf.Max(1, Mathf.RoundToInt(NAME_MIN_FONT_SIZE * s));
		int fontSize = baseFontSize;

		Font font = _nameLabel.GetThemeFont("font") ?? ThemeDB.FallbackFont;
		for (; fontSize > minFontSize; fontSize--)
		{
			Vector2 textSize = font.GetStringSize(_nameLabel.Text, HorizontalAlignment.Left, -1, fontSize);
			if (textSize.X <= availableWidth)
				break;
		}

		_nameLabel.AddThemeFontSizeOverride("font_size", fontSize);
	}

	/// <summary>
	/// 设置页切换描述对齐后，当前已存在的 CardUI 也需要即时重排名称和描述。
	/// </summary>
	private void OnEmojiIconsChanged()
	{
		RefreshRuntimeDisplay();
	}

	private void OnCardDescriptionSettingsChanged()
	{
		if (!_built)
			return;

		_nameLabel.HorizontalAlignment = GetNameAlignment();
		float s = _uiScale > 0f ? _uiScale : (UIScaler.Instance?.GetScaleFactor() ?? 1.0f);
		ApplyNameFontSize(s);
		ApplyResponsiveContentLayout();
	}

	// ============================================================
	// 稀有度标识渲染
	// ============================================================

	/// <summary>
	/// 根据卡牌稀有度更新底部彩色数字标识。
	/// 所有卡牌类型均显示稀有度数字（0-6）。
	/// </summary>
	private void UpdateRarityIndicator(Card.Card card, float s)
	{
		if (_rarityLabel == null)
			return;

		var rarity = card.Data.Rarity;
		int scheme = UIScaler.Instance?.RarityColorSchemeIndex ?? 0;
		Color clr = Core.RarityColorScheme.GetColor(scheme, rarity);
		int num = Core.RarityColorScheme.GetNumber(rarity);

		_rarityLabel.Text = num.ToString();
		_rarityLabel.AddThemeColorOverride("font_color", clr);
		_rarityLabel.AddThemeFontSizeOverride("font_size", Mathf.RoundToInt(10 * s));
	}

	/// <summary>
	/// 响应稀有度颜色方案切换事件，刷新所有已存在 CardUI 的稀有度标识颜色。
	/// </summary>
	private void OnRarityColorSchemeChanged()
	{
		if (!_built || Card == null || _rarityLabel == null)
			return;

		var rarity = Card.Data.Rarity;
		int scheme = UIScaler.Instance?.RarityColorSchemeIndex ?? 0;
		Color clr = Core.RarityColorScheme.GetColor(scheme, rarity);
		_rarityLabel.AddThemeColorOverride("font_color", clr);
	}

	// 收藏网格只借用绘制层级；战斗手牌的聚焦完全由 HandUI 控制。
	public void ApplyHoverEffect()
	{
		ZIndex = 2;
	}

	public void RemoveHoverEffect()
	{
		ZIndex = 0;
	}

	/// <summary>卡面战术徽章覆盖层：位于所有牌面控件之上，只绘制矢量图标，不拦截输入。</summary>
	private partial class TacticalBadgeOverlay : Control
	{
		private readonly float _scale;
		private bool _isMinion;
		private UnitClass _unitClass;
		private bool _hasTaunt;
		private bool _hasGuard;
		private bool _isPlayerSide;

		private TacticalBadgeOverlay() : this(1f) { }

		public TacticalBadgeOverlay(float scale)
		{
			_scale = scale <= 0f ? 1f : scale;
			MouseFilter = MouseFilterEnum.Ignore;
		}

		public void Bind(OdysseyCards.Card.Card? card)
		{
			_isMinion = card is OdysseyCards.Card.Minion minion;
			if (card?.Data != null)
				_unitClass = card.Data.UnitClass;
			if (card is OdysseyCards.Card.Minion runtimeMinion)
			{
				_hasTaunt = runtimeMinion.HasTaunt;
				_hasGuard = runtimeMinion.HasGuard;
				_isPlayerSide = runtimeMinion.IsPlayerSide;
			}
			else
			{
				_hasTaunt = false;
				_hasGuard = false;
				_isPlayerSide = true;
			}
			Visible = _isMinion;
			QueueRedraw();
		}

		public override void _Draw()
		{
			if (!_isMinion || Size.X < 24f || Size.Y < 24f)
				return;
			Color primary = _isPlayerSide ? UIThemeFactory.CyanGold : UIThemeFactory.Crimson;
			Color outline = new(0.04f, 0.05f, 0.09f, 0.95f);
			var center = new Vector2(Size.X - 17f * _scale, (HEADER_H + 17f) * _scale);
			TacticalIconDrawer.DrawUnitClass(this, BoardUI.GetTacticalUnitIcon(_unitClass),
				center, 24f * _scale, primary, outline);
			float badgeY = center.Y + 21f * _scale;
			if (_hasTaunt)
			{
				TacticalIconDrawer.DrawTaunt(this, new Vector2(center.X, badgeY), 17f * _scale,
					UIThemeFactory.WarmGold, outline);
				badgeY += 20f * _scale;
			}
			if (_hasGuard)
			{
				TacticalIconDrawer.DrawGuard(this, new Vector2(center.X, badgeY), 17f * _scale,
					UIThemeFactory.CyanGold.Lerp(UIThemeFactory.WarmGold, 0.35f), outline);
			}
		}
	}
}
