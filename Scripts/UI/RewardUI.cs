using Godot;
using OdysseyCards.Core;
using System;
using System.Collections.Generic;
using Loc = OdysseyCards.Localization.Localization;

namespace OdysseyCards.UI;

/// <summary>
/// 战后卡牌奖励选择覆盖层。
/// 全屏半透明遮罩 + 居中标题 + 3 个奖励卡牌包（每包 = 1 张卡 × N 张同名）。
/// 点击选取 / 右键跳过 / 按钮跳过。
/// 奖励通过 GameManager 发放：解锁卡牌 + 加入当前牌堆 + 保存。
/// </summary>
public partial class RewardUI : Control
{
    // ===== 子控件 =====

    private ColorRect _background = null!;
    private Label _titleLabel = null!;
    private HBoxContainer _bundlesContainer = null!;
    private Button? _skipButton;

    // ===== 状态 =====

    private sealed class BundleState
    {
        public required CardData CardData;
        public int CopyCount;
        public CardUI CardUI = null!;
        public Label CountLabel = null!;
    }

    private readonly List<BundleState> _bundles = new();
    private bool _isShowing;
    private ulong _openedTicks;

    /// <summary>
    /// 打开后 350ms 内忽略点击，防止误触。
    /// </summary>
    private const ulong ClickProtectionMs = 350;

    // ===== 公开 API =====

    /// <summary>
    /// 奖励选取完成时触发（选取后自毁）。
    /// </summary>
    public event Action? OnRewardCompleted;

    /// <summary>
    /// 生成 3 组奖励并显示 UI。
    /// 如果没有符合条件的卡牌则直接触发 OnRewardCompleted 并跳过。
    /// </summary>
    public void ShowRewards()
    {
        // 获取可用奖励池
        var eligible = GameManager.Instance.GetRewardEligibleCards();
        if (eligible.Count == 0)
        {
            GD.Print("[RewardUI] 无可用奖励卡牌，跳过");
            OnRewardCompleted?.Invoke();
            QueueFree();
            return;
        }

        // Fisher-Yates 洗牌，取最多 3 张
        Shuffle(eligible);
        int count = Mathf.Min(eligible.Count, 3);

        for (int i = 0; i < count; i++)
        {
            var cardData = eligible[i];
            _bundles.Add(new BundleState
            {
                CardData = cardData,
                CopyCount = cardData.Rarity.GetMaxRewardCopies(),
            });
        }

        GD.Print($"[RewardUI] 生成 {_bundles.Count} 组奖励");

        _isShowing = true;
        _openedTicks = Time.GetTicksMsec();

        BuildLayout();
        PlayEntryAnimation();

        GameManager.Instance.LanguageChanged += OnLanguageChanged;
    }

    // ===== 洗牌 =====

    private static void Shuffle<T>(List<T> list)
    {
        var rng = new Random();
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    // ===== 布局构建 =====

    private void BuildLayout()
    {
        float s = UIScaler.Instance?.GetScaleFactor() ?? 1f;

        // 全屏覆盖层，拦截所有鼠标事件
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        ZIndex = 300;

        // 半透明暗色背景
        _background = new ColorRect
        {
            Color = new Color(0, 0, 0, 0.8f),
        };
        _background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _background.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(_background);

        // 居中根容器
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
            Text = Loc.T("ui.reward.title", "选择奖励卡牌"),
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

        // 奖励包行（水平居中排列）
        _bundlesContainer = new HBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center,
        };
        _bundlesContainer.AddThemeConstantOverride("separation", Mathf.RoundToInt(20 * s));
        _bundlesContainer.MouseFilter = MouseFilterEnum.Ignore;
        center.AddChild(_bundlesContainer);

        foreach (var bundle in _bundles)
        {
            // 每包一个垂直容器：卡牌 + 数量标签
            var bundleBox = new VBoxContainer();
            bundleBox.Alignment = BoxContainer.AlignmentMode.Center;
            bundleBox.MouseFilter = MouseFilterEnum.Ignore;

            // 卡牌
            var card = new Card.Card(bundle.CardData);
            var cardUI = new CardUI();
            cardUI.Name = $"RewardCard_{bundle.CardData.Id}";
            cardUI.SetCard(card);
            cardUI.CustomMinimumSize = new Vector2(130 * s, 195 * s);

            // 入场前透明（动画渐变显示）
            cardUI.Modulate = new Color(1, 1, 1, 0);

            // 点击选取
            cardUI.OnCardClicked += OnCardClicked;

            bundle.CardUI = cardUI;
            bundleBox.AddChild(cardUI);

            // 数量 + 稀有度标签
            string rarityName = GetLocalizedRarityName(bundle.CardData.Rarity);
            string labelText = BuildCopyLabel(bundle.CopyCount, rarityName);

            var countLabel = new Label
            {
                Text = labelText,
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            countLabel.AddThemeColorOverride("font_color", new Color(1, 1, 1, 0.7f));
            countLabel.AddThemeFontSizeOverride("font_size", Mathf.RoundToInt(14 * s));
            countLabel.MouseFilter = MouseFilterEnum.Ignore;

            bundle.CountLabel = countLabel;
            bundleBox.AddChild(countLabel);

            _bundlesContainer.AddChild(bundleBox);
        }

        // 间距
        var spacer2 = new Control { CustomMinimumSize = new Vector2(0, 20 * s) };
        spacer2.MouseFilter = MouseFilterEnum.Ignore;
        center.AddChild(spacer2);

        // 跳过按钮
        _skipButton = new Button
        {
            Text = Loc.T("ui.reward.skip", "跳过"),
            CustomMinimumSize = new Vector2(120 * s, 38 * s),
        };
        _skipButton.AddThemeFontSizeOverride("font_size", Mathf.RoundToInt(16 * s));
        _skipButton.Pressed += OnSkipPressed;
        center.AddChild(_skipButton);
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

        // 卡牌依次浮现
        float cardDelay = 0.06f;
        for (int i = 0; i < _bundles.Count; i++)
        {
            var card = _bundles[i].CardUI;
            tween.TweenProperty(card, "modulate:a", 1f, 0.25).SetDelay(cardDelay * i);
        }
    }

    // ===== 事件处理 =====

    private void OnCardClicked(CardUI cardUI)
    {
        if (!_isShowing) return;

        // 立即终止拖拽
        cardUI.CancelDragSilent();

        // 350ms 点击保护
        if (Time.GetTicksMsec() - _openedTicks < ClickProtectionMs)
        {
            GD.Print("[RewardUI] 点击太快，忽略（350ms 保护）");
            return;
        }

        var bundle = _bundles.Find(b => b.CardUI == cardUI);
        if (bundle == null) return;

        GD.Print($"[RewardUI] 玩家选择了：{bundle.CardData.GetLocalizedName()} ×{bundle.CopyCount}");

        // 视觉反馈：高亮选中的卡牌
        cardUI.Modulate = new Color(1, 0.85f, 0.3f, 1);

        _isShowing = false;
        ApplyReward(bundle);
    }

    private void OnSkipPressed()
    {
        if (!_isShowing) return;

        GD.Print("[RewardUI] 玩家跳过了奖励");

        _isShowing = false;
        OnRewardCompleted?.Invoke();
        QueueFree();
    }

    // ===== 奖励发放 =====

    /// <summary>
    /// 应用奖励：解锁卡牌 → 加入牌堆 → 保存。
    /// </summary>
    private void ApplyReward(BundleState bundle)
    {
        GameManager.Instance.UnlockCard(bundle.CardData.Id);

        for (int i = 0; i < bundle.CopyCount; i++)
        {
            GameManager.Instance.AddCardToDeckInCombat(bundle.CardData);
        }

        GameManager.Instance.SaveToDisk();

        GD.Print($"[RewardUI] 奖励已应用：{bundle.CardData.GetLocalizedName()} ×{bundle.CopyCount}");

        OnRewardCompleted?.Invoke();
        QueueFree();
    }

    // ===== 本地化辅助 =====

    /// <summary>
    /// 获取稀有度的本地化显示名称。
    /// </summary>
    private static string GetLocalizedRarityName(CardRarity rarity)
    {
        return rarity switch
        {
            CardRarity.Common => Loc.T("ui.reward.rarity_common", "普通"),
            CardRarity.Good => Loc.T("ui.reward.rarity_good", "良好"),
            CardRarity.Excellent => Loc.T("ui.reward.rarity_excellent", "极佳"),
            CardRarity.Master => Loc.T("ui.reward.rarity_master", "大师级"),
            _ => Loc.T("ui.reward.rarity_unknown", "未知"),
        };
    }

    /// <summary>
    /// 构建「×N — 稀有度」格式的标签文本。
    /// </summary>
    private static string BuildCopyLabel(int copyCount, string rarityName)
    {
        return Loc.T("ui.reward.copy_format", "×{count} — {rarity}")
            .Replace("{count}", copyCount.ToString())
            .Replace("{rarity}", rarityName);
    }

    /// <summary>
    /// 语言切换时刷新所有文本。
    /// </summary>
    private void OnLanguageChanged(string lang)
    {
        _titleLabel.Text = Loc.T("ui.reward.title", "选择奖励卡牌");
        if (_skipButton != null)
            _skipButton.Text = Loc.T("ui.reward.skip", "跳过");

        foreach (var bundle in _bundles)
        {
            if (bundle.CountLabel == null) continue;
            string rarityName = GetLocalizedRarityName(bundle.CardData.Rarity);
            bundle.CountLabel.Text = BuildCopyLabel(bundle.CopyCount, rarityName);
        }
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
