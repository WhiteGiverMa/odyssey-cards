#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using OdysseyCards.Combat;
using OdysseyCards.Core;
using OdysseyCards.Infrastructure;
using MotionVector = System.Numerics.Vector2;

namespace OdysseyCards.UI;

/// <summary>按运行时卡牌实例保留手牌视图，负责槽位、命中和连续姿态；牌面由 CardUI 绘制。</summary>
public partial class HandUI : Control
{
	[Export] public PackedScene? CardScene { get; set; }

	public event Action<Card.Card>? OnCardSelectedForPlay;
	public event Action? OnCardCancelled;
	public event Action<Card.Card>? OnCardSelectionToggled;
	public bool IsKeyboardSelection { get; private set; }

	private Control _cardContainer = null!;
	private CombatManager? _combat;
	private readonly List<CardSlot> _slots = new();
	private readonly Dictionary<Card.Card, CardSlot> _byCard = new(ReferenceEqualityComparer.Instance);
	private readonly Dictionary<Card.Card, Vector2> _drawOrigins = new(ReferenceEqualityComparer.Instance);
	private HandHitFrame[] _hitFrames = Array.Empty<HandHitFrame>();
	private bool[] _hitCandidates = Array.Empty<bool>();
	private HandLayout _layout;
	private Vector2 _layoutSize;
	private float _layoutScale;
	private int _layoutCount = -1;
	private CardSlot? _hoveredSlot;
	private CardSlot? _tappedSlot;
	private CardSlot? _keyboardFocus;
	private CardSlot? _loanedForPlay;
	private bool _handSelectMode;
	private bool _syncPending;
	private double _motionTime;
	private double _nextDrawTime;
	private Action[]? _cardSelectActions;
	private Action? _leftAction;
	private Action? _rightAction;
	private Action? _acceptAction;

	public override void _Ready()
	{
		_cardContainer = GetNodeOrNull<Control>("CardContainer") ?? new Control
		{
			Name = "CardContainer",
			MouseFilter = MouseFilterEnum.Ignore,
		};
		if (_cardContainer.GetParent() == null)
		{
			_cardContainer.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			AddChild(_cardContainer);
		}
	}

	public override void _EnterTree()
	{
		if (!Engine.IsEditorHint())
		{
			RegisterHotkeyBindings();
		}
	}

	public override void _ExitTree()
	{
		if (!Engine.IsEditorHint())
		{
			SceneLifecycleGuard.OnExitTree(this);
			UnregisterHotkeyBindings();
		}
		foreach (CardSlot slot in _slots)
		{
			Unsubscribe(slot.View);
		}
	}

	public override void _Process(double delta)
	{
		if (Engine.IsEditorHint() || SceneLifecycleGuard.ShouldSkip(this))
		{
			return;
		}
		_motionTime += delta;
		if (_slots.Count == 0 || _cardContainer.Size.X <= 1f || _cardContainer.Size.Y <= 1f)
		{
			return;
		}

		HandLayout layout = GetLayout(_slots.Count);
		if (!MobileInputRouter.IsMobile)
		{
			UpdateHover();
		}
		CardSlot? expanded = _handSelectMode ? null : _hoveredSlot ?? _tappedSlot;
		int focusIndex = expanded == null ? -1 : _slots.IndexOf(expanded);

		for (int i = 0; i < _slots.Count; i++)
		{
			CardSlot slot = _slots[i];
			if (!IsInHand(slot) || slot.AwaitingModelRemoval || slot.View.IsDragging)
			{
				continue;
			}
			HandPose target = layout.GetFocusPose(i, focusIndex);
			// 已构建的牌面不重建；窗口变化按比例更新根节点，命中与视觉仍使用同一尺寸。
			HandPose pose = AdvanceSlot(slot, target, (float)delta, _layoutScale);
			int zIndex = slot.Draw.HasValue ? 3 : i == focusIndex ? 2 : slot.Returning ? 1 : 0;
			ApplyPose(slot.View, pose, _layoutScale, zIndex);
		}
	}

	public void Initialize(CombatManager combat)
	{
		_combat = combat;
	}

	/// <summary>记录真正由抽牌事件加入的卡，普通生成、找回或费用刷新不冒充抽牌。</summary>
	internal void QueueDrawOrigins(IReadOnlyList<Card.Card> cards, Vector2 origin)
	{
		foreach (Card.Card card in cards)
		{
			_drawOrigins[card] = origin;
		}
	}

	internal void OnHandMembershipChanged()
	{
		if (_combat == null)
		{
			return;
		}
		foreach (CardSlot slot in _slots)
		{
			if (slot.AwaitingModelRemoval && !_combat.PlayerHero.Hand.Contains(slot.Card, ReferenceEqualityComparer.Instance))
			{
				slot.RemovalObserved = true;
			}
		}
		RefreshHand();
	}

	/// <summary>原地同步牌面与费用，并在 deferred 边界合并牌序变动。</summary>
	public void RefreshHand()
	{
		if (_combat == null || SceneLifecycleGuard.ShouldSkip(this))
		{
			return;
		}
		RefreshCardFaces();
		RefreshCardCosts();
		if (_syncPending)
		{
			return;
		}
		IReadOnlyList<Card.Card> hand = _combat.PlayerHero.Hand;
		if (IsSameHand(hand) && _drawOrigins.Count == 0)
		{
			return;
		}
		_syncPending = true;
		SceneLifecycleGuard.CallDeferredSafe(this, nameof(ApplyHandDiff));
	}

	/// <summary>同步费用与支付状态；不重测卡图或描述布局，也不更改动画状态。</summary>
	internal void RefreshCardCosts()
	{
		if (_combat == null)
		{
			return;
		}
		foreach (CardSlot slot in _slots)
		{
			if (IsLive(slot.View))
			{
				ApplyCardCost(slot.View, slot.Card);
			}
		}
	}

	/// <summary>同步战斗中可能变化的描述、数值、待命徽章等，资源仍沿用原牌面。</summary>
	private void RefreshCardFaces()
	{
		foreach (CardSlot slot in _slots)
		{
			if (IsLive(slot.View))
			{
				slot.View.RefreshRuntimeDisplay();
			}
		}
	}

	private bool IsSameHand(IReadOnlyList<Card.Card> hand)
	{
		if (hand.Count != _slots.Count)
		{
			return false;
		}
		for (int i = 0; i < hand.Count; i++)
		{
			if (!ReferenceEquals(_slots[i].Card, hand[i]) || !IsLive(_slots[i].View) || _slots[i].RemovalObserved)
			{
				return false;
			}
		}
		return true;
	}

	private void ApplyHandDiff()
	{
		_syncPending = false;
		if (_combat == null || SceneLifecycleGuard.ShouldSkip(this))
		{
			return;
		}

		IReadOnlyList<Card.Card> hand = _combat.PlayerHero.Hand;
		var remaining = new HashSet<Card.Card>(hand, ReferenceEqualityComparer.Instance);
		foreach (CardSlot slot in _slots)
		{
			if (remaining.Contains(slot.Card) && IsLive(slot.View))
			{
				continue;
			}
			_ = _byCard.Remove(slot.Card);
			ClearSlotFocus(slot);
			Unsubscribe(slot.View);
			if (IsInHand(slot))
			{
				// 先隔离出列节点；释放另行延迟，不和下方 AddChild 共用调用栈。
				RetireDetachedCard(slot.View);
			}
		}

		_slots.Clear();
		HandLayout layout = GetLayout(hand.Count);
		for (int i = 0; i < hand.Count; i++)
		{
			Card.Card card = hand[i];
			if (!_byCard.TryGetValue(card, out CardSlot? slot))
			{
				slot = CreateSlot(card, layout.GetRestPose(i));
				_byCard.Add(card, slot);
			}
			else if (slot.RemovalObserved)
			{
				// 结算期间移出又移回（如「解释」）：保留真实视图并从原姿态归位。
				slot.AwaitingModelRemoval = false;
				slot.RemovalObserved = false;
				if (!BeginDraw(slot, layout.GetRestPose(i)))
				{
					slot.Returning = true;
					slot.View.Show();
				}
			}
			_slots.Add(slot);
		}
		_drawOrigins.Clear();
		RestoreDrawOrder();
	}

	private CardSlot CreateSlot(Card.Card card, HandPose rest)
	{
		CardUI view = CardScene != null ? CardScene.Instantiate<CardUI>() : new CardUI();
		view.SetAnchorsPreset(LayoutPreset.TopLeft, keepOffsets: true);
		view.SetCard(card);
		view.PreventDrag = _handSelectMode;
		view.OnCardClicked += OnCardClicked;
		view.OnCardRightClicked += OnCardRightClicked;
		view.OnMobileDragBegan += OnMobileDragBegan;
		view.Hide();
		_cardContainer.AddChild(view);

		var slot = new CardSlot(card, view);
		if (!BeginDraw(slot, rest))
		{
			ApplyPose(view, rest, UiScale, zIndex: 0);
			if (_cardContainer.Size.X > 1f && _cardContainer.Size.Y > 1f)
			{
				view.Show();
			}
		}
		ApplyCardCost(view, card);
		return slot;
	}

	private bool BeginDraw(CardSlot slot, HandPose rest)
	{
		if (!_drawOrigins.TryGetValue(slot.Card, out Vector2 origin))
		{
			return false;
		}
		double start = Math.Max(_motionTime, _nextDrawTime);
		_nextDrawTime = start + HandMotionMath.DrawStagger;
		var flight = new DrawFlight(origin, start);
		slot.Draw = flight;
		slot.View.MouseFilter = MouseFilterEnum.Ignore;
		float scale = UiScale;
		ApplyPose(slot.View, GetDrawPose(flight, rest, 0f, scale), scale, zIndex: 3);
		return true;
	}

	private void ApplyCardCost(CardUI view, Card.Card card)
	{
		int cost = _combat!.Relics.ApplyCostModifiers(card, card.GetEffectiveCost());
		bool canPay = _combat.PlayerHero.CanSpendMana(cost);
		bool canPlay = card.Type == CardType.Countermeasure ? card.IsCountermeasureArmed || canPay : !card.HasUnplayable && canPay;
		view.SetRuntimeCost(cost);
		view.SetCanPlay(canPlay);
	}

	/// <summary>借出真卡保留模型槽位；拖拽层和选牌展示容器不会被手牌布局覆盖。</summary>
	internal bool MoveCardOutOfHand(CardUI view, Control destination, bool forPlay)
	{
		if (!TryGetSlot(view, out CardSlot slot) || !IsAvailable(slot)
			|| SceneLifecycleGuard.ShouldSkip(destination))
		{
			return false;
		}
		ClearSlotFocus(slot);
		slot.Returning = false;
		view.ReparentKeepingScreenPose(destination);
		if (forPlay)
		{
			_loanedForPlay = slot;
		}
		return true;
	}

	/// <summary>归还同一张 CardUI：保留屏幕中心与当前姿态，由原槽位阻尼接管。</summary>
	public bool ReturnCardToHand(CardUI view)
	{
		if (!TryGetSlot(view, out CardSlot slot) || _combat == null || SceneLifecycleGuard.ShouldSkip(this)
			|| !_combat.PlayerHero.Hand.Contains(slot.Card, ReferenceEqualityComparer.Instance))
		{
			return false;
		}

		view.CancelDragSilent();
		view.SetHandSelectionHighlight(false);
		view.PreventDrag = _handSelectMode;
		if (view.GetParent() != _cardContainer)
		{
			view.ReparentKeepingScreenPose(_cardContainer);
		}
		view.MouseFilter = MouseFilterEnum.Ignore;
		view.ZIndex = 1;
		slot.Returning = true;
		if (_loanedForPlay == slot)
		{
			_loanedForPlay = null;
		}
		ApplyCardCost(view, slot.Card);
		RestoreDrawOrder();
		return true;
	}

	/// <summary>已经承诺结算的牌可能暂时仍在模型里，先从借牌状态移除，等模型变动时对齐。</summary>
	internal bool ParkPlayedCard(CardUI view)
	{
		if (!TryGetSlot(view, out CardSlot slot) || _combat == null || SceneLifecycleGuard.ShouldSkip(this)
			|| !_combat.PlayerHero.Hand.Contains(slot.Card, ReferenceEqualityComparer.Instance))
		{
			return false;
		}
		ClearSlotFocus(slot);
		slot.AwaitingModelRemoval = true;
		view.CancelDragSilent();
		view.Hide();
		view.MouseFilter = MouseFilterEnum.Ignore;
		view.ReparentKeepingScreenPose(_cardContainer);
		RestoreDrawOrder();
		return true;
	}

	/// <summary>卡牌已离开手牌，不让删除排队与新卡挂树发生在同一调用栈。</summary>
	internal static void RetireDetachedCard(CardUI view)
	{
		if (!IsLive(view))
		{
			return;
		}
		view.CancelDragSilent();
		view.Hide();
		view.MouseFilter = MouseFilterEnum.Ignore;
		view.GetParent()?.RemoveChild(view);
		Callable.From(() =>
		{
			if (IsLive(view))
			{
				view.QueueFree();
			}
		}).CallDeferred();
	}

	public void SetHandSelectionMode(bool enabled)
	{
		_handSelectMode = enabled;
		_hoveredSlot = null;
		_tappedSlot = null;
		ClearKeyboardFocus();
		foreach (CardSlot slot in _slots)
		{
			if (IsInHand(slot))
			{
				slot.View.PreventDrag = enabled;
			}
		}
	}

	public CardUI? GetCardUIFor(Card.Card card)
	{
		return _byCard.TryGetValue(card, out CardSlot? slot) && IsLive(slot.View) ? slot.View : null;
	}

	/// <summary>返回可交互的手牌视图，供虚拟指针坐标目录采样；排除尚在飞行和借出的牌。</summary>
	public IReadOnlyList<CardUI> GetHandCardViews()
	{
		var views = new List<CardUI>(_slots.Count);
		foreach (CardSlot slot in _slots)
		{
			if (IsAvailable(slot))
			{
				views.Add(slot.View);
			}
		}
		return views;
	}

	private void UpdateHover()
	{
		if (_handSelectMode || _loanedForPlay != null)
		{
			_hoveredSlot = null;
			return;
		}
		for (int i = 0; i < _slots.Count; i++)
		{
			_hitCandidates[i] = IsAvailable(_slots[i]);
		}
		MotionVector pointer = ToMotion(ToHandLocal(EvalGateway.GetEffectiveMousePosition(_cardContainer)));
		int previous = _hoveredSlot == null ? -1 : _slots.IndexOf(_hoveredSlot);
		int next = HandMotionMath.FindHovered(_hitFrames.AsSpan(0, _slots.Count), pointer, previous,
			_hitCandidates.AsSpan(0, _slots.Count));
		_hoveredSlot = next < 0 ? null : _slots[next];
	}

	private HandPose AdvanceSlot(CardSlot slot, HandPose target, float delta, float scale)
	{
		CardUI view = slot.View;
		if (slot.Draw is DrawFlight flight)
		{
			float progress = (float)((_motionTime - flight.StartTime) / HandMotionMath.DrawDuration);
			if (progress < 0f)
			{
				return GetDrawPose(flight, target, 0f, scale);
			}
			if (!view.Visible)
			{
				view.Show();
			}
			if (progress < 1f)
			{
				return GetDrawPose(flight, target, progress, scale);
			}
			slot.Draw = null;
			view.MouseFilter = MouseFilterEnum.Stop;
			return target;
		}
		if (!view.Visible)
		{
			view.Show();
			return target;
		}

		float renderRatio = scale / view.FaceRenderScale;
		HandPose current = new(ToMotion(view.Position + view.PivotOffset), view.Scale.X / renderRatio,
			view.RotationDegrees, view.Elevation);
		float rate = slot.Returning ? HandMotionMath.ReturnRate : HandMotionMath.LayoutRate;
		if (MotionVector.DistanceSquared(current.Center, target.Center) < 0.36f
			&& MathF.Abs(current.Scale - target.Scale) < 0.003f
			&& MathF.Abs(current.AngleDegrees - target.AngleDegrees) < 0.08f
			&& MathF.Abs(current.Elevation - target.Elevation) < 0.008f)
		{
			if (slot.Returning)
			{
				slot.Returning = false;
				view.MouseFilter = MouseFilterEnum.Stop;
			}
			return target;
		}
		return HandMotionMath.DampPose(current, target, delta, rate);
	}

	private HandPose GetDrawPose(DrawFlight flight, HandPose target, float t, float scale)
	{
		// 起速稍快、收尾减速；末端斜率为零，不和入列后的阻尼发生速度跳变。
		float eased = t * (2f - t);
		MotionVector from = ToMotion(ToHandLocal(flight.Origin));
		return new HandPose(HandMotionMath.DrawArc(from, target.Center, HandMotionMath.DrawArcHeight * scale, eased),
			Mathf.Lerp(0.42f, target.Scale, eased), Mathf.Lerp(0f, target.AngleDegrees, eased),
			(0.55f * (1f - eased)) + (target.Elevation * eased));
	}

	private static void ApplyPose(CardUI view, HandPose pose, float scale, int zIndex)
	{
		Vector2 position = ToGodot(pose.Center) - view.PivotOffset;
		Vector2 cardScale = Vector2.One * (pose.Scale * scale / view.FaceRenderScale);
		if (view.Position != position)
		{
			view.Position = position;
		}
		if (!view.Scale.IsEqualApprox(cardScale))
		{
			view.Scale = cardScale;
		}
		if (!Mathf.IsEqualApprox(view.RotationDegrees, pose.AngleDegrees))
		{
			view.RotationDegrees = pose.AngleDegrees;
		}
		view.SetElevation(pose.Elevation);
		if (view.ZIndex != zIndex)
		{
			view.ZIndex = zIndex;
		}
	}

	private HandLayout GetLayout(int count)
	{
		Vector2 size = _cardContainer.Size;
		float scale = UiScale;
		float width = size.X > 1f ? size.X : GetViewportRect().Size.X;
		float height = GetViewportRect().End.Y - _cardContainer.GlobalPosition.Y;
		if (height <= 1f || size.Y <= 1f)
		{
			height = 110f * scale;
		}
		var layoutSize = new Vector2(width, height);
		if (_layoutCount != count || _layoutSize != layoutSize || _layoutScale != scale)
		{
			_layoutCount = count;
			_layoutSize = layoutSize;
			_layoutScale = scale;
			_layout = new HandLayout(count, width, height, scale);
			if (_hitFrames.Length < count)
			{
				_hitFrames = new HandHitFrame[count];
				_hitCandidates = new bool[count];
			}
			for (int i = 0; i < count; i++)
			{
				_hitFrames[i] = _layout.GetHitFrame(i);
			}
		}
		return _layout;
	}

	private Vector2 ToHandLocal(Vector2 global)
	{
		return _cardContainer.GetGlobalTransformWithCanvas().AffineInverse() * global;
	}

	private static float UiScale => UIScaler.Instance?.GetScaleFactor() ?? 1f;
	private static MotionVector ToMotion(Vector2 point) => new(point.X, point.Y);
	private static Vector2 ToGodot(MotionVector point) => new(point.X, point.Y);
	internal static bool IsLive(CardUI? view) => IsInstanceValid(view) && !view!.IsQueuedForDeletion();
	private bool IsInHand(CardSlot slot) => IsLive(slot.View) && slot.View.GetParent() == _cardContainer;
	private bool IsAvailable(CardSlot slot) => IsInHand(slot) && !slot.AwaitingModelRemoval && !slot.Draw.HasValue
		&& !slot.Returning && slot.View.IsVisibleInTree();

	private bool TryGetSlot(CardUI view, out CardSlot slot)
	{
		if (IsLive(view) && view.Card != null && _byCard.TryGetValue(view.Card, out CardSlot? found) && found.View == view)
		{
			slot = found;
			return true;
		}
		slot = null!;
		return false;
	}

	private void RestoreDrawOrder()
	{
		int index = 0;
		foreach (CardSlot slot in _slots)
		{
			if (IsInHand(slot))
			{
				if (slot.View.GetIndex() != index)
				{
					_cardContainer.MoveChild(slot.View, index);
				}
				index++;
			}
		}
	}

	private void Unsubscribe(CardUI view)
	{
		view.OnCardClicked -= OnCardClicked;
		view.OnCardRightClicked -= OnCardRightClicked;
		view.OnMobileDragBegan -= OnMobileDragBegan;
	}

	private void ClearSlotFocus(CardSlot slot)
	{
		if (_hoveredSlot == slot)
		{
			_hoveredSlot = null;
		}
		if (_tappedSlot == slot)
		{
			_tappedSlot = null;
		}
		if (_keyboardFocus == slot)
		{
			ClearKeyboardFocus();
		}
		if (_loanedForPlay == slot)
		{
			_loanedForPlay = null;
		}
	}

	private void OnCardRightClicked(CardUI view)
	{
		if (!MobileInputRouter.IsMobile || _loanedForPlay?.View == view)
		{
			OnCardCancelled?.Invoke();
		}
	}

	private void OnMobileDragBegan(CardUI view)
	{
		if (!_handSelectMode && TryGetSlot(view, out CardSlot slot) && IsAvailable(slot))
		{
			_tappedSlot = null;
			OnCardSelectedForPlay?.Invoke(slot.Card);
		}
	}

	private void OnCardClicked(CardUI view)
	{
		if (!TryGetSlot(view, out CardSlot slot))
		{
			return;
		}
		if (_handSelectMode)
		{
			OnCardSelectionToggled?.Invoke(slot.Card);
		}
		else if (IsAvailable(slot))
		{
			if (MobileInputRouter.IsMobile && !IsKeyboardSelection)
			{
				_tappedSlot = _tappedSlot == slot ? null : slot;
			}
			else
			{
				OnCardSelectedForPlay?.Invoke(slot.Card);
			}
		}
	}

	/// <summary>外部触控点击收起展开卡，不新增一条触控选牌路径。</summary>
	internal void CollapseTouchPreviewOutside(Vector2 screenPosition)
	{
		if (_tappedSlot != null && !SceneLifecycleGuard.ShouldSkip(this)
			&& !GetGlobalRect().HasPoint(screenPosition)
			&& (!IsLive(_tappedSlot.View) || !_tappedSlot.View.IsVisibleInTree()
				|| !_tappedSlot.View.GetGlobalRect().HasPoint(screenPosition)))
		{
			_tappedSlot = null;
		}
	}

	private void RegisterHotkeyBindings()
	{
		HotkeyManager? hotkeys = HotkeyManager.Instance;
		if (hotkeys == null)
		{
			return;
		}
		_cardSelectActions = new Action[10];
		for (int i = 0; i < _cardSelectActions.Length; i++)
		{
			int index = i;
			_cardSelectActions[i] = () => SelectCardByIndex(index);
			hotkeys.PushPressedBinding(OdysseyInput.SelectCardActions[i], _cardSelectActions[i]);
		}
		_leftAction = () => CycleFocus(-1);
		_rightAction = () => CycleFocus(1);
		_acceptAction = AcceptFocusedCard;
		hotkeys.PushPressedBinding(OdysseyInput.Left, _leftAction);
		hotkeys.PushPressedBinding(OdysseyInput.Right, _rightAction);
		hotkeys.PushPressedBinding(OdysseyInput.Accept, _acceptAction);
		hotkeys.KeyboardFocusChanged += OnKeyboardFocusChanged;
	}

	private void UnregisterHotkeyBindings()
	{
		HotkeyManager? hotkeys = HotkeyManager.Instance;
		if (hotkeys == null || _cardSelectActions == null)
		{
			return;
		}
		for (int i = 0; i < _cardSelectActions.Length; i++)
		{
			hotkeys.RemovePressedBinding(OdysseyInput.SelectCardActions[i], _cardSelectActions[i]);
		}
		hotkeys.RemovePressedBinding(OdysseyInput.Left, _leftAction!);
		hotkeys.RemovePressedBinding(OdysseyInput.Right, _rightAction!);
		hotkeys.RemovePressedBinding(OdysseyInput.Accept, _acceptAction!);
		hotkeys.KeyboardFocusChanged -= OnKeyboardFocusChanged;
		_cardSelectActions = null;
	}

	private void SelectCardByIndex(int index)
	{
		if (SceneLifecycleGuard.ShouldSkip(this) || _slots.Count == 0)
		{
			return;
		}
		CardSlot slot = _slots[Math.Min(index, _slots.Count - 1)];
		if (_loanedForPlay == slot)
		{
			OnCardCancelled?.Invoke();
			return;
		}
		if (!IsAvailable(slot) && (!_handSelectMode || !IsLive(slot.View) || IsInHand(slot)))
		{
			return;
		}
		SetKeyboardFocus(slot);
		IsKeyboardSelection = true;
		try
		{
			OnCardClicked(slot.View);
		}
		finally
		{
			IsKeyboardSelection = false;
		}
	}

	private void CycleFocus(int direction)
	{
		if (SceneLifecycleGuard.ShouldSkip(this) || _slots.Count == 0)
		{
			return;
		}
		int index = _keyboardFocus == null ? (direction > 0 ? -1 : 0) : _slots.IndexOf(_keyboardFocus);
		for (int i = 0; i < _slots.Count; i++)
		{
			index = (index + direction + _slots.Count) % _slots.Count;
			if (IsAvailable(_slots[index]))
			{
				SetKeyboardFocus(_slots[index]);
				break;
			}
		}
	}

	private void AcceptFocusedCard()
	{
		int index = _keyboardFocus != null ? _slots.IndexOf(_keyboardFocus) : _slots.FindIndex(IsAvailable);
		if (index >= 0)
		{
			SelectCardByIndex(index);
		}
	}

	private void OnKeyboardFocusChanged(bool active)
	{
		if (!active)
		{
			ClearKeyboardFocus();
		}
	}

	private void SetKeyboardFocus(CardSlot slot)
	{
		ClearKeyboardFocus();
		_keyboardFocus = slot;
		slot.View.SetKeyboardFocusTint(true);
	}

	internal void ClearKeyboardFocus()
	{
		if (_keyboardFocus != null && IsLive(_keyboardFocus.View))
		{
			_keyboardFocus.View.SetKeyboardFocusTint(false);
		}
		_keyboardFocus = null;
	}

	private sealed class CardSlot(Card.Card card, CardUI view)
	{
		public Card.Card Card { get; } = card;
		public CardUI View { get; } = view;
		public bool Returning { get; set; }
		public bool AwaitingModelRemoval { get; set; }
		public bool RemovalObserved { get; set; }
		public DrawFlight? Draw { get; set; }
	}

	private readonly record struct DrawFlight(Vector2 Origin, double StartTime);
}
