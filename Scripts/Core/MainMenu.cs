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
    private Button _settingsButton;
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

        _zoneTokens.Add(router.RegisterTapZone(_collectionButton,
            new Rect2(_collectionButton.GlobalPosition, _collectionButton.Size),
            priority: 400, onTap: () => OnCollectionPressed()));

        _zoneTokens.Add(router.RegisterTapZone(_settingsButton,
            new Rect2(_settingsButton.GlobalPosition, _settingsButton.Size),
            priority: 400, onTap: () => OnSettingsPressed()));
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

    private void OnCollectionPressed()
    {
        GD.Print("[MainMenu] OnCollectionPressed → 收藏界面");
        GetTree().ChangeSceneToFile("res://Scenes/Collection.tscn");
    }

    private void OnSettingsPressed()
    {
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
        _collectionButton.Text = Localization.Localization.T("ui.menu.collection", "我的收藏");
        _settingsButton.Text = Localization.Localization.T("ui.menu.settings", "Settings");
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
