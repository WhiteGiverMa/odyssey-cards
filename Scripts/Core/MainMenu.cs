using System;
using System.Collections.Generic;
using Godot;
using OdysseyCards.Character;
using OdysseyCards.Infrastructure;
using OdysseyCards.Localization;
using OdysseyCards.UI;

namespace OdysseyCards.Core;

public partial class MainMenu : Control
{
	private Button _startButton;
	private Button _collectionButton;
	private Button _continueButton = null!;
	private Button _settingsButton;
	private Button _abandonButton = null!;
	private Button _quitButton = null!;
	private Label _titleLabel;
	private Control _mainMenuContainer;
	private VBoxContainer _buttonContainer;

	/// <summary>移动端 TouchZone 注册 token，ExitTree 时释放。</summary>
	private readonly List<IDisposable> _zoneTokens = new();

	/// <summary>当前活跃的模态覆盖层（SettingsPage），用于 PopModalLayer。</summary>
	private Control? _activeModalOverlay;

	public override void _Ready()
	{
		Localization.Localization.Initialize();

		_mainMenuContainer = GetNode<Control>("MainMenuContainer");
		_startButton = GetNode<Button>("MainMenuContainer/ButtonContainer/StartButton");
		_settingsButton = GetNode<Button>("MainMenuContainer/ButtonContainer/SettingsButton");
		_titleLabel = GetNode<Label>("MainMenuContainer/TitleLabel");
		_buttonContainer = GetNode<VBoxContainer>("MainMenuContainer/ButtonContainer");

		// 占位资源只在编辑器开发期生成。
		try
		{
			PlaceholderAssetGenerator.GenerateAllPlaceholders();
		}
		catch (Exception ex)
		{
			GD.PushWarning($"[MainMenu] PlaceholderAssetGenerator skipped: {ex.Message}");
		}

		// 动态插入「我的收藏」按钮
		_collectionButton = new Button
		{
			Name = "CollectionButton",
			LayoutMode = 2,
		};
		_collectionButton.AddThemeColorOverride("font_color", new Color(1, 1, 1, 1));
		_collectionButton.AddThemeFontSizeOverride("font_size", 24);
		int settingsIdx = _settingsButton.GetIndex();
		_buttonContainer.AddChild(_collectionButton);
		_buttonContainer.MoveChild(_collectionButton, settingsIdx);

		_startButton.Pressed += OnStartPressed;
		_collectionButton.Pressed += OnCollectionPressed;
		_settingsButton.Pressed += OnSettingsPressed;

		// 动态插入「继续冒险」按钮
		_continueButton = new Button
		{
			Name = "ContinueButton",
			LayoutMode = 2,
			Text = Localization.Localization.T("ui.menu.continue_run", "继续冒险"),
		};
		_continueButton.AddThemeColorOverride("font_color", new Color(1, 0.85f, 0.3f, 1));
		_continueButton.AddThemeFontSizeOverride("font_size", 24);
		_continueButton.Pressed += OnContinuePressed;
		_buttonContainer.AddChild(_continueButton);
		_buttonContainer.MoveChild(_continueButton, 0); // 插入到第一个按钮位置

		// 动态插入「放弃当前冒险」按钮
		_abandonButton = new Button
		{
			Name = "AbandonButton",
			LayoutMode = 2,
			Text = Localization.Localization.T("ui.menu.abandon_run", "放弃当前冒险"),
		};
		_abandonButton.AddThemeColorOverride("font_color", new Color(1, 0.4f, 0.4f, 1));
		_abandonButton.AddThemeFontSizeOverride("font_size", 20);
		_abandonButton.Pressed += OnAbandonPressed;
		_buttonContainer.AddChild(_abandonButton);
		_buttonContainer.MoveChild(_abandonButton, 1); // 插入到第二个按钮位置

		// 动态插入「退出游戏」按钮
		_quitButton = new Button
		{
			Name = "QuitButton",
			LayoutMode = 2,
			Text = Localization.Localization.T("ui.menu.quit", "退出"),
		};
		_quitButton.AddThemeColorOverride("font_color", new Color(0.65f, 0.65f, 0.65f, 1));
		_quitButton.AddThemeFontSizeOverride("font_size", 20);
		_quitButton.Pressed += OnQuitPressed;
		_buttonContainer.AddChild(_quitButton);

		// 根据当前是否有活跃冒险控制 Continue / Abandon 按钮的可见性
		// 运行完成（胜利/失败）后不显示继续/放弃按钮
		var runState = GameManager.Instance.RunState;
		bool hasActiveRun = runState != null && !runState.IsRunComplete;
		_continueButton.Visible = hasActiveRun;
		_abandonButton.Visible = hasActiveRun;

		// 移动端：通过 MobileInputRouter 注册触控区域
		if (MobileInputRouter.IsMobile)
		{
			RegisterMobileZones();
		}

		Localization.Localization.OnLanguageChanged += OnLanguageChanged;
		GameManager.Instance.LanguageChanged += OnLanguageChanged;
		UpdateLabels();
	}

	/// <summary>为三个主菜单按钮注册轻触区域（仅移动端）。</summary>
	private void RegisterMobileZones()
	{
		var router = MobileInputRouter.Instance;

		_zoneTokens.Add(router.RegisterTapZone(_startButton,
			new Rect2(_startButton.GlobalPosition, _startButton.Size),
			priority: 400, onTap: () => OnStartPressed()));

		_zoneTokens.Add(router.RegisterTapZone(_continueButton,
			new Rect2(_continueButton.GlobalPosition, _continueButton.Size),
			priority: 400, onTap: () => OnContinuePressed()));

		_zoneTokens.Add(router.RegisterTapZone(_abandonButton,
			new Rect2(_abandonButton.GlobalPosition, _abandonButton.Size),
			priority: 400, onTap: () => OnAbandonPressed()));

		_zoneTokens.Add(router.RegisterTapZone(_collectionButton,
			new Rect2(_collectionButton.GlobalPosition, _collectionButton.Size),
			priority: 400, onTap: () => OnCollectionPressed()));

		_zoneTokens.Add(router.RegisterTapZone(_settingsButton,
			new Rect2(_settingsButton.GlobalPosition, _settingsButton.Size),
			priority: 400, onTap: () => OnSettingsPressed()));

		_zoneTokens.Add(router.RegisterTapZone(_quitButton,
			new Rect2(_quitButton.GlobalPosition, _quitButton.Size),
			priority: 400, onTap: () => OnQuitPressed()));
	}



	private void OnStartPressed()
	{
		GD.Print("[MainMenu] OnStartPressed called");

		var gm = GameManager.Instance;
		var deck = gm?.ActiveDeck;

		var validation = DeckValidityService.ValidateForStart(deck);
		if (!validation.IsValid)
		{
			ShowDeckNotReadyDialog(
				Localization.Localization.T(validation.ErrorKey ?? "ui.menu.deck_not_ready",
					validation.DefaultMessage ?? "牌组未就绪。\n请先前往收藏界面构筑牌组。"));
			return;
		}

		gm?.ClearActiveRun();
		gm?.StartNewRun();
		GetTree().ChangeSceneToFile("res://Scenes/Map.tscn");
	}

	private void ShowDeckNotReadyDialog(string description)
	{
		var dialog = new AcceptDialog
		{
			Title = Localization.Localization.T("ui.menu.deck_not_ready_title", "牌组未就绪"),
			OkButtonText = Localization.Localization.T("ui.menu.go_to_collection", "前往收藏"),
			Exclusive = true,
			Size = new Vector2I(400, 180),
		};
		dialog.GetOkButton().Text = Localization.Localization.T("ui.menu.go_to_collection", "前往收藏");

		var label = new Label
		{
			Text = description,
			HorizontalAlignment = HorizontalAlignment.Center,
			LayoutMode = 2,
		};
		label.AddThemeFontSizeOverride("font_size", 14);
		dialog.AddChild(label);

		dialog.Confirmed += () =>
		{
			GetTree().ChangeSceneToFile("res://Scenes/Collection.tscn");
		};

		AddChild(dialog);
		dialog.PopupCentered();
	}

	private void OnContinuePressed()
	{
		GD.Print("[MainMenu] OnContinuePressed");
		var gm = GameManager.Instance;
		if (gm != null && gm.ContinueRun())
		{
			GetTree().ChangeSceneToFile("res://Scenes/Map.tscn");
		}
		else
		{
			var dialog = new AcceptDialog
			{
				Title = Localization.Localization.T("ui.menu.no_active_run_title", "无法继续"),
				DialogText = Localization.Localization.T("ui.menu.no_active_run_desc", "未找到进行中的冒险存档。\n请先开始新游戏，或检查存档是否完整。"),
				OkButtonText = Localization.Localization.T("ui.menu.ok", "确定"),
				Exclusive = true,
			};
			AddChild(dialog);
			dialog.PopupCentered();
		}
	}

	private void OnAbandonPressed()
	{
		GD.Print("[MainMenu] OnAbandonPressed");
		GameManager.Instance?.ClearActiveRun();
		_continueButton.Visible = false;
		_abandonButton.Visible = false;
	}

	private void OnCollectionPressed()
	{
		GD.Print("[MainMenu] OnCollectionPressed → 收藏界面");
		GetTree().ChangeSceneToFile("res://Scenes/Collection.tscn");
	}

	private void OnSettingsPressed()
	{
		if (IsSettingsOverlayActive())
			return;

		SettingsPage settingsPage = new();
		settingsPage.Name = "SettingsPage";
		settingsPage.SetAnchorsPreset(LayoutPreset.FullRect);
		AddChild(settingsPage);

		_mainMenuContainer.Visible = false;

		if (MobileInputRouter.IsMobile)
		{
			_activeModalOverlay = settingsPage;
			MobileInputRouter.Instance.PushModalLayer(settingsPage);
		}
	}

	private bool IsSettingsOverlayActive()
	{
		return _activeModalOverlay != null
			&& GodotObject.IsInstanceValid(_activeModalOverlay)
			&& _activeModalOverlay.IsInsideTree();
	}

	private void OnQuitPressed()
	{
		GD.Print("[MainMenu] OnQuitPressed → 退出游戏");
		GetTree().Quit();
	}

	public void ShowMainMenu()
	{
		_mainMenuContainer.Visible = true;

		if (_activeModalOverlay != null)
		{
			if (MobileInputRouter.IsMobile)
			{
				MobileInputRouter.Instance.PopModalLayer(_activeModalOverlay);
			}
			_activeModalOverlay = null;
		}
	}

	private void OnLanguageChanged(string newLanguage)
	{
		UpdateLabels();
	}

	private void UpdateLabels()
	{
		_titleLabel.Text = Localization.Localization.T("ui.menu.title", "Odyssey Cards");
		_startButton.Text = Localization.Localization.T("ui.menu.start_game", "Start Game");
		_continueButton.Text = Localization.Localization.T("ui.menu.continue_run", "继续冒险");
		_abandonButton.Text = Localization.Localization.T("ui.menu.abandon_run", "放弃当前冒险");
		_collectionButton.Text = Localization.Localization.T("ui.menu.collection", "我的收藏");
		_settingsButton.Text = Localization.Localization.T("ui.menu.settings", "Settings");
		_quitButton.Text = Localization.Localization.T("ui.menu.quit", "退出");
	}

	public override void _ExitTree()
	{
		SceneLifecycleGuard.OnExitTree(this);

		// 释放所有移动端 TouchZone 注册
		foreach (var token in _zoneTokens)
		{
			token.Dispose();
		}
		_zoneTokens.Clear();

		// 置空引用以支持 GC
		_startButton = null!;
		_collectionButton = null!;
		_continueButton = null!;
		_abandonButton = null!;
		_quitButton = null!;
		_settingsButton = null!;
		_titleLabel = null!;
		_mainMenuContainer = null!;
		_buttonContainer = null!;
		_activeModalOverlay = null;

		Localization.Localization.OnLanguageChanged -= OnLanguageChanged;
		if (GameManager.Instance != null)
		{
			GameManager.Instance.LanguageChanged -= OnLanguageChanged;
		}
	}
}
