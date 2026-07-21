using Godot;
using OdysseyCards.Core;
using OdysseyCards.Infrastructure;
using System;
using System.Collections.Generic;
using Loc = OdysseyCards.Localization.Localization;

namespace OdysseyCards.UI;

/// <summary>
/// 卡牌选择覆盖层抽象基类。
/// 提供全屏遮罩布局、入场动画、350ms 防误触、键盘导航、跳过/确认等公共行为。
/// 子类只需覆写卡牌项创建和选择处理逻辑。
///
/// 消除 DiscoverUI 和 RewardUI 之间约 75% 的重复代码。
/// 参考 STS2 的 IOverlayScreen 接口模式。
/// </summary>
public abstract partial class CardSelectionScreen : Control
{
	// ===== 子控件（由基类统一创建管理） =====

	private ColorRect _background = null!;
	private Label _titleLabel = null!;
	protected HBoxContainer _cardsContainer = null!;
	private Button? _skipButton;
	private Button? _confirmButton;

	// ===== 状态 =====

	protected bool _isShowing;
	protected ulong _openedTicks;
	protected const ulong ClickProtectionMs = 350;

	/// <summary>已注册的可选卡牌项列表（基类管理键盘焦点和点击事件）。</summary>
	protected readonly List<CardUI> _items = new();

	// ===== 键盘导航 =====

	private Action?[] _selectActions = Array.Empty<Action>();
	private Action? _acceptAction;
	private Action? _skipAction;
	private Action? _leftAction;
	private Action? _rightAction;
	private int _focusedIndex = -1;
	private CardUI? _keyboardFocusedUI;

	// ===== 子类覆写入口 =====

	/// <summary>标题文本（每次语言切换时刷新）。</summary>
	protected abstract string TitleText { get; }

	/// <summary>跳过按钮文本。</summary>
	protected abstract string SkipButtonText { get; }

	/// <summary>确认按钮文本（null = 不显示确认按钮，即单选模式）。</summary>
	protected virtual string? ConfirmButtonText => null;

	/// <summary>弹窗期望宽度（像素）。</summary>
	protected abstract int DialogWidth { get; }

	/// <summary>覆盖层 Z-Index。</summary>
	protected abstract int OverlayZIndex { get; }

	/// <summary>是否始终显示跳过按钮（移动端始终 true）。</summary>
	protected virtual bool ShowSkipButton => true;

	/// <summary>选中态高亮色。</summary>
	protected virtual Color SelectedColor => new(1, 0.85f, 0.3f, 1);

	/// <summary>键盘焦点指示色（蓝色调）。</summary>
	protected virtual Color KeyboardFocusColor => new(0.72f, 0.85f, 1f, 1f);

	/// <summary>
	/// 构建卡牌项——子类在此创建 CardUI 并通过 <see cref="RegisterItem"/> 注册到基类。
	/// 基类在调用此方法前已创建好 _cardsContainer（HBoxContainer）。
	/// </summary>
	protected abstract void BuildCardItems();

	/// <summary>某项被选中（已通过 350ms 保护和拖拽取消）。</summary>
	protected abstract void OnItemSelected(int index);

	/// <summary>跳过按钮/右键被按下。</summary>
	protected virtual void OnSkip() { }

	/// <summary>确认按钮被按下（多选模式）。</summary>
	protected virtual void OnConfirm() { }

	/// <summary>语言切换时刷新所有本地化文本（子类覆写以刷新自定义标签）。</summary>
	protected virtual void RefreshLocalizedTexts()
	{
		_titleLabel.Text = TitleText;
		if (_skipButton != null)
			_skipButton.Text = SkipButtonText;
		if (_confirmButton != null)
			_confirmButton.Text = ConfirmButtonText!;
	}

	/// <summary>子类可设置某项的选中态（基类用此判断焦点视觉效果）。</summary>
	protected virtual bool IsItemSelected(int index) => false;

	// ===== 子类调用：注册卡牌项 =====

	/// <summary>
	/// 注册一个卡牌 UI 到选择列表，并连接到基类的点击/键盘焦点系统。
	/// 应在 <see cref="BuildCardItems"/> 中每创建一个 CardUI 后调用。
	/// </summary>
	protected void RegisterItem(CardUI cardUI)
	{
		int index = _items.Count;
		cardUI.OnCardClicked += OnAnyItemClicked;
		_items.Add(cardUI);
	}

	// ===== 布局构建（由子类在 Show 方法中调用） =====

	/// <summary>
	/// 构建全屏覆盖层和内部布局容器。子类在确认所有状态设置完毕后调用。
	/// </summary>
	protected void BuildOverlay()
	{
		ClearExistingLayout();
		float s = UIScaler.Instance?.GetScaleFactor() ?? 1f;

		// 全屏覆盖层
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		MouseFilter = MouseFilterEnum.Stop;
		ZIndex = OverlayZIndex;

		// 半透明暗色背景
		_background = new ColorRect
		{
			Color = new Color(0, 0, 0, 0.8f),
			MouseFilter = MouseFilterEnum.Ignore,
		};
		_background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(_background);

		// 居中根容器
		var root = new CenterContainer
		{
			MouseFilter = MouseFilterEnum.Ignore,
		};
		root.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(root);

		// 垂直布局
		var center = new VBoxContainer
		{
			Alignment = BoxContainer.AlignmentMode.Center,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		root.AddChild(center);

		// 标题
		_titleLabel = new Label
		{
			Text = TitleText,
			HorizontalAlignment = HorizontalAlignment.Center,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		_titleLabel.AddThemeColorOverride("font_color", new Color(1, 1, 1, 0.9f));
		_titleLabel.AddThemeFontSizeOverride("font_size", Mathf.RoundToInt(28 * s));
		center.AddChild(_titleLabel);

		// 间距
		var spacer1 = new Control
		{
			CustomMinimumSize = new Vector2(0, 24 * s),
			MouseFilter = MouseFilterEnum.Ignore,
		};
		center.AddChild(spacer1);

		// 卡牌行
		_cardsContainer = new HBoxContainer
		{
			Alignment = BoxContainer.AlignmentMode.Center,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		_cardsContainer.AddThemeConstantOverride("separation", Mathf.RoundToInt(20 * s));
		center.AddChild(_cardsContainer);

		// 子类构建卡牌项
		BuildCardItems();

		// 间距
		var spacer2 = new Control
		{
			CustomMinimumSize = new Vector2(0, 20 * s),
			MouseFilter = MouseFilterEnum.Ignore,
		};
		center.AddChild(spacer2);

		// 跳过按钮
		if (ShowSkipButton)
		{
			_skipButton = new Button
			{
				Text = SkipButtonText,
				CustomMinimumSize = new Vector2(120 * s, 38 * s),
			};
			_skipButton.AddThemeFontSizeOverride("font_size", Mathf.RoundToInt(16 * s));
			_skipButton.Pressed += OnSkipPressed;
			center.AddChild(_skipButton);
		}

		// 确认按钮（多选模式）
		if (ConfirmButtonText != null)
		{
			_confirmButton = new Button
			{
				Text = ConfirmButtonText,
				CustomMinimumSize = new Vector2(120 * s, 38 * s),
				Disabled = true,
			};
			_confirmButton.AddThemeFontSizeOverride("font_size", Mathf.RoundToInt(16 * s));
			_confirmButton.Pressed += OnConfirmPressed;
			center.AddChild(_confirmButton);
		}

		// 注册键盘热键
		RegisterHotkeyBindings();
		// 订阅语言变更（先取消再订阅防止 BuildOverlay 被多次调用时重复订阅）
		GameManager.Instance.LanguageChanged -= OnLanguageChanged;
		GameManager.Instance.LanguageChanged += OnLanguageChanged;

		// 星途全局主题 + 按钮悬停微交互（一次性屏幕，构建时统一应用）
		UIThemeFactory.ApplyTo(this);
	}

	private void ClearExistingLayout()
	{
		foreach (Node child in GetChildren())
			child.QueueFree();
		_items.Clear();
		_skipButton = null;
		_confirmButton = null;
	}

	// ===== 入场动画 =====

	/// <summary>
	/// 播放入场动画：背景 0.2s 淡入 + 卡牌项依次弹入。
	/// 支持额外的动画目标（如 RewardUI 的数量标签）。
	/// </summary>
	protected void PlayEntryAnimation(IReadOnlyList<Control>? extraTargets = null)
	{
		var tween = CreateTween();
		tween.SetParallel(true);

		// 背景淡入
		tween.TweenProperty(_background, "color:a", 0.8f, 0.2);

		// 卡牌依次弹入
		float cardDelay = 0.06f;
		for (int i = 0; i < _items.Count; i++)
		{
			var card = _items[i];
			card.Modulate = new Color(1, 1, 1, 0);
			tween.TweenProperty(card, "modulate", Colors.White, 0.45)
				.SetDelay(cardDelay * i)
				.SetEase(Tween.EaseType.Out)
				.SetTrans(Tween.TransitionType.Expo)
				.From(Colors.Black);
			tween.TweenProperty(card, "scale", Vector2.One, 0.45)
				.SetDelay(cardDelay * i)
				.SetEase(Tween.EaseType.Out)
				.SetTrans(Tween.TransitionType.Back)
				.From(new Vector2(0.85f, 0.85f));
		}

		// 额外目标（如数量标签）：仅做 modulate 淡入
		if (extraTargets != null)
		{
			foreach (var target in extraTargets)
			{
				if (target == null)
					continue;
				tween.TweenProperty(target, "modulate", Colors.White, 0.45)
					.From(Colors.Black);
			}
		}
	}

	// ===== 点击处理 =====

	private void OnAnyItemClicked(CardUI cardUI)
	{
		if (!_isShowing)
			return;

		// 终止拖拽状态
		cardUI.CancelDragSilent();

		// 350ms 防误触
		if (Time.GetTicksMsec() - _openedTicks < ClickProtectionMs)
		{
			GD.Print($"[{GetType().Name}] 点击太快，忽略（350ms 保护）");
			return;
		}

		int index = _items.IndexOf(cardUI);
		if (index < 0)
			return;

		GD.Print($"[{GetType().Name}] 玩家选择了第 {index} 项");
		OnItemSelected(index);
	}

	private void OnSkipPressed()
	{
		if (!_isShowing)
			return;
		GD.Print($"[{GetType().Name}] 玩家跳过");
		_isShowing = false;
		OnSkip();
	}

	private void OnConfirmPressed()
	{
		if (!_isShowing)
			return;
		GD.Print($"[{GetType().Name}] 玩家确认");
		OnConfirm();
	}

	// ===== 键盘导航 =====

	/// <summary>子类可调用以启用/禁用确认按钮（多选模式）。</summary>
	protected void SetConfirmEnabled(bool enabled)
	{
		if (_confirmButton != null)
			_confirmButton.Disabled = !enabled;
	}

	private void RegisterHotkeyBindings()
	{
		UnregisterHotkeyBindings();

		var hm = HotkeyManager.Instance;
		if (hm == null)
			return;

		int count = _items.Count;

		// 数字键 1~N
		_selectActions = new Action[count];
		for (int i = 0; i < count; i++)
		{
			int capturedIndex = i;
			_selectActions[i] = () => SelectItemByIndex(capturedIndex);
			hm.PushPressedBinding(OdysseyInput.SelectCardActions[i], _selectActions[i]);
		}

		// Enter
		_acceptAction = AcceptFocusedOrConfirm;
		hm.PushPressedBinding(OdysseyInput.Accept, _acceptAction);

		// Escape/Backspace → 跳过
		_skipAction = OnSkipPressed;
		hm.PushPressedBinding(OdysseyInput.Skip, _skipAction);
		hm.PushPressedBinding(OdysseyInput.Cancel, _skipAction);

		// 方向键
		_leftAction = () => CycleFocus(-1);
		_rightAction = () => CycleFocus(1);
		hm.PushPressedBinding(OdysseyInput.Left, _leftAction);
		hm.PushPressedBinding(OdysseyInput.Right, _rightAction);

		hm.KeyboardFocusChanged += OnKeyboardFocusChanged;
	}

	private void UnregisterHotkeyBindings()
	{
		var hm = HotkeyManager.Instance;
		if (hm == null)
			return;

		hm.KeyboardFocusChanged -= OnKeyboardFocusChanged;

		for (int i = 0; i < _selectActions.Length; i++)
		{
			if (_selectActions[i] != null)
				hm.RemovePressedBinding(OdysseyInput.SelectCardActions[i], _selectActions[i]);
		}
		_selectActions = Array.Empty<Action>();

		if (_acceptAction != null)
		{ hm.RemovePressedBinding(OdysseyInput.Accept, _acceptAction); _acceptAction = null; }
		if (_skipAction != null)
		{
			hm.RemovePressedBinding(OdysseyInput.Skip, _skipAction);
			hm.RemovePressedBinding(OdysseyInput.Cancel, _skipAction);
			_skipAction = null;
		}
		if (_leftAction != null)
		{ hm.RemovePressedBinding(OdysseyInput.Left, _leftAction); _leftAction = null; }
		if (_rightAction != null)
		{ hm.RemovePressedBinding(OdysseyInput.Right, _rightAction); _rightAction = null; }
	}

	private void SelectItemByIndex(int index)
	{
		if (!_isShowing)
			return;
		if (index < 0 || index >= _items.Count)
			return;
		if (Time.GetTicksMsec() - _openedTicks < ClickProtectionMs)
			return;

		_focusedIndex = index;
		OnAnyItemClicked(_items[index]);
	}

	private void CycleFocus(int direction)
	{
		if (!_isShowing || _items.Count == 0)
			return;

		if (_focusedIndex < 0 || _focusedIndex >= _items.Count)
			_focusedIndex = direction > 0 ? 0 : _items.Count - 1;
		else
		{
			_focusedIndex += direction;
			if (_focusedIndex >= _items.Count)
				_focusedIndex = 0;
			else if (_focusedIndex < 0)
				_focusedIndex = _items.Count - 1;
		}

		ApplyKeyboardFocusVisual();
	}

	private void AcceptFocusedOrConfirm()
	{
		if (!_isShowing)
			return;

		if (_confirmButton != null && !_confirmButton.Disabled)
		{
			OnConfirm();
		}
		else
		{
			if (_focusedIndex < 0 || _focusedIndex >= _items.Count)
				_focusedIndex = 0;
			if (_focusedIndex < _items.Count)
				OnAnyItemClicked(_items[_focusedIndex]);
		}
	}

	private void OnKeyboardFocusChanged(bool active)
	{
		if (!active)
		{
			_focusedIndex = -1;
			ClearKeyboardFocusVisual();
		}
	}

	private void ApplyKeyboardFocusVisual()
	{
		bool shouldShow = _focusedIndex >= 0
			&& _focusedIndex < _items.Count
			&& HotkeyManager.Instance.LastKeyboardActivityMsec > 0;

		ClearKeyboardFocusVisual();

		if (!shouldShow)
			return;

		var cardUI = _items[_focusedIndex];
		if (cardUI == null || !GodotObject.IsInstanceValid(cardUI))
			return;

		// 已选中项不覆盖（保留金色高亮）
		if (IsItemSelected(_focusedIndex))
			return;

		cardUI.SelfModulate = KeyboardFocusColor;
		_keyboardFocusedUI = cardUI;
	}

	private void ClearKeyboardFocusVisual()
	{
		if (_keyboardFocusedUI != null && GodotObject.IsInstanceValid(_keyboardFocusedUI))
		{
			int idx = _items.IndexOf(_keyboardFocusedUI);
			if (idx >= 0 && IsItemSelected(idx))
				_keyboardFocusedUI.SelfModulate = SelectedColor;
			else
				_keyboardFocusedUI.SelfModulate = Colors.White;
		}
		_keyboardFocusedUI = null;
	}

	// ===== 语言切换 =====

	private void OnLanguageChanged(string lang)
	{
		RefreshLocalizedTexts();
	}

	// ===== 生命周期 =====

	public override void _ExitTree()
	{
		GameManager.Instance.LanguageChanged -= OnLanguageChanged;
		UnregisterHotkeyBindings();
	}

	/// <summary>
	/// 右键取消（仅桌面端，移动端使用跳过按钮）。
	/// </summary>
	public override void _GuiInput(InputEvent @event)
	{
		if (MobileInputHelper.IsMobile)
			return;

		if (@event is InputEventMouseButton mb
			&& mb.Pressed
			&& mb.ButtonIndex == MouseButton.Right)
		{
			OnSkipPressed();
			AcceptEvent();
		}
	}
}
