using Godot;
using OdysseyCards.Core;
using OdysseyCards.Localization;

namespace OdysseyCards.UI;

public partial class SettingsPage : Control
{
    private OptionButton _languageOptionButton;
    private Button _backButton;
    private Label _titleLabel;
    private Label _languageLabel;

    public override void _Ready()
    {
        SetupUI();
        LoadLanguages();
        ConnectSignals();
        UpdateCurrentLanguage();
    }

    private void SetupUI()
    {
        _titleLabel = new Label();
        _titleLabel.Name = "TitleLabel";
        _titleLabel.Text = Localization.Localization.T("ui.settings.title", "Settings");
        _titleLabel.AddThemeFontSizeOverride("font_size", 36);
        _titleLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _titleLabel.SetAnchorsPreset(LayoutPreset.CenterTop);
        _titleLabel.Position = new Vector2(-100, 80);
        _titleLabel.Size = new Vector2(200, 50);

        _languageLabel = new Label();
        _languageLabel.Name = "LanguageLabel";
        _languageLabel.Text = Localization.Localization.T("ui.settings.language", "Language");
        _languageLabel.AddThemeFontSizeOverride("font_size", 20);

        _languageOptionButton = new OptionButton();
        _languageOptionButton.Name = "LanguageOptionButton";

        _backButton = new Button();
        _backButton.Name = "BackButton";
        _backButton.Text = Localization.Localization.T("ui.settings.back", "Back");
        _backButton.AddThemeFontSizeOverride("font_size", 18);

        HBoxContainer languageRow = new();
        languageRow.Name = "LanguageRow";
        languageRow.AddThemeConstantOverride("separation", 20);
        languageRow.AddChild(_languageLabel);
        languageRow.AddChild(_languageOptionButton);
        languageRow.SetAnchorsPreset(LayoutPreset.Center);
        languageRow.Position = new Vector2(-100, 0);

        _backButton.SetAnchorsPreset(LayoutPreset.CenterBottom);
        _backButton.Position = new Vector2(-60, 60);
        _backButton.Size = new Vector2(120, 40);

        VBoxContainer container = new();
        container.Name = "SettingsContainer";
        container.SetAnchorsPreset(LayoutPreset.Center);
        container.AddThemeConstantOverride("separation", 30);

        container.AddChild(_titleLabel);
        container.AddChild(languageRow);
        container.AddChild(_backButton);

        AddChild(container);
    }

    private void LoadLanguages()
    {
        _languageOptionButton.Clear();

        System.Collections.Generic.IReadOnlyList<string> languages = Localization.Localization.AvailableLanguages;
        System.Collections.Generic.Dictionary<string, string> languageNames = new()
        {
            { "en", "English" },
            { "zh", "中文" }
        };

        int currentIndex = 0;
        int selectedIndex = 0;

        foreach (string lang in languages)
        {
            string displayName = languageNames.TryGetValue(lang, out string name) ? name : lang;
            _languageOptionButton.AddItem(displayName);

            int langIndex = currentIndex;
            _languageOptionButton.SetItemMetadata(langIndex, lang);

            if (lang == Localization.Localization.CurrentLanguage)
            {
                selectedIndex = currentIndex;
            }

            currentIndex++;
        }

        _languageOptionButton.Selected = selectedIndex;
    }

    private void ConnectSignals()
    {
        _languageOptionButton.ItemSelected += OnLanguageSelected;
        _backButton.Pressed += OnBackPressed;
        Localization.Localization.OnLanguageChanged += OnLanguageChanged;
    }

    private void OnLanguageSelected(long index)
    {
        Variant langVariant = _languageOptionButton.GetItemMetadata((int)index);
        string lang = langVariant.AsString();
        if (!string.IsNullOrEmpty(lang))
        {
            Localization.Localization.SetLanguage(lang);
        }
    }

    private void OnLanguageChanged(string newLanguage)
    {
        UpdateLabels();
    }

    private void UpdateCurrentLanguage()
    {
        for (int i = 0; i < _languageOptionButton.ItemCount; i++)
        {
            Variant langVariant = _languageOptionButton.GetItemMetadata(i);
            string lang = langVariant.AsString();
            if (lang == Localization.Localization.CurrentLanguage)
            {
                _languageOptionButton.Selected = i;
                break;
            }
        }
    }

    private void UpdateLabels()
    {
        _titleLabel.Text = Localization.Localization.T("ui.settings.title", "Settings");
        _languageLabel.Text = Localization.Localization.T("ui.settings.language", "Language");
        _backButton.Text = Localization.Localization.T("ui.settings.back", "Back");
    }

    private void OnBackPressed()
    {
        Node parent = GetParent();
        if (parent is MainMenu mainMenu)
        {
            mainMenu.ShowMainMenu();
        }

        QueueFree();
    }

    public override void _ExitTree()
    {
        Localization.Localization.OnLanguageChanged -= OnLanguageChanged;

        if (_languageOptionButton != null)
        {
            _languageOptionButton.ItemSelected -= OnLanguageSelected;
        }

        if (_backButton != null)
        {
            _backButton.Pressed -= OnBackPressed;
        }
    }
}
