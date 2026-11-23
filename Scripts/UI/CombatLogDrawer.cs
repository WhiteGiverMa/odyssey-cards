#nullable enable
using System;
using System.Collections.Generic;
using Godot;
using OdysseyCards.Combat;
using OdysseyCards.Core;
using OdysseyCards.Infrastructure;
using Loc = OdysseyCards.Localization.Localization;

namespace OdysseyCards.UI;

/// <summary>
/// KARDS 风格左侧战术抽屉。只有面板与把手接收输入，其余全屏区域完全穿透；
/// 卡名芯片与弃牌卡面芯片都可悬停/点按显示真实 <see cref="CardUI"/> 历史卡面。
/// </summary>
public partial class CombatLogDrawer : Control
{
	private const float PanelWidth = 320f;
	private const float HandleWidth = 34f;
	private const float HandleHeight = 110f;
	private const float TopMargin = 82f;
	private const float BottomMargin = 200f;

	// ===== 基础字号（实际字号 = 基础字号 × 界面字体倍率） =====
	private const int TitleBaseFontSize = 16;
	private const int HandleBaseFontSize = 13;
	private const int TurnBaseFontSize = 13;
	private const int ChipBaseFontSize = 12;
	private const int BadgeBaseFontSize = 11;
	private const int CloseButtonBaseFontSize = 14;
	private static readonly Color PlayerColor = new("#7FD8FF");
	private static readonly Color EnemyColor = new("#FF6B7A");
	private static readonly Color PanelColor = new(0.071f, 0.063f, 0.122f, 0.94f);

	private CombatLogService? _service;
	private PanelContainer _panel = null!;
	private Button _handle = null!;
	private Label _handleLabel = null!;
	private Label _titleLabel = null!;
	private Button _closeButton = null!;
	private ScrollContainer _scroll = null!;
	private VBoxContainer _entries = null!;
	private CardUI _preview = null!;
	private Tween? _slideTween;
	private Tween? _pulseTween;
	private bool _isOpen;
	private bool _clearPending;
	private bool _rebuildPending;
	private bool _manualHistory;
	private bool _programmaticScroll;
	private Vector2 _lastViewportSize;
	private Rect2 _previewSourceRect;
	private readonly List<Action> _unsubscribe = new();

	public bool IsOpen => _isOpen;

	public override void _Ready()
	{
		Name = "CombatLogDrawer";
		MouseFilter = MouseFilterEnum.Ignore;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		BuildSurface();
		var fontSource = UIScaler.Instance;
		if (fontSource != null)
		{
			fontSource.OnFontScaleChanged += OnFontScaleChanged;
			_unsubscribe.Add(() => fontSource.OnFontScaleChanged -= OnFontScaleChanged);
		}
		if (GameManager.Instance != null)
		{
			GameManager.Instance.LanguageChanged += OnLanguageChanged;
			_unsubscribe.Add(() => GameManager.Instance.LanguageChanged -= OnLanguageChanged);
		}
	}

	/// <summary>绑定战斗日志真源；可在 _Ready 前后调用，重复调用会先解除旧订阅。</summary>
	public void Initialize(CombatLogService service)
	{
		ArgumentNullException.ThrowIfNull(service);
		if (_service != null)
		{
			_service.OnEntryAdded -= OnEntryAdded;
			_service.OnCleared -= OnCleared;
		}
		_service = service;
		_service.OnEntryAdded += OnEntryAdded;
		_service.OnCleared += OnCleared;
		_unsubscribe.Add(() =>
		{
			service.OnEntryAdded -= OnEntryAdded;
			service.OnCleared -= OnCleared;
		});
		if (IsNodeReady())
			RebuildEntries();
	}

	public override void _ExitTree()
	{
		_slideTween?.Kill();
		_pulseTween?.Kill();
		foreach (Action unsubscribe in _unsubscribe)
			unsubscribe();
		_unsubscribe.Clear();
		_service = null;
	}

	public void Toggle()
	{
		SetOpen(!_isOpen);
	}

	public void SetOpen(bool open)
	{
		if (!IsNodeReady() || _isOpen == open)
			return;
		_isOpen = open;
		_slideTween?.Kill();
		Vector2 viewport = GetViewportRect().Size;
		float scale = GetUiScale();
		float width = PanelWidth * scale;
		// 展开时立即显示面板参与滑入；折叠时等滑出动画结束再隐藏，避免半途消失。
		if (open)
			_panel.Visible = true;
		float targetX = open ? 0f : -width;
		_slideTween = CreateTween();
		_slideTween.TweenProperty(_panel, "position:x", targetX, 0.18)
			.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		_slideTween.TweenProperty(_handle, "position:x", open ? width : 0f, 0.18)
			.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		_slideTween.Finished += OnSlideFinished;
		// 折叠态只有左侧窄把手拦截输入；抽屉主体（含滚动区）完全穿透。
		_panel.MouseFilter = open ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
		_scroll.MouseFilter = _panel.MouseFilter;
		if (open)
		{
			_manualHistory = false;
			_programmaticScroll = true;
			CallDeferred(nameof(ScrollToLatestDeferred));
		}
		else
		{
			HidePreview();
		}
		UpdateLayout(viewport);
	}

	private void OnSlideFinished()
	{
		if (!GodotObject.IsInstanceValid(_panel))
			return;
		_panel.Visible = _isOpen;
	}

	public override void _Process(double delta)
	{
		Vector2 viewport = GetViewportRect().Size;
		if (viewport != _lastViewportSize)
			UpdateLayout(viewport);
	}

	public override void _Input(InputEvent @event)
	{
		if (!MobileInputRouter.IsMobile || !_preview.Visible || @event is not InputEventScreenTouch touch || !touch.Pressed)
			return;
		if (!_previewSourceRect.HasPoint(touch.Position))
			HidePreview();
	}

	private void BuildSurface()
	{
		_panel = new PanelContainer
		{
			Name = "LogPanel",
			MouseFilter = MouseFilterEnum.Ignore,
		};
		_panel.AddThemeStyleboxOverride("panel", CreateStyle(PanelColor, new Color(PlayerColor, 0.65f), 1, 5));
		AddChild(_panel);

		var layout = new VBoxContainer();
		layout.AddThemeConstantOverride("separation", 7);
		_panel.AddChild(layout);

		var titleRow = new HBoxContainer();
		_titleLabel = new Label
		{
			Text = Loc.T("ui.combat.log_title", "战术战报"),
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		_titleLabel.AddThemeColorOverride("font_color", PlayerColor);
		titleRow.AddChild(_titleLabel);
		_closeButton = new Button { Text = "<", CustomMinimumSize = new Vector2(34, 32) };
		_closeButton.Pressed += () => SetOpen(false);
		titleRow.AddChild(_closeButton);
		layout.AddChild(titleRow);
		layout.AddChild(new HSeparator());

		_scroll = new ScrollContainer
		{
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			MouseFilter = MouseFilterEnum.Stop,
		};
		_entries = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		_entries.AddThemeConstantOverride("separation", 5);
		_scroll.AddChild(_entries);
		_scroll.GetVScrollBar().ValueChanged += OnScrollValueChanged;
		layout.AddChild(_scroll);

		_handle = new Button
		{
			Name = "LogHandle",
			MouseFilter = MouseFilterEnum.Stop,
		};
		_handle.AddThemeStyleboxOverride("normal", CreateStyle(new Color(0.071f, 0.063f, 0.122f, 0.91f), PlayerColor, 1, 5));
		_handle.AddThemeStyleboxOverride("hover", CreateStyle(new Color(0.10f, 0.12f, 0.19f, 0.97f), PlayerColor, 2, 5));
		_handle.AddThemeStyleboxOverride("pressed", CreateStyle(new Color(0.12f, 0.15f, 0.22f, 0.98f), PlayerColor, 2, 5));
		_handleLabel = new Label
		{
			Text = Loc.T("ui.combat.log_handle", "战报"),
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		_handleLabel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		_handleLabel.AddThemeColorOverride("font_color", PlayerColor);
		_handle.AddChild(_handleLabel);
		_handle.Pressed += Toggle;
		AddChild(_handle);

		ApplyStaticFontScale();

		var previewLayer = new CanvasLayer { Name = "CombatLogPreviewLayer", Layer = 25 };
		AddChild(previewLayer);
		_preview = new CardUI
		{
			DisplayOnly = true,
			RenderScaleOverride = 1f,
			Visible = false,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		previewLayer.AddChild(_preview);
		_preview.SetProcess(false);
		_lastViewportSize = GetViewportRect().Size;
		UpdateLayout(_lastViewportSize);
		_panel.Visible = _isOpen;
	}

	private void UpdateLayout(Vector2 viewport)
	{
		_lastViewportSize = viewport;
		if (!IsNodeReady())
			return;
		float scale = GetUiScale();
		float width = PanelWidth * scale;
		float height = Mathf.Max(220f * scale, viewport.Y - (TopMargin + BottomMargin) * scale);
		_panel.Size = new Vector2(width, height);
		_handle.Size = new Vector2(ResolveHandleWidth(scale), HandleHeight * scale);
		if (_slideTween == null || !_slideTween.IsRunning())
		{
			_panel.Position = new Vector2(_isOpen ? 0f : -width, TopMargin * scale);
			_handle.Position = new Vector2(_isOpen ? width : 0f, TopMargin * scale + 32f * scale);
		}
		else
		{
			_panel.Position = new Vector2(_panel.Position.X, TopMargin * scale);
			_handle.Position = new Vector2(_handle.Position.X, TopMargin * scale + 32f * scale);
		}
		if (_preview.Visible)
			ClampPreviewPosition();
	}

	private void OnEntryAdded(CombatLogEntry entry)
	{
		UpdateHandleCount();
		if (!_isOpen && !entry.IsPlayerSide && entry.Kind != CombatLogEntryKind.TurnStart)
			PulseHandle();
		// 重建排队中：本次条目会随整表重建一并出现，避免与清空/语言切换竞态。
		if (_clearPending || _rebuildPending || !IsNodeReady())
			return;
		_entries.AddChild(CreateEntryRow(entry));
		PruneOverflowRows();
		CallDeferred(nameof(ScrollToLatestDeferred));
	}

	/// <summary>与 <see cref="CombatLogService"/> 的环形上限保持一致：UI 行数不得超过容量。</summary>
	private void PruneOverflowRows()
	{
		int capacity = _service?.Capacity ?? CombatLogService.DefaultCapacity;
		int overflow = _entries.GetChildCount() - capacity;
		for (int i = 0; i < overflow; i++)
		{
			Node oldest = _entries.GetChild(0);
			_entries.RemoveChild(oldest);
			oldest.QueueFree();
		}
	}

	private void OnCleared()
	{
		_clearPending = true;
		HidePreview();
		UpdateHandleCount();
		RebuildEntries();
	}

	/// <summary>
	/// 标记整表重建。实际改树发生在 deferred 边界，绝不在同一调用栈帧内
	/// QueueFree 旧行后立刻 AddChild 新行（语言切换 / 连续刷新时栈序更安全）。
	/// </summary>
	private void RebuildEntries()
	{
		if (_service == null || !IsNodeReady())
			return;
		_rebuildPending = true;
		SceneLifecycleGuard.CallDeferredSafe(this, nameof(RebuildEntriesDeferred));
	}

	private void RebuildEntriesDeferred()
	{
		_clearPending = false;
		_rebuildPending = false;
		if (_service == null || !IsInsideTree() || IsQueuedForDeletion())
			return;
		foreach (Node child in _entries.GetChildren())
		{
			// 先脱离容器再销毁，避免同一帧内新旧行同时参与布局。
			_entries.RemoveChild(child);
			child.QueueFree();
		}
		foreach (CombatLogEntry entry in _service.Entries)
			_entries.AddChild(CreateEntryRow(entry));
		UpdateHandleCount();
		CallDeferred(nameof(ScrollToLatestDeferred));
	}

	private HBoxContainer CreateEntryRow(CombatLogEntry entry)
	{
		if (entry.Kind == CombatLogEntryKind.TurnStart)
			return CreateTurnRow(entry);

		var row = new HBoxContainer
		{
			MouseFilter = MouseFilterEnum.Ignore,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		row.AddThemeConstantOverride("separation", 5);
		Color sideColor = entry.IsPlayerSide ? PlayerColor : EnemyColor;
		row.AddChild(new ColorRect
		{
			Color = sideColor,
			CustomMinimumSize = new Vector2(4, 0),
			SizeFlagsVertical = SizeFlags.ExpandFill,
			MouseFilter = MouseFilterEnum.Ignore,
		});
		var flow = new HFlowContainer
		{
			MouseFilter = MouseFilterEnum.Ignore,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		flow.AddThemeConstantOverride("h_separation", 4);
		flow.AddThemeConstantOverride("v_separation", 3);
		flow.AddChild(CreateNameChip(entry.ActorName, entry.SourceCard, sideColor));
		if (!string.IsNullOrEmpty(entry.TargetName))
		{
			flow.AddChild(CreatePlainLabel("->", new Color(0.62f, 0.66f, 0.74f), Fs(ChipBaseFontSize)));
			foreach (CombatLogTag tag in entry.Tags)
				flow.AddChild(CreateTagBadge(tag));
			flow.AddChild(CreateNameChip(entry.TargetName!, entry.TargetCard, sideColor));
		}
		else
		{
			foreach (CombatLogTag tag in entry.Tags)
				flow.AddChild(CreateTagBadge(tag));
		}
		row.AddChild(flow);
		return row;
	}

	private static HBoxContainer CreateTurnRow(CombatLogEntry entry)
	{
		var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
		row.AddThemeConstantOverride("separation", 6);
		Color side = entry.IsPlayerSide ? PlayerColor : EnemyColor;
		row.AddChild(new ColorRect { Color = side, CustomMinimumSize = new Vector2(7, 7), SizeFlagsVertical = SizeFlags.ShrinkCenter });
		string key = entry.IsPlayerSide ? "ui.combat.log_turn_player" : "ui.combat.log_turn_enemy";
		string fallback = entry.IsPlayerSide ? "T{turn} · 我方回合" : "T{turn} · 敌方回合";
		row.AddChild(CreatePlainLabel(Loc.T(key, fallback).Replace("{turn}", entry.TurnNumber.ToString()), side, Fs(TurnBaseFontSize)));
		row.AddChild(new HSeparator { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkCenter });
		return row;
	}

	private Control CreateNameChip(string name, Card.Card? card, Color side)
	{
		if (card == null)
			return CreatePlainLabel(name, new Color(0.90f, 0.91f, 0.95f), Fs(ChipBaseFontSize));
		int chipFontSize = Fs(ChipBaseFontSize);
		var chip = new Button
		{
			Text = name,
			// 高度按字号自适应，放大字体后不会被 24px 硬高度裁切。
			CustomMinimumSize = new Vector2(0, chipFontSize + 12),
			SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
		};
		chip.AddThemeFontSizeOverride("font_size", chipFontSize);
		chip.AddThemeStyleboxOverride("normal", CreateStyle(new Color(side, 0.15f), new Color(side, 0.60f), 1, 3));
		chip.AddThemeStyleboxOverride("hover", CreateStyle(new Color(side, 0.26f), side, 1, 3));
		chip.AddThemeStyleboxOverride("pressed", CreateStyle(new Color(side, 0.32f), side, 1, 3));
		chip.MouseEntered += () => ShowPreview(card, chip.GetGlobalRect());
		chip.MouseExited += HidePreview;
		chip.Pressed += () =>
		{
			if (MobileInputRouter.IsMobile)
			{
				if (_preview.Visible)
					HidePreview();
				else
					ShowPreview(card, chip.GetGlobalRect());
			}
		};
		return chip;
	}

	private static Label CreatePlainLabel(string text, Color color, int fontSize)
	{
		var label = new Label { Text = text, MouseFilter = MouseFilterEnum.Ignore };
		label.AddThemeColorOverride("font_color", color);
		label.AddThemeFontSizeOverride("font_size", fontSize);
		return label;
	}

	private Control CreateTagBadge(CombatLogTag tag)
	{
		// 真实被弃掉的牌渲染为可悬停/点按的卡面芯片；纯数字弃牌仍走普通徽章。
		if (tag.Kind == CombatLogTagKind.Discard && tag.Card != null)
			return CreateDiscardCardChip(tag);

		(string key, string fallback, Color color, string emoji) = tag.Kind switch
		{
			CombatLogTagKind.Damage => ("ui.combat.log_tag_damage", "伤 {value}", new Color("#A83E4C"), "⚔ "),
			CombatLogTagKind.CounterDamage => ("ui.combat.log_tag_counter_damage", "反击 {value}", new Color("#873642"), "⚔ "),
			CombatLogTagKind.Heal => ("ui.combat.log_tag_heal", "疗 +{value}", new Color("#317657"), "+"),
			CombatLogTagKind.Armor => ("ui.combat.log_tag_armor", "甲 +{value}", new Color("#326D91"), "🛡 "),
			CombatLogTagKind.Kill => ("ui.combat.log_tag_kill", "击毁", new Color("#752C3A"), "☠ "),
			CombatLogTagKind.Canceled => ("ui.combat.log_tag_canceled", "已取消", new Color("#63344D"), "X "),
			CombatLogTagKind.Intel => ("ui.combat.log_tag_intel", "情报 {value}", new Color("#326D91"), "I "),
			CombatLogTagKind.Draw => ("ui.combat.log_tag_draw", "抽 {value}", new Color("#326D91"), "+"),
			CombatLogTagKind.Discard => ("ui.combat.log_tag_discard", "弃 {value}", new Color("#63344D"), "-"),
			CombatLogTagKind.Summon => ("ui.combat.log_tag_summon", "召唤 {value}", new Color("#317657"), "+"),
			CombatLogTagKind.Move => ("ui.combat.log_tag_move", "推进", new Color("#326D91"), ">"),
			CombatLogTagKind.Countermeasure => ("ui.combat.log_tag_countermeasure", "反制", new Color("#326D91"), "⚡ "),
			_ => ("ui.combat.log_tag_status", "状态", new Color("#655080"), "*"),
		};
		string text = Loc.T(key, fallback).Replace("{value}", tag.Value.ToString());
		if (!string.IsNullOrEmpty(tag.Text))
			text = $"{text} {tag.Text}";
		text = UiSymbolMode.Prefix(text, emoji);
		var badge = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore };
		badge.AddThemeStyleboxOverride("panel", CreateStyle(color, color.Lightened(0.18f), 1, 3));
		badge.AddChild(CreatePlainLabel(text, Colors.White, Fs(BadgeBaseFontSize)));
		return badge;
	}

	/// <summary>
	/// 弃牌卡面芯片：弃牌紫底 + 被弃牌所属阵营描边，
	/// 悬停（移动端点按）像主体/目标芯片一样弹出真实 <see cref="CardUI"/> 历史快照。
	/// </summary>
	private Button CreateDiscardCardChip(CombatLogTag tag)
	{
		Card.Card card = tag.Card!;
		Color side = tag.IsPlayerCard switch
		{
			true => PlayerColor,
			false => EnemyColor,
			_ => new Color("#C08BE8"),
		};
		Color baseColor = new("#63344D");
		int fontSize = Fs(BadgeBaseFontSize);
		var chip = new Button
		{
			Text = UiSymbolMode.Prefix(
				Loc.T("ui.combat.log_tag_discard_card", "弃: {card}")
					.Replace("{card}", card.GetLocalizedName()),
				"- "),
			CustomMinimumSize = new Vector2(0, fontSize + 12),
			SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
		};
		chip.AddThemeFontSizeOverride("font_size", fontSize);
		chip.AddThemeStyleboxOverride("normal", CreateStyle(baseColor, new Color(side, 0.70f), 1, 3));
		chip.AddThemeStyleboxOverride("hover", CreateStyle(new Color(side, 0.30f), side, 1, 3));
		chip.AddThemeStyleboxOverride("pressed", CreateStyle(new Color(side, 0.40f), side, 1, 3));
		chip.MouseEntered += () => ShowPreview(card, chip.GetGlobalRect());
		chip.MouseExited += HidePreview;
		chip.Pressed += () =>
		{
			if (!MobileInputRouter.IsMobile)
				return;
			if (_preview.Visible)
				HidePreview();
			else
				ShowPreview(card, chip.GetGlobalRect());
		};
		return chip;
	}

	private void ShowPreview(Card.Card card, Rect2 sourceRect)
	{
		if (!IsNodeReady())
			return;
		_previewSourceRect = sourceRect;
		_preview.SetCard(card);
		_preview.Visible = true;
		ClampPreviewPosition();
	}

	private void ClampPreviewPosition()
	{
		if (!_preview.Visible)
			return;
		Vector2 viewport = GetViewportRect().Size;
		Vector2 cardSize = _preview.Size;
		if (cardSize.X <= 0f || cardSize.Y <= 0f)
			cardSize = new Vector2(CardUI.DESIGN_WIDTH, CardUI.DESIGN_HEIGHT);
		const float margin = 12f;
		// 紧贴把手/抽屉右缘 + 12px，避免预览压住抽屉侧边的战报把手按钮。
		Rect2 panelRect = _panel.GetGlobalRect();
		float rightEdge = _handle != null && _handle.Visible
			? Mathf.Max(panelRect.End.X, _handle.GetGlobalRect().End.X)
			: panelRect.End.X;
		float x = Mathf.Clamp(rightEdge + margin, margin,
			Mathf.Max(margin, viewport.X - cardSize.X - margin));
		float y = Mathf.Clamp(_previewSourceRect.GetCenter().Y - cardSize.Y * 0.5f, margin,
			Mathf.Max(margin, viewport.Y - cardSize.Y - margin));
		_preview.GlobalPosition = new Vector2(x, y);
	}

	private void HidePreview()
	{
		if (_preview != null)
			_preview.Visible = false;
		_previewSourceRect = default;
	}

	private void PulseHandle()
	{
		_pulseTween?.Kill();
		_handle.Modulate = Colors.White;
		_pulseTween = CreateTween();
		_pulseTween.TweenProperty(_handle, "modulate", new Color(0.65f, 0.94f, 1f), 0.18);
		_pulseTween.TweenProperty(_handle, "modulate", Colors.White, 0.32);
	}

	private void UpdateHandleCount()
	{
		if (_handleLabel != null)
			_handleLabel.Text = $"{Loc.T("ui.combat.log_handle", "战报")}\n{_service?.Entries.Count ?? 0}";
	}

	private void OnScrollValueChanged(double value)
	{
		if (_programmaticScroll)
			return;
		VScrollBar bar = _scroll.GetVScrollBar();
		_manualHistory = bar.MaxValue - bar.Page - value > 12.0;
	}

	private void ScrollToLatestDeferred()
	{
		if (!IsInsideTree() || IsQueuedForDeletion() || !_isOpen || _manualHistory)
		{
			_programmaticScroll = false;
			return;
		}
		_programmaticScroll = true;
		VScrollBar bar = _scroll.GetVScrollBar();
		bar.Value = bar.MaxValue;
		_programmaticScroll = false;
	}

	private void OnLanguageChanged(string language)
	{
		if (!IsInsideTree() || IsQueuedForDeletion())
			return;
		RebuildEntries();
	}

	private static float GetUiScale() => UIScaler.Instance?.CurrentScale ?? 1f;

	/// <summary>把手宽度随界面字体倍率适度加宽，保证「战报/计数」换行不被裁切。</summary>
	private static float ResolveHandleWidth(float scale)
	{
		float fontScale = UIScaler.Instance?.FontScale ?? UIScaler.DefaultFontScale;
		float extra = Mathf.Clamp(fontScale / UIScaler.DefaultFontScale, 1f, 1.2f);
		return HandleWidth * scale * extra;
	}

	private static int Fs(int baseSize) => UIScaler.ApplyFontScale(baseSize);

	/// <summary>抽屉内不随条目重建的字号（标题、关闭按钮、把手）。</summary>
	private void ApplyStaticFontScale()
	{
		if (_titleLabel == null || !GodotObject.IsInstanceValid(_titleLabel))
			return;
		_titleLabel.AddThemeFontSizeOverride("font_size", Fs(TitleBaseFontSize));
		_closeButton.AddThemeFontSizeOverride("font_size", Fs(CloseButtonBaseFontSize));
		_closeButton.CustomMinimumSize = new Vector2(34f, Fs(CloseButtonBaseFontSize) + 16f);
		_handleLabel.AddThemeFontSizeOverride("font_size", Fs(HandleBaseFontSize));
	}

	/// <summary>界面字体倍率变化：重建条目并重新测量抽屉，保证放大字号不被裁切。</summary>
	private void OnFontScaleChanged()
	{
		if (!IsInsideTree() || IsQueuedForDeletion())
			return;
		ApplyStaticFontScale();
		RebuildEntries();
		UpdateLayout(_lastViewportSize);
	}

	private static StyleBoxFlat CreateStyle(Color background, Color border, int borderWidth, int radius)
	{
		return new StyleBoxFlat
		{
			BgColor = background,
			BorderColor = border,
			BorderWidthLeft = borderWidth,
			BorderWidthRight = borderWidth,
			BorderWidthTop = borderWidth,
			BorderWidthBottom = borderWidth,
			CornerRadiusTopLeft = radius,
			CornerRadiusTopRight = radius,
			CornerRadiusBottomLeft = radius,
			CornerRadiusBottomRight = radius,
			ContentMarginLeft = 5,
			ContentMarginRight = 5,
			ContentMarginTop = 3,
			ContentMarginBottom = 3,
		};
	}
}
