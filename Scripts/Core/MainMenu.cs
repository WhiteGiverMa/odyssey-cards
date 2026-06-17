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
	private Button _emoteButton = null!;
	private Button _continueButton = null!;
	private Button _settingsButton;
	private Button _abandonButton = null!;
	private Button _quitButton = null!;
	private Label _titleLabel;
	private Control _mainMenuContainer;
	private VBoxContainer _buttonContainer;
	private Control? _heroSelectOverlay;
	private Label? _heroSelectTitleLabel;
	private Label? _heroDescriptionLabel;
	private Button? _heroConfirmButton;
	private Button? _heroCancelButton;
	private readonly List<Button> _heroOptionButtons = new();

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

		// 动态插入「我的表情」按钮
		_emoteButton = new Button
		{
			Name = "EmoteButton",
			LayoutMode = 2,
		};
		_emoteButton.AddThemeColorOverride("font_color", new Color(1, 1, 1, 1));
		_emoteButton.AddThemeFontSizeOverride("font_size", 24);
		_buttonContainer.AddChild(_emoteButton);
		_buttonContainer.MoveChild(_emoteButton, settingsIdx + 1);

		_startButton.Pressed += OnStartPressed;
		_collectionButton.Pressed += OnCollectionPressed;
		_emoteButton.Pressed += OnEmotePressed;
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

		_zoneTokens.Add(router.RegisterTapZone(_emoteButton,
			new Rect2(_emoteButton.GlobalPosition, _emoteButton.Size),
			priority: 400, onTap: () => OnEmotePressed()));

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
		if (IsSettingsOverlayActive() || IsHeroSelectorActive())
			return;

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

		ShowHeroSelectOverlay();
	}

	private bool IsHeroSelectorActive()
	{
		return _heroSelectOverlay != null
			&& GodotObject.IsInstanceValid(_heroSelectOverlay)
			&& _heroSelectOverlay.IsInsideTree();
	}

	private void ShowHeroSelectOverlay()
	{
		if (IsHeroSelectorActive())
			return;

		var gm = GameManager.Instance;
		if (gm == null)
			return;

		var overlay = new ColorRect
		{
			Name = "HeroSelectOverlay",
			Color = new Color(0f, 0f, 0f, 0.78f),
			MouseFilter = MouseFilterEnum.Stop,
		};
		overlay.SetAnchorsPreset(LayoutPreset.FullRect);

		var panel = new PanelContainer();
		panel.SetAnchorsPreset(LayoutPreset.Center);
		panel.CustomMinimumSize = new Vector2(520f, 420f);
		panel.OffsetLeft = -260f;
		panel.OffsetTop = -210f;
		panel.OffsetRight = 260f;
		panel.OffsetBottom = 210f;
		overlay.AddChild(panel);

		var root = new VBoxContainer();
		root.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		root.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		root.AddThemeConstantOverride("separation", 14);
		panel.AddChild(root);

		_heroSelectTitleLabel = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Center,
		};
		_heroSelectTitleLabel.AddThemeFontSizeOverride("font_size", 28);
		root.AddChild(_heroSelectTitleLabel);

		var optionList = new VBoxContainer();
		optionList.AddThemeConstantOverride("separation", 10);
		root.AddChild(optionList);
		_heroOptionButtons.Clear();
		foreach (var hero in HeroProfile.All)
		{
			var button = new Button
			{
				ToggleMode = true,
				Text = hero.DisplayName,
			};
			button.Pressed += () => SelectHero(hero.Id);
			optionList.AddChild(button);
			_heroOptionButtons.Add(button);
		}

		_heroDescriptionLabel = new Label
		{
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			HorizontalAlignment = HorizontalAlignment.Left,
			VerticalAlignment = VerticalAlignment.Top,
			CustomMinimumSize = new Vector2(0f, 120f),
		};
		root.AddChild(_heroDescriptionLabel);

		var actions = new HBoxContainer();
		actions.Alignment = BoxContainer.AlignmentMode.End;
		actions.AddThemeConstantOverride("separation", 12);
		root.AddChild(actions);

		_heroCancelButton = new Button();
		_heroCancelButton.Pressed += HideHeroSelectOverlay;
		actions.AddChild(_heroCancelButton);

		_heroConfirmButton = new Button();
		_heroConfirmButton.Pressed += StartSelectedHeroRun;
		actions.AddChild(_heroConfirmButton);

		AddChild(overlay);
		_heroSelectOverlay = overlay;
		_mainMenuContainer.Visible = false;

		if (MobileInputRouter.IsMobile)
		{
			_activeModalOverlay = overlay;
			MobileInputRouter.Instance.PushModalLayer(overlay);
		}

		SelectHero(gm.SelectedHeroId);
		UpdateHeroSelectorTexts();
	}

	private void SelectHero(string heroId)
	{
		var gm = GameManager.Instance;
		if (gm == null)
			return;

		gm.SelectedHeroId = HeroProfile.Get(heroId).Id;
		gm.SaveToDisk();
		for (int i = 0; i < _heroOptionButtons.Count; i++)
		{
			var profile = HeroProfile.All[i];
			_heroOptionButtons[i].ButtonPressed = profile.Id == gm.SelectedHeroId;
		}

		UpdateHeroSelectorTexts();
	}

	private void UpdateHeroSelectorTexts()
	{
		var gm = GameManager.Instance;
		if (gm == null)
			return;

		var hero = gm.SelectedHeroProfile;
		if (_heroSelectTitleLabel != null)
			_heroSelectTitleLabel.Text = Localization.Localization.T("ui.menu.hero_select_title", "选择英雄");

		for (int i = 0; i < _heroOptionButtons.Count && i < HeroProfile.All.Count; i++)
		{
			var profile = HeroProfile.All[i];
			string localName = Localization.Localization.T(profile.NameKey, profile.DisplayName);
			_heroOptionButtons[i].Text = Localization.Localization.T("ui.menu.hero_option_format", "{name} / {romanized}")
				.Replace("{name}", localName)
				.Replace("{romanized}", profile.RomanizedName);
		}

		if (_heroDescriptionLabel != null)
		{
			string localName = Localization.Localization.T(hero.NameKey, hero.DisplayName);
			string desc = Localization.Localization.T(hero.DescriptionKey, hero.DefaultDescription);
			var weapon = hero.CreateWeapon();
			string weaponName = Localization.Localization.T(weapon.NameKey, weapon.Name);
			string heroPowerName = hero.CreateHeroPower().Name;
			_heroDescriptionLabel.Text = Localization.Localization.T("ui.menu.hero_detail_format", "{name}\n生命值：{hp}\n武器：{weapon}\n英雄技能：{power}\n\n{desc}")
				.Replace("{name}", localName)
				.Replace("{hp}", hero.MaxHealth.ToString())
				.Replace("{weapon}", weaponName)
				.Replace("{power}", heroPowerName)
				.Replace("{desc}", desc);
		}

		if (_heroCancelButton != null)
			_heroCancelButton.Text = Localization.Localization.T("ui.hand_select.cancel", "取消");
		if (_heroConfirmButton != null)
			_heroConfirmButton.Text = Localization.Localization.T("ui.menu.start_selected_hero", "以该英雄开始");
	}

	private void StartSelectedHeroRun()
	{
		var gm = GameManager.Instance;
		if (gm == null)
			return;
		string heroId = gm.SelectedHeroId;

		gm.ClearActiveRun();
		gm.SelectedHeroId = heroId;
		gm.StartNewRun();
		HideHeroSelectOverlay();
		GetTree().ChangeSceneToFile("res://Scenes/Map.tscn");
	}

	private void HideHeroSelectOverlay()
	{
		if (!IsHeroSelectorActive())
		{
			ShowMainMenu();
			return;
		}

		if (MobileInputRouter.IsMobile && _heroSelectOverlay != null)
			MobileInputRouter.Instance.PopModalLayer(_heroSelectOverlay);

		_heroSelectOverlay?.QueueFree();
		_heroSelectOverlay = null;
		_activeModalOverlay = null;
		_heroSelectTitleLabel = null;
		_heroDescriptionLabel = null;
		_heroConfirmButton = null;
		_heroCancelButton = null;
		_heroOptionButtons.Clear();
		ShowMainMenu();
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

	private void OnEmotePressed()
	{
		if (IsSettingsOverlayActive() || IsHeroSelectorActive())
			return;

		var page = new EmotePresetPage
		{
			Name = "EmotePresetPage",
		};
		page.SetAnchorsPreset(LayoutPreset.FullRect);
		AddChild(page);
		_mainMenuContainer.Visible = false;

		if (MobileInputRouter.IsMobile)
		{
			_activeModalOverlay = page;
			MobileInputRouter.Instance.PushModalLayer(page);
		}
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
		_emoteButton.Text = Localization.Localization.T("ui.menu.emotes", "我的表情");
		_settingsButton.Text = Localization.Localization.T("ui.menu.settings", "Settings");
		_quitButton.Text = Localization.Localization.T("ui.menu.quit", "退出");
		if (IsHeroSelectorActive())
			UpdateHeroSelectorTexts();
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
		_emoteButton = null!;
		_continueButton = null!;
		_abandonButton = null!;
		_quitButton = null!;
		_settingsButton = null!;
		_titleLabel = null!;
		_mainMenuContainer = null!;
		_buttonContainer = null!;
		_activeModalOverlay = null;
		_heroSelectOverlay = null;
		_heroSelectTitleLabel = null;
		_heroDescriptionLabel = null;
		_heroConfirmButton = null;
		_heroCancelButton = null;
		_heroOptionButtons.Clear();

		Localization.Localization.OnLanguageChanged -= OnLanguageChanged;
		if (GameManager.Instance != null)
		{
			GameManager.Instance.LanguageChanged -= OnLanguageChanged;
		}
	}
}
