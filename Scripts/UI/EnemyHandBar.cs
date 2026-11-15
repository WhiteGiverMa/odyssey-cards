#nullable enable
using System;
using System.Collections.Generic;
using Godot;
using OdysseyCards.Core;
using OdysseyCards.Infrastructure;
using Loc = OdysseyCards.Localization.Localization;
using RuntimeCard = OdysseyCards.Card.Card;

namespace OdysseyCards.UI;

/// <summary>
/// 指挥官敌方手牌栏：70px 内展示暗金卡背、情报明牌、法力与牌库厚度。
/// 明暗由调用方传入的实例集合/委托决定，不读取任何尚未合入的规则字段。
/// </summary>
[Tool]
public partial class EnemyHandBar : Control
{
	private sealed class HandSlot
	{
		public readonly RuntimeCard Card;
		public bool Revealed;
		public bool Present;
		public bool StartScan;
		public Rect2 Bounds;
		public HandCardVisual? Visual;

		public HandSlot(RuntimeCard card) { Card = card; }
	}

	private const float _miniHeight = 58f;
	private const float _resourceWidth = 190f;
	private static readonly Color _backGold = new("#c9a970");
	private readonly List<HandSlot> _slots = new();
	private readonly Dictionary<RuntimeCard, HandSlot> _byCard = new(ReferenceEqualityComparer.Instance);
	private readonly List<RuntimeCard> _removed = new();
	private readonly List<(RuntimeCard Card, bool Revealed)> _pendingHand = new();
	private readonly Stack<HandCardVisual> _spareVisuals = new();
	private bool _syncQueued;
	private bool _ready;
	private int _currentMana;
	private int _maxMana;
	private int _deckCount;
	private string _manaText = "";
	private string _deckText = "";
	private string _handText = "";
	private CardUI? _preview;
	private RuntimeCard? _previewCard;
	private Rect2 _previewSource;
	private IDisposable? _touchRegistration;
	private GameManager? _languageSource;

	/// <summary>预览浮层距逻辑视口边缘的安全留白，默认 12px。</summary>
	[Export] public float PreviewSafeMargin { get; set; } = 12f;

	/// <summary>预览层级；应低于暂停/设置等全局模态层。</summary>
	[Export] public int PreviewLayer { get; set; } = 20;

	/// <summary>当前显示的敌方手牌张数。</summary>
	public int CardCount => _pendingHand.Count;

	/// <summary>无参构造兼容编辑器；仅本栏接收 GUI，所有卡牌子视图均穿透。</summary>
	public EnemyHandBar()
	{
		CustomMinimumSize = new Vector2(0, 70);
		MouseFilter = MouseFilterEnum.Stop;
		ProcessMode = ProcessModeEnum.Always;
	}

	/// <summary>
	/// 以运行时卡牌实例集合更新明暗；同名不同实例分别处理，调用时立即取得快照。
	/// 外部集合之后变化时需再次调用，不轮询或更改战斗状态。
	/// </summary>
	public void SetHand(IReadOnlyList<RuntimeCard> cards, ISet<RuntimeCard> revealedCards)
	{
		ArgumentNullException.ThrowIfNull(revealedCards);
		SetHand(cards, revealedCards.Contains);
	}

	/// <summary>以外部明暗判定委托更新手牌，委托只在本次快照时执行。</summary>
	public void SetHand(IReadOnlyList<RuntimeCard> cards, Func<RuntimeCard, bool> isRevealed)
	{
		ArgumentNullException.ThrowIfNull(cards);
		ArgumentNullException.ThrowIfNull(isRevealed);
		// 先验证输入再替换快照，避免无效参数令现有显示落入半更新状态。
		var snapshot = new List<(RuntimeCard Card, bool Revealed)>(cards.Count);
		var instances = new HashSet<RuntimeCard>(ReferenceEqualityComparer.Instance);
		for (int i = 0; i < cards.Count; i++)
		{
			RuntimeCard card = cards[i];
			ArgumentNullException.ThrowIfNull(card);
			if (!instances.Add(card))
			{
				throw new ArgumentException("手牌列表不可重复包含同一个卡牌实例。", nameof(cards));
			}
			snapshot.Add((card, isRevealed(card)));
		}
		_pendingHand.Clear();
		_pendingHand.AddRange(snapshot);
		HidePreview();
		// 旧的明牌立即隐藏，直到延迟同步完成，防止回收/重新变暗时泄露一帧牌面。
		foreach (HandSlot slot in _slots)
		{
			slot.Visual?.Hide();
		}
		RequestSynchronization();
	}

	/// <summary>独立刷新敌方法力与剩余抽牌堆，不依赖指挥官实现类型。</summary>
	public void SetResources(int currentMana, int maxMana, int drawPileCount)
	{
		_currentMana = Mathf.Max(0, currentMana);
		_maxMana = Mathf.Max(0, maxMana);
		_deckCount = Mathf.Max(0, drawPileCount);
		RefreshLocalizedText();
		QueueRedraw();
	}

	public override void _Ready()
	{
		_ready = true;
		if (!Engine.IsEditorHint())
		{
			var previewLayer = new CanvasLayer { Layer = PreviewLayer };
			AddChild(previewLayer);
			_preview = new CardUI { DisplayOnly = true, RenderScaleOverride = 1f, Visible = false };
			previewLayer.AddChild(_preview);
			_preview.SetProcess(false);
			_languageSource = GameManager.Instance;
			if (_languageSource != null)
			{
				_languageSource.LanguageChanged += OnLanguageChanged;
			}
			RegisterTouchZone();
		}
		RefreshLocalizedText();
		RequestSynchronization();
	}

	public override void _ExitTree()
	{
		_ready = false;
		_syncQueued = false;
		_touchRegistration?.Dispose();
		_touchRegistration = null;
		if (_languageSource != null)
		{
			_languageSource.LanguageChanged -= OnLanguageChanged;
		}
		_languageSource = null;
		HidePreview();
	}

	public override void _Notification(int what)
	{
		if (what == NotificationResized && _ready)
		{
			LayoutCards();
			HidePreview();
			QueueRedraw();
		}
		else if (what == NotificationVisibilityChanged && _ready)
		{
			if (IsVisibleInTree())
			{
				RegisterTouchZone();
			}
			else
			{
				_touchRegistration?.Dispose();
				_touchRegistration = null;
				HidePreview();
			}
		}
	}

	public override void _Process(double delta)
	{
		if (Engine.IsEditorHint() || !_ready || _syncQueued || !IsVisibleInTree() || IsQueuedForDeletion() || GetTree().Paused)
		{
			HidePreview();
			return;
		}
		if (MobileInputRouter.IsMobile)
		{
			MobileInputRouter? router = MobileInputRouter.Instance;
			if (router == null || !router.IsTouchActive || router.ActiveGestureOwner != this
				|| router.TouchTravelDistance > 12f)
			{
				HidePreview();
			}
			return;
		}

		// 仅在本控件是 GUI 实际悬停目标时响应，避免暂停或模态页背后的明牌冒出。
		if (GetViewport().GuiGetHoveredControl() != this)
		{
			HidePreview();
			return;
		}
		Vector2 pointer = GetLocalMousePosition();
		HandSlot? hovered = FindSlot(pointer);
		if (hovered == null || !hovered.Revealed)
		{
			HidePreview();
		}
		else
		{
			ShowPreview(hovered);
		}
	}

	public override void _Draw()
	{
		if (Size.X < 1f || Size.Y < 1f)
		{
			return;
		}
		DrawRect(new Rect2(Vector2.Zero, Size), new Color(UIThemeFactory.SpaceBg, 0.84f));
		DrawLine(new Vector2(0, Size.Y - 1f), new Vector2(Size.X, Size.Y - 1f), new Color(_backGold, 0.28f), 1f);
		DrawResources();
	}

	private void RequestSynchronization()
	{
		if (!_ready || !IsInsideTree() || IsQueuedForDeletion() || _syncQueued)
		{
			return;
		}
		_syncQueued = true;
		_ = CallDeferred(nameof(SynchronizeHand));
	}

	/// <summary>延迟边界增量同步并复用 CardUI；回收只隐藏，不与 AddChild 混用 QueueFree。</summary>
	private void SynchronizeHand()
	{
		_syncQueued = false;
		if (!_ready || !IsInsideTree() || IsQueuedForDeletion())
		{
			return;
		}
		foreach (HandSlot slot in _slots)
		{
			slot.Present = false;
		}
		foreach ((RuntimeCard card, bool revealed) in _pendingHand)
		{
			if (!_byCard.TryGetValue(card, out HandSlot? slot))
			{
				slot = new HandSlot(card);
				_byCard.Add(card, slot);
			}
			slot.Present = true;
			slot.StartScan = !slot.Revealed && revealed;
			slot.Revealed = revealed;
		}
		_removed.Clear();
		foreach (HandSlot slot in _slots)
		{
			if (slot.Present)
			{
				continue;
			}
			if (slot.Visual != null)
			{
				slot.Visual.ResetVisual();
				_spareVisuals.Push(slot.Visual);
				slot.Visual = null;
			}
			_removed.Add(slot.Card);
		}
		foreach (RuntimeCard removed in _removed)
		{
			_ = _byCard.Remove(removed);
		}
		_removed.Clear();
		_slots.Clear();
		foreach ((RuntimeCard card, _) in _pendingHand)
		{
			HandSlot slot = _byCard[card];
			_slots.Add(slot);
			if (slot.Visual == null)
			{
				if (_spareVisuals.Count > 0)
				{
					slot.Visual = _spareVisuals.Pop();
				}
				else
				{
					slot.Visual = new HandCardVisual();
					AddChild(slot.Visual);
				}
			}
			slot.Visual.Bind(card, slot.Revealed, slot.StartScan);
		}
		LayoutCards();
		RefreshLocalizedText();
		QueueRedraw();
	}

	private void LayoutCards()
	{
		if (_slots.Count == 0)
		{
			return;
		}
		float reserved = Mathf.Min(_resourceWidth, Size.X * 0.35f);
		float available = Mathf.Max(1f, Size.X - reserved - 28f);
		float height = Mathf.Max(1f, Mathf.Min(_miniHeight, Size.Y - 12f));
		float width = height * CardUI.DESIGN_WIDTH / CardUI.DESIGN_HEIGHT;
		width = Mathf.Min(width, available);
		height = width * CardUI.DESIGN_HEIGHT / CardUI.DESIGN_WIDTH;
		float step = _slots.Count == 1 ? 0 : Mathf.Min(width + 9f, Mathf.Max(0, available - width) / (_slots.Count - 1));
		float total = width + step * (_slots.Count - 1);
		float left = 14f + (available - total) * 0.5f;
		for (int i = 0; i < _slots.Count; i++)
		{
			HandSlot slot = _slots[i];
			slot.Bounds = new Rect2(left + i * step, (Size.Y - height) * 0.5f, width, height);
			if (slot.Visual == null)
			{
				continue;
			}
			slot.Visual.Position = slot.Bounds.Position;
			slot.Visual.Size = slot.Bounds.Size;
			slot.Visual.LayoutVisual(height / _miniHeight);
			MoveChild(slot.Visual, -1);
		}
	}

	private static void DrawCardBack(CanvasItem canvas, Rect2 rect)
	{
		if (rect.Size.X <= 0f || rect.Size.Y <= 0f)
		{
			return;
		}
		float scale = rect.Size.Y / _miniHeight;
		canvas.DrawRect(rect.Grow(1f), new Color(0, 0, 0, 0.6f));
		canvas.DrawRect(rect, new Color("#171020"));
		canvas.DrawRect(rect.Grow(-1.5f * scale), new Color("#282035"), false, 1f);
		Vector2 center = rect.GetCenter();
		float radius = rect.Size.X * 0.34f;
		canvas.DrawCircle(center, radius * 1.1f, new Color(_backGold, 0.055f));
		canvas.DrawArc(center, radius, 0, Mathf.Tau, 48, new Color(_backGold, 0.8f), Mathf.Max(0.5f, scale * 0.8f), true);
		canvas.DrawArc(center, radius * 0.76f, 0, Mathf.Tau, 40, new Color(_backGold, 0.55f), 0.65f, true);
		for (int tick = 0; tick < 24; tick++)
		{
			var axis = Vector2.FromAngle(tick * Mathf.Tau / 24f);
			canvas.DrawLine(center + axis * radius * 0.9f, center + axis * radius * (tick % 3 == 0 ? 1.12f : 1.02f),
				new Color(_backGold, 0.65f), 0.65f, true);
		}
		Span<Vector2> star = stackalloc Vector2[9];
		for (int i = 0; i < 8; i++)
		{
			star[i] = center + Vector2.FromAngle(i * Mathf.Tau / 8f - Mathf.Pi * 0.5f)
				* radius * ((i & 1) == 0 ? 0.67f : 0.2f);
		}
		star[8] = star[0];
		canvas.DrawColoredPolygon(star[..8], new Color(_backGold, 0.15f), ReadOnlySpan<Vector2>.Empty, null);
		canvas.DrawPolyline(star, new Color(_backGold, 0.9f), Mathf.Max(0.6f, scale * 0.8f), true);
		canvas.DrawCircle(center, radius * 0.08f, UIThemeFactory.WarmGold);
		for (int i = 0; i < 4; i++)
		{
			float sx = (i & 1) == 0 ? 1f : -1f;
			float sy = i < 2 ? 1f : -1f;
			Vector2 corner = new(sx > 0 ? rect.Position.X : rect.End.X, sy > 0 ? rect.Position.Y : rect.End.Y);
			Vector2 p = corner + new Vector2(sx * 3f, sy * 9f) * scale;
			Vector2 bend = corner + new Vector2(sx * 3f, sy * 5f) * scale;
			Vector2 end = corner + new Vector2(sx * 10f, sy * 3f) * scale;
			canvas.DrawLine(p, bend, _backGold, 0.75f, true);
			canvas.DrawLine(bend, end, _backGold, 0.75f, true);
		}
	}

	private void DrawResources()
	{
		float width = Mathf.Min(_resourceWidth, Size.X * 0.35f);
		float left = Size.X - width;
		DrawLine(new Vector2(left, 12f), new Vector2(left, Size.Y - 12f), new Color(_backGold, 0.2f), 0.8f);
		Font font = GetThemeDefaultFont();
		int fontSize = Mathf.Max(8, Mathf.Min(12, (int)(Size.Y * 0.18f)));
		DrawString(font, new Vector2(left + 12f, Size.Y * 0.28f), _handText,
			HorizontalAlignment.Left, Mathf.Max(0, width - 20f), fontSize - 1, UIThemeFactory.TextDim);
		int crystals = Mathf.Min(10, Mathf.Max(_currentMana, _maxMana));
		float crystalStep = Mathf.Min(11f, Mathf.Max(2f, (width - 16f) / Mathf.Max(1, crystals)));
		for (int i = 0; i < crystals; i++)
		{
			ManaCrystalBar.DrawCrystal(this, new Vector2(left + 16f + i * crystalStep, Size.Y * 0.48f),
				Mathf.Min(4.5f, crystalStep * 0.4f), i < _currentMana);
		}
		DrawString(font, new Vector2(left + 12f, Size.Y * 0.83f), _manaText + "  ·  " + _deckText,
			HorizontalAlignment.Left, Mathf.Max(0, width - 20f), fontSize, UIThemeFactory.TextBright);
	}

	private HandSlot? FindSlot(Vector2 localPosition)
	{
		for (int i = _slots.Count - 1; i >= 0; i--)
		{
			if (_slots[i].Bounds.HasPoint(localPosition))
			{
				return _slots[i];
			}
		}
		return null;
	}

	private void ShowPreview(HandSlot slot)
	{
		if (_preview == null || !slot.Revealed || !IsVisibleInTree())
		{
			return;
		}
		if (!ReferenceEquals(_previewCard, slot.Card))
		{
			_preview.SetCard(slot.Card);
			_previewCard = slot.Card;
		}
		_previewSource = slot.Bounds;
		Rect2 viewport = GetViewport().GetVisibleRect();
		float margin = Mathf.Max(0, PreviewSafeMargin);
		Vector2 cardSize = new(CardUI.DESIGN_WIDTH, CardUI.DESIGN_HEIGHT);
		Vector2 source = GetGlobalTransformWithCanvas() * slot.Bounds.GetCenter();
		Vector2 bottom = GetGlobalTransformWithCanvas() * new Vector2(slot.Bounds.End.X, Size.Y);
		float minX = viewport.Position.X + margin;
		float minY = viewport.Position.Y + margin;
		float x = source.X + slot.Bounds.Size.X * 0.5f + 12f;
		if (x + cardSize.X > viewport.End.X - margin)
		{
			x = source.X - cardSize.X - 12f;
		}
		// 特写始终保持 1.0x，以视口上部安全区为锚，不能被 70px 宿主裁剪。
		_preview.Position = new Vector2(Mathf.Clamp(x, minX, Mathf.Max(minX, viewport.End.X - margin - cardSize.X)),
			Mathf.Clamp(bottom.Y + 8f, minY, Mathf.Max(minY, viewport.End.Y - margin - cardSize.Y)));
		_preview.Show();
	}

	private void HidePreview()
	{
		_preview?.Hide();
		_previewCard = null;
	}

	private void RegisterTouchZone()
	{
		if (_touchRegistration != null || !MobileInputRouter.IsMobile || !IsVisibleInTree())
		{
			return;
		}
		MobileInputRouter? router = MobileInputRouter.Instance;
		if (router == null)
		{
			return;
		}
		_touchRegistration = router.RegisterDragZone(this, GetGlobalRect(), 110,
			onDragStart: _ => HidePreview(), onDragMove: OnTouchMoved, onDragEnd: _ => HidePreview(),
			onLongPress: OnLongPress);
	}

	private void OnLongPress()
	{
		MobileInputRouter? router = MobileInputRouter.Instance;
		if (router == null || !IsVisibleInTree() || router.ActiveGestureOwner != this)
		{
			return;
		}
		Vector2 local = GetGlobalTransformWithCanvas().AffineInverse() * router.TouchPosition;
		HandSlot? slot = FindSlot(local);
		if (slot?.Revealed == true)
		{
			ShowPreview(slot);
		}
	}

	private void OnTouchMoved(Vector2 position, Vector2 relative)
	{
		if (!_previewSource.HasPoint(GetGlobalTransformWithCanvas().AffineInverse() * position))
		{
			HidePreview();
		}
	}

	private void OnLanguageChanged(string language)
	{
		if (!IsInsideTree() || IsQueuedForDeletion())
		{
			return;
		}
		RefreshLocalizedText();
		foreach (HandSlot slot in _slots)
		{
			slot.Visual?.RefreshCard();
		}
		HidePreview();
		QueueRedraw();
	}

	private void RefreshLocalizedText()
	{
		_handText = Loc.T("combat.enemy_hand_count", "敌方手牌 {count}")
			.Replace("{count}", _pendingHand.Count.ToString(), StringComparison.Ordinal);
		_manaText = Loc.T("combat.enemy_mana", "法力 {current}/{max}")
			.Replace("{current}", _currentMana.ToString(), StringComparison.Ordinal)
			.Replace("{max}", _maxMana.ToString(), StringComparison.Ordinal);
		_deckText = Loc.T("combat.enemy_draw_pile", "牌库 {count}")
			.Replace("{count}", _deckCount.ToString(), StringComparison.Ordinal);
	}

	/// <summary>明牌显示池条目；无参构造与显式刷新兼容编辑器，不注册战斗交互。</summary>
	private partial class HandCardVisual : Control
	{
		private CardUI? _cardView;
		private RevealScanOverlay? _scan;
		private RuntimeCard? _card;
		private bool _revealed;

		public HandCardVisual() { MouseFilter = MouseFilterEnum.Ignore; }

		public override void _Ready()
		{
			_cardView = new CardUI { DisplayOnly = true, RenderScaleOverride = _miniHeight / CardUI.DESIGN_HEIGHT };
			AddChild(_cardView);
			_cardView.SetProcess(false);
			_scan = new RevealScanOverlay();
			AddChild(_scan);
		}

		public void Bind(RuntimeCard card, bool revealed, bool startScan)
		{
			_card = card;
			_revealed = revealed;
			if (_cardView != null)
			{
				_cardView.Visible = revealed;
				if (revealed)
				{
					_cardView.SetCard(card);
				}
			}
			if (!revealed)
			{
				_scan?.StopScan();
			}
			Show();
			if (startScan)
			{
				_scan?.PlayScan();
			}
			QueueRedraw();
		}

		public override void _Draw()
		{
			if (!_revealed)
			{
				DrawCardBack(this, new Rect2(Vector2.Zero, Size));
			}
		}

		public void RefreshCard()
		{
			if (_card != null && _revealed)
			{
				_cardView?.SetCard(_card);
			}
		}

		public void LayoutVisual(float scale)
		{
			if (_cardView != null)
			{
				_cardView.Scale = Vector2.One * scale;
			}
			if (_scan != null)
			{
				_scan.Size = Size;
			}
			QueueRedraw();
		}

		public void ResetVisual()
		{
			_card = null;
			_scan?.StopScan();
			Hide();
		}
	}

	/// <summary>情报揭露的金青扫描流光，位于明牌之上；Tween 结束后完全停止重绘。</summary>
	private partial class RevealScanOverlay : Control
	{
		private Tween? _tween;
		private float _progress;

		public RevealScanOverlay()
		{
			MouseFilter = MouseFilterEnum.Ignore;
			Visible = false;
		}

		public void PlayScan()
		{
			StopScan();
			Show();
			_progress = 0;
			_tween = CreateTween();
			_ = _tween.TweenMethod(Callable.From<float>(SetProgress), 0f, 1f, 0.85f);
			_ = _tween.TweenCallback(Callable.From(Hide));
		}

		public void StopScan()
		{
			_tween?.Kill();
			_tween = null;
			Hide();
		}

		public override void _ExitTree() { StopScan(); }

		public override void _Draw()
		{
			float fade = (1f - _progress) * 0.9f;
			Color color = UIThemeFactory.WarmGold.Lerp(UIThemeFactory.CyanGold, _progress);
			DrawRect(new Rect2(Vector2.Zero, Size).Grow(1f), new Color(color, fade), false, 1.2f);
			DrawRect(new Rect2(Vector2.Zero, Size).Grow(3f), new Color(color, fade * 0.18f), false, 2f);
			float y = Size.Y * _progress;
			for (int i = 0; i < 5; i++)
			{
				float lineY = Mathf.Max(0, y - i * 1.2f);
				DrawLine(new Vector2(0, lineY), new Vector2(Size.X, lineY), new Color(color, fade * (1f - i / 5f)), 1f, true);
			}
		}

		private void SetProgress(float value)
		{
			_progress = value;
			QueueRedraw();
		}
	}
}
