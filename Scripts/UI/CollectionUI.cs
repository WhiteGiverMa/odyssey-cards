using Godot;
using OdysseyCards.Core;
using OdysseyCards.Character;
using OdysseyCards.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using Loc = OdysseyCards.Localization.Localization;

namespace OdysseyCards.UI;

/// <summary>
/// 卡牌收藏/牌组编辑界面。
/// 左侧显示当前牌组内卡牌（按 ID 分组，点击移除），
/// 右侧使用 CardGrid 组件显示收藏中已解锁的卡牌（点击添加）。
/// 顶栏包含返回按钮、牌组名称编辑、多牌组管理。
/// 左侧底部提供卡牌计数、清空/撤销/保存、导出/导入功能。
/// 修改不自动保存——仅在用户主动保存或新建/删除牌组时持久化。
/// 离开界面时若有未保存更改，提示用户确认。
/// </summary>
public partial class CollectionUI : Control
{
	/// <summary>移动端 TouchZone 注册 token，ExitTree 时释放。</summary>
	private readonly List<IDisposable> _zoneTokens = new();

	/// <summary>牌组列表行的移动端 TouchZone 注册 token，列表刷新时释放。</summary>
	private readonly List<IDisposable> _deckRowZoneTokens = new();

	// ===== UI 控件 =====

	private Button _backButton = null!;
	private LineEdit _deckNameEdit = null!;
	private Label _cardCountLabel = null!;
	private OptionButton _deckSelector = null!;
	private Button _newDeckButton = null!;
	private Button _deleteDeckButton = null!;
	private Button _clearDeckButton = null!;
	private Button _undoButton = null!;
	private Button _saveButton = null!;
	private Button _exportButton = null!;
	private Button _importButton = null!;
	private VBoxContainer _deckCardList = null!;
	private Label _emptyDeckLabel = null!;
	private CardGrid _cardGrid = null!;
	private FileDialog _fileDialog = null!;
	private Label _minCardsWarning = null!;
	private Control _cardPreviewOverlay = null!;
	private bool _hasCardPreviewOverlay;
	private IDisposable? _cardPreviewTouchToken;

	/// <summary>
	/// 飞入动画层：用于卡牌从网格飞到牌组列表的临时卡片。
	/// </summary>
	private Control _flyLayer = null!;

	/// <summary>
	/// 左侧牌组面板引用（用于飞入动画目标位置）。
	/// </summary>
	private Control _deckPanelRef = null!;

	/// <summary>
	/// 牌组卡片拖拽状态。
	/// </summary>
	private CardUI? _deckDragClone;
	private CardData? _deckDraggingCardData;
	private Vector2 _deckDragStartPos;
	private bool _deckIsDragging;
	private const float DeckDragThreshold = 8f;

	// ===== 未保存更改追踪 =====

	/// <summary>
	/// 上次保存时的牌组快照。
	/// </summary>
	private Deck? _checkpointDeck;

	/// <summary>
	/// 当前是否有未保存的更改。
	/// </summary>
	private bool _hasUnsavedChanges;

	/// <summary>
	/// 切换牌组时暂存的目标索引（用于确认后切换）。
	/// </summary>
	private int _pendingDeckIndex = -1;

	// ===== 状态 =====

	private bool _isExportMode;

	// ===== 键盘导航 =====

	/// <summary>键盘焦点卡牌索引（当前页内，-1 表示无焦点）。</summary>
	private int _focusedCardIndex = -1;

	/// <summary>热键回调引用（用于注销时精确移除）。</summary>
	private Action? _leftAction, _rightAction, _upAction, _downAction;
	private Action? _acceptAction, _cancelAction;
	private Action? _pageUpAction, _pageDownAction;
	private Action? _filterAllAction, _filterMinionAction, _filterSpellAction, _filterDomainAction;

	// ===== 生命周期 =====

	public override void _EnterTree()
	{
		base._EnterTree();
		RegisterHotkeyBindings();
	}

	public override void _Ready()
	{
		GD.Print("[CollectionUI] _Ready — 初始化牌组编辑界面");

		SetupUI();
		SetProcessInput(true);
		CaptureCheckpoint();
		RefreshAll();

		// 订阅事件
		GameManager.Instance.LanguageChanged += OnLanguageChanged;
		GameManager.Instance.OnCollectionChanged += OnCollectionChanged;

		if (MobileInputHelper.IsMobile)
		{
			MouseFilter = MouseFilterEnum.Stop;
		}

		// 移动端：通过 MobileInputRouter 注册触控区域
		if (MobileInputRouter.IsMobile)
		{
			RegisterMobileZones();
		}
	}

	public override void _Input(InputEvent @event)
	{
		if (SceneLifecycleGuard.ShouldSkip(this))
			return;

		// 特写预览期间，任意鼠标点击即关闭（桌面端）。
		// 在 _Input 层拦截而非依赖 GuiInput，避免 ScrollContainer 等控件
		// 拦截 GUI 事件导致 overlay 的 GuiInput 无法收到。
		if (_hasCardPreviewOverlay && !MobileInputRouter.IsMobile)
		{
			if (@event is InputEventMouseButton { Pressed: true })
			{
				CloseCardPreview();
				return;
			}
		}

		if (!MobileInputRouter.IsMobile)
		{
			HandleDesktopDeckDragInput(@event);
			return;
		}

		if (!MobileInputHelper.IsMobile)
			return;

		// 触控已迁移至 MobileInputRouter.RegisterTapZone
	}

	private void HandleDesktopDeckDragInput(InputEvent @event)
	{
		if (_deckDraggingCardData == null)
			return;

		if (@event is InputEventMouseMotion)
		{
			Vector2 mousePos = GetViewport().GetMousePosition();
			float dist = mousePos.DistanceTo(_deckDragStartPos);
			if (dist > DeckDragThreshold && !_deckIsDragging)
			{
				_deckIsDragging = true;
				StartDeckDragClone(_deckDraggingCardData, _deckDragStartPos);
			}

			if (_deckIsDragging && _deckDragClone != null)
			{
				_deckDragClone.GlobalPosition = mousePos - (_deckDragClone.Size / 2);
				GetViewport().SetInputAsHandled();
			}
		}
		else if (@event is InputEventMouseButton mb
			&& mb.ButtonIndex == MouseButton.Left
			&& !mb.Pressed)
		{
			if (_deckIsDragging)
			{
				Vector2 dropPos = GetViewport().GetMousePosition();
				Rect2 gridRect = new(_cardGrid.GlobalPosition, _cardGrid.Size);
				if (gridRect.HasPoint(dropPos))
				{
					OnDeckCardRemoveClicked(_deckDraggingCardData);
				}
				CleanupDeckDrag();
				GetViewport().SetInputAsHandled();
			}

			_deckDraggingCardData = null;
		}
	}

	private static void CycleOptionButton(OptionButton optionButton, Action<long> onSelected)
	{
		if (optionButton.ItemCount <= 0)
			return;

		int nextIndex = (optionButton.Selected + 1) % optionButton.ItemCount;
		optionButton.Selected = nextIndex;
		onSelected(nextIndex);
	}

	public override void _ExitTree()
	{
		SceneLifecycleGuard.OnExitTree(this);

		// 注销键盘热键绑定
		UnregisterHotkeyBindings();

		// 释放所有移动端 TouchZone 注册
		foreach (var token in _zoneTokens)
		{
			token.Dispose();
		}
		_zoneTokens.Clear();

		ClearDeckRowMobileZones();
		CloseCardPreview();

		if (GameManager.Instance != null)
		{
			GameManager.Instance.LanguageChanged -= OnLanguageChanged;
			GameManager.Instance.OnCollectionChanged -= OnCollectionChanged;
		}
	}

	/// <summary>为 9 个 UI 按钮注册轻触区域（仅移动端）。</summary>
	private void RegisterMobileZones()
	{
		var router = MobileInputRouter.Instance;

		_zoneTokens.Add(router.RegisterTapZone(_backButton,
			_backButton.GetGlobalRect(),
			priority: 400, onTap: () => OnBackPressed()));

		_zoneTokens.Add(router.RegisterTapZone(_deckSelector,
			_deckSelector.GetGlobalRect(),
			priority: 400, onTap: () => CycleOptionButton(_deckSelector, OnDeckSelectorChanged)));

		_zoneTokens.Add(router.RegisterTapZone(_newDeckButton,
			_newDeckButton.GetGlobalRect(),
			priority: 400, onTap: () => OnNewDeckPressed()));

		_zoneTokens.Add(router.RegisterTapZone(_deleteDeckButton,
			_deleteDeckButton.GetGlobalRect(),
			priority: 400, onTap: () => OnDeleteDeckPressed()));

		_zoneTokens.Add(router.RegisterTapZone(_clearDeckButton,
			_clearDeckButton.GetGlobalRect(),
			priority: 400, onTap: () => OnClearDeckPressed()));

		_zoneTokens.Add(router.RegisterTapZone(_undoButton,
			_undoButton.GetGlobalRect(),
			priority: 400, onTap: () => OnUndoPressed()));

		_zoneTokens.Add(router.RegisterTapZone(_saveButton,
			_saveButton.GetGlobalRect(),
			priority: 400, onTap: () => OnSavePressed()));

		_zoneTokens.Add(router.RegisterTapZone(_exportButton,
			_exportButton.GetGlobalRect(),
			priority: 400, onTap: () => OnExportPressed()));

		_zoneTokens.Add(router.RegisterTapZone(_importButton,
			_importButton.GetGlobalRect(),
			priority: 400, onTap: () => OnImportPressed()));
	}

	// ===== 键盘导航 — HotkeyManager 注册/注销 =====

	/// <summary>
	/// 注册所有键盘热键绑定到 HotkeyManager。
	/// 方向键导航卡牌网格，Enter 确认选中，Escape 返回，
	/// PageUp/PageDown 翻页，数字键 1~4 切换过滤标签。
	/// </summary>
	private void RegisterHotkeyBindings()
	{
		var hm = HotkeyManager.Instance;
		if (hm == null)
			return;

		// 方向键 — 网格导航
		_leftAction = () => NavigateCardFocus(-1, 0);
		_rightAction = () => NavigateCardFocus(1, 0);
		_upAction = () => NavigateCardFocus(0, -1);
		_downAction = () => NavigateCardFocus(0, 1);
		hm.PushPressedBinding(OdysseyInput.Left, _leftAction);
		hm.PushPressedBinding(OdysseyInput.Right, _rightAction);
		hm.PushPressedBinding(OdysseyInput.Up, _upAction);
		hm.PushPressedBinding(OdysseyInput.Down, _downAction);

		// 确认 / 取消
		_acceptAction = AcceptFocusedCard;
		_cancelAction = HandleKeyboardCancel;
		hm.PushPressedBinding(OdysseyInput.Accept, _acceptAction);
		hm.PushPressedBinding(OdysseyInput.Cancel, _cancelAction);

		// 翻页
		_pageUpAction = PrevPage;
		_pageDownAction = NextPage;
		hm.PushPressedBinding(OdysseyInput.PageUp, _pageUpAction);
		hm.PushPressedBinding(OdysseyInput.PageDown, _pageDownAction);

		// 数字键 1~4 — 过滤标签切换
		_filterAllAction = () => SetFilterByNumber(1);
		_filterMinionAction = () => SetFilterByNumber(2);
		_filterSpellAction = () => SetFilterByNumber(3);
		_filterDomainAction = () => SetFilterByNumber(4);
		hm.PushPressedBinding(OdysseyInput.SelectCard1, _filterAllAction);
		hm.PushPressedBinding(OdysseyInput.SelectCard2, _filterMinionAction);
		hm.PushPressedBinding(OdysseyInput.SelectCard3, _filterSpellAction);
		hm.PushPressedBinding(OdysseyInput.SelectCard4, _filterDomainAction);

		// 键盘焦点超时事件 — 超时后清除焦点指示器
		hm.KeyboardFocusChanged += OnKeyboardFocusChanged;
	}

	/// <summary>
	/// 注销所有键盘热键绑定。
	/// </summary>
	private void UnregisterHotkeyBindings()
	{
		var hm = HotkeyManager.Instance;
		if (hm == null)
			return;

		hm.KeyboardFocusChanged -= OnKeyboardFocusChanged;

		if (_leftAction != null)
		{ hm.RemovePressedBinding(OdysseyInput.Left, _leftAction); _leftAction = null; }
		if (_rightAction != null)
		{ hm.RemovePressedBinding(OdysseyInput.Right, _rightAction); _rightAction = null; }
		if (_upAction != null)
		{ hm.RemovePressedBinding(OdysseyInput.Up, _upAction); _upAction = null; }
		if (_downAction != null)
		{ hm.RemovePressedBinding(OdysseyInput.Down, _downAction); _downAction = null; }
		if (_acceptAction != null)
		{ hm.RemovePressedBinding(OdysseyInput.Accept, _acceptAction); _acceptAction = null; }
		if (_cancelAction != null)
		{ hm.RemovePressedBinding(OdysseyInput.Cancel, _cancelAction); _cancelAction = null; }
		if (_pageUpAction != null)
		{ hm.RemovePressedBinding(OdysseyInput.PageUp, _pageUpAction); _pageUpAction = null; }
		if (_pageDownAction != null)
		{ hm.RemovePressedBinding(OdysseyInput.PageDown, _pageDownAction); _pageDownAction = null; }
		if (_filterAllAction != null)
		{ hm.RemovePressedBinding(OdysseyInput.SelectCard1, _filterAllAction); _filterAllAction = null; }
		if (_filterMinionAction != null)
		{ hm.RemovePressedBinding(OdysseyInput.SelectCard2, _filterMinionAction); _filterMinionAction = null; }
		if (_filterSpellAction != null)
		{ hm.RemovePressedBinding(OdysseyInput.SelectCard3, _filterSpellAction); _filterSpellAction = null; }
		if (_filterDomainAction != null)
		{ hm.RemovePressedBinding(OdysseyInput.SelectCard4, _filterDomainAction); _filterDomainAction = null; }
	}

	// ===== 键盘导航 — 业务方法 =====

	/// <summary>
	/// 方向键导航：在卡牌网格中按方向移动焦点。
	/// dx: 水平偏移（-1 左, +1 右）
	/// dy: 垂直偏移（-1 上, +1 下）
	/// </summary>
	private void NavigateCardFocus(int dx, int dy)
	{
		if (SceneLifecycleGuard.ShouldSkip(this))
			return;
		int count = _cardGrid.CurrentPageCardCount;
		if (count <= 0)
			return;

		int columns = _cardGrid.EstimatedColumns;
		if (dx != 0)
		{
			// 水平移动：线性 +1/-1，到达行尾/首时自动换行
			if (_focusedCardIndex < 0)
			{
				_focusedCardIndex = (dx > 0) ? 0 : count - 1;
			}
			else
			{
				_focusedCardIndex += dx;
				// 换行边界处理
				if (_focusedCardIndex >= count)
					_focusedCardIndex = 0;
				if (_focusedCardIndex < 0)
					_focusedCardIndex = count - 1;
			}
		}
		else if (dy != 0)
		{
			// 垂直移动：按估算列数跳转
			if (_focusedCardIndex < 0)
			{
				_focusedCardIndex = (dy > 0) ? 0 : count - 1;
			}
			else
			{
				int currentRow = _focusedCardIndex / columns;
				int currentCol = _focusedCardIndex % columns;
				int newRow = currentRow + dy;
				if (newRow < 0)
					newRow = (count - 1) / columns;
				int maxRow = (count - 1) / columns;
				if (newRow > maxRow)
					newRow = 0;
				int newIndex = newRow * columns + currentCol;
				if (newIndex >= count)
					newIndex = count - 1;
				_focusedCardIndex = newIndex;
			}
		}

		UpdateCardFocus();
	}

	/// <summary>
	/// 刷新键盘焦点高亮。仅当 HotkeyManager 记录到近期键盘活动时显示。
	/// </summary>
	private void UpdateCardFocus()
	{
		int count = _cardGrid.CurrentPageCardCount;
		if (_focusedCardIndex >= 0 && _focusedCardIndex < count
			&& HotkeyManager.Instance.LastKeyboardActivityMsec > 0)
		{
			_cardGrid.SetCardHighlight(_focusedCardIndex);
		}
		else
		{
			_cardGrid.ClearCardHighlights();
		}
	}

	/// <summary>
	/// Enter 键：确认选中当前键盘焦点的卡牌（触发点击行为）。
	/// </summary>
	private void AcceptFocusedCard()
	{
		if (SceneLifecycleGuard.ShouldSkip(this))
			return;
		int count = _cardGrid.CurrentPageCardCount;
		if (_focusedCardIndex < 0 || _focusedCardIndex >= count)
			return;

		var cardData = _cardGrid.GetCardDataAt(_focusedCardIndex);
		if (cardData != null)
		{
			OnCardGridCardClicked(cardData);
		}
	}

	/// <summary>
	/// Escape 键：返回。
	/// </summary>
	private void HandleKeyboardCancel()
	{
		if (SceneLifecycleGuard.ShouldSkip(this))
			return;
		OnBackPressed();
	}

	/// <summary>
	/// PageUp 键：上一页。
	/// </summary>
	private void PrevPage()
	{
		if (SceneLifecycleGuard.ShouldSkip(this))
			return;
		_cardGrid.GoToPreviousPage();
		_focusedCardIndex = 0;
		UpdateCardFocus();
	}

	/// <summary>
	/// PageDown 键：下一页。
	/// </summary>
	private void NextPage()
	{
		if (SceneLifecycleGuard.ShouldSkip(this))
			return;
		_cardGrid.GoToNextPage();
		_focusedCardIndex = 0;
		UpdateCardFocus();
	}

	/// <summary>
	/// 数字键切换过滤标签：1=全部, 2=随从, 3=法术, 4=领域。
	/// </summary>
	private void SetFilterByNumber(int n)
	{
		if (SceneLifecycleGuard.ShouldSkip(this))
			return;
		switch (n)
		{
			case 1:
				_cardGrid.SetTypeFilter(null);
				break;
			case 2:
				_cardGrid.SetTypeFilter(CardType.Minion);
				break;
			case 3:
				_cardGrid.SetTypeFilter(CardType.Spell);
				break;
			case 4:
				_cardGrid.SetTypeFilter(CardType.Domain);
				break;
		}
		_focusedCardIndex = 0;
		UpdateCardFocus();
	}

	/// <summary>
	/// HotkeyManager 键盘焦点超时事件：超时后清除焦点指示器。
	/// </summary>
	private void OnKeyboardFocusChanged(bool active)
	{
		if (!active)
		{
			_focusedCardIndex = -1;
			_cardGrid.ClearCardHighlights();
		}
	}

	// ===== 快照/还原 =====

	/// <summary>
	/// 捕获当前牌组快照，清除脏标记。
	/// </summary>
	private void CaptureCheckpoint()
	{
		_checkpointDeck = GameManager.Instance.ActiveDeck?.Clone();
		_hasUnsavedChanges = false;
		GD.Print("[CollectionUI] 已捕获牌组快照，清除脏标记");
	}

	/// <summary>
	/// 将牌组还原到上次快照状态。
	/// </summary>
	private void RestoreCheckpoint()
	{
		var activeDeck = GameManager.Instance.ActiveDeck;
		if (activeDeck == null || _checkpointDeck == null)
			return;

		activeDeck.Cards.Clear();
		activeDeck.Cards.AddRange(_checkpointDeck.Cards);
		activeDeck.Name = _checkpointDeck.Name;
		_hasUnsavedChanges = false;
		RefreshAll();
		GD.Print("[CollectionUI] 已还原牌组到快照状态");
	}

	/// <summary>
	/// 标记有未保存更改，刷新按钮和计数标签状态。
	/// </summary>
	private void MarkChanged()
	{
		_hasUnsavedChanges = true;
		RefreshBottomBar();
	}

	// ===== UI 构建 =====

	/// <summary>
	/// 构建所有 UI 控件。
	/// </summary>
	private void SetupUI()
	{
		float s = UIScaler.Instance?.GetScaleFactor() ?? 1f;

		// 全尺寸根
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		MouseFilter = MouseFilterEnum.Pass;

		// 暗色背景
		var background = new ColorRect
		{
			Color = new Color(0.06f, 0.06f, 0.1f, 1),
		};
		background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		background.MouseFilter = MouseFilterEnum.Ignore;
		AddChild(background);

		// 主容器（垂直布局：顶栏 + 内容区）
		var mainVBox = new VBoxContainer();
		mainVBox.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		mainVBox.AddThemeConstantOverride("separation", 0);
		AddChild(mainVBox);

		// ===== 顶栏 =====
		var topBar = new HBoxContainer();
		topBar.CustomMinimumSize = new Vector2(0, 48 * s);
		topBar.AddThemeConstantOverride("separation", Mathf.RoundToInt(12 * s));

		// 顶栏背景
		var topBarBg = new StyleBoxFlat
		{
			BgColor = new Color(0.1f, 0.1f, 0.15f, 1),
		};
		topBar.AddThemeStyleboxOverride("panel", topBarBg);

		// 返回按钮
		_backButton = new Button
		{
			Text = Loc.T("ui.collection.back", "← 返回主菜单"),
			CustomMinimumSize = new Vector2(120 * s, 36 * s),
		};
		_backButton.AddThemeFontSizeOverride("font_size", Mathf.RoundToInt(14 * s));
		_backButton.Pressed += OnBackPressed;
		topBar.AddChild(_backButton);

		// 弹性间距
		var topSpacer = new Control { SizeFlagsHorizontal = SizeFlags.Expand };
		topBar.AddChild(topSpacer);

		// 牌组标签
		var deckLabel = new Label
		{
			Text = Loc.T("ui.collection.deck_label", "牌组:"),
			VerticalAlignment = VerticalAlignment.Center,
		};
		deckLabel.AddThemeFontSizeOverride("font_size", Mathf.RoundToInt(14 * s));
		topBar.AddChild(deckLabel);

		// 牌组名称编辑框
		_deckNameEdit = new LineEdit
		{
			CustomMinimumSize = new Vector2(150 * s, 30 * s),
			PlaceholderText = Loc.T("ui.collection.deck_name_placeholder", "输入牌组名称"),
		};
		_deckNameEdit.AddThemeFontSizeOverride("font_size", Mathf.RoundToInt(14 * s));
		_deckNameEdit.TextSubmitted += OnDeckNameSubmitted;
		_deckNameEdit.FocusExited += OnDeckNameFocusExited;
		topBar.AddChild(_deckNameEdit);

		// 牌组切换下拉
		_deckSelector = new OptionButton();
		_deckSelector.CustomMinimumSize = new Vector2(140 * s, 30 * s);
		_deckSelector.AddThemeFontSizeOverride("font_size", Mathf.RoundToInt(13 * s));
		_deckSelector.ItemSelected += OnDeckSelectorChanged;
		topBar.AddChild(_deckSelector);

		// 新建牌组按钮
		_newDeckButton = new Button
		{
			Text = "+",
			TooltipText = Loc.T("ui.collection.new_deck", "新建牌组"),
			CustomMinimumSize = new Vector2(32 * s, 30 * s),
		};
		_newDeckButton.AddThemeFontSizeOverride("font_size", Mathf.RoundToInt(16 * s));
		_newDeckButton.Pressed += OnNewDeckPressed;
		topBar.AddChild(_newDeckButton);

		// 删除牌组按钮
		_deleteDeckButton = new Button
		{
			Text = "✕",
			TooltipText = Loc.T("ui.collection.delete_deck", "删除当前牌组"),
			CustomMinimumSize = new Vector2(32 * s, 30 * s),
		};
		_deleteDeckButton.AddThemeColorOverride("font_color", new Color(0.9f, 0.3f, 0.3f, 1));
		_deleteDeckButton.AddThemeFontSizeOverride("font_size", Mathf.RoundToInt(14 * s));
		_deleteDeckButton.Pressed += OnDeleteDeckPressed;
		topBar.AddChild(_deleteDeckButton);

		mainVBox.AddChild(topBar);

		// 最小卡牌数警告标签
		_minCardsWarning = new Label
		{
			Text = Loc.T("ui.collection.min_cards_warning", "⚠ 牌组不足 10 张卡牌，无法用于战斗"),
			HorizontalAlignment = HorizontalAlignment.Center,
			Visible = false,
		};
		_minCardsWarning.AddThemeFontSizeOverride("font_size", Mathf.RoundToInt(13 * s));
		_minCardsWarning.AddThemeColorOverride("font_color", new Color(1, 0.5f, 0.3f, 1));
		mainVBox.AddChild(_minCardsWarning);

		// ===== 内容区：左右分栏 =====
		var contentSplit = new HSplitContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			DraggerVisibility = SplitContainer.DraggerVisibilityEnum.Visible,
		};
		contentSplit.SplitOffsets = new int[] { Mathf.RoundToInt(300 * s) };
		mainVBox.AddChild(contentSplit);

		// ----- 左侧面板：牌组卡片列表 -----
		var leftPanel = new VBoxContainer();
		_deckPanelRef = leftPanel;
		leftPanel.AddThemeConstantOverride("separation", Mathf.RoundToInt(4 * s));

		// 左侧面板标题
		var leftTitle = new Label
		{
			Text = Loc.T("ui.collection.deck_contents", "牌组内容"),
			HorizontalAlignment = HorizontalAlignment.Center,
		};
		leftTitle.AddThemeFontSizeOverride("font_size", Mathf.RoundToInt(16 * s));
		leftTitle.AddThemeColorOverride("font_color", new Color(1, 1, 1, 0.8f));
		leftPanel.AddChild(leftTitle);

		// 牌组卡片可滚动列表
		var deckListScroll = new ScrollContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		_deckCardList = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		_deckCardList.AddThemeConstantOverride("separation", Mathf.RoundToInt(2 * s));
		deckListScroll.AddChild(_deckCardList);
		leftPanel.AddChild(deckListScroll);

		// 空牌组提示
		_emptyDeckLabel = new Label
		{
			Text = Loc.T("ui.collection.empty_deck", "牌组为空\n\n点击右侧卡牌添加到牌组"),
			HorizontalAlignment = HorizontalAlignment.Center,
			Visible = false,
		};
		_emptyDeckLabel.AddThemeFontSizeOverride("font_size", Mathf.RoundToInt(13 * s));
		_emptyDeckLabel.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.5f, 1));
		_emptyDeckLabel.MouseFilter = MouseFilterEnum.Pass;
		deckListScroll.AddChild(_emptyDeckLabel);

		// 左侧底部区域（VBox 三行布局）
		var leftBottomArea = new VBoxContainer();
		leftBottomArea.AddThemeConstantOverride("separation", Mathf.RoundToInt(4 * s));
		leftBottomArea.Alignment = BoxContainer.AlignmentMode.Center;

		// 行 1：卡牌计数
		_cardCountLabel = new Label
		{
			Text = "0/20",
			VerticalAlignment = VerticalAlignment.Center,
			HorizontalAlignment = HorizontalAlignment.Center,
		};
		_cardCountLabel.AddThemeFontSizeOverride("font_size", Mathf.RoundToInt(16 * s));
		_cardCountLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.85f, 0.4f, 1));
		leftBottomArea.AddChild(_cardCountLabel);

		// 行 2：清空 / 撤销 / 保存
		var actionRow = new HBoxContainer();
		actionRow.AddThemeConstantOverride("separation", Mathf.RoundToInt(6 * s));
		actionRow.Alignment = BoxContainer.AlignmentMode.Center;

		_clearDeckButton = new Button
		{
			Text = Loc.T("ui.collection.clear_deck", "一键清空卡组"),
			CustomMinimumSize = new Vector2(90 * s, 30 * s),
		};
		_clearDeckButton.AddThemeFontSizeOverride("font_size", Mathf.RoundToInt(12 * s));
		_clearDeckButton.Pressed += OnClearDeckPressed;
		actionRow.AddChild(_clearDeckButton);

		_undoButton = new Button
		{
			Text = Loc.T("ui.collection.undo", "撤销"),
			CustomMinimumSize = new Vector2(60 * s, 30 * s),
			Disabled = true,
		};
		_undoButton.AddThemeFontSizeOverride("font_size", Mathf.RoundToInt(12 * s));
		_undoButton.Pressed += OnUndoPressed;
		actionRow.AddChild(_undoButton);

		_saveButton = new Button
		{
			Text = Loc.T("ui.collection.save_changes", "保存"),
			CustomMinimumSize = new Vector2(60 * s, 30 * s),
			Disabled = true,
		};
		_saveButton.AddThemeFontSizeOverride("font_size", Mathf.RoundToInt(12 * s));
		_saveButton.Pressed += OnSavePressed;
		actionRow.AddChild(_saveButton);

		leftBottomArea.AddChild(actionRow);

		// 行 3：导出 / 导入
		var ioRow = new HBoxContainer();
		ioRow.AddThemeConstantOverride("separation", Mathf.RoundToInt(8 * s));
		ioRow.Alignment = BoxContainer.AlignmentMode.Center;

		_exportButton = new Button
		{
			Text = Loc.T("ui.collection.export", "导出牌组"),
			CustomMinimumSize = new Vector2(100 * s, 32 * s),
		};
		_exportButton.AddThemeFontSizeOverride("font_size", Mathf.RoundToInt(13 * s));
		_exportButton.Pressed += OnExportPressed;
		ioRow.AddChild(_exportButton);

		_importButton = new Button
		{
			Text = Loc.T("ui.collection.import", "导入牌组"),
			CustomMinimumSize = new Vector2(100 * s, 32 * s),
		};
		_importButton.AddThemeFontSizeOverride("font_size", Mathf.RoundToInt(13 * s));
		_importButton.Pressed += OnImportPressed;
		ioRow.AddChild(_importButton);

		leftBottomArea.AddChild(ioRow);

		leftPanel.AddChild(leftBottomArea);

		contentSplit.AddChild(leftPanel);

		// ----- 右侧面板：CardGrid -----
		_cardGrid = new CardGrid
		{
			Name = "CollectionCardGrid",
			ShowFilterBar = true,
			ShowPagination = true,
			Clickable = true,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		_cardGrid.OnCardClicked += OnCardGridCardClicked;
		_cardGrid.OnCardDragCompleted += OnCardGridDragCompleted;
		_cardGrid.OnCardLongPressed += ShowCardPreview;
		_cardGrid.OnCardRightClicked += ShowCardPreview;
		contentSplit.AddChild(_cardGrid);

		// ===== 飞入动画层（最高 ZIndex） =====
		_flyLayer = new Control
		{
			Name = "FlyLayer",
			MouseFilter = MouseFilterEnum.Ignore,
			ZIndex = 200,
		};
		_flyLayer.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(_flyLayer);

		// ===== 文件对话框（导出/导入共用） =====
		_fileDialog = new FileDialog
		{
			Access = FileDialog.AccessEnum.Filesystem,
			Title = Loc.T("ui.collection.export_title", "导出牌组"),
		};
		_fileDialog.AddFilter("*.json", Loc.T("ui.collection.deck_file_filter", "JSON 牌组文件"));
		_fileDialog.FileSelected += OnFileDialogFileSelected;
		_fileDialog.Canceled += OnFileDialogCancelled;
		AddChild(_fileDialog);
	}

	// ===== 刷新 =====

	/// <summary>
	/// 刷新整个界面（顶栏 + 牌组列表 + CardGrid + 底部栏）。
	/// </summary>
	private void RefreshAll()
	{
		// 确保数据一致性（首次进入或数据异常时修复）
		GameManager.Instance.VerifyAndRepairCollection();

		RefreshTopBar();
		RefreshDeckList();
		RefreshCardGrid();
		RefreshBottomBar();
	}

	/// <summary>
	/// 刷新顶栏：牌组名称、牌组选择器。
	/// </summary>
	private void RefreshTopBar()
	{
		var deck = GameManager.Instance.ActiveDeck;

		_deckNameEdit.Text = deck?.Name ?? "";
		_deleteDeckButton.Disabled = GameManager.Instance.Decks.Count <= 1;

		// 牌组选择器
		_deckSelector.Clear();
		foreach (var d in GameManager.Instance.Decks)
		{
			_deckSelector.AddItem($"{d.Name} ({d.CardCount})");
		}

		int activeIndex = GameManager.Instance.ActiveDeckIndex;
		if (activeIndex >= 0 && activeIndex < _deckSelector.ItemCount)
			_deckSelector.Select(activeIndex);

		// 最小卡牌数警告
		_minCardsWarning.Visible = deck != null && !deck.MeetsMinimum();
	}

	/// <summary>
	/// 刷新底部栏：卡牌计数颜色、撤销/保存按钮状态。
	/// </summary>
	private void RefreshBottomBar()
	{
		var deck = GameManager.Instance.ActiveDeck;
		_cardCountLabel.Text = $"{deck?.CardCount ?? 0}/{Deck.MaxDeckSize}";

		// 牌组计数色
		if (deck != null && deck.CardCount > Deck.MaxDeckSize)
			_cardCountLabel.AddThemeColorOverride("font_color", new Color(1, 0.4f, 0.3f, 1));
		else if (deck != null && !deck.MeetsMinimum())
			_cardCountLabel.AddThemeColorOverride("font_color", new Color(1, 0.8f, 0.3f, 1));
		else
			_cardCountLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.85f, 0.4f, 1));

		_undoButton.Disabled = !_hasUnsavedChanges;
		_saveButton.Disabled = !_hasUnsavedChanges;
	}

	/// <summary>
	/// 获取安全的卡牌显示名称。优先使用本地化名称，回调到硬编码名称。
	/// </summary>
	private static string GetSafeCardName(CardData cardData)
	{
		var localized = cardData.GetLocalizedName();
		if (!string.IsNullOrEmpty(localized))
			return localized;

		// 回调到 .tres 中的硬编码名称
		var hardcoded = cardData.CardName;
		if (!string.IsNullOrEmpty(hardcoded))
			return hardcoded;

		// 最后回调到 Id
		return cardData.Id ?? "???";
	}

	/// <summary>
	/// 根据卡牌稀有度返回对应的颜色。
	/// </summary>
	private static Color GetRarityColor(CardRarity rarity)
	{
		return rarity switch
		{
			CardRarity.Derivative => new Color(0.53f, 0.53f, 0.53f, 1),  // 灰色 — 衍生卡
			CardRarity.Master => new Color(1f, 0.84f, 0f, 1),            // 亮金色 — 金卡
			CardRarity.Excellent => new Color(0.75f, 0.75f, 0.75f, 1),   // 银白色 — 银卡
			CardRarity.Good => new Color(0.8f, 0.5f, 0.2f, 1),           // 铜色 — 铜卡
			CardRarity.Common => new Color(0.53f, 0.53f, 0.53f, 1),      // 灰色 — 铁卡
			CardRarity.Special => new Color(1f, 0.55f, 0f, 1),           // 橙色 — 特殊卡
			_ => new Color(0.6f, 0.6f, 0.6f, 1),
		};
	}

	/// <summary>
	/// 刷新左侧牌组卡片列表（按 ID 分组显示）。
	/// </summary>
	private void RefreshDeckList()
	{
		float s = UIScaler.Instance?.GetScaleFactor() ?? 1f;

		ClearDeckRowMobileZones();

		// 清除旧条目
		foreach (var child in _deckCardList.GetChildren())
		{
			child.QueueFree();
		}

		var deck = GameManager.Instance.ActiveDeck;
		if (deck == null || deck.CardCount == 0)
		{
			_emptyDeckLabel.Visible = true;
			return;
		}

		_emptyDeckLabel.Visible = false;

		// 按 ID 分组
		var grouped = new Dictionary<string, (CardData Card, int Count)>();
		foreach (var card in deck.Cards)
		{
			if (grouped.TryGetValue(card.Id, out var entry))
				grouped[card.Id] = (entry.Card, entry.Count + 1);
			else
				grouped[card.Id] = (card, 1);
		}

		// 按费用排序
		var sorted = grouped.Values.OrderBy(g => g.Card.Cost).ThenBy(g => g.Card.GetLocalizedName());

		foreach (var (cardData, count) in sorted)
		{
			var displayName = GetSafeCardName(cardData);

			// 用 Panel + Label 替代 Button（避免 Godot Button 主题干扰）
			var row = new Panel
			{
				CustomMinimumSize = new Vector2(0, 28 * s),
				SizeFlagsHorizontal = SizeFlags.ExpandFill,
				MouseFilter = MouseFilterEnum.Stop,
			};
			var rowStyle = new StyleBoxFlat
			{
				BgColor = new Color(0.12f, 0.12f, 0.18f, 1),
				CornerRadiusTopLeft = 3,
				CornerRadiusTopRight = 3,
				CornerRadiusBottomLeft = 3,
				CornerRadiusBottomRight = 3,
			};
			row.AddThemeStyleboxOverride("panel", rowStyle);

			// 内部 HBox 布局
			var innerRow = new HBoxContainer();
			innerRow.AddThemeConstantOverride("separation", Mathf.RoundToInt(4 * s));
			innerRow.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			innerRow.MouseFilter = MouseFilterEnum.Ignore;
			row.AddChild(innerRow);

			// 卡牌名称
			var nameLabel = new Label
			{
				Text = displayName,
				SizeFlagsHorizontal = SizeFlags.ExpandFill,
				VerticalAlignment = VerticalAlignment.Center,
				ClipText = true,
			};
			nameLabel.AddThemeFontSizeOverride("font_size", Mathf.RoundToInt(13 * s));
			nameLabel.AddThemeColorOverride("font_color", new Color(0.95f, 0.92f, 0.85f, 1));
			nameLabel.MouseFilter = MouseFilterEnum.Ignore;
			innerRow.AddChild(nameLabel);

			// 数量徽章
			var countLabel = new Label
			{
				Text = $"×{count}",
				VerticalAlignment = VerticalAlignment.Center,
				HorizontalAlignment = HorizontalAlignment.Right,
				CustomMinimumSize = new Vector2(36 * s, 0),
			};
			countLabel.AddThemeFontSizeOverride("font_size", Mathf.RoundToInt(13 * s));
			countLabel.AddThemeColorOverride("font_color", GetRarityColor(cardData.Rarity));
			countLabel.MouseFilter = MouseFilterEnum.Ignore;
			innerRow.AddChild(countLabel);

			// 点击/拖拽逻辑
			bool rowDragStarted = false;
			// 移动端触控状态（区分轻触移除 vs 滚动牌组列表）
			Vector2 rowTouchStart = Vector2.Zero;
			bool rowTouchMoved = false;
			const float ScrollThreshold = 10f;

			row.GuiInput += (InputEvent @event) =>
			{
				if (MobileInputRouter.IsMobile)
				{
					return;
				}

				if (MobileInputHelper.IsMobile)
				{
					if (@event is InputEventScreenTouch touch)
					{
						if (touch.Pressed)
						{
							rowTouchStart = touch.Position;
							rowTouchMoved = false;
						}
						else
						{
							if (!rowTouchMoved)
								OnDeckCardRemoveClicked(cardData);
						}
						row.AcceptEvent();
						return;
					}
					else if (@event is InputEventScreenDrag drag)
					{
						float dist = drag.Position.DistanceTo(rowTouchStart);
						if (dist > ScrollThreshold)
						{
							rowTouchMoved = true;
							return; // 不消费——让 ScrollContainer 处理
						}
						row.AcceptEvent();
						return;
					}
				}

				if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left)
				{
					if (mb.Pressed)
					{
						rowDragStarted = true;
						_deckDraggingCardData = cardData;
						_deckDragStartPos = row.GetGlobalMousePosition();
						_deckIsDragging = false;
						row.AcceptEvent();
					}
					else
					{
						if (!_deckIsDragging && rowDragStarted)
						{
							OnDeckCardRemoveClicked(cardData);
						}
						else if (_deckIsDragging)
						{
							var dropPos = GetViewport().GetMousePosition();
							Rect2 gridRect = new(_cardGrid.GlobalPosition, _cardGrid.Size);
							if (gridRect.HasPoint(dropPos))
								OnDeckCardRemoveClicked(cardData);
							CleanupDeckDrag();
						}
						rowDragStarted = false;
						_deckDraggingCardData = null;
					}
				}
				else if (@event is InputEventMouseMotion
					&& rowDragStarted && _deckDraggingCardData != null)
				{
					float dist = row.GetGlobalMousePosition().DistanceTo(_deckDragStartPos);
					if (dist > DeckDragThreshold && !_deckIsDragging)
					{
						_deckIsDragging = true;
						StartDeckDragClone(cardData, _deckDragStartPos);
					}
					if (_deckIsDragging && _deckDragClone != null)
					{
						_deckDragClone.GlobalPosition = row.GetGlobalMousePosition()
							- (_deckDragClone.Size / 2);
					}
				}
				else if (@event is InputEventMouseButton rmb
					&& rmb.Pressed && rmb.ButtonIndex == MouseButton.Right)
				{
					OnDeckCardRemoveClicked(cardData);
					row.AcceptEvent();
				}
			};

			if (MobileInputRouter.IsMobile)
			{
				_deckRowZoneTokens.Add(MobileInputRouter.Instance.RegisterDragZone(
					row,
					row.GetGlobalRect(),
					priority: 260,
					onTap: () => OnDeckCardRemoveClicked(cardData),
					onDragStart: startPos =>
					{
						_deckDraggingCardData = cardData;
						_deckDragStartPos = startPos;
						_deckIsDragging = true;
						StartDeckDragClone(cardData, startPos);
					},
					onDragMove: (pos, _) =>
					{
						if (_deckDragClone != null)
						{
							_deckDragClone.GlobalPosition = pos - (_deckDragClone.Size / 2);
						}
					},
					onDragEnd: dropPos =>
					{
						Rect2 gridRect = new(_cardGrid.GlobalPosition, _cardGrid.Size);
						if (_deckDraggingCardData != null && gridRect.HasPoint(dropPos))
						{
							OnDeckCardRemoveClicked(_deckDraggingCardData);
						}
						_deckDraggingCardData = null;
						CleanupDeckDrag();
					}));
			}

			_deckCardList.AddChild(row);
		}
	}

	private void ClearDeckRowMobileZones()
	{
		foreach (var token in _deckRowZoneTokens)
		{
			token.Dispose();
		}
		_deckRowZoneTokens.Clear();
	}

	/// <summary>
	/// 刷新右侧 CardGrid：只显示已解锁的卡牌。
	/// </summary>
	private void RefreshCardGrid()
	{
		var allCards = GameManager.Instance.GetAllCards();
		var ownedCardIds = GameManager.Instance.OwnedCardIds;

		GD.Print($"[CollectionUI] RefreshCardGrid — allCards: {allCards.Count}, ownedIds: {ownedCardIds.Count}");
		if (allCards.Count > 0)
			GD.Print($"[CollectionUI] 首张卡牌 ID: {allCards[0].Id}, Name: {GetSafeCardName(allCards[0])}");

		var ownedCards = allCards
			.Where(c => GameManager.Instance.OwnedCardIds.Contains(c.Id))
			.ToList();

		GD.Print($"[CollectionUI] RefreshCardGrid — 过滤后 ownedCards: {ownedCards.Count}");
		_cardGrid.SetCards(ownedCards);
	}

	// ===== 退出拦截 ====

	/// <summary>
	/// 显示未保存更改确认对话框。
	/// </summary>
	private void ShowUnsavedChangesDialog(
		Action onSave,
		Action onDiscard,
		Action? onCancel = null,
		string? saveLabel = null,
		string? discardLabel = null,
		string? cancelLabel = null,
		string? message = null)
	{
		var dialog = new AcceptDialog
		{
			Title = Loc.T("ui.collection.unsaved_title", "未保存的修改"),
			Exclusive = true,
		};

		// 隐藏默认 OK 按钮
		dialog.GetOkButton().Visible = false;

		var msgLabel = new Label
		{
			Text = message ?? Loc.T("ui.collection.unsaved_message", "当前牌组有未保存的更改。\n是否保存？"),
			HorizontalAlignment = HorizontalAlignment.Center,
		};
		msgLabel.AddThemeFontSizeOverride("font_size", 14);
		dialog.AddChild(msgLabel);

		var saveBtn = dialog.AddButton(saveLabel ?? Loc.T("ui.collection.unsaved_save", "保存"), true);
		var discardBtn = dialog.AddButton(discardLabel ?? Loc.T("ui.collection.unsaved_discard", "不保存"), true);
		var cancelBtn = dialog.AddButton(cancelLabel ?? Loc.T("ui.collection.unsaved_cancel", "取消"), true);

		saveBtn.Pressed += () =>
		{
			dialog.Hide();
			onSave();
			dialog.QueueFree();
		};
		discardBtn.Pressed += () =>
		{
			dialog.Hide();
			onDiscard();
			dialog.QueueFree();
		};
		cancelBtn.Pressed += () =>
		{
			dialog.Hide();
			(onCancel ?? (() => { }))();
			dialog.QueueFree();
		};

		AddChild(dialog);
		dialog.PopupCentered();
	}

	private void GoBack()
	{
		// 使用 CallDeferred 延迟场景切换，避免在输入事件回调链路中直接 ChangeSceneToFile
		// 导致 Godot _push_unhandled_input_internal 访问已脱离场景树的节点
		GetTree().CallDeferred(SceneTree.MethodName.ChangeSceneToFile, "res://Scenes/Main.tscn");
	}

	private void SwitchToPendingDeck()
	{
		DoSwitchDeck(_pendingDeckIndex);
		_pendingDeckIndex = -1;
	}

	private void DoSwitchDeck(int newIndex)
	{
		GameManager.Instance.SetActiveDeck(newIndex);
		CaptureCheckpoint();
		RefreshAll();
		GD.Print($"[CollectionUI] 切换到牌组 {newIndex}");
	}

	// ===== 事件处理 =====

	private void OnBackPressed()
	{
		GD.Print("[CollectionUI] 返回主菜单");
		if (_hasUnsavedChanges)
		{
			ShowUnsavedChangesDialog(
				onSave: () =>
				{
					if (TrySaveDeck())
						GoBack();
				},
				onDiscard: GoBack
			);
		}
		else
		{
			GoBack();
		}
	}

	private void OnCardGridCardClicked(CardData cardData)
	{
		if (cardData == null)
			return;

		var deck = GameManager.Instance.ActiveDeck;
		if (deck == null)
		{
			ShowNotification(Loc.T("ui.collection.no_deck_selected", "请先创建或选择一个牌组"));
			return;
		}

		// 飞入动画：从点击位置飞到牌组面板
		float s = UIScaler.Instance?.GetScaleFactor() ?? 1f;
		var cardSize = new Vector2(60 * s, 90 * s);  // 缩小版
		var clickPos = GetViewport().GetMousePosition();

		var card = new OdysseyCards.Card.Card(cardData);
		var flyCard = new CardUI
		{
			DisplayOnly = true,
			Modulate = new Color(1, 1, 1, 0.9f),
			CustomMinimumSize = cardSize,
			Size = cardSize,
			GlobalPosition = clickPos - (cardSize / 2),
		};
		flyCard.SetCard(card);
		_flyLayer.AddChild(flyCard);

		// 目标位置：牌组面板中心
		Vector2 targetPos = _deckPanelRef.GlobalPosition + (_deckPanelRef.Size / 2) - (cardSize / 2);

		var tween = CreateTween();
		tween.TweenProperty(flyCard, "global_position", targetPos, 0.25)
			.SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
		tween.TweenCallback(Callable.From(() =>
		{
			flyCard.QueueFree();
			// 实际添加卡牌到牌组
			bool added = GameManager.Instance.AddCardToActiveCollectionDeck(cardData);
			if (added)
			{
				GD.Print($"[CollectionUI] 添加卡牌到牌组: {GetSafeCardName(cardData)}");
				MarkChanged();
				RefreshTopBar();
				RefreshDeckList();
			}
		}));
	}

	private void OnCardGridDragCompleted(CardData cardData, Vector2 dropScreenPos)
	{
		if (cardData == null)
			return;

		var deck = GameManager.Instance.ActiveDeck;
		if (deck == null)
			return;

		// 检查松手位置是否在牌组面板上方
		Rect2 deckRect = new(_deckPanelRef.GlobalPosition, _deckPanelRef.Size);
		if (!deckRect.HasPoint(dropScreenPos))
			return;

		// 直接添加到牌组（拖拽不需要飞入动画）
		bool added = GameManager.Instance.AddCardToActiveCollectionDeck(cardData);
		if (added)
		{
			GD.Print($"[CollectionUI] 拖拽添加卡牌到牌组: {GetSafeCardName(cardData)}");
			MarkChanged();
			RefreshTopBar();
			RefreshDeckList();
		}
	}

	private void OnDeckCardRemoveClicked(CardData cardData)
	{
		if (cardData == null)
			return;

		float s = UIScaler.Instance?.GetScaleFactor() ?? 1f;
		var cardSize = new Vector2(60 * s, 90 * s);
		var startPos = GetViewport().GetMousePosition();

		// 创建飞行动画卡片
		var card = new OdysseyCards.Card.Card(cardData);
		var flyCard = new CardUI
		{
			DisplayOnly = true,
			Modulate = new Color(1, 1, 1, 0.85f),
			CustomMinimumSize = cardSize,
			Size = cardSize,
			GlobalPosition = startPos - (cardSize / 2),
		};
		flyCard.SetCard(card);
		_flyLayer.AddChild(flyCard);

		// 目标位置：CardGrid 中心（如果卡牌在网格中不可见，飞向网格中心）
		Vector2 targetPos = _cardGrid.GlobalPosition + (_cardGrid.Size / 2) - (cardSize / 2);

		var tween = CreateTween();
		tween.TweenProperty(flyCard, "global_position", targetPos, 0.3)
			.SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Cubic);
		tween.TweenCallback(Callable.From(() =>
		{
			flyCard.QueueFree();
			GameManager.Instance.RemoveCardFromActiveCollectionDeck(cardData);
			GD.Print($"[CollectionUI] 从牌组移除卡牌: {GetSafeCardName(cardData)}");
			MarkChanged();
			RefreshTopBar();
			RefreshDeckList();
		}));
	}

	/// <summary>
	/// 移动端长按卡牌时显示居中大图预览。轻触遮罩关闭。
	/// </summary>
	private void ShowCardPreview(CardData cardData)
	{
		if (cardData == null)
			return;

		CloseCardPreview();

		Vector2 viewportSize = GetViewportRect().Size;
		var overlay = new Control
		{
			Name = "CardPreviewOverlay",
			MouseFilter = MouseFilterEnum.Stop,
			ZIndex = 500,
		};
		overlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

		var dim = new ColorRect
		{
			Color = new Color(0, 0, 0, 0.72f),
			MouseFilter = MouseFilterEnum.Stop,
		};
		dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		overlay.AddChild(dim);

		float uiScale = UIScaler.Instance?.GetScaleFactor() ?? 1f;
		float previewHeight = Mathf.Min(viewportSize.Y * 0.86f, 430f * uiScale);
		float renderScale = previewHeight / CardUI.DESIGN_HEIGHT;
		float previewWidth = renderScale * CardUI.DESIGN_WIDTH;
		if (previewWidth > viewportSize.X * 0.72f)
		{
			renderScale = viewportSize.X * 0.72f / CardUI.DESIGN_WIDTH;
		}

		var previewCard = new CardUI
		{
			DisplayOnly = true,
			RenderScaleOverride = renderScale,
		};
		previewCard.SetCard(new OdysseyCards.Card.Card(cardData));
		overlay.AddChild(previewCard);

		overlay.GuiInput += @event =>
		{
			if (@event is InputEventMouseButton { Pressed: true }
				|| @event is InputEventScreenTouch { Pressed: true })
			{
				CloseCardPreview();
				overlay.AcceptEvent();
			}
		};

		_cardPreviewOverlay = overlay;
		_hasCardPreviewOverlay = true;
		AddChild(overlay);

		// _Ready 在 AddChild 后已执行，Size 已由 renderScale 计算完毕，此时居中定位。
		previewCard.GlobalPosition = (viewportSize - previewCard.Size) / 2f;

		if (MobileInputRouter.IsMobile)
		{
			_cardPreviewTouchToken = MobileInputRouter.Instance.RegisterTapZone(
				overlay,
				overlay.GetGlobalRect(),
				priority: 350,
				onTap: CloseCardPreview
			);
			MobileInputRouter.Instance.PushModalLayer(overlay);
		}
	}

	private void CloseCardPreview()
	{
		if (!_hasCardPreviewOverlay)
			return;

		if (MobileInputRouter.IsMobile)
		{
			_cardPreviewTouchToken?.Dispose();
			_cardPreviewTouchToken = null;
			MobileInputRouter.Instance.PopModalLayer(_cardPreviewOverlay);
		}

		_cardPreviewOverlay.QueueFree();
		_hasCardPreviewOverlay = false;
		_cardPreviewOverlay = null;
	}

	private void StartDeckDragClone(CardData cardData, Vector2 startScreenPos)
	{
		float s = UIScaler.Instance?.GetScaleFactor() ?? 1f;
		var cardSize = new Vector2(CardUI.DESIGN_WIDTH * s, CardUI.DESIGN_HEIGHT * s);

		var card = new OdysseyCards.Card.Card(cardData);
		_deckDragClone = new CardUI
		{
			DisplayOnly = true,
			Modulate = new Color(1, 1, 1, 0.75f),
			CustomMinimumSize = cardSize,
			Size = cardSize,
			GlobalPosition = startScreenPos - (cardSize / 2),
		};
		_deckDragClone.SetCard(card);
		_flyLayer.AddChild(_deckDragClone);
	}

	private void CleanupDeckDrag()
	{
		_deckIsDragging = false;
		if (_deckDragClone != null)
		{
			_deckDragClone.QueueFree();
			_deckDragClone = null;
		}
	}


	private void OnDeckNameSubmitted(string newName)
	{
		ApplyDeckName(newName);
	}

	private void OnDeckNameFocusExited()
	{
		ApplyDeckName(_deckNameEdit.Text);
	}

	private void ApplyDeckName(string newName)
	{
		var deck = GameManager.Instance.ActiveDeck;
		if (deck == null || string.IsNullOrWhiteSpace(newName))
			return;

		if (deck.Name != newName)
		{
			deck.Name = newName.Trim();
			MarkChanged();
			RefreshTopBar();
			GD.Print($"[CollectionUI] 牌组重命名 → {deck.Name}");
		}
	}

	private void OnDeckSelectorChanged(long index)
	{
		var newIndex = (int)index;
		if (newIndex == GameManager.Instance.ActiveDeckIndex)
			return;

		if (_hasUnsavedChanges)
		{
			_pendingDeckIndex = newIndex;
			ShowUnsavedChangesDialog(
				onSave: () =>
				{
					if (TrySaveDeck())
						SwitchToPendingDeck();
				},
				onDiscard: () =>
				{
					RestoreCheckpoint();
					SwitchToPendingDeck();
				},
				onCancel: () =>
				{
					// 恢复选择器到原索引
					_deckSelector.Select(GameManager.Instance.ActiveDeckIndex);
					_pendingDeckIndex = -1;
				}
			);
		}
		else
		{
			DoSwitchDeck(newIndex);
		}
	}

	private void OnNewDeckPressed()
	{
		if (_hasUnsavedChanges)
		{
			ShowUnsavedChangesDialog(
				onSave: () =>
				{
					if (TrySaveDeck())
						DoCreateNewDeck();
				},
				onDiscard: DoCreateNewDeck
			);
		}
		else
		{
			DoCreateNewDeck();
		}
	}

	private void DoCreateNewDeck()
	{
		string defaultName = Loc.T("ui.collection.default_deck_name", "新牌组");
		int count = GameManager.Instance.Decks.Count(d => d.Name.StartsWith(defaultName, StringComparison.Ordinal));
		string name = count > 0 ? $"{defaultName} {count + 1}" : defaultName;

		GameManager.Instance.CreateDeck(name);
		GameManager.Instance.SaveToDisk();
		CaptureCheckpoint();
		RefreshAll();
		GD.Print($"[CollectionUI] 创建新牌组: {name}");
	}

	private void OnDeleteDeckPressed()
	{
		var deck = GameManager.Instance.ActiveDeck;
		if (deck == null)
			return;

		// 最后一个牌组不能删除
		if (GameManager.Instance.Decks.Count <= 1)
		{
			ShowNotification(Loc.T("ui.collection.cannot_delete_last", "至少需要保留一个牌组"));
			return;
		}

		// 空牌组且无未保存更改 → 直接删；其余情况需要确认
		if (deck.CardCount == 0 && !_hasUnsavedChanges)
		{
			DoDeleteDeck(deck.Name);
			return;
		}

		ShowDeleteConfirmDialog(deck);
	}

	/// <summary>
	/// 非空牌组删除确认弹窗。
	/// </summary>
	private void ShowDeleteConfirmDialog(OdysseyCards.Character.Deck deck)
	{
		var confirm = new ConfirmationDialog
		{
			Title = Loc.T("ui.collection.delete_confirm_title", "确认删除牌组"),
			DialogText = Loc.T("ui.collection.delete_confirm_message", "确定要删除牌组「{name}」吗？（共 {count} 张卡牌）")
				.Replace("{name}", deck.Name)
				.Replace("{count}", deck.CardCount.ToString()),
			Exclusive = true,
		};
		confirm.Confirmed += () =>
		{
			confirm.QueueFree();
			ExecuteDelete(deck);
		};
		confirm.Canceled += () => confirm.QueueFree();
		AddChild(confirm);
		confirm.PopupCentered();
	}

	/// <summary>
	/// 执行牌组删除（处理未保存变更后）。
	/// </summary>
	private void ExecuteDelete(OdysseyCards.Character.Deck deck)
	{
		if (_hasUnsavedChanges)
		{
			ShowUnsavedChangesDialog(
				onSave: () =>
				{
					if (TrySaveDeck())
						DoDeleteDeck(deck.Name);
				},
				onDiscard: () => DoDeleteDeck(deck.Name)
			);
		}
		else
		{
			DoDeleteDeck(deck.Name);
		}
	}

	private void DoDeleteDeck(string deckName)
	{
		GameManager.Instance.DeleteDeck(GameManager.Instance.ActiveDeckIndex);
		GameManager.Instance.SaveToDisk();
		CaptureCheckpoint();
		RefreshAll();
		GD.Print($"[CollectionUI] 删除牌组: {deckName}");
	}

	// ===== 保存/撤销/清空按钮 =====

	/// <summary>
	/// 尝试保存牌组。如果牌组超过上限则弹出警告并返回 false。
	/// </summary>
	private bool TrySaveDeck()
	{
		var deck = GameManager.Instance.ActiveDeck;
		if (deck != null && deck.CardCount > Deck.MaxDeckSize)
		{
			ShowDeckOverflowDialog();
			return false;
		}
		CaptureCheckpoint();
		GameManager.Instance.SaveToDisk();
		return true;
	}

	/// <summary>
	/// 牌组超过上限的警告弹窗。
	/// </summary>
	private void ShowDeckOverflowDialog()
	{
		var dialog = new AcceptDialog
		{
			Title = Loc.T("ui.collection.deck_overflow_title", "牌组超过上限"),
			OkButtonText = Loc.T("ui.common.ok", "确定"),
			Exclusive = true,
		};
		var label = new Label
		{
			Text = Loc.T("ui.collection.deck_overflow_message", "牌组超过上限（最多 20 张）。\n请减少卡牌后再保存。"),
			HorizontalAlignment = HorizontalAlignment.Center,
		};
		label.AddThemeFontSizeOverride("font_size", 14);
		dialog.AddChild(label);
		dialog.Confirmed += () => dialog.QueueFree();
		AddChild(dialog);
		dialog.PopupCentered();
	}

	private void OnSavePressed()
	{
		if (TrySaveDeck())
		{
			ShowNotification(Loc.T("ui.collection.deck_saved", "牌组已保存"));
			RefreshBottomBar();
		}
		GD.Print("[CollectionUI] 牌组保存尝试");
	}

	private void OnUndoPressed()
	{
		RestoreCheckpoint();
		ShowNotification(Loc.T("ui.collection.changes_undone", "已撤销未保存的更改"));
	}

	private void OnClearDeckPressed()
	{
		var deck = GameManager.Instance.ActiveDeck;
		if (deck == null || deck.CardCount == 0)
			return;

		var confirm = new ConfirmationDialog
		{
			Title = Loc.T("ui.collection.clear_confirm_title", "确认清空"),
			DialogText = Loc.T("ui.collection.clear_confirm_message", "确定要清空当前牌组中的所有卡牌吗？"),
			Exclusive = true,
		};

		confirm.Confirmed += () =>
		{
			deck.Cards.Clear();
			MarkChanged();
			RefreshAll();
			confirm.QueueFree();
			GD.Print("[CollectionUI] 牌组已清空");
		};
		confirm.Canceled += () => confirm.QueueFree();

		AddChild(confirm);
		confirm.PopupCentered();
	}

	// ===== 导出/导入 =====

	private void OnExportPressed()
	{
		var deck = GameManager.Instance.ActiveDeck;
		if (deck == null)
		{
			ShowNotification(Loc.T("ui.collection.no_deck_to_export", "没有可导出的牌组"));
			return;
		}

		_isExportMode = true;
		_fileDialog.Title = Loc.T("ui.collection.export_title", "导出牌组");
		_fileDialog.FileMode = FileDialog.FileModeEnum.SaveFile;
		_fileDialog.CurrentFile = $"{deck.Name}.json";
		_fileDialog.PopupCentered();
	}

	private void OnImportPressed()
	{
		if (_hasUnsavedChanges)
		{
			ShowUnsavedChangesDialog(
				onSave: () =>
				{
					if (TrySaveDeck())
						OpenImportDialog();
				},
				onDiscard: OpenImportDialog,
				saveLabel: Loc.T("ui.collection.import_warn_save", "保存并导入"),
				discardLabel: Loc.T("ui.collection.import_warn_discard", "不保存直接导入"),
				cancelLabel: Loc.T("ui.collection.import_warn_cancel", "取消"),
				message: Loc.T("ui.collection.import_unsaved_warning", "当前牌组有未保存的更改。\n导入牌组前是否保存当前改动？")
			);
		}
		else
		{
			OpenImportDialog();
		}
	}

	private void OpenImportDialog()
	{
		_isExportMode = false;
		_fileDialog.Title = Loc.T("ui.collection.import_title", "导入牌组");
		_fileDialog.FileMode = FileDialog.FileModeEnum.OpenFile;
		_fileDialog.CurrentFile = "";
		_fileDialog.PopupCentered();
	}

	private void OnFileDialogFileSelected(string path)
	{
		if (_isExportMode)
		{
			if (GameManager.Instance.ExportActiveDeck(path))
				ShowNotification(Loc.T("ui.collection.export_success", "牌组导出成功"));
			else
				ShowNotification(Loc.T("ui.collection.export_failed", "牌组导出失败"));
		}
		else
		{
			if (GameManager.Instance.ImportDeck(path))
			{
				ShowNotification(Loc.T("ui.collection.import_success", "牌组导入成功"));
				CaptureCheckpoint();
				RefreshAll();
			}
			else
			{
				ShowNotification(Loc.T("ui.collection.import_failed", "牌组导入失败（文件格式可能无效）"));
			}
		}
	}

	private void OnFileDialogCancelled()
	{
		GD.Print("[CollectionUI] 文件选择已取消");
	}

	private void OnLanguageChanged(string lang)
	{
		RefreshLanguageTexts();
	}

	private void OnCollectionChanged()
	{
		RefreshAll();
	}

	// ===== 语言刷新 =====

	/// <summary>
	/// 刷新所有可本地化的文本。
	/// </summary>
	private void RefreshLanguageTexts()
	{
		_backButton.Text = Loc.T("ui.collection.back", "← 返回主菜单");
		_deckNameEdit.PlaceholderText = Loc.T("ui.collection.deck_name_placeholder", "输入牌组名称");
		_newDeckButton.TooltipText = Loc.T("ui.collection.new_deck", "新建牌组");
		_deleteDeckButton.TooltipText = Loc.T("ui.collection.delete_deck", "删除当前牌组");
		_minCardsWarning.Text = Loc.T("ui.collection.min_cards_warning", "⚠ 牌组不足 10 张卡牌，无法用于战斗");
		_clearDeckButton.Text = Loc.T("ui.collection.clear_deck", "一键清空卡组");
		_undoButton.Text = Loc.T("ui.collection.undo", "撤销");
		_saveButton.Text = Loc.T("ui.collection.save_changes", "保存");

		// 刷新 CardGrid（CardGrid 自身有 Refresh 方法处理语言切换）
		_cardGrid.Refresh();

		// 刷新牌组列表（重新生成卡牌名称文本）
		RefreshDeckList();
	}

	// ===== 辅助方法 =====

	/// <summary>
	/// 显示一个临时通知弹出框。
	/// </summary>
	private void ShowNotification(string message)
	{
		var popup = new AcceptDialog
		{
			Title = Loc.T("ui.collection.notification_title", "提示"),
			OkButtonText = Loc.T("ui.common.ok", "确定"),
			Exclusive = true,
		};

		var label = new Label
		{
			Text = message,
			HorizontalAlignment = HorizontalAlignment.Center,
		};
		label.AddThemeFontSizeOverride("font_size", 14);
		popup.AddChild(label);

		popup.Confirmed += () => popup.QueueFree();
		AddChild(popup);
		popup.PopupCentered();
	}
}
