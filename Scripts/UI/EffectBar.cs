using Godot;
using OdysseyCards.Core;
using OdysseyCards.Localization;
using System.Collections.Generic;

namespace OdysseyCards.UI;

/// <summary>
/// 效果图标栏——横向排列的 Emoji 图标 + 层数标签。
/// 位于英雄/随从的 HP 条下方，支持 hover tooltip。
/// Buff 使用绿色色调，Debuff 使用红色色调。
/// </summary>
public partial class EffectBar : HBoxContainer
{
    private const int IconCellSize = 22;
    private const int IconFontSize = 13;
    private const int StackFontSize = 9;
    private const int Spacing = 2;
    private const float BuffAlpha = 0.85f;
    private const float DebuffAlpha = 0.85f;

    private static readonly Color BuffBgColor = new(0.15f, 0.45f, 0.15f, BuffAlpha);
    private static readonly Color DebuffBgColor = new(0.45f, 0.12f, 0.12f, DebuffAlpha);
    private static readonly Color BuffBorderColor = new(0.25f, 0.7f, 0.25f, 0.8f);
    private static readonly Color DebuffBorderColor = new(0.7f, 0.2f, 0.2f, 0.8f);
    private static readonly Color StackTextColor = new(1f, 1f, 1f, 0.95f);

    private IReadOnlyList<DisplayableEffect> _effects = System.Array.Empty<DisplayableEffect>();
    private EffectTooltip? _activeTooltip;

    public EffectBar()
    {
        Alignment = AlignmentMode.Center;
        AddThemeConstantOverride("separation", Spacing);
    }

    /// <summary>
    /// 填充效果列表，重建所有图标。
    /// </summary>
    public void Populate(IReadOnlyList<DisplayableEffect> effects)
    {
        _effects = effects;

        // 清除旧图标
        foreach (var child in GetChildren())
        {
            if (child is EffectIcon icon)
                icon.QueueFree();
        }

        if (effects.Count == 0)
        {
            Visible = false;
            return;
        }

        Visible = true;

        // 按 SortOrder 排序后创建图标
        var sorted = new List<DisplayableEffect>(effects);
        sorted.Sort((a, b) => a.SortOrder.CompareTo(b.SortOrder));

        foreach (var effect in sorted)
        {
            var icon = new EffectIcon(effect, this);
            AddChild(icon);
        }
    }

    /// <summary>
    /// 清空所有图标并隐藏。
    /// </summary>
    public void Clear()
    {
        foreach (var child in GetChildren())
        {
            if (child is EffectIcon icon)
                icon.QueueFree();
        }
        _effects = System.Array.Empty<DisplayableEffect>();
        Visible = false;
    }

    /// <summary>
    /// 显示 tooltip 弹窗。
    /// </summary>
    internal void ShowTooltip(DisplayableEffect effect, Vector2 screenPos)
    {
        HideTooltip();

        GD.Print($"[EffectBar] ShowTooltip: {effect.Icon} {effect.Name} at ({screenPos.X:F0}, {screenPos.Y:F0})");
        _activeTooltip = new EffectTooltip(effect);
        var root = GetTree().Root;
        root.AddChild(_activeTooltip);

        // 手动计算 tooltip 尺寸：标题行约22px + 描述行约40px（含边距）
        int estimatedW = 200;
        int estimatedH = string.IsNullOrEmpty(effect.Description) ? 40 : 70;

        // 确保不超出屏幕边界
        int posX = (int)Mathf.Clamp(screenPos.X + 8, 4, root.Size.X - estimatedW - 8);
        int posY = (int)Mathf.Clamp(screenPos.Y - estimatedH - 8, 4, root.Size.Y - estimatedH - 8);

        _activeTooltip.Popup(new Rect2I(posX, posY, estimatedW, estimatedH));
    }

    /// <summary>
    /// 隐藏 tooltip。
    /// </summary>
    internal void HideTooltip()
    {
        _activeTooltip?.Hide();
        _activeTooltip?.QueueFree();
        _activeTooltip = null;
    }

    // ==================================================================
    // EffectIcon —— 单个效果图标（嵌套类）
    // ==================================================================

    private partial class EffectIcon : Control
    {
        private readonly DisplayableEffect _effect;
        private readonly EffectBar _parent;
        private readonly ColorRect _background;
        private readonly Label _iconLabel;
        private readonly Label _stackLabel;
        private bool _isHovered;

        public EffectIcon(DisplayableEffect effect, EffectBar parent)
        {
            _effect = effect;
            _parent = parent;

            CustomMinimumSize = new Vector2(IconCellSize, IconCellSize);
            MouseFilter = MouseFilterEnum.Stop;

            bool isBuff = effect.IsBuff;

            // 边框背景
            var borderStyle = new StyleBoxFlat
            {
                BgColor = isBuff ? BuffBorderColor : DebuffBorderColor,
                CornerRadiusTopLeft = 3,
                CornerRadiusTopRight = 3,
                CornerRadiusBottomLeft = 3,
                CornerRadiusBottomRight = 3,
            };
            var borderRect = new ColorRect
            {
                AnchorsPreset = (int)LayoutPreset.FullRect,
                MouseFilter = MouseFilterEnum.Ignore,
            };
            borderRect.AddThemeStyleboxOverride("panel", borderStyle);
            AddChild(borderRect);

            // 内容背景（内缩 1px）
            _background = new ColorRect
            {
                Position = new Vector2(1, 1),
                Size = new Vector2(IconCellSize - 2, IconCellSize - 2),
            };
            var bgStyle = new StyleBoxFlat
            {
                BgColor = isBuff ? BuffBgColor : DebuffBgColor,
                CornerRadiusTopLeft = 2,
                CornerRadiusTopRight = 2,
                CornerRadiusBottomLeft = 2,
                CornerRadiusBottomRight = 2,
            };
            _background.AddThemeStyleboxOverride("panel", bgStyle);
            _background.MouseFilter = MouseFilterEnum.Ignore;
            AddChild(_background);

            // Emoji 图标
            _iconLabel = new Label
            {
                Text = effect.Icon,
                Position = new Vector2(2, 1),
                Size = new Vector2(IconCellSize - 4, IconCellSize - 4),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            _iconLabel.AddThemeFontSizeOverride("font_size", IconFontSize);
            _iconLabel.MouseFilter = MouseFilterEnum.Ignore;
            AddChild(_iconLabel);

            // 右下角层数标签
            if (effect.Stacks > 0)
            {
                _stackLabel = new Label
                {
                    Text = effect.Stacks.ToString(),
                    Position = new Vector2(IconCellSize - 12, IconCellSize - 11),
                    Size = new Vector2(10, 10),
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Bottom,
                };
                _stackLabel.AddThemeColorOverride("font_color", StackTextColor);
                _stackLabel.AddThemeFontSizeOverride("font_size", StackFontSize);
                _stackLabel.MouseFilter = MouseFilterEnum.Ignore;
                AddChild(_stackLabel);
            }

            // 使用 Connect 而非 += 确保 Godot Mono 嵌套类中信号可靠投递
            Connect(Control.SignalName.MouseEntered, Callable.From(() => OnMouseEnter()));
            Connect(Control.SignalName.MouseExited, Callable.From(() => OnMouseExit()));
        }

        public override void _GuiInput(InputEvent @event)
        {
            if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left && !mb.Pressed)
            {
                // 不处理点击，仅用于确认事件可达
            }
        }

        public override void _Notification(int what)
        {
            if (what == NotificationMouseEnter)
            {
                OnMouseEnter();
            }
            else if (what == NotificationMouseExit)
            {
                OnMouseExit();
            }
        }

        private void OnMouseEnter()
        {
            GD.Print($"[EffectBar] MouseEntered on {_effect.Icon} {_effect.Name}");
            _isHovered = true;
            Scale = new Vector2(1.15f, 1.15f);
            var globalPos = GetGlobalMousePosition();
            _parent.ShowTooltip(_effect, globalPos);
        }

        private void OnMouseExit()
        {
            _isHovered = false;
            Scale = new Vector2(1f, 1f);
            _parent.HideTooltip();
        }
    }

    // ==================================================================
    // EffectTooltip —— Hover 提示弹窗（嵌套类）
    // ==================================================================

    private partial class EffectTooltip : PopupPanel
    {
        public EffectTooltip(DisplayableEffect effect)
        {
            // PopupPanel 自动渲染在最顶层，无需 MouseFilter 或 ZIndex
            var style = new StyleBoxFlat
            {
                BgColor = new Color(0.08f, 0.08f, 0.1f, 0.92f),
                BorderWidthLeft = 1,
                BorderWidthRight = 1,
                BorderWidthTop = 1,
                BorderWidthBottom = 1,
                BorderColor = effect.IsBuff ? BuffBorderColor : DebuffBorderColor,
                CornerRadiusTopLeft = 4,
                CornerRadiusTopRight = 4,
                CornerRadiusBottomLeft = 4,
                CornerRadiusBottomRight = 4,
            };
            AddThemeStyleboxOverride("panel", style);

            var vbox = new VBoxContainer { Name = "TooltipContent" };
            AddChild(vbox);

            // 标题行：图标 + 名称 + 层数
            var titleRow = new HBoxContainer();
            var iconLabel = new Label
            {
                Text = effect.Icon,
            };
            iconLabel.AddThemeFontSizeOverride("font_size", 12);
            titleRow.AddChild(iconLabel);

            var nameLabel = new Label
            {
                Text = effect.Stacks > 0
                    ? $"{effect.Name} ×{effect.Stacks}"
                    : effect.Name,
            };
            nameLabel.AddThemeColorOverride("font_color", effect.IsBuff
                ? new Color(0.5f, 1f, 0.5f)
                : new Color(1f, 0.5f, 0.5f));
            nameLabel.AddThemeFontSizeOverride("font_size", 13);
            titleRow.AddChild(nameLabel);
            vbox.AddChild(titleRow);

            // 描述
            if (!string.IsNullOrEmpty(effect.Description))
            {
                var descLabel = new Label
                {
                    Text = effect.Description,
                    CustomMinimumSize = new Vector2(160, 0),
                    AutowrapMode = TextServer.AutowrapMode.Word,
                };
                descLabel.AddThemeColorOverride("font_color", new Color(0.75f, 0.75f, 0.8f));
                descLabel.AddThemeFontSizeOverride("font_size", 10);
                vbox.AddChild(descLabel);
            }
        }
    }
}
