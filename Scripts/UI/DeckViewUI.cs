using System.Collections.Generic;
using Godot;
using OdysseyCards.Core;

namespace OdysseyCards.UI;

public partial class DeckViewUI : Control
{
    private Panel _backgroundPanel;
    private VBoxContainer _mainContainer;
    private Label _titleLabel;
    private ScrollContainer _scrollContainer;
    private VBoxContainer _cardListContainer;
    private Button _closeButton;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        CreateUI();
        Visible = false;
    }

    public void ShowDeckList(string title, List<Card.Card> cards)
    {
        if (_titleLabel != null)
        {
            _titleLabel.Text = title;
        }

        PopulateCards(cards);
        Visible = true;
    }

    public new void Hide()
    {
        Visible = false;
    }

    private void CreateUI()
    {
        var overlay = new ColorRect
        {
            Color = new Color(0f, 0f, 0f, 0.6f),
            MouseFilter = MouseFilterEnum.Stop
        };
        overlay.SetAnchorsPreset(LayoutPreset.FullRect);
        overlay.GuiInput += (InputEvent evt) =>
        {
            if (evt is InputEventMouseButton mouse && mouse.ButtonIndex == MouseButton.Left && mouse.Pressed)
            {
                Hide();
            }
        };
        AddChild(overlay);

        _backgroundPanel = new Panel
        {
            CustomMinimumSize = new Vector2(400, 500)
        };
        _backgroundPanel.SetAnchorsPreset(LayoutPreset.Center);
        _backgroundPanel.OffsetLeft = -200;
        _backgroundPanel.OffsetTop = -250;
        _backgroundPanel.OffsetRight = 200;
        _backgroundPanel.OffsetBottom = 250;
        AddChild(_backgroundPanel);

        var panelStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.15f, 0.15f, 0.18f),
            BorderColor = new Color(0.4f, 0.4f, 0.45f),
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8
        };
        _backgroundPanel.AddThemeStyleboxOverride("panel", panelStyle);

        _mainContainer = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.Expand | SizeFlags.Fill,
            SizeFlagsVertical = SizeFlags.Expand | SizeFlags.Fill
        };
        _mainContainer.SetAnchorsPreset(LayoutPreset.FullRect);
        _mainContainer.OffsetLeft = 15;
        _mainContainer.OffsetTop = 15;
        _mainContainer.OffsetRight = -15;
        _mainContainer.OffsetBottom = -15;
        _backgroundPanel.AddChild(_mainContainer);

        var titleContainer = new HBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.Expand | SizeFlags.Fill
        };
        _mainContainer.AddChild(titleContainer);

        _titleLabel = new Label
        {
            Text = "Card List",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            SizeFlagsHorizontal = SizeFlags.Expand | SizeFlags.Fill,
            LabelSettings = new LabelSettings
            {
                FontColor = new Color(1f, 1f, 1f),
                FontSize = 20
            }
        };
        titleContainer.AddChild(_titleLabel);

        _closeButton = new Button
        {
            Text = "X",
            CustomMinimumSize = new Vector2(32, 32)
        };
        _closeButton.Pressed += OnClosePressed;
        titleContainer.AddChild(_closeButton);

        var separator = new HSeparator
        {
            CustomMinimumSize = new Vector2(0, 2)
        };
        _mainContainer.AddChild(separator);

        _scrollContainer = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.Expand | SizeFlags.Fill,
            SizeFlagsVertical = SizeFlags.Expand | SizeFlags.Fill
        };
        _mainContainer.AddChild(_scrollContainer);

        _cardListContainer = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.Expand | SizeFlags.Fill
        };
        _scrollContainer.AddChild(_cardListContainer);
    }

    private void PopulateCards(List<Card.Card> cards)
    {
        if (_cardListContainer == null)
        {
            return;
        }

        foreach (Node child in _cardListContainer.GetChildren())
        {
            child.QueueFree();
        }

        if (cards == null || cards.Count == 0)
        {
            var emptyLabel = new Label
            {
                Text = "Empty",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                SizeFlagsHorizontal = SizeFlags.Expand | SizeFlags.Fill,
                SizeFlagsVertical = SizeFlags.Expand | SizeFlags.Fill,
                LabelSettings = new LabelSettings
                {
                    FontColor = new Color(0.6f, 0.6f, 0.6f),
                    FontSize = 16
                }
            };
            _cardListContainer.AddChild(emptyLabel);
            return;
        }

        foreach (Card.Card card in cards)
        {
            HBoxContainer cardItem = CreateCardItem(card);
            _cardListContainer.AddChild(cardItem);
        }
    }

    private HBoxContainer CreateCardItem(Card.Card card)
    {
        var itemContainer = new HBoxContainer
        {
            CustomMinimumSize = new Vector2(0, 36),
            SizeFlagsHorizontal = SizeFlags.Expand | SizeFlags.Fill
        };

        var costLabel = new Label
        {
            CustomMinimumSize = new Vector2(50, 0),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            LabelSettings = new LabelSettings
            {
                FontColor = new Color(0.9f, 0.8f, 0.3f),
                FontSize = 14
            }
        };

        if (card is Card.Unit unit)
        {
            costLabel.Text = $"{unit.DeployCost}K";
        }
        else if (card is Card.Order order)
        {
            costLabel.Text = $"{order.Cost}K";
        }
        else
        {
            costLabel.Text = "-";
        }
        itemContainer.AddChild(costLabel);

        Color typeColor = card.Type switch
        {
            CardType.Unit => new Color(0.3f, 0.7f, 0.4f),
            CardType.Order => new Color(0.7f, 0.5f, 0.3f),
            _ => new Color(0.5f, 0.5f, 0.55f)
        };

        var typeLabel = new Label
        {
            CustomMinimumSize = new Vector2(70, 0),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Text = card.Type.ToString().ToUpper(),
            LabelSettings = new LabelSettings
            {
                FontColor = typeColor,
                FontSize = 12
            }
        };
        itemContainer.AddChild(typeLabel);

        var nameLabel = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            SizeFlagsHorizontal = SizeFlags.Expand | SizeFlags.Fill,
            Text = card.CardName,
            LabelSettings = new LabelSettings
            {
                FontColor = new Color(1f, 1f, 1f),
                FontSize = 14
            }
        };
        itemContainer.AddChild(nameLabel);

        var itemBg = new ColorRect
        {
            Color = new Color(0.2f, 0.2f, 0.22f),
            MouseFilter = MouseFilterEnum.Ignore
        };
        itemBg.SetAnchorsPreset(LayoutPreset.FullRect);
        itemContainer.AddChild(itemBg);
        itemContainer.MoveChild(itemBg, 0);

        return itemContainer;
    }

    private void OnClosePressed()
    {
        Hide();
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey key && key.Pressed && key.Keycode == Key.Escape)
        {
            if (Visible)
            {
                Hide();
                GetViewport().SetInputAsHandled();
            }
        }
    }
}
