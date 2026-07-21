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
	public const float DESIGN_WIDTH = 140f;
	public const float DESIGN_HEIGHT = 196f;
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
	private const float HOVER_LIFT = 10f;

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
	/// 当前是否处于拖拽状态。HandUI 通过此属性在拖拽期间抑制悬停效果。
	/// </summary>
	public bool IsDragging => _isDragging;

	/// <summary>
	/// 注入交互状态机，将拖拽/点击/目标选择的状态管理委托给 FSM。
	/// 设为 null 时 CardUI 回退到本地行为，保持向后兼容。
	/// </summary>
	public void SetInteractionFsm(InteractionFsm fsm) => _interactionFsm = fsm;

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
	private CardArtworkView _artworkView = null!;

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

	// 播放区高亮缓存的 StyleBoxFlat（复用避免每帧分配新对象）
	private StyleBoxFlat? _playZoneHighlightStyle;

	// ============================================================
	// 内部状态
	// ============================================================
	private InteractionFsm? _interactionFsm;
	private bool _canPlay = true;
	private bool _isDragging;
	private Vector2 _dragOffset;
	private Vector2 _dragStartScreenPos;
	private bool _hasDragged;
	/// <summary>点击选中模式：用户快速点击（松手无拖拽位移）后进入，卡片跟随鼠标但不响应松手掉落。</summary>
	private bool _clickSelectMode;
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

		if (!DisplayOnly)
		{
			GuiInput += OnGuiInputHandler;
		}

		if (UIScaler.Instance != null)
		{
			UIScaler.Instance.OnCardDescriptionSettingsChanged += OnCardDescriptionSettingsChanged;
			UIScaler.Instance.OnRarityColorSchemeChanged += OnRarityColorSchemeChanged;
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

	public override void _ExitTree()
	{
		// 移除主题重载，释放 Godot 原生层持有的 StyleBoxFlat 引用，减少 Mono 退出泄露
		if (_bgPanel != null && GodotObject.IsInstanceValid(_bgPanel))
		{
			_bgPanel.RemoveThemeStyleboxOverride("panel");
		}
		_playZoneHighlightStyle?.Dispose();
		_playZoneHighlightStyle = null;

		if (UIScaler.Instance != null)
		{
			UIScaler.Instance.OnCardDescriptionSettingsChanged -= OnCardDescriptionSettingsChanged;
			UIScaler.Instance.OnRarityColorSchemeChanged -= OnRarityColorSchemeChanged;
		}

		if (_interactionFsm != null)
		{
			_interactionFsm.OnDragMove -= OnFsmDragMove;
			_interactionFsm.OnDrop -= OnFsmDrop;
			_interactionFsm.OnCancel -= OnFsmCancel;
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

		float s = RenderScaleOverride > 0f ? RenderScaleOverride
			: UIScaler.Instance?.GetScaleFactor() ?? 1.0f;

		// 法力消耗
		_manaLabel.Text = card.GetEffectiveCost().ToString();

		// 行动花费（随从始终显示，法术隐藏）
		_actionCostLabel.Text = card.ActionCost.ToString();

		// 卡牌名称（本地化）- 响应式字号防止长名称溢出
		_nameLabel.Text = card.GetLocalizedName();
		_nameLabel.HorizontalAlignment = GetNameAlignment();
		ApplyNameFontSize(s);

		// 卡图区域：程序化生成（真实 Artwork 存在时优先）
		_artworkView.Setup(card.Data);

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

		// 稀有度标识（所有卡牌类型均显示）
		UpdateRarityIndicator(card, s);
		ApplyResponsiveContentLayout();
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
		if (DisplayOnly)
			return;
		if (Engine.IsEditorHint())
			return; // 编辑器模式下不处理交互

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

		_interactionFsm?.PickUpCard(LastClickGlobalPosition, isClickSelect: false, isMobile: MobileInputRouter.IsMobile);
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

		if (_interactionFsm != null)
		{
			// 订阅 FSM 事件（先取消再订阅，防止重复）
			_interactionFsm.OnDragMove -= OnFsmDragMove;
			_interactionFsm.OnDragMove += OnFsmDragMove;
			_interactionFsm.OnDrop -= OnFsmDrop;
			_interactionFsm.OnDrop += OnFsmDrop;
			_interactionFsm.OnCancel -= OnFsmCancel;
			_interactionFsm.OnCancel += OnFsmCancel;

			_interactionFsm.PickUpCard(globalAnchor, isClickSelect: startAsClickFollow, isMobile: MobileInputRouter.IsMobile);
		}
		else
		{
			// 回退到本地行为
			if (startAsClickFollow)
			{
				_hasDragged = false;
				_clickSelectMode = true;
				_pointerFollowStartPos = GlobalPosition;
				_hasMovedFromOrigin = false;
			}
			else
			{
				_hasDragged = true;
				_clickSelectMode = false;
				_hasMovedFromOrigin = true;
			}
		}
	}

	/// <summary>
	/// 进入目标选择展示态：卡牌不再跟随鼠标，移动到统一的展示位置。
	/// </summary>
	public void PresentForTargeting(Vector2 globalCenter, float targetScale)
	{
		_interactionFsm?.EnterTargeting();
		CancelDragSilent();
		MouseFilter = MouseFilterEnum.Ignore;
		ZIndex = 10;

		Vector2 targetSize = Size * targetScale;
		Vector2 targetTopLeft = globalCenter - targetSize * 0.5f;

		var tween = CreateTween();
		tween.SetParallel(true);
		tween.SetTrans(Tween.TransitionType.Cubic);
		tween.SetEase(Tween.EaseType.Out);
		tween.TweenProperty(this, "global_position", targetTopLeft, 0.15f);
		tween.TweenProperty(this, "scale", Vector2.One * targetScale, 0.15f);
	}

	/// <summary>
	/// 退出拖拽状态（静默，不触发事件）。
	/// 用于切换卡牌时直接清理旧卡，避免触发 RefreshHand 链条。
	/// </summary>
	public void CancelDragSilent()
	{
		_interactionFsm?.ForceReset();
		_isDragging = false;
		_hasDragged = false;
		_clickSelectMode = false;
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
		if (!_isDragging)
			return;
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
		if (SceneLifecycleGuard.ShouldSkip(this))
			return;
		if (DisplayOnly || !_isDragging)
			return;

		if (MobileInputRouter.IsMobile)
		{
			MobileDragProcess();
			return;
		}

		// ==================== FSM 委托路径 ====================
		if (_interactionFsm != null && _interactionFsm.CurrentPhase != InteractionPhase.Idle)
		{
			Vector2 fsmMousePos = GetGlobalMousePosition();
			bool fsmLeftDown = Input.IsMouseButtonPressed(MouseButton.Left);
			float viewportH = GetViewportRect().Size.Y;
			_interactionFsm.Tick(fsmMousePos, fsmLeftDown, Input.IsMouseButtonPressed(MouseButton.Right), viewportH, _dragStartScreenPos.Y);
			return;
		}

		// ==================== 桌面端鼠标交互（回退路径——无 FSM 时） ====================
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
				OnCardDropped?.Invoke(this, dropPos);
			}
			else if (!_clickSelectMode)
			{
				// 首次松手 → 进入点击选中模式
				_clickSelectMode = true;
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
	/// 委托给 FSM 处理状态转换、阈值检测和松手事件。
	/// 无 FSM 时回退到本地行为。
	/// </summary>
	private void MobileDragProcess()
	{
		if (_interactionFsm != null)
		{
			if (_interactionFsm.CurrentPhase == InteractionPhase.Idle)
				return;

			var fsmRouter = MobileInputRouter.Instance;
			Vector2 touchPos = fsmRouter.TouchPosition;
			bool isTouchActive = fsmRouter.IsTouchActive;
			float viewportH = GetViewportRect().Size.Y;
			_interactionFsm.Tick(touchPos, isTouchActive, isRightDown: false, viewportH, _dragStartScreenPos.Y);
			return;
		}

		// ==================== 回退路径（无 FSM） ====================
		var routerFallback = MobileInputRouter.Instance;
		if (routerFallback.IsTouchActive)
		{
			Vector2 touchPos = routerFallback.TouchPosition;

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
		if (_wasMobileTouchActive && !routerFallback.IsTouchActive)
		{
			Vector2 dropScreenPos = routerFallback.TouchReleasePosition;
			bool wasDragging = _hasDragged;
			_isDragging = false;
			_hasDragged = false;
			_clickSelectMode = false;
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

		_wasMobileTouchActive = routerFallback.IsTouchActive;
	}

	/// <summary>
	/// 选中卡牌：切换高亮状态，上移产生抬起效果。
	/// 使用 OffsetTop 而非直接改 Position，避免与 HBoxContainer 布局冲突。
	/// 拖拽中跳过——位置由 _Process 控制，不应额外偏移。
	/// </summary>
	public void Select()
	{
		if (_isDragging)
			return;
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
		if (_bgPanel == null)
			return;

		// 复用缓存的 StyleBoxFlat，避免每帧分配新对象导致 Mono 泄露
		if (_playZoneHighlightStyle == null)
		{
			_playZoneHighlightStyle = new StyleBoxFlat
			{
				BgColor = ClrBg,
				CornerRadiusTopLeft = (int)(6 * (UIScaler.Instance?.GetScaleFactor() ?? 1f)),
				CornerRadiusTopRight = (int)(6 * (UIScaler.Instance?.GetScaleFactor() ?? 1f)),
				CornerRadiusBottomLeft = (int)(6 * (UIScaler.Instance?.GetScaleFactor() ?? 1f)),
				CornerRadiusBottomRight = (int)(6 * (UIScaler.Instance?.GetScaleFactor() ?? 1f)),
			};
		}

		_playZoneHighlightStyle.BorderWidthBottom = active ? 3 : 1;
		_playZoneHighlightStyle.BorderWidthLeft = active ? 3 : 1;
		_playZoneHighlightStyle.BorderWidthRight = active ? 3 : 1;
		_playZoneHighlightStyle.BorderWidthTop = active ? 3 : 1;
		_playZoneHighlightStyle.BorderColor = active ? new Color(0.3f, 0.9f, 0.3f, 0.8f) : ClrBorder;

		_bgPanel.AddThemeStyleboxOverride("panel", _playZoneHighlightStyle);
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
	/// 非随从布局：隐藏攻防属性，显示卡牌类型标签（法术/领域/状态）。
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
			CardType.Spell => Loc.T("card_type.spell", "法术"),
			CardType.Domain => Loc.T("card_type.domain", "领域"),
			CardType.Status => Loc.T("card_type.status", "状态"),
			_ => Loc.T("card_type.spell", "法术")
		};
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
	private float GetRarityAreaHeight(float s)
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

	// ============================================================
	// FSM 事件处理
	// ============================================================

	/// <summary>
	/// FSM 拖拽移动回调：更新卡牌位置以跟随指针，并转发 OnDragMove 事件。
	/// 仅在 CardPickedUp 或 BoardDrag 阶段更新位置。
	/// </summary>
	private void OnFsmDragMove(Vector2 pos, bool inPlay, bool inCancel)
	{
		if (_interactionFsm is { CurrentPhase: InteractionPhase.CardPickedUp or InteractionPhase.BoardDrag })
		{
			GlobalPosition = pos - _dragOffset;
		}
		OnDragMove?.Invoke(this, pos);
	}

	/// <summary>
	/// FSM 松手/掉落回调：恢复视觉状态，转发 OnCardDropped。
	/// </summary>
	private void OnFsmDrop(Vector2 pos, bool wasDrag)
	{
		_isDragging = false;
		_hasDragged = false;
		_clickSelectMode = false;
		_isHoverEffectActive = false;
		KillHoverTween();
		MouseFilter = MouseFilterEnum.Stop;
		OffsetTop = 0;
		OnCardDropped?.Invoke(this, pos);
	}

	/// <summary>
	/// FSM 取消回调：恢复视觉状态，转发 OnCardRightClicked。
	/// </summary>
	private void OnFsmCancel()
	{
		_isDragging = false;
		_hasDragged = false;
		_clickSelectMode = false;
		_isHoverEffectActive = false;
		KillHoverTween();
		MouseFilter = MouseFilterEnum.Stop;
		OffsetTop = 0;
		OnCardRightClicked?.Invoke(this);
	}

	// ============================================================
	// 交互事件
	// ============================================================

	/// <summary>
	/// 悬停入效果：仅设置 ZIndex。上浮和缩放由 HandUI.RefreshLayout 统一控制。
	/// </summary>
	public void ApplyHoverEffect()
	{
		if (_isDragging || IsSelected || _isHoverEffectActive)
			return;
		_isHoverEffectActive = true;
		KillHoverTween();
		ZIndex = 2;
	}

	/// <summary>
	/// 悬停出效果：仅恢复 ZIndex。位置恢复由 RefreshLayout 处理。
	/// </summary>
	public void RemoveHoverEffect()
	{
		if (!_isHoverEffectActive)
			return;
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
