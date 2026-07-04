using Godot;
using OdysseyCards.Core;
using OdysseyCards.Infrastructure;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Loc = OdysseyCards.Localization.Localization;

namespace OdysseyCards.UI;

/// <summary>
/// 抉择控件——参考炉石 Choose One。
/// 弹出全屏覆盖层展示 2 个文字选项，玩家点选其一（或右键取消，若允许跳过）。
/// 异步 API：调用方 await <see cref="ShowAsync"/> 获取选中的选项索引（0-based）；跳过返回 -1。
/// 一次性屏幕：选择完成或跳过后自行 QueueFree。
///
/// 与 <see cref="DiscoverUI"/> 区别：DiscoverUI 选择卡牌数据（CardUI 渲染）；
/// 本控件选择抽象文字选项（Button 渲染），适合「抉择：A 还是 B」语义。
/// </summary>
public partial class ChooseOneUI : Control
{
	private ColorRect _background = null!;
	private Label _titleLabel = null!;
	private VBoxContainer _optionsContainer = null!;
	private Button? _skipButton;

	private ulong _openedTicks;
	private const ulong ClickProtectionMs = 350;

	private readonly List<Button> _optionButtons = new();
	private TaskCompletionSource<int>? _tcs;

	private Action? _skipAction;
	private Action?[] _selectActions = Array.Empty<Action>();
	private Action? _acceptAction;
	private Action? _leftAction;
	private Action? _rightAction;
	private int _focusedIndex = -1;

	/// <summary>本地化 key 前缀——调用方传入的 optionKeys 将被 Lookup 成显示文本。</summary>
	private IReadOnlyList<string> _optionLabels = Array.Empty<string>();
	private IReadOnlyList<string> _optionDescriptions = Array.Empty<string>();
	private string _titleKey = "";
	private string _titleFallback = "";
	private bool _canSkip;

	/// <summary>
	/// 异步展示抉择界面。
	/// </summary>
	/// <param name="titleKey">标题本地化 key（找不到时回退 titleFallback）</param>
	/// <param name="titleFallback">标题默认文本</param>
	/// <param name="optionLabels">各选项显示文本（中文文案，直接显示；如需本地化由调用方预先 Lookup）</param>
	/// <param name="optionDescriptions">各选项副标题/描述（可空字符串）</param>
	/// <param name="canSkip">是否允许跳过（右键/Skip 按钮）</param>
	/// <returns>选中的选项索引（0-based）；玩家跳过返回 -1</returns>
	public async Task<int> ShowAsync(
		string titleKey,
		string titleFallback,
		IReadOnlyList<string> optionLabels,
		IReadOnlyList<string>? optionDescriptions = null,
		bool canSkip = false)
	{
		if (optionLabels.Count == 0)
			return -1;

		_titleKey = titleKey;
		_titleFallback = titleFallback;
		_optionLabels = optionLabels;
		_optionDescriptions = optionDescriptions ?? Array.Empty<string>();
		_canSkip = canSkip;

		_tcs = new TaskCompletionSource<int>();
		_openedTicks = Time.GetTicksMsec();

		BuildOverlay();
		Show();

		GameManager.Instance.LanguageChanged -= OnLanguageChanged;
		GameManager.Instance.LanguageChanged += OnLanguageChanged;

		return await _tcs.Task;
	}

	private void BuildOverlay()
	{
		float s = UIScaler.Instance?.GetScaleFactor() ?? 1f;

		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		MouseFilter = MouseFilterEnum.Stop;
		ZIndex = 220;

		_background = new ColorRect
		{
			Color = new Color(0, 0, 0, 0.8f),
			MouseFilter = MouseFilterEnum.Ignore,
		};
		_background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(_background);

		var root = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
		root.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(root);

		var center = new VBoxContainer
		{
			Alignment = BoxContainer.AlignmentMode.Center,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		root.AddChild(center);

		_titleLabel = new Label
		{
			Text = Loc.T(_titleKey, _titleFallback),
			HorizontalAlignment = HorizontalAlignment.Center,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		_titleLabel.AddThemeColorOverride("font_color", new Color(1, 1, 1, 0.9f));
		_titleLabel.AddThemeFontSizeOverride("font_size", Mathf.RoundToInt(26 * s));
		center.AddChild(_titleLabel);

		var spacer1 = new Control
		{
			CustomMinimumSize = new Vector2(0, 24 * s),
			MouseFilter = MouseFilterEnum.Ignore,
		};
		center.AddChild(spacer1);

		_optionsContainer = new VBoxContainer
		{
			Alignment = BoxContainer.AlignmentMode.Center,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		_optionsContainer.AddThemeConstantOverride("separation", Mathf.RoundToInt(16 * s));
		center.AddChild(_optionsContainer);

		BuildOptionButtons(s);

		var spacer2 = new Control
		{
			CustomMinimumSize = new Vector2(0, 20 * s),
			MouseFilter = MouseFilterEnum.Ignore,
		};
		center.AddChild(spacer2);

		if (_canSkip)
		{
			_skipButton = new Button
			{
				Text = Loc.T("ui.choose_one.skip", "跳过"),
				CustomMinimumSize = new Vector2(120 * s, 38 * s),
			};
			_skipButton.AddThemeFontSizeOverride("font_size", Mathf.RoundToInt(16 * s));
			_skipButton.Pressed += OnSkipPressed;
			center.AddChild(_skipButton);
		}

		RegisterHotkeyBindings();

		// 入场淡入
		Modulate = new Color(1, 1, 1, 0);
		var tween = CreateTween();
		tween.TweenProperty(this, "modulate", Colors.White, 0.2);
	}

	private void BuildOptionButtons(float s)
	{
		_optionButtons.Clear();
		_selectActions = new Action[_optionLabels.Count];

		for (int i = 0; i < _optionLabels.Count; i++)
		{
			int capturedIndex = i;
			string desc = i < _optionDescriptions.Count ? _optionDescriptions[i] : "";
			bool hasDesc = !string.IsNullOrWhiteSpace(desc);

			var btn = new Button
			{
				Text = _optionLabels[i],
				CustomMinimumSize = new Vector2(320 * s, hasDesc ? 70 * s : 48 * s),
				Alignment = HorizontalAlignment.Center,
			};
			btn.AddThemeFontSizeOverride("font_size", Mathf.RoundToInt(18 * s));
			btn.Pressed += () => OnOptionSelected(capturedIndex);
			_optionsContainer.AddChild(btn);
			_optionButtons.Add(btn);

			if (hasDesc)
			{
				var descLabel = new Label
				{
					Text = desc,
					HorizontalAlignment = HorizontalAlignment.Center,
					MouseFilter = MouseFilterEnum.Ignore,
				};
				descLabel.AddThemeColorOverride("font_color", new Color(0.85f, 0.85f, 0.85f, 0.85f));
				descLabel.AddThemeFontSizeOverride("font_size", Mathf.RoundToInt(13 * s));
				_optionsContainer.AddChild(descLabel);
			}

			_selectActions[i] = () => OnOptionSelected(capturedIndex);
		}
	}

	private void OnOptionSelected(int index)
	{
		if (Time.GetTicksMsec() - _openedTicks < ClickProtectionMs)
			return;
		if (_tcs == null)
			return;

		GD.Print($"[ChooseOneUI] 玩家选择了第 {index} 项");
		var tcs = _tcs;
		_tcs = null;
		Cleanup();
		tcs.TrySetResult(index);
	}

	private void OnSkipPressed()
	{
		if (_tcs == null)
			return;
		GD.Print("[ChooseOneUI] 玩家跳过");
		var tcs = _tcs;
		_tcs = null;
		Cleanup();
		tcs.TrySetResult(-1);
	}

	private void Cleanup()
	{
		UnregisterHotkeyBindings();
		GameManager.Instance.LanguageChanged -= OnLanguageChanged;
		QueueFree();
	}

	private void RegisterHotkeyBindings()
	{
		var hm = HotkeyManager.Instance;
		if (hm == null)
			return;

		for (int i = 0; i < _selectActions.Length; i++)
		{
			if (i < OdysseyInput.SelectCardActions.Length)
				hm.PushPressedBinding(OdysseyInput.SelectCardActions[i], _selectActions[i]);
		}
		_acceptAction = AcceptFocused;
		hm.PushPressedBinding(OdysseyInput.Accept, _acceptAction);
		_skipAction = OnSkipPressed;
		hm.PushPressedBinding(OdysseyInput.Skip, _skipAction);
		hm.PushPressedBinding(OdysseyInput.Cancel, _skipAction);
		_leftAction = () => CycleFocus(-1);
		_rightAction = () => CycleFocus(1);
		hm.PushPressedBinding(OdysseyInput.Left, _leftAction);
		hm.PushPressedBinding(OdysseyInput.Right, _rightAction);
	}

	private void UnregisterHotkeyBindings()
	{
		var hm = HotkeyManager.Instance;
		if (hm == null)
			return;

		for (int i = 0; i < _selectActions.Length; i++)
		{
			if (_selectActions[i] != null && i < OdysseyInput.SelectCardActions.Length)
				hm.RemovePressedBinding(OdysseyInput.SelectCardActions[i], _selectActions[i]);
		}
		_selectActions = Array.Empty<Action>();
		if (_acceptAction != null) { hm.RemovePressedBinding(OdysseyInput.Accept, _acceptAction); _acceptAction = null; }
		if (_skipAction != null)
		{
			hm.RemovePressedBinding(OdysseyInput.Skip, _skipAction);
			hm.RemovePressedBinding(OdysseyInput.Cancel, _skipAction);
			_skipAction = null;
		}
		if (_leftAction != null) { hm.RemovePressedBinding(OdysseyInput.Left, _leftAction); _leftAction = null; }
		if (_rightAction != null) { hm.RemovePressedBinding(OdysseyInput.Right, _rightAction); _rightAction = null; }
	}

	private void CycleFocus(int direction)
	{
		if (_optionButtons.Count == 0)
			return;
		if (_focusedIndex < 0 || _focusedIndex >= _optionButtons.Count)
			_focusedIndex = direction > 0 ? 0 : _optionButtons.Count - 1;
		else
		{
			_focusedIndex += direction;
			if (_focusedIndex >= _optionButtons.Count) _focusedIndex = 0;
			else if (_focusedIndex < 0) _focusedIndex = _optionButtons.Count - 1;
		}
		ApplyFocusVisual();
	}

	private void AcceptFocused()
	{
		if (_focusedIndex >= 0 && _focusedIndex < _optionButtons.Count)
			OnOptionSelected(_focusedIndex);
		else if (_optionButtons.Count > 0)
			OnOptionSelected(0);
	}

	private void ApplyFocusVisual()
	{
		foreach (var btn in _optionButtons)
			btn.Modulate = Colors.White;
		if (_focusedIndex >= 0 && _focusedIndex < _optionButtons.Count)
			_optionButtons[_focusedIndex].Modulate = new Color(1, 0.85f, 0.3f);
	}

	private void OnLanguageChanged(string lang)
	{
		_titleLabel.Text = Loc.T(_titleKey, _titleFallback);
		if (_skipButton != null)
			_skipButton.Text = Loc.T("ui.choose_one.skip", "跳过");
		for (int i = 0; i < _optionButtons.Count && i < _optionLabels.Count; i++)
			_optionButtons[i].Text = _optionLabels[i];
	}

	public override void _GuiInput(InputEvent @event)
	{
		if (MobileInputHelper.IsMobile)
			return;
		if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Right)
		{
			OnSkipPressed();
			AcceptEvent();
		}
	}

	public override void _ExitTree()
	{
		GameManager.Instance.LanguageChanged -= OnLanguageChanged;
		UnregisterHotkeyBindings();
	}
}
