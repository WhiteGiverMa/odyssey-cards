#nullable enable

using Godot;
using OdysseyCards.Core;
using Loc = OdysseyCards.Localization.Localization;

namespace OdysseyCards.UI;

/// <summary>
/// 我的表情页面——管理多套可切换的消息/表情预设。
/// </summary>
public partial class EmotePresetPage : Control
{
	private OptionButton _presetSelector = null!;
	private LineEdit _presetNameEdit = null!;
	private VBoxContainer _entryList = null!;
	private Label _hintLabel = null!;
	private bool _refreshing;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Stop;
		SetAnchorsPreset(LayoutPreset.FullRect);
		BuildUi();
		GameManager.Instance.OnEmotePresetsChanged += RefreshAll;
		RefreshAll();
	}

	private void BuildUi()
	{
		var bg = new ColorRect
		{
			Color = new Color(0.02f, 0.02f, 0.06f, 0.95f),
			MouseFilter = MouseFilterEnum.Stop,
		};
		bg.SetAnchorsPreset(LayoutPreset.FullRect);
		AddChild(bg);

		var panel = new PanelContainer
		{
			CustomMinimumSize = new Vector2(760, 540),
		};
		panel.SetAnchorsPreset(LayoutPreset.Center);
		panel.OffsetLeft = -380;
		panel.OffsetTop = -270;
		panel.OffsetRight = 380;
		panel.OffsetBottom = 270;
		AddChild(panel);

		var root = new VBoxContainer();
		root.AddThemeConstantOverride("separation", 12);
		panel.AddChild(root);

		var top = new HBoxContainer();
		top.AddThemeConstantOverride("separation", 10);
		root.AddChild(top);

		var title = new Label
		{
			Text = Loc.T("ui.emote_page.title", "我的表情"),
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		title.AddThemeFontSizeOverride("font_size", 28);
		top.AddChild(title);

		var backButton = new Button { Text = Loc.T("ui.emote_page.back", "返回") };
		backButton.Pressed += ClosePage;
		top.AddChild(backButton);

		var selectorRow = new HBoxContainer();
		selectorRow.AddThemeConstantOverride("separation", 8);
		root.AddChild(selectorRow);

		selectorRow.AddChild(new Label { Text = Loc.T("ui.emote_page.preset", "表情组:"), CustomMinimumSize = new Vector2(70, 0) });
		_presetSelector = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		_presetSelector.ItemSelected += OnPresetSelected;
		selectorRow.AddChild(_presetSelector);

		var newButton = new Button { Text = Loc.T("ui.emote_page.new_preset", "新建") };
		newButton.Pressed += () => GameManager.Instance.CreateEmotePreset(Loc.T("ui.emote_page.default_preset_name", "新表情组"));
		selectorRow.AddChild(newButton);

		var deleteButton = new Button { Text = Loc.T("ui.emote_page.delete_preset", "删除") };
		deleteButton.Pressed += OnDeletePresetPressed;
		selectorRow.AddChild(deleteButton);

		_presetNameEdit = new LineEdit { PlaceholderText = Loc.T("ui.emote_page.name_placeholder", "输入表情组名称") };
		_presetNameEdit.TextSubmitted += text => GameManager.Instance.RenameActiveEmotePreset(text);
		_presetNameEdit.FocusExited += () => GameManager.Instance.RenameActiveEmotePreset(_presetNameEdit.Text);
		root.AddChild(_presetNameEdit);

		_hintLabel = new Label
		{
			Text = Loc.T("ui.emote_page.hint", "战斗中打开信息发送界面，输入预设表情前几个字即可 Tab 补全；不带 / 的未知输入会直接发送为消息。"),
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
		};
		_hintLabel.AddThemeColorOverride("font_color", new Color(0.78f, 0.82f, 0.9f));
		root.AddChild(_hintLabel);

		var scroll = new ScrollContainer
		{
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		root.AddChild(scroll);

		_entryList = new VBoxContainer();
		_entryList.AddThemeConstantOverride("separation", 8);
		scroll.AddChild(_entryList);

		var addButton = new Button { Text = Loc.T("ui.emote_page.add_entry", "添加表情") };
		addButton.Pressed += () => GameManager.Instance.AddActiveEmoteEntry();
		root.AddChild(addButton);
	}

	private void RefreshAll()
	{
		if (!IsInsideTree())
			return;

		_refreshing = true;
		var gm = GameManager.Instance;
		gm.EnsureEmotePresetsInitialized();

		_presetSelector.Clear();
		int selected = 0;
		for (int i = 0; i < gm.EmotePresets.Count; i++)
		{
			var preset = gm.EmotePresets[i];
			_presetSelector.AddItem(preset.Name, i);
			if (preset.Id == gm.ActiveEmotePresetId)
				selected = i;
		}
		_presetSelector.Select(selected);

		var active = gm.GetActiveEmotePreset();
		if (active == null)
		{
			_refreshing = false;
			return;
		}

		_presetNameEdit.Text = active.Name;
		RefreshEntries(active);
		_refreshing = false;
	}

	private void RefreshEntries(EmotePresetSaveData preset)
	{
		foreach (var child in _entryList.GetChildren())
			child.QueueFree();

		for (int i = 0; i < preset.Entries.Count; i++)
			_entryList.AddChild(CreateEntryRow(i, preset.Entries[i]));
	}

	private static HBoxContainer CreateEntryRow(int index, EmotePresetEntrySaveData entry)
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 8);

		var badge = new Label
		{
			Text = entry.IsOfficialCollection ? "★" : (index + 1).ToString(),
			CustomMinimumSize = new Vector2(28, 0),
			HorizontalAlignment = HorizontalAlignment.Center,
		};
		badge.AddThemeColorOverride("font_color", entry.IsOfficialCollection ? new Color(1f, 0.86f, 0.2f) : new Color(0.75f, 0.75f, 0.75f));
		row.AddChild(badge);

		var edit = new LineEdit
		{
			Text = entry.Text,
			PlaceholderText = Loc.T("ui.emote_page.entry_placeholder", "输入表情内容"),
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		edit.TextSubmitted += text => GameManager.Instance.UpdateActiveEmoteEntryText(index, text);
		edit.FocusExited += () => GameManager.Instance.UpdateActiveEmoteEntryText(index, edit.Text);
		row.AddChild(edit);

		var deleteButton = new Button { Text = Loc.T("ui.emote_page.remove_entry", "移除") };
		deleteButton.Pressed += () => GameManager.Instance.RemoveActiveEmoteEntry(index);
		row.AddChild(deleteButton);

		return row;
	}

	private void OnPresetSelected(long itemIndex)
	{
		if (_refreshing)
			return;

		int index = (int)itemIndex;
		var presets = GameManager.Instance.EmotePresets;
		if (index < 0 || index >= presets.Count)
			return;

		GameManager.Instance.SetActiveEmotePreset(presets[index].Id);
	}

	private void OnDeletePresetPressed()
	{
		if (!GameManager.Instance.DeleteActiveEmotePreset())
			ShowNotice(Loc.T("ui.emote_page.cannot_delete_last", "至少需要保留一个表情组"));
	}

	private void ShowNotice(string text)
	{
		var dialog = new AcceptDialog
		{
			Title = Loc.T("ui.collection.notification_title", "提示"),
			DialogText = text,
			OkButtonText = Loc.T("ui.common.ok", "确定"),
			Exclusive = true,
		};
		AddChild(dialog);
		dialog.PopupCentered();
	}

	private void ClosePage()
	{
		if (GetParent() is MainMenu menu)
			menu.ShowMainMenu();
		QueueFree();
	}

	public override void _ExitTree()
	{
		if (GameManager.Instance != null)
			GameManager.Instance.OnEmotePresetsChanged -= RefreshAll;
	}
}
