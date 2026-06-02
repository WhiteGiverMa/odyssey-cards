using Godot;
using OdysseyCards.Core;
using System;
using System.Collections.Generic;
using Loc = OdysseyCards.Localization.Localization;

namespace OdysseyCards.UI;

/// <summary>
/// 发现（N选1）选牌覆盖层。
/// 全屏半透明遮罩 + 居中标题 + N张候选卡牌 + 跳过按钮。
/// 参考炉石「发现」和杀戮尖塔「选牌」界面（STS2 NChooseACardSelectionScreen）。
/// </summary>
public partial class DiscoverUI : Control
{
    // ===== 子控件 =====

    private ColorRect _background = null!;
    private Label _titleLabel = null!;
    private HBoxContainer _cardsContainer = null!;
    private Button? _skipButton;
    private Button? _confirmButton;

    // ===== 状态 =====

    private readonly List<CardUI> _cardUIs = new();
    private Action<CardData?>? _onChosen;
    private Action<IReadOnlyList<Card.Card>>? _onCardsChosen;
    private readonly List<CardUI> _selectedCardUIs = new();
    private bool _isShowing;
    private ulong _openedTicks;
    private int _pickCount = 1;

    /// <summary>
    /// STS2 模式：打开后 350ms 内忽略点击，防止误触。
    /// </summary>
    private const ulong ClickProtectionMs = 350;

    /// <summary>
    /// 自定义标题。如果设置了，则覆盖默认的本地化标题。
    /// 用于弃牌选择等场景（如刀盾危机、主动弃牌）。
    /// </summary>
    public string? CustomTitle { get; set; }

    // ===== 公开 API =====

    /// <summary>
    /// 显示选牌界面。
    /// </summary>
    /// <param name="cards">N 张候选卡牌数据</param>
    /// <param name="canSkip">是否显示「跳过」按钮</param>
    /// <param name="onChosen">选择回调，null 表示跳过</param>
    public void ShowCards(IReadOnlyList<CardData> cards, bool canSkip, Action<CardData?> onChosen)
    {
        _onChosen = onChosen;
        _onCardsChosen = null;
        _pickCount = 1;
        _isShowing = true;
        _openedTicks = Time.GetTicksMsec();
        Show();

        var runtimeCards = new List<Card.Card>();
        foreach (var cardData in cards)
            runtimeCards.Add(new Card.Card(cardData));

        BuildLayout(runtimeCards, canSkip);
        PlayEntryAnimation();

        // 订阅语言变更事件
        GameManager.Instance.LanguageChanged += OnLanguageChanged;
    }

    /// <summary>
    /// 显示可多选的选牌界面。
    /// </summary>
    public void ShowCards(IReadOnlyList<Card.Card> cards, int pickCount, bool canSkip, Action<IReadOnlyList<Card.Card>> onChosen)
    {
        _onChosen = null;
        _onCardsChosen = onChosen;
        _pickCount = Math.Max(1, pickCount);
        _isShowing = true;
        _openedTicks = Time.GetTicksMsec();
        Show();

        BuildLayout(cards, canSkip);
        PlayEntryAnimation();

        GameManager.Instance.LanguageChanged += OnLanguageChanged;
    }

    // ===== 布局构建 =====

    private void BuildLayout(IReadOnlyList<Card.Card> cards, bool canSkip)
    {
        ClearExistingLayout();
        float s = UIScaler.Instance?.GetScaleFactor() ?? 1f;

        // 全屏覆盖层，拦截所有鼠标事件
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        ZIndex = 200;

        // 半透明暗色背景
        _background = new ColorRect
        {
            Color = new Color(0, 0, 0, 0.8f),
        };
        _background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _background.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(_background);

        // 居中根容器（参考 PauseMenu：用 CenterContainer 而非 VBoxContainer 的 Center 预设）
        var root = new CenterContainer();
        root.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        root.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(root);

        // 垂直布局：标题 + 间距 + 卡牌行 + 间距 + 跳过按钮
        var center = new VBoxContainer();
        center.Alignment = BoxContainer.AlignmentMode.Center;
        center.MouseFilter = MouseFilterEnum.Ignore;
        root.AddChild(center);

        // 标题
        _titleLabel = new Label
        {
            Text = CustomTitle ?? (_pickCount > 1
                ? Loc.T("ui.discover.pick_count", "选择 {count} 张").Replace("{count}", _pickCount.ToString())
                : Loc.T("ui.discover.title", "发现")),
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        _titleLabel.AddThemeColorOverride("font_color", new Color(1, 1, 1, 0.9f));
        _titleLabel.AddThemeFontSizeOverride("font_size", Mathf.RoundToInt(28 * s));
        _titleLabel.MouseFilter = MouseFilterEnum.Ignore;
        center.AddChild(_titleLabel);

        // 间距
        var spacer1 = new Control { CustomMinimumSize = new Vector2(0, 24 * s) };
        spacer1.MouseFilter = MouseFilterEnum.Ignore;
        center.AddChild(spacer1);

        // 卡牌行（水平居中排列）
        // 参考 STS2 间距公式：Vector2.Left * (count - 1) * 340f * 0.5f
        _cardsContainer = new HBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center,
        };
        _cardsContainer.AddThemeConstantOverride("separation", Mathf.RoundToInt(20 * s));
        _cardsContainer.MouseFilter = MouseFilterEnum.Ignore;
        center.AddChild(_cardsContainer);

        foreach (var card in cards)
        {
            var cardUI = new CardUI();
            cardUI.Name = $"DiscoverCard_{card.Id}";
            cardUI.SetCard(card);
            cardUI.CustomMinimumSize = new Vector2(130 * s, 195 * s);

            // 入场前透明（动画渐变显示）
            cardUI.Modulate = new Color(1, 1, 1, 0);

            // 点击选取
            cardUI.OnCardClicked += OnCardClicked;

            _cardUIs.Add(cardUI);
            _cardsContainer.AddChild(cardUI);
        }

        // 间距
        var spacer2 = new Control { CustomMinimumSize = new Vector2(0, 20 * s) };
        spacer2.MouseFilter = MouseFilterEnum.Ignore;
        center.AddChild(spacer2);

        // 跳过按钮
        if (canSkip)
        {
            _skipButton = new Button
            {
                Text = Loc.T("ui.discover.skip", "跳过"),
                CustomMinimumSize = new Vector2(120 * s, 38 * s),
            };
            _skipButton.AddThemeFontSizeOverride("font_size", Mathf.RoundToInt(16 * s));
            _skipButton.Pressed += OnSkipPressed;
            center.AddChild(_skipButton);
        }

        if (_pickCount > 1)
        {
            _confirmButton = new Button
            {
                Text = Loc.T("ui.discover.confirm", "确认"),
                CustomMinimumSize = new Vector2(120 * s, 38 * s),
                Disabled = true,
            };
            _confirmButton.AddThemeFontSizeOverride("font_size", Mathf.RoundToInt(16 * s));
            _confirmButton.Pressed += OnConfirmPressed;
            center.AddChild(_confirmButton);
        }
    }

    private void ClearExistingLayout()
    {
        foreach (Node child in GetChildren())
            child.QueueFree();

        _cardUIs.Clear();
        _selectedCardUIs.Clear();
        _skipButton = null;
        _confirmButton = null;
    }

    // ===== 入场动画 =====

    /// <summary>
    /// 入场动画：背景淡入 + 卡牌依次浮现。
    /// </summary>
    private void PlayEntryAnimation()
    {
        var tween = CreateTween();
        tween.SetParallel(true);

        // 背景从透明淡入
        tween.TweenProperty(_background, "color:a", 0.8f, 0.25);

        // 卡牌依次浮现（STS2 风格：从黑色淡入 + 延迟错开）
        float cardDelay = 0.06f;
        for (int i = 0; i < _cardUIs.Count; i++)
        {
            var card = _cardUIs[i];
            tween.TweenProperty(card, "modulate:a", 1f, 0.25).SetDelay(cardDelay * i);
        }
    }

    // ===== 事件处理 =====

    private void OnCardClicked(CardUI cardUI)
    {
        if (!_isShowing) return;

        // 立即终止拖拽（CardUI.StartDrag 已将 MouseFilter 设为 Ignore）
        cardUI.CancelDragSilent();

        // 350ms 点击保护（参考 STS2 NChooseACardSelectionScreen）
        if (Time.GetTicksMsec() - _openedTicks < ClickProtectionMs)
        {
            GD.Print("[DiscoverUI] 点击太快，忽略（350ms 保护）");
            return;
        }

        if (_pickCount > 1)
        {
            ToggleCardSelection(cardUI);
            return;
        }

        var chosen = cardUI.Card?.Data;
        GD.Print($"[DiscoverUI] 玩家选择了：{chosen?.GetLocalizedName() ?? "(null)"}");

        cardUI.Modulate = new Color(1, 0.85f, 0.3f, 1);

        _isShowing = false;
        var callback = _onChosen;
        _onChosen = null;
        callback?.Invoke(chosen);
    }

    private void ToggleCardSelection(CardUI cardUI)
    {
        if (_selectedCardUIs.Remove(cardUI))
        {
            cardUI.Modulate = new Color(1, 1, 1, 1);
        }
        else
        {
            if (_selectedCardUIs.Count >= _pickCount) return;
            _selectedCardUIs.Add(cardUI);
            cardUI.Modulate = new Color(1, 0.85f, 0.3f, 1);
        }

        if (_confirmButton != null)
            _confirmButton.Disabled = _selectedCardUIs.Count != _pickCount;
    }

    private void OnConfirmPressed()
    {
        if (!_isShowing || _selectedCardUIs.Count != _pickCount) return;

        var chosenCards = new List<Card.Card>();
        foreach (var cardUI in _selectedCardUIs)
        {
            if (cardUI.Card != null)
                chosenCards.Add(cardUI.Card);
        }

        GD.Print($"[DiscoverUI] 玩家确认选择 {chosenCards.Count} 张牌");
        _isShowing = false;
        var callback = _onCardsChosen;
        _onCardsChosen = null;
        callback?.Invoke(chosenCards);
    }

    private void OnSkipPressed()
    {
        if (!_isShowing) return;

        GD.Print("[DiscoverUI] 玩家跳过选牌");

        _isShowing = false;
        var callback = _onChosen;
        var cardsCallback = _onCardsChosen;
        _onChosen = null;
        _onCardsChosen = null;
        callback?.Invoke(null);
        cardsCallback?.Invoke(Array.Empty<Card.Card>());
    }

    private void OnLanguageChanged(string lang)
    {
        _titleLabel.Text = CustomTitle ?? (_pickCount > 1
            ? Loc.T("ui.discover.pick_count", "选择 {count} 张").Replace("{count}", _pickCount.ToString())
            : Loc.T("ui.discover.title", "发现"));
        if (_skipButton != null)
            _skipButton.Text = Loc.T("ui.discover.skip", "跳过");
        if (_confirmButton != null)
            _confirmButton.Text = Loc.T("ui.discover.confirm", "确认");
    }

    // ===== 生命周期 =====

    public override void _ExitTree()
    {
        GameManager.Instance.LanguageChanged -= OnLanguageChanged;
    }

    /// <summary>
    /// 右键取消（等效跳过）。
    /// </summary>
    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mb
            && mb.Pressed
            && mb.ButtonIndex == MouseButton.Right)
        {
            OnSkipPressed();
            AcceptEvent();
        }
    }
}
