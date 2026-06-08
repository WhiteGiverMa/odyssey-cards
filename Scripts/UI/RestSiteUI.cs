using Godot;
using OdysseyCards.Core;
using OdysseyCards.Roguelike;
using System;
using System.Collections.Generic;

namespace OdysseyCards.UI;

/// <summary>
/// 休息站点 UI — 使用 MobileDialogHost 弹窗模式，直接在父控件上创建。
/// 提供回复生命值 + 选择金血祝颂的功能。
/// </summary>
public static class RestSiteUI
{
	/// <summary>
	/// 创建并显示休息站点 UI。
	/// </summary>
	/// <param name="parent">父控件（通常是 MapUI）。</param>
	/// <param name="room">休息站点房间定义。</param>
	/// <param name="onComplete">完成回调——调用方在此处理 CompleteRoomAndAdvance。</param>
	public static void Show(Control parent, RoomDefinition room, Action onComplete)
	{
		var title = $"{MapUI.GetRoomIcon(room.Type)} {room.DisplayName}";
		var (dialog, content, buttonRow) = MobileDialogHost.CreateDialog(parent, title, width: 450);

		var hasRested = false;
		var hasPickedBlessing = false;
		Button? continueBtn = null;

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
		var restButton = MobileDialogHost.CreateDialogButton(
			Localization.Localization.T("ui.rest_site.rest_button", "回复 30% 生命值"));
		restButton.Pressed += () =>
		{
			if (hasRested) return;
			var gm = GameManager.Instance;
			if (gm != null)
			{
				var healAmount = (int)(gm.PlayerMaxHealth * 0.3f);
				var newHealth = Mathf.Min(gm.PlayerHealth + healAmount, gm.PlayerMaxHealth);
				var actualHealed = newHealth - gm.PlayerHealth;
				gm.PlayerHealth = newHealth;
				GD.Print($"[RestSiteUI] 回复 {actualHealed} 点生命值（当前 {gm.PlayerHealth}/{gm.PlayerMaxHealth}）");
			}
			hasRested = true;
			restButton.Disabled = true;
			restButton.Text = $"{restButton.Text} ✓";
			restButton.AddThemeColorOverride("font_color", new Color(0.5f, 0.8f, 0.5f, 1));
		};
		content.AddChild(restButton);

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
		var blessingButtons = new List<Button>();
		foreach (var blessing in BlessingPool.Placeholders)
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
				if (hasPickedBlessing) return;
				hasPickedBlessing = true;
				GD.Print($"[RestSiteUI] 选择了祝福：{blessing.Name}（{blessing.Id}）— 占位符，无实际效果");

				foreach (var b in blessingButtons)
				{
					b.Disabled = true;
					b.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.5f, 1));
				}
				if (continueBtn != null) continueBtn.Disabled = false;
			};
			blessingButtons.Add(btn);
			content.AddChild(btn);
		}

		// === 继续按钮（初始禁用，选祝福后启用）===
		continueBtn = MobileDialogHost.CreateDialogButton(
			Localization.Localization.T("ui.map.continue_button", "继续"));
		continueBtn.Disabled = true;
		continueBtn.Pressed += () =>
		{
			MobileDialogHost.CloseDialog(dialog, parent);
			onComplete();
		};
		buttonRow.AddChild(continueBtn);
	}
}
