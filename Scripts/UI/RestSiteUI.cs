using Godot;
using OdysseyCards.Core;
using OdysseyCards.Roguelike;
using System;

namespace OdysseyCards.UI;

/// <summary>
/// 休息站点 UI — 使用 MobileDialogHost 弹窗模式。
/// 提供回复生命值 + 选择金血祝颂的功能。
/// </summary>
public partial class RestSiteUI : Control
{
	private RoomDefinition _room = null!;
	private Action? _onComplete;
	private Control? _dialog;
	private Button? _restButton;
	private Button? _continueButton;
	private bool _hasRested;
	private bool _hasPickedBlessing;

	/// <summary>
	/// 创建并显示休息站点 UI。
	/// </summary>
	public static void Show(Control parent, RoomDefinition room, Action onComplete)
	{
		var ui = new RestSiteUI
		{
			Name = "RestSiteUI",
			MouseFilter = MouseFilterEnum.Ignore,
		};
		ui.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		parent.AddChild(ui);
		ui.Build(room, onComplete);
	}

	private void Build(RoomDefinition room, Action onComplete)
	{
		_room = room;
		_onComplete = onComplete;

		var title = $"{MapUI.GetRoomIcon(room.Type)} {room.DisplayName}";
		var (dialog, content, buttonRow) = MobileDialogHost.CreateDialog(
			this,
			title,
			width: 450);
		_dialog = dialog;

		// 描述文本
		var descLabel = new Label
		{
			Text = room.Description,
			HorizontalAlignment = HorizontalAlignment.Center,
		};
		descLabel.AddThemeFontSizeOverride("font_size", 16);
		descLabel.AddThemeColorOverride("font_color", new Color(0.8f, 0.8f, 0.9f, 1));
		content.AddChild(descLabel);

		// === 休息按钮 ===
		_restButton = MobileDialogHost.CreateDialogButton(
			Localization.Localization.T("ui.rest_site.rest_button", "回复 30% 生命值"));
		_restButton.Pressed += OnRestPressed;
		content.AddChild(_restButton);

		// === 祝福选择标题 ===
		var blessingTitle = new Label
		{
			Text = Localization.Localization.T("ui.rest_site.select_blessing", "选择一项金血祝颂："),
			HorizontalAlignment = HorizontalAlignment.Center,
		};
		blessingTitle.AddThemeFontSizeOverride("font_size", 18);
		blessingTitle.AddThemeColorOverride("font_color", new Color(1, 0.85f, 0.3f, 1));
		content.AddChild(blessingTitle);

		// === 3 个祝福按钮 ===
		var blessings = BlessingPool.Placeholders;
		foreach (var blessing in blessings)
		{
			var blessingBtn = CreateBlessingButton(blessing);
			content.AddChild(blessingBtn);
		}

		// === 继续按钮 ===
		_continueButton = MobileDialogHost.CreateDialogButton(
			Localization.Localization.T("ui.map.continue_button", "继续"));
		_continueButton.Disabled = true;
		_continueButton.Pressed += OnContinuePressed;
		buttonRow.AddChild(_continueButton);
	}

	/// <summary>
	/// 创建一个祝福选择按钮。
	/// </summary>
	private Button CreateBlessingButton(BlessingData blessing)
	{
		var btn = new Button
		{
			Text = $"{blessing.Name}\n{blessing.Description}",
			CustomMinimumSize = new Vector2(0, MobileDialogHost.MinTouchTargetHeight * 1.5f),
		};
		btn.AddThemeFontSizeOverride("font_size", 16);
		btn.AddThemeColorOverride("font_color", new Color(1, 0.85f, 0.3f, 1));
		btn.Pressed += () =>
		{
			if (_hasPickedBlessing)
				return;

			_hasPickedBlessing = true;
			ApplyBlessing(blessing);

			// 禁用所有祝福按钮（使用 GetParent 遍历）
			if (btn.GetParent() is VBoxContainer parentContainer)
			{
				foreach (var child in parentContainer.GetChildren())
				{
					if (child is Button b && b != _restButton && b != btn)
					{
						b.Disabled = true;
						b.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.5f, 1));
					}
				}
			}
			btn.Disabled = true;
			btn.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.5f, 1));

			// 启用继续按钮
			if (_continueButton != null)
			{
				_continueButton.Disabled = false;
			}
		};
		return btn;
	}

	/// <summary>
	/// 应用祝福效果。当前为占位符——仅打印日志。
	/// </summary>
	private void ApplyBlessing(BlessingData blessing)
	{
		GD.Print($"[RestSiteUI] 选择了祝福：{blessing.Name}（{blessing.Id}）— 占位符，无实际效果");
	}

	/// <summary>
	/// 休息按钮按下——回复 30% 最大生命值。
	/// </summary>
	private void OnRestPressed()
	{
		if (_hasRested)
			return;

		var gm = GameManager.Instance;
		if (gm != null)
		{
			var healAmount = (int)(gm.PlayerMaxHealth * 0.3f);
			var newHealth = Mathf.Min(gm.PlayerHealth + healAmount, gm.PlayerMaxHealth);
			var actualHealed = newHealth - gm.PlayerHealth;

			gm.PlayerHealth = newHealth;
			GD.Print($"[RestSiteUI] 回复 {actualHealed} 点生命值（当前 {gm.PlayerHealth}/{gm.PlayerMaxHealth}）");
		}

		_hasRested = true;
		if (_restButton != null)
		{
			_restButton.Disabled = true;
			_restButton.Text = $"{_restButton.Text} ✓";
			_restButton.AddThemeColorOverride("font_color", new Color(0.5f, 0.8f, 0.5f, 1));
		}
	}

	/// <summary>
	/// 继续按钮按下——关闭弹窗，推进冒险。
	/// </summary>
	private void OnContinuePressed()
	{
		Close();
	}

	/// <summary>
	/// 关闭弹窗并清理自身。
	/// </summary>
	private void Close()
	{
		if (_dialog != null)
		{
			MobileDialogHost.CloseDialog(_dialog, this);
			_dialog = null;
		}
		_onComplete?.Invoke();
		QueueFree();
	}
}
