using System;
using System.Collections.Generic;
using Godot;
using OdysseyCards.Core;
using OdysseyCards.Infrastructure;
using OdysseyCards.Roguelike;

namespace OdysseyCards.UI;

/// <summary>
/// 叙事事件 UI——全屏覆盖层，展示事件叙述文本和选择项。
/// 由 MapUI.ShowEventRoom 创建并添加到场景树。
/// 选择完成后触发 OnEventComplete，由 MapUI 推进冒险。
/// </summary>
public partial class EventUI : Control
{
	// ──── 构造参数 ────

	private readonly RoomDefinition _room;
	private readonly EventData _eventData;

	/// <summary>事件完成后触发（MapUI 订阅以调用 CompleteRoomAndAdvance）。</summary>
	public event Action? OnEventComplete;

	// ──── UI 引用 ────

	private Control _dialog = null!;
	private Label _storyLabel = null!;
	private Label _titleLabel = null!;
	private VBoxContainer _contentArea = null!;
	private HBoxContainer _buttonArea = null!;
	private readonly List<Button> _choiceButtons = new();
	private Label _resultLabel = null!;
	private Button _continueButton = null!;
	private bool _choiceSelected;

	// ──── 构造 ────

	/// <summary>
	/// 创建事件 UI。
	/// </summary>
	/// <param name="room">当前房间定义（含图标映射）。</param>
	/// <param name="eventData">随机选中的事件数据。</param>
	public EventUI(RoomDefinition room, EventData eventData)
	{
		_room = room;
		_eventData = eventData;
	}

	public override void _Ready()
	{
		if (SceneLifecycleGuard.ShouldSkip(this))
			return;

		// 全屏铺满父控件（MapUI）
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		MouseFilter = MouseFilterEnum.Ignore;

		BuildUI();
	}

	// ──── UI 构建 ────

	private void BuildUI()
	{
		string icon = MapUIIcons.GetRoomIcon(_room.Type);
		string title = $"{icon} {_eventData.Title}";

		var (dialog, content, buttonRow) = MobileDialogHost.CreateDialog(
			parent: this,
			title: title,
			width: 520);

		_dialog = dialog;
		_contentArea = content;
		_buttonArea = buttonRow;

		// 缓存对话框标题标签引用（用于涩情开关实时刷新）
		// 从 buttonRow 的父节点（PanelVBox）获取第一个 Label（标题）
		var titleBox = _buttonArea.GetParent();
		_titleLabel = titleBox != null ? titleBox.GetChild<Label>(0) : new Label();

		// ── 叙事文本 ──
		var storyLabel = new Label
		{
			Text = _eventData.Story,
			HorizontalAlignment = HorizontalAlignment.Left,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
		};
		storyLabel.AddThemeFontSizeOverride("font_size", 16);
		storyLabel.AddThemeColorOverride("font_color", new Color(0.8f, 0.8f, 0.9f, 1));
		_contentArea.AddChild(storyLabel);
		_storyLabel = storyLabel;

		// 故事与按钮之间的间距
		var spacer = new Control { CustomMinimumSize = new Vector2(0, 16) };
		_contentArea.AddChild(spacer);

		// ── 结果标签（初始隐藏） ──
		_resultLabel = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			Visible = false,
		};
		_resultLabel.AddThemeFontSizeOverride("font_size", 18);
		_resultLabel.AddThemeColorOverride("font_color", new Color(1, 0.85f, 0.3f, 1));
		_contentArea.AddChild(_resultLabel);

		// ── 选择按钮 ──
		foreach (var choice in _eventData.Choices)
		{
			var btn = new Button
			{
				Text = choice.Text,
				CustomMinimumSize = new Vector2(0, 52),
				SizeFlagsHorizontal = SizeFlags.ExpandFill,
			};
			btn.AddThemeFontSizeOverride("font_size", 16);

			btn.Pressed += () => OnChoiceSelected(choice, btn);
			_choiceButtons.Add(btn);
			_buttonArea.AddChild(btn);
		}

		// ── 继续按钮（初始隐藏，选择后显示） ──
		// 放在 panelVBox 中、ButtonRow 之后——确保在 ScrollContainer 外部，不受滚动布局限制
		_continueButton = MobileDialogHost.CreateDialogButton(
			Localization.Localization.T("ui.map.continue_button", "继续"));
		_continueButton.Visible = false;
		_continueButton.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		_continueButton.Pressed += () =>
		{
			MobileDialogHost.CloseDialog(_dialog, this);
			OnEventComplete?.Invoke();
		};
		// 将继续按钮加到 buttonRow 的父容器（即 panelVBox），位于 buttonRow 之后
		var panelVBox = _buttonArea.GetParent();
		panelVBox.AddChild(_continueButton);
	}

	// ──── 交互 ────

	private void OnChoiceSelected(EventChoice choice, Button clickedButton)
	{
		if (_choiceSelected)
			return;

		// 非停留事件：锁定选择（防止二次点击）
		// 停留事件（StaysInEvent）：允许再次选择，不锁定
		if (!choice.StaysInEvent)
			_choiceSelected = true;

		// 禁用所有选择按钮（执行期间防抖）
		foreach (var btn in _choiceButtons)
			btn.Disabled = true;

		// 执行选择效果
		var gm = GameManager.Instance;
		if (gm != null && choice.Execute != null)
		{
			choice.Execute(gm);
		}

		// 刷新按钮文本（Execute 可能修改了 choice.Text）
		for (int i = 0; i < _choiceButtons.Count && i < _eventData.Choices.Length; i++)
		{
			_choiceButtons[i].Text = _eventData.Choices[i].Text;
		}

		if (choice.StaysInEvent)
		{
			// 停留事件：显示结果→重新激活按钮（不显示「继续」按钮）
			_resultLabel.Text = choice.ResultText;
			_resultLabel.Visible = true;
			foreach (var btn in _choiceButtons)
				btn.Disabled = false;
		}
		else
		{
			// 退出事件：高亮选中按钮→显示结果→显示「继续」按钮
			clickedButton.Modulate = new Color(0.3f, 0.3f, 0.3f, 1);
			_resultLabel.Text = choice.ResultText;
			_resultLabel.Visible = true;
			_continueButton.Visible = true;
		}
	}

	// ──── 涩情文案实时刷新 ────

	public override void _EnterTree()
	{
		base._EnterTree();
		if (UIScaler.Instance != null)
			UIScaler.Instance.OnEcchiTextChanged += RefreshEcchiText;
	}

	public override void _ExitTree()
	{
		base._ExitTree();
		if (UIScaler.Instance != null)
			UIScaler.Instance.OnEcchiTextChanged -= RefreshEcchiText;
	}

	/// <summary>
	/// 涩情开关切换时重新从事件池读取文本并刷新 UI。
	/// 仅对 ayame_mirror 事件生效（其他事件无涩情版文案）。
	/// </summary>
	private void RefreshEcchiText()
	{
		if (!_eventData.Id.StartsWith("ayame_mirror"))
			return;
		if (!IsInsideTree())
			return;
		// BuildUI 未完成（如 dialog 尚未构造完毕）时跳过
		if (_titleLabel == null || _storyLabel == null)
			return;

		var gm = GameManager.Instance;
		if (gm == null)
			return;

		var fresh = EventPool.FindEvent(_eventData.Id, gm.SelectedHeroId);
		if (fresh == null)
			return;

		// 更新标题
		string icon = MapUIIcons.GetRoomIcon(_room.Type);
		_titleLabel.Text = $"{icon} {fresh.Title}";

		// 更新叙事文本
		_storyLabel.Text = fresh.Story;

		// 更新选择按钮文本（尚未选择时）
		if (!_choiceSelected && fresh.Choices.Length == _choiceButtons.Count)
		{
			for (int i = 0; i < _choiceButtons.Count; i++)
				_choiceButtons[i].Text = fresh.Choices[i].Text;
		}
	}
}

/// <summary>
/// 提供 MapUI 房间图标映射（供 EventUI 复用，避免代码重复）。
/// </summary>
internal static class MapUIIcons
{
	/// <summary>获取房间类型对应的图标字符串。</summary>
	public static string GetRoomIcon(RoomType type) => type switch
	{
		RoomType.Monster => Localization.Localization.T("ui.map.room_battle", "[战斗]"),
		RoomType.Elite => Localization.Localization.T("ui.map.room_elite", "[精英]"),
		RoomType.Boss => Localization.Localization.T("ui.map.room_boss", "[BOSS]"),
		RoomType.Treasure => Localization.Localization.T("ui.map.room_reward", "[奖励]"),
		RoomType.Shop => Localization.Localization.T("ui.map.room_shop", "[商店]"),
		RoomType.RestSite => Localization.Localization.T("ui.map.room_rest", "[休息]"),
		RoomType.Event => Localization.Localization.T("ui.map.room_event", "[事件]"),
		_ => "[?]",
	};
}
