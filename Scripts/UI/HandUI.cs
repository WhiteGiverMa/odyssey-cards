using System;
using System.Collections.Generic;
using Godot;
using OdysseyCards.Character;
using OdysseyCards.Combat;

namespace OdysseyCards.UI;

/// <summary>
/// 手牌管理组件 — STS2 风格重构版。
/// 卡片在屏幕底部折叠（仅露出顶部 ~30%），鼠标悬停时上浮 + 放大，
/// 相邻卡牌被推开。使用手动布局替代 HBoxContainer，
/// HandUI._Process 追踪鼠标位置管理悬停状态，避免闪烁。
/// </summary>
public partial class HandUI : Control
{
	/// <summary>
	/// 卡牌折叠态可见部分（设计单位）。90 设计单位 ≈ 50% 卡牌高度（考虑 BASE_SCALE=0.85 后的视觉效果）。
	/// 实际像素 = 90 * UIScaler.CurrentScale。
	/// </summary>
	public const float COLLAPSED_VISIBLE = 90f;

	[Export] public PackedScene CardScene { get; set; }

	public event Action<Card.Card>? OnCardSelectedForPlay;
	public event Action<Card.Card, ICommander>? OnCardPlayRequested;
	public event Action? OnCardCancelled;

	public bool HandSelectMode { get; set; }

	public void SetHandSelectionMode(bool enabled)
	{
		HandSelectMode = enabled;
		foreach (var slot in _cardSlots)
		{
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

	public override void _Process(double delta)
	{
		if (_cardSlots.Count == 0) return;

		// 拖拽中或选择模式下不触发悬停
		if (HandSelectMode) return;
		if (_cardSlots.Exists(s => s.CardUI.IsDragging)) return;

		var mousePos = GetGlobalMousePosition();
		float s = UIScaler.Instance?.GetScaleFactor() ?? 1f;
		float buffer = HOVER_BUFFER * s;
		float exitBuffer = buffer * EXIT_HYSTERESIS;

		// 视口底部 Y（卡牌下方不应有检测区延伸出屏幕）
		float viewportBottom = GetViewport()?.GetVisibleRect().Size.Y ?? 900f;

		CardSlot? bestSlot = null;

		foreach (var slot in _cardSlots)
		{
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
			_hoveredSlot?.CardUI.RemoveHoverEffect();
			_hoveredSlot = bestSlot;
			_hoveredSlot?.CardUI.ApplyHoverEffect();
			RefreshLayout();
		}
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
		foreach (var slot in _cardSlots)
			slot.CardUI.QueueFree();
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
				if (slot.CardUI.Card == _selectedCard)
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

		_cardSlots.RemoveAll(s => s.CardUI == cardUI);
		_restingPositions.Remove(cardUI);
		if (_selectedCard == cardUI.Card)
			_selectedCard = null;
		if (_hoveredSlot?.CardUI == cardUI)
		{
			_hoveredSlot = null;
			cardUI.RemoveHoverEffect();
		}
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
	}

	/// <summary>
	/// 将一张卡牌重新加入手牌列表并刷新布局。
	/// 用于选中切换时旧卡从 DragLayer 销毁后归位。
	/// </summary>
	public void AddCardBack(Card.Card card)
	{
		if (card == null) return;
		var cardUI = CreateCardUI(card);
		var slot = new CardSlot(cardUI);
		_cardSlots.Add(slot);
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
			if (slot.CardUI.Card == card)
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

		// 悬停卡索引
		int hoverIndex = _hoveredSlot != null
			? _cardSlots.IndexOf(_hoveredSlot)
			: -1;

		// 悬停卡扩大后额外需要的推开空间
		float hoverExpand = hoverIndex >= 0 ? (cardWidth - scaledCardWidth) * 0.5f : 0f;

		for (int i = 0; i < count; i++)
		{
			var cardUI = _cardSlots[i].CardUI;

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
		return cardUI;
	}

	private void OnCardRightClicked(CardUI cardUI)
	{
		DeselectCard();
		OnCardCancelled?.Invoke();
	}

	private void OnCardClicked(CardUI cardUI)
	{
		if (cardUI.Card == null) return;

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
			if (_cardSlots[i].CardUI.Card == card)
			{
				var cardUI = _cardSlots[i].CardUI;
				cardUI.QueueFree();
				_cardSlots.RemoveAt(i);
				_restingPositions.Remove(cardUI);
				_positionTweens.Remove(cardUI);
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
			_hoveredSlot.CardUI.RemoveHoverEffect();
			_hoveredSlot = null;
		}
	}

	// ============================================================
	// 内部数据结构
	// ============================================================

	private sealed class CardSlot
	{
		public CardUI CardUI { get; }
		public CardSlot(CardUI cardUI) => CardUI = cardUI;
	}
}
