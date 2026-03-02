using Godot;
using OdysseyCards.Character;

namespace OdysseyCards.UI;

public partial class EnemyUI : Control
{
    private Enemy _enemy;
    private Label _nameLabel;
    private Label _hqHealthLabel;
    private Panel _panel;

    public Enemy Enemy => _enemy;

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(150, 100);
        MouseFilter = MouseFilterEnum.Stop;
        AddToGroup("Enemy");

        CreateUI();
    }

    private void CreateUI()
    {
        _panel = new Panel
        {
            SizeFlagsHorizontal = SizeFlags.Expand | SizeFlags.Fill,
            SizeFlagsVertical = SizeFlags.Expand | SizeFlags.Fill
        };
        _panel.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(_panel);

        var styleBox = new StyleBoxFlat
        {
            BgColor = new Color(0.3f, 0.15f, 0.15f),
            BorderColor = new Color(0.6f, 0.2f, 0.2f),
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8
        };
        _panel.AddThemeStyleboxOverride("panel", styleBox);

        var vbox = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.Expand | SizeFlags.Fill,
            SizeFlagsVertical = SizeFlags.Expand | SizeFlags.Fill
        };
        vbox.SetAnchorsPreset(LayoutPreset.FullRect);
        vbox.OffsetLeft = 10;
        vbox.OffsetTop = 5;
        vbox.OffsetRight = -10;
        vbox.OffsetBottom = -5;
        _panel.AddChild(vbox);

        _nameLabel = new Label
        {
            Text = "Enemy",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            SizeFlagsHorizontal = SizeFlags.Expand | SizeFlags.Fill
        };
        _nameLabel.AddThemeFontSizeOverride("font_size", 16);
        _nameLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.3f, 0.3f));
        vbox.AddChild(_nameLabel);

        _hqHealthLabel = new Label
        {
            Text = "HQ: 8/8",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            SizeFlagsHorizontal = SizeFlags.Expand | SizeFlags.Fill
        };
        _hqHealthLabel.AddThemeFontSizeOverride("font_size", 14);
        _hqHealthLabel.AddThemeColorOverride("font_color", Colors.White);
        vbox.AddChild(_hqHealthLabel);

        var handLabel = new Label
        {
            Text = "Hand: 0",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            SizeFlagsHorizontal = SizeFlags.Expand | SizeFlags.Fill
        };
        handLabel.AddThemeFontSizeOverride("font_size", 12);
        handLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.7f));
        vbox.AddChild(handLabel);
    }

    public void SetEnemy(Enemy enemy)
    {
        _enemy = enemy;
        SetMeta("EnemyObject", enemy);
        UpdateDisplay();

        if (_enemy != null)
        {
            _enemy.OnHandChanged += UpdateDisplay;
            _enemy.OnHealthChanged += OnEnemyHealthChanged;
            _enemy.OnHQHealthChanged += OnEnemyHQHealthChanged;
        }
    }

    private void OnEnemyHealthChanged(int current, int max)
    {
        UpdateDisplay();
    }

    private void OnEnemyHQHealthChanged(int current, int max)
    {
        UpdateDisplay();
    }

    public void UpdateDisplay()
    {
        if (_enemy == null) return;

        if (_nameLabel != null)
        {
            _nameLabel.Text = _enemy.CharacterName;
        }

        if (_hqHealthLabel != null)
        {
            _hqHealthLabel.Text = $"HQ: {_enemy.HQCurrentHealth}/{_enemy.HQMaxHealth}";
        }
    }

    public override void _ExitTree()
    {
        if (_enemy != null)
        {
            _enemy.OnHandChanged -= UpdateDisplay;
            _enemy.OnHealthChanged -= OnEnemyHealthChanged;
            _enemy.OnHQHealthChanged -= OnEnemyHQHealthChanged;
        }
    }
}
