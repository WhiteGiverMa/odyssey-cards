#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using OdysseyCards.Character;
using OdysseyCards.Combat;
using OdysseyCards.Infrastructure;

namespace OdysseyCards.UI;

/// <summary>
/// 手牌管理组件 — STS2 风格重构版。
/// 卡片在屏幕底部折叠（仅露出顶部 ~30%），鼠标悬停时上浮 + 放大，
/// 相邻卡牌被推开。使用手动布局替代 HBoxContainer，
/// HandUI._Process 追踪鼠标位置管理悬停状态，避免闪烁。
/// </summary>
public partial class HandUI : Control
{
	private bool _wasMobileTouchActive;

	/// <summary>
	/// 卡牌折叠态可见部分（设计单位）。90 设计单位 ≈ 50% 卡牌高度（考虑 BASE_SCALE=0.85 后的视觉效果）。
	/// 实际像素 = 90 * UIScaler.CurrentScale。
	/// </summary>
	public const float COLLAPSED_VISIBLE = 90f;

	[Export] public PackedScene? CardScene { get; set; }

	public event Action<Card.Card>? OnCardSelectedForPlay;
	public event Action<Card.Card, ICommander>? OnCardPlayRequested;
	public event Action? OnCardCancelled;

	public bool HandSelectMode { get; set; }

	public void SetHandSelectionMode(bool enabled)
	{
		HandSelectMode = enabled;
		foreach (var slot in _cardSlots)
		{
			if (slot.CardUI == null) continue;

			slot.CardUI.PreventDrag = enabled;
			if (enabled)
				slot.CardUI.RemoveHoverEffect();
		}
		_hoveredSlot = null;
	}

	public event Action<Card.Card, bool>? OnCardSelectionToggled;

	// ============================================================
	// 内部状态
	// ============================================================

	private Control _cardContainer = null!;
	private Player? _player;
	private CombatManager? _combat;
	private readonly List<CardSlot> _cardSlots = new();
	private Card.Card? _selectedCard;
	private CardSlot? _hoveredSlot;

	/// <summary>移动端点击展开的卡槽（非 hover，手动维持）</summary>
	private CardSlot? _tappedSlot;

	/// <summary>键盘焦点卡牌索引（-1 = 无焦点）。方向键导航或数字键直选时更新。</summary>
	private int _focusedCardIndex = -1;

	/// <summary>当前选牌是否由键盘快捷键触发（供 CombatUI 调整动画起点）。</summary>
	public bool IsKeyboardSelection { get; private set; }

	/// <summary>当前正在显示键盘焦点视觉的 CardUI。用于清除旧焦点时重置 SelfModulate。</summary>
	private CardUI? _keyboardFocusedCardUI;

	/// <summary>缓存的 HotkeyManager 回调委托——用于 Push/Remove 配对，保证引用相等。</summary>
	private Action[]? _cardSelectActions;
	private Action? _leftAction;
	private Action? _rightAction;
	private Action? _acceptAction;
	private Action? _cancelAction;

	/// <summary>
	/// 存储每张卡牌在其父容器中的"静止位置"（不含 OffsetTop），
	/// 用于悬停检测——始终基于静止位置判断鼠标是否在卡牌区域内，
	/// 避免卡牌上浮后鼠标相对位置变化导致闪烁。
	/// </summary>
	private readonly Dictionary<CardUI, Vector2> _restingPositions = new();

	// ============================================================
	// 布局常量
	// ============================================================

	/// <summary>卡牌折叠态基础缩放</summary>
	private const float BASE_SCALE = 0.85f;

	/// <summary>
	/// 交叠系数：每张卡从左侧露出的宽度 = scaledCardWidth * OVERLAP_FACTOR。
	/// 0.35 表示每张卡约 65% 被前一卡覆盖，形成 STS2 风"手抓扇"效果。
	/// </summary>
	private const float OVERLAP_FACTOR = 0.85f;

	/// <summary>悬停相邻卡牌推开距离（设计单位）</summary>
	private const float PUSH_DISTANCE = 30f;

	/// <summary>推开衰减系数——每远离悬停卡 1 位，推开量乘以 decay</summary>
	private const float PUSH_DECAY = 0.35f;

	/// <summary>悬停检测区上方缓冲（设计单位）——避免轻微鼠标偏移就退出悬停</summary>
	private const float HOVER_BUFFER = 12f;

	/// <summary>退出悬停的迟滞缓冲倍数——退出需要鼠标移到比进入更远的位置</summary>
	private const float EXIT_HYSTERESIS = 3f;

	// ============================================================
	// Godot 生命周期
	// ============================================================

	public override void _Ready()
	{
		_cardContainer = GetNodeOrNull<Control>("CardContainer");
		if (_cardContainer == null)
		{
			_cardContainer = new Control
			{
				Name = "CardContainer",
				AnchorLeft = 0,
				AnchorTop = 0,
				AnchorRight = 1,
				AnchorBottom = 1,
				MouseFilter = MouseFilterEnum.Pass,
			};
			AddChild(_cardContainer);
		}
	}

	public override void _EnterTree()
	{
		base._EnterTree();
		RegisterHotkeyBindings();
	}

	public override void _ExitTree()
	{
		SceneLifecycleGuard.OnExitTree(this);
		UnregisterHotkeyBindings();
		base._ExitTree();
	}

	public override void _Process(double delta)
	{
		if (SceneLifecycleGuard.ShouldSkip(this)) return;
		if (_cardSlots.Count == 0) return;

		if (MobileInputRouter.IsMobile)
		{
			MobileProcess();
			return;
		}

		// 拖拽中或选择模式下不触发悬停
		if (HandSelectMode) return;
		if (_cardSlots.Exists(s => s.CardUI?.IsDragging == true)) return;

		var mousePos = GetGlobalMousePosition();
		float s = UIScaler.Instance?.GetScaleFactor() ?? 1f;
		float buffer = HOVER_BUFFER * s;
		float exitBuffer = buffer * EXIT_HYSTERESIS;

		// 视口底部 Y（卡牌下方不应有检测区延伸出屏幕）
		float viewportBottom = GetViewport()?.GetVisibleRect().Size.Y ?? 900f;

		CardSlot? bestSlot = null;

		foreach (var slot in _cardSlots)
		{
			if (slot.CardUI == null) continue;
			if (slot.CardUI.IsSelected) continue;

			if (!_restingPositions.TryGetValue(slot.CardUI, out var restingPos))
				continue;

			var cardSize = slot.CardUI.Size;
			float cardGlobalLeft = GlobalPosition.X + restingPos.X;
			float cardGlobalTop = GlobalPosition.Y + restingPos.Y;

			float detectLeft = cardGlobalLeft;
			float detectRight = cardGlobalLeft + cardSize.X;

			// 进入检测：紧贴卡牌上方小缓冲
			float detectTop = cardGlobalTop - buffer;
			float detectBottom = Mathf.Min(cardGlobalTop + cardSize.Y, viewportBottom);

			// 退出迟滞：当前已悬停的卡牌，退出区域比进入更宽松
			bool isCurrentlyHovered = _hoveredSlot == slot;
			if (isCurrentlyHovered)
			{
				detectTop -= exitBuffer;
				detectBottom = Mathf.Min(detectBottom + exitBuffer, viewportBottom + exitBuffer);
			}

			if (mousePos.X >= detectLeft && mousePos.X <= detectRight &&
				mousePos.Y >= detectTop && mousePos.Y <= detectBottom)
			{
				bestSlot = slot;
				break;
			}
		}

		if (bestSlot != _hoveredSlot)
		{
			_hoveredSlot?.CardUI?.RemoveHoverEffect();
			_hoveredSlot = bestSlot;
			_hoveredSlot?.CardUI?.ApplyHoverEffect();
			RefreshLayout();
		}
	}

	/// <summary>
	/// 移动端 _Process 替代逻辑：检测手牌区域外点击，收回展开的卡牌。
	/// 悬停检测由移动端触控替代，不在此处处理。
	/// </summary>
	private void MobileProcess()
	{
		if (_tappedSlot == null) return;

		// 检测触控松手是否发生在手牌区域外
		var router = MobileInputRouter.Instance;
		if (_wasMobileTouchActive && !router.IsTouchActive)
		{
			// 确认没有任何卡牌正在拖拽（拖拽中的触控由 CardUI 内部处理）
			bool anyCardDragging = false;
			foreach (var slot in _cardSlots)
			{
				if (slot.CardUI?.IsDragging == true)
				{
					anyCardDragging = true;
					break;
				}
			}

			if (!anyCardDragging)
			{
				Vector2 releasePos = router.TouchReleasePosition;
				Rect2 handRect = new Rect2(GlobalPosition, Size);

				if (!handRect.HasPoint(releasePos))
				{
					ClearTapExpansion();
				}
			}
		}

		_wasMobileTouchActive = router.IsTouchActive;
	}

	// ============================================================
	// 公共 API
	// ============================================================

	public void Initialize(Player player, CombatManager combat)
	{
		_player = player;
		_combat = combat;
	}

	public void Initialize(Player player)
	{
		Initialize(player, CombatManager.Instance!);
	}

	/// <summary>
	/// 重建手牌——销毁旧卡牌 UI，创建新的，延迟一帧布局以等待容器尺寸就绪。
	/// </summary>
	public void RefreshHand()
	{
		ClearHoverState();
		ClearTapExpansion();
		ClearKeyboardFocus();
		foreach (var slot in _cardSlots)
			slot.CardUI?.QueueFree();
		_cardSlots.Clear();
		_restingPositions.Clear();
		_selectedCard = null;

		if (_player == null) return;

		foreach (var card in _combat!.PlayerHero.Hand)
		{
			var cardUI = CreateCardUI(card);
			var slot = new CardSlot(cardUI);
			_cardSlots.Add(slot);
			_cardContainer.AddChild(cardUI);
		}

		// 延迟到下一帧布局，确保 _cardContainer 已被 VBoxContainer 赋予最终尺寸
		CallDeferred(nameof(RefreshLayout));
	}

	public void DeselectCard()
	{
		if (_selectedCard != null)
		{
			foreach (var slot in _cardSlots)
			{
				if (slot.Card == _selectedCard && slot.CardUI != null)
				{
					slot.CardUI.Deselect();
					break;
				}
			}
			_selectedCard = null;
		}
	}

	public void DetachCardFromList(CardUI cardUI)
	{
		ClearLayoutTween(cardUI);

		var slot = GetSlotFor(cardUI);
		if (slot != null)
			slot.DetachVisual();
		_restingPositions.Remove(cardUI);
		if (_selectedCard == cardUI.Card)
			_selectedCard = null;
		if (_hoveredSlot?.CardUI == cardUI)
		{
			_hoveredSlot = null;
			cardUI.RemoveHoverEffect();
		}
		if (_tappedSlot?.CardUI == cardUI)
			_tappedSlot = null;
		CallDeferred(nameof(RefreshLayout));
	}

	public void StopLayoutControl(CardUI cardUI)
	{
		ClearLayoutTween(cardUI);
		_restingPositions.Remove(cardUI);
		if (_hoveredSlot?.CardUI == cardUI)
		{
			_hoveredSlot = null;
			cardUI.RemoveHoverEffect();
		}
		if (_tappedSlot?.CardUI == cardUI)
			_tappedSlot = null;
	}

	/// <summary>
	/// 将一张卡牌重新加入手牌列表并刷新布局。
	/// 用于选中切换时旧卡从 DragLayer 销毁后归位。
	/// </summary>
	public void AddCardBack(Card.Card card)
	{
		if (card == null) return;
		var cardUI = CreateCardUI(card);
		var placeholder = _cardSlots.FirstOrDefault(s => s.Card == card && s.CardUI == null);
		if (placeholder != null)
			placeholder.AttachVisual(cardUI);
		else
			_cardSlots.Add(new CardSlot(cardUI));
		_cardContainer.AddChild(cardUI);
		RefreshLayout();
	}

	private void ClearLayoutTween(CardUI cardUI)
	{
		if (_positionTweens.TryGetValue(cardUI, out var tween) && tween.IsValid())
			tween.Kill();

		_positionTweens.Remove(cardUI);
	}

	public Card.Card? PlaySelectedCard()
	{
		var card = _selectedCard;
		if (card != null)
			RemoveCardFromHand(card);
		return card;
	}

	public Card.Card? PlaySelectedCardOnTarget()
	{
		return PlaySelectedCard();
	}

	public void UpdateHand(IReadOnlyList<Card.Card> hand)
	{
		RefreshHand();
	}

	public CardUI? GetCardUIFor(Card.Card card)
	{
		foreach (var slot in _cardSlots)
		{
			if (slot.Card == card && slot.CardUI != null)
				return slot.CardUI;
		}
		return null;
	}

	// ============================================================
	// 布局计算 —— STS2 风扇交叠风格
	// ============================================================

	private void RefreshLayout()
	{
		int count = _cardSlots.Count;
		if (count == 0) return;

		float s = UIScaler.Instance?.GetScaleFactor() ?? 1f;
		float cardWidth = CardUI.DESIGN_WIDTH * s;
		float containerWidth = GetContainerWidth();

		// 折叠态卡牌宽度
		float scaledCardWidth = cardWidth * BASE_SCALE;

		// 交叠步长：每张卡露出的宽度。stepX < scaledCardWidth 即产生交叠
		float stepX = scaledCardWidth * OVERLAP_FACTOR;

		// 卡牌组的水平跨度 & 居中偏移
		float totalSpread = stepX * (count - 1);
		float startX = (containerWidth - totalSpread) / 2f;

		// 悬停卡索引（桌面端 _hoveredSlot；移动端回退到 _tappedSlot）
		int hoverIndex = _hoveredSlot != null
			? _cardSlots.IndexOf(_hoveredSlot)
			: _tappedSlot != null
				? _cardSlots.IndexOf(_tappedSlot)
				: -1;

		// 悬停卡扩大后额外需要的推开空间
		float hoverExpand = hoverIndex >= 0 ? (cardWidth - scaledCardWidth) * 0.5f : 0f;

		for (int i = 0; i < count; i++)
		{
			var cardUI = _cardSlots[i].CardUI;
			if (cardUI == null) continue;

			// 拖拽中的卡牌位置由 CardUI._Process 控制，布局系统不应干预
			if (cardUI.IsDragging) continue;

			// 卡牌中心 X（在折叠态风扇布局中的位置）
			float centerX = startX + i * stepX + scaledCardWidth * 0.5f;

			Vector2 targetPos;
			float targetScale;

			if (i == hoverIndex)
			{
				// 悬停卡牌：中心对齐 + 上浮到完整露出
				float hoverLift = (CardUI.DESIGN_HEIGHT - COLLAPSED_VISIBLE + 10f) * s;
				targetPos = new Vector2(centerX - cardWidth * 0.5f, -hoverLift);
				targetScale = 1f;
			}
			else
			{
				float x = startX + i * stepX;

				// 推开相邻卡牌
				if (hoverIndex >= 0)
				{
					int dist = Mathf.Abs(i - hoverIndex);
					float push = PUSH_DISTANCE * s * Mathf.Pow(PUSH_DECAY, dist - 1);
					// 额外补偿悬停卡扩大占据的空间
					push += hoverExpand * Mathf.Pow(PUSH_DECAY, dist);
					if (i < hoverIndex)
						x -= push;
					else
						x += push;
				}

				targetPos = new Vector2(x, 0f);
				targetScale = BASE_SCALE;
			}

			AnimateCardPosition(cardUI, targetPos, targetScale);
			_restingPositions[cardUI] = targetPos;
		}

		// 键盘焦点视觉指示器
		ApplyKeyboardFocusVisual();
	}

	/// <summary>
	/// 获取卡片容器的有效宽度。
	/// 若容器尚未布局（Size ≈ 0），回退到视口宽度。
	/// </summary>
	private float GetContainerWidth()
	{
		float w = _cardContainer.Size.X;
		if (w > 10f) return w;

		// 回退：视口宽度（HandArea 填满 VBoxContainer 全宽）
		var vp = GetViewport();
		return vp != null ? vp.GetVisibleRect().Size.X : 1152f;
	}

	private void AnimateCardPosition(CardUI cardUI, Vector2 targetPos, float targetScale)
	{
		if (_positionTweens.TryGetValue(cardUI, out var oldTween) && oldTween.IsValid())
			oldTween.Kill();

		var tween = CreateTween().SetParallel(true);
		tween.TweenProperty(cardUI, "position", targetPos, 0.18f)
			.SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Quad);
		tween.TweenProperty(cardUI, "scale", new Vector2(targetScale, targetScale), 0.18f)
			.SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Quad);

		_positionTweens[cardUI] = tween;
	}

	private readonly Dictionary<CardUI, Tween> _positionTweens = new();

	// ============================================================
	// 内部方法
	// ============================================================

	private CardUI CreateCardUI(Card.Card card)
	{
		CardUI cardUI = CardScene != null
			? CardScene.Instantiate<CardUI>()
			: new CardUI();

		cardUI.SetCard(card);
		cardUI.OnCardClicked += OnCardClicked;
		cardUI.OnCardRightClicked += OnCardRightClicked;
		cardUI.OnMobileDragBegan += OnMobileDragBegan;
		return cardUI;
	}

	private void OnCardRightClicked(CardUI cardUI)
	{
		// 移动端快速点击（非拖拽）后重新施加展开效果
		if (MobileInputRouter.IsMobile)
		{
			if (_tappedSlot?.CardUI == cardUI)
			{
				// CardUI 的拖拽周期已结束，重新施加视觉展开
				cardUI.ApplyHoverEffect();
				RefreshLayout();
			}
			return;
		}

		DeselectCard();
		OnCardCancelled?.Invoke();
	}

    /// <summary>
    /// 移动端拖拽开始时触发。
    /// 手指移动超过阈值后，自动进入选择模式——跳过二次点击，实现纯拖拽出牌。
    /// </summary>
    private void OnMobileDragBegan(CardUI cardUI)
    {
        if (cardUI.Card == null) return;
        if (HandSelectMode) return;

        // 清除展开态，通知 CombatUI 进入对应的选择模式
        ClearTapExpansion();
        OnCardSelectedForPlay?.Invoke(cardUI.Card);
    }

    private void OnCardClicked(CardUI cardUI)
	{
		if (cardUI.Card == null) return;

		// 移动端触控：首次点击展开预览，拖拽出牌，不保留二次点击选中
		if (MobileInputRouter.IsMobile)
		{
			if (_tappedSlot?.CardUI == cardUI)
			{
				// 二次点击同一张 → 收回展开态（移动端用拖拽出牌，不进入选中模式）
				ClearTapExpansion();
				return;
			}
			else
			{
				// 首次点击 或 点击不同的卡 → 展开这张
				ClearTapExpansion();
				_tappedSlot = GetSlotFor(cardUI);
				return;
			}
		}

		if (HandSelectMode)
		{
			OnCardSelectionToggled?.Invoke(cardUI.Card, true);
			return;
		}

		if (_selectedCard == cardUI.Card)
		{
			DeselectCard();
			OnCardCancelled?.Invoke();
			return;
		}

		ClearHoverState();

		// 旧选中卡的清理由 CombatUI.OnCardSelectedFromHand 统一负责
		// （移动→DragLayer→DetachCardFromList→QueueFree 或返回手牌），
		// HandUI 不再插手——避免双方面同时操作同一张卡导致死引用和位置冲突。
		_selectedCard = null;

		_selectedCard = cardUI.Card;
		cardUI.Select();

		OnCardSelectedForPlay?.Invoke(_selectedCard);
	}

	private void RemoveCardFromHand(Card.Card card)
	{
		for (int i = _cardSlots.Count - 1; i >= 0; i--)
		{
			if (_cardSlots[i].Card == card)
			{
				var cardUI = _cardSlots[i].CardUI;
				if (cardUI != null)
				{
					cardUI.QueueFree();
					_restingPositions.Remove(cardUI);
					_positionTweens.Remove(cardUI);
				}
				_cardSlots.RemoveAt(i);
				break;
			}
		}
		_selectedCard = null;
		RefreshLayout();
	}

	private void ClearHoverState()
	{
		if (_hoveredSlot != null)
		{
			_hoveredSlot.CardUI?.RemoveHoverEffect();
			_hoveredSlot = null;
		}
	}

	/// <summary>
	/// 收回移动端展开的卡牌，恢复折叠态。
	/// </summary>
	private void ClearTapExpansion()
	{
		if (_tappedSlot != null)
		{
			_tappedSlot.CardUI?.RemoveHoverEffect();
			_tappedSlot = null;
			RefreshLayout();
		}
	}

	/// <summary>
	/// 按 CardUI 查找对应的 CardSlot。
	/// </summary>
	private CardSlot? GetSlotFor(CardUI cardUI)
	{
		foreach (var slot in _cardSlots)
		{
			if (slot.CardUI == cardUI)
				return slot;
		}
		return null;
	}

	// ============================================================
	// 键盘交互 — HotkeyManager 回调注册/注销
	// ============================================================

	/// <summary>
	/// 注册所有键盘热键绑定到 HotkeyManager。
	/// 数字键 1~0 直选手牌，方向键左右切换焦点，Enter 确认，Escape 取消。
	/// </summary>
	private void RegisterHotkeyBindings()
	{
		var hm = HotkeyManager.Instance;
		if (hm == null) return;

		// 数字键 1~10 对应手牌第 1~10 张
		_cardSelectActions = new Action[10];
		for (int i = 0; i < 10; i++)
		{
			int capturedIndex = i;
			_cardSelectActions[i] = () => SelectCardByIndex(capturedIndex);
			hm.PushPressedBinding(OdysseyInput.SelectCardActions[i], _cardSelectActions[i]);
		}

		// 方向键导航
		_leftAction = () => CycleFocus(-1);
		_rightAction = () => CycleFocus(1);
		hm.PushPressedBinding(OdysseyInput.Left, _leftAction);
		hm.PushPressedBinding(OdysseyInput.Right, _rightAction);

		// 确认 / 取消
		_acceptAction = AcceptFocusedCard;
		_cancelAction = CancelKeyboardSelection;
		hm.PushPressedBinding(OdysseyInput.Accept, _acceptAction);
		hm.PushPressedBinding(OdysseyInput.Cancel, _cancelAction);

		// 监听键盘焦点超时事件——超时后清除焦点指示器
		hm.KeyboardFocusChanged += OnKeyboardFocusChanged;
	}

	/// <summary>
	/// 注销所有键盘热键绑定。
	/// </summary>
	private void UnregisterHotkeyBindings()
	{
		var hm = HotkeyManager.Instance;
		if (hm == null) return;

		hm.KeyboardFocusChanged -= OnKeyboardFocusChanged;

		if (_cardSelectActions != null)
		{
			for (int i = 0; i < 10; i++)
				hm.RemovePressedBinding(OdysseyInput.SelectCardActions[i], _cardSelectActions[i]);
			_cardSelectActions = null;
		}

		if (_leftAction != null) { hm.RemovePressedBinding(OdysseyInput.Left, _leftAction); _leftAction = null; }
		if (_rightAction != null) { hm.RemovePressedBinding(OdysseyInput.Right, _rightAction); _rightAction = null; }
		if (_acceptAction != null) { hm.RemovePressedBinding(OdysseyInput.Accept, _acceptAction); _acceptAction = null; }
		if (_cancelAction != null) { hm.RemovePressedBinding(OdysseyInput.Cancel, _cancelAction); _cancelAction = null; }
	}

	// ============================================================
	// 键盘交互 — 业务方法
	// ============================================================

	/// <summary>
	/// 数字键直选：选择手牌中指定索引的卡牌并立即触发打出/选中。
	/// 索引 0 对应最左侧（第一张）卡牌。
	/// </summary>
	private void SelectCardByIndex(int index)
	{
		if (SceneLifecycleGuard.ShouldSkip(this)) return;
		if (_cardSlots.Count == 0) return;

		index = Mathf.Clamp(index, 0, _cardSlots.Count - 1);
		_focusedCardIndex = index;

		var cardUI = _cardSlots[index].CardUI;
		if (cardUI == null) return;
		if (cardUI.Card == null) return;

		if (HandSelectMode)
		{
			OnCardSelectionToggled?.Invoke(cardUI.Card, true);
		}
		else
		{
			// 标记为键盘选牌——CombatUI 将从卡牌在手中的位置开始动画，
			// 而非跳到屏幕左上角（LastClickGlobalPosition 在未点击时为过期值）
			IsKeyboardSelection = true;
			OnCardClicked(cardUI);
			IsKeyboardSelection = false;
		}

		RefreshLayout();
	}

	/// <summary>
	/// 方向键导航：循环切换键盘焦点到上一张（direction=-1）或下一张（direction=+1）卡牌。
	/// 仅移动焦点指示器，不触发打出或选中。
	/// </summary>
	private void CycleFocus(int direction)
	{
		if (SceneLifecycleGuard.ShouldSkip(this)) return;
		if (_cardSlots.Count == 0) return;

		if (_focusedCardIndex < 0 || _focusedCardIndex >= _cardSlots.Count)
		{
			// 无焦点时，从边界开始
			_focusedCardIndex = direction > 0 ? 0 : _cardSlots.Count - 1;
		}
		else
		{
			_focusedCardIndex += direction;
			if (_focusedCardIndex >= _cardSlots.Count)
				_focusedCardIndex = 0;
			else if (_focusedCardIndex < 0)
				_focusedCardIndex = _cardSlots.Count - 1;
		}

		RefreshLayout();
	}

	/// <summary>
	/// Enter 键确认：打出/选中当前键盘焦点所在的卡牌。
	/// 无焦点时默认选中第一张。
	/// </summary>
	private void AcceptFocusedCard()
	{
		if (SceneLifecycleGuard.ShouldSkip(this)) return;
		if (_cardSlots.Count == 0) return;

		int index = _focusedCardIndex >= 0 && _focusedCardIndex < _cardSlots.Count
			? _focusedCardIndex : 0;
		_focusedCardIndex = index;

		var cardUI = _cardSlots[index].CardUI;
		if (cardUI == null) return;
		if (cardUI.Card == null) return;

		IsKeyboardSelection = true;
		OnCardClicked(cardUI);
		IsKeyboardSelection = false;
		RefreshLayout();
	}

	/// <summary>
	/// Escape 键取消：取消当前选中卡牌的选中状态，重置键盘焦点。
	/// </summary>
	private void CancelKeyboardSelection()
	{
		if (SceneLifecycleGuard.ShouldSkip(this)) return;

		_focusedCardIndex = -1;

		if (_selectedCard != null)
		{
			DeselectCard();
			OnCardCancelled?.Invoke();
		}

		ClearHoverState();
		ClearKeyboardFocus();
		RefreshLayout();
	}

	/// <summary>
	/// HotkeyManager 键盘焦点超时回调。
	/// 键盘闲置 3 秒后自动清除焦点指示器。
	/// </summary>
	private void OnKeyboardFocusChanged(bool active)
	{
		if (!active)
		{
			_focusedCardIndex = -1;
			ClearKeyboardFocus();
		}
	}

	// ============================================================
	// 键盘交互 — 视觉指示器
	// ============================================================

	/// <summary>
	/// 给当前键盘焦点的卡牌施加蓝色调 SelfModulate 指示器。
	/// 从所有其他卡牌清除键盘焦点视觉。
	/// 仅当 HotkeyManager 记录到近期键盘活动时才显示。
	/// </summary>
	private void ApplyKeyboardFocusVisual()
	{
		bool shouldShowFocus = _focusedCardIndex >= 0
			&& _focusedCardIndex < _cardSlots.Count
			&& HotkeyManager.Instance.LastKeyboardActivityMsec > 0;

		// 先清除所有卡牌的键盘焦点视觉
		if (_keyboardFocusedCardUI != null)
		{
			if (GodotObject.IsInstanceValid(_keyboardFocusedCardUI))
				_keyboardFocusedCardUI.SelfModulate = Colors.White;
			_keyboardFocusedCardUI = null;
		}

		if (!shouldShowFocus) return;

		var cardUI = _cardSlots[_focusedCardIndex].CardUI;
		if (cardUI == null || !GodotObject.IsInstanceValid(cardUI)) return;

		// 蓝色调指示器（通过 SelfModulate 叠加，不影响 CardUI 自身的 Modulate）
		cardUI.SelfModulate = new Color(0.72f, 0.85f, 1f, 1f);
		_keyboardFocusedCardUI = cardUI;
	}

	/// <summary>
	/// 清除所有卡牌的键盘焦点视觉，重置 _focusedCardIndex。
	/// </summary>
	private void ClearKeyboardFocus()
	{
		_focusedCardIndex = -1;
		if (_keyboardFocusedCardUI != null)
		{
			if (GodotObject.IsInstanceValid(_keyboardFocusedCardUI))
				_keyboardFocusedCardUI.SelfModulate = Colors.White;
			_keyboardFocusedCardUI = null;
		}
	}

	// ============================================================
	// 内部数据结构
	// ============================================================

	private sealed class CardSlot
	{
		public Card.Card Card { get; }
		public CardUI? CardUI { get; private set; }

		public CardSlot(CardUI cardUI)
		{
			CardUI = cardUI;
			Card = cardUI.Card ?? throw new InvalidOperationException("CardSlot 不能绑定空卡牌 UI");
		}

		public void DetachVisual()
		{
			CardUI = null;
		}

		public void AttachVisual(CardUI cardUI)
		{
			CardUI = cardUI;
		}
	}
}
