using Godot;
using OdysseyCards.Relic;
using System.Collections.Generic;

namespace OdysseyCards.UI;

/// <summary>
/// 藏品栏 UI——显示在战斗界面的藏品图标列表。
/// 以小型图标+提示的形式排列，供玩家查看当前持有的藏品。
/// </summary>
public partial class RelicBar : HBoxContainer
{
    private RelicManager? _relics;
    private readonly List<RelicIcon> _icons = new();

    public override void _Ready()
    {
        Alignment = AlignmentMode.Center;
    }

    /// <summary>
    /// 绑定藏品管理器并刷新显示。
    /// </summary>
    public void Bind(RelicManager relics)
    {
        _relics = relics;
        Refresh();
    }

    /// <summary>
    /// 刷新藏品列表显示（diff 式更新）。
    /// </summary>
    public void Refresh()
    {
        if (_relics == null) return;

        // 清除旧图标
        foreach (var icon in _icons)
            icon.QueueFree();
        _icons.Clear();

        // 创建新图标
        foreach (var relic in _relics.Relics)
        {
            var icon = new RelicIcon(relic);
            AddChild(icon);
            _icons.Add(icon);
        }
    }

    /// <summary>
    /// 单个藏品图标——彩色圆点 + 名称标签。
    /// </summary>
    private partial class RelicIcon : HBoxContainer
    {
        private readonly AbstractRelic _relic;

        public RelicIcon(AbstractRelic relic)
        {
            _relic = relic;

            // 颜色圆点
            var dot = new ColorRect();
            dot.CustomMinimumSize = new Vector2(10, 10);
            dot.Color = relic.IsNegative
                ? new Color(1.0f, 0.3f, 0.3f)   // 红色 = 负面
                : relic.IsSubtle
                    ? new Color(1.0f, 0.8f, 0.3f) // 黄 = 微妙
                    : new Color(0.3f, 0.8f, 0.3f); // 绿 = 正面
            AddChild(dot);

            // 名称（缩写）
            var label = new Label();
            label.Text = relic.Name.Length > 3 ? relic.Name[..3] : relic.Name;
            label.AddThemeFontSizeOverride("font_size", 10);
            AddChild(label);

            // 提示
            TooltipText = $"{relic.Name}\n{relic.Description}";
        }
    }
}
