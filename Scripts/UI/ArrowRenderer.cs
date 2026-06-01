using System.Collections.Generic;
using Godot;

namespace OdysseyCards.UI;

/// <summary>
/// 箭头渲染器——在 Control 层上用 _Draw() 方法绘制带箭头的直线。
/// 用于炉石传说风格卡牌对战中的攻击选择箭头、敌方意图箭头、增益箭头等。
/// 纯渲染组件，不包含位置计算逻辑。
/// </summary>
public partial class ArrowRenderer : Control
{
    // ===== 预设颜色常量 =====

    /// <summary>攻击选择箭头——亮橙色</summary>
    public static readonly Color AttackSelectColor = new(1f, 0.55f, 0f);

    /// <summary>敌方攻击意图箭头——红色</summary>
    public static readonly Color EnemyAttackColor = new(1f, 0.15f, 0.15f);

    /// <summary>增益意图箭头——蓝色</summary>
    public static readonly Color BuffColor = new(0.3f, 0.5f, 1f);

    // ===== 箭头头部尺寸常量 =====

    private const float DefaultWidth = 3f;
    private const float HeadLength = 15f;
    private const float HeadWidth = 5f;

    // ===== 数据结构 =====

    /// <summary>
    /// 单条箭头的运行时数据。
    /// </summary>
    private struct ArrowData
    {
        public string Key;
        public Vector2 From;
        public Vector2 To;
        public Color Color;
        public float Width;
    }

    // ===== 内部状态 =====

    private readonly Dictionary<string, ArrowData> _arrows = new();

    // ===== 公共 API =====

    /// <summary>
    /// 添加或更新一条箭头。
    /// </summary>
    /// <param name="key">唯一标识键</param>
    /// <param name="from">起始位置</param>
    /// <param name="to">终止位置（箭头尖端指向）</param>
    /// <param name="color">箭头颜色，默认为攻击选择橙色</param>
    /// <param name="width">线条宽度，默认 3.0</param>
    public void AddArrow(string key, Vector2 from, Vector2 to, Color? color = null, float width = DefaultWidth)
    {
        _arrows[key] = new ArrowData
        {
            Key = key,
            From = from,
            To = to,
            Color = color ?? AttackSelectColor,
            Width = width,
        };
        QueueRedraw();
    }

    /// <summary>
    /// 按 key 移除一条箭头。
    /// </summary>
    public void RemoveArrow(string key)
    {
        if (_arrows.Remove(key))
            QueueRedraw();
    }

    /// <summary>
    /// 清除所有箭头。
    /// </summary>
    public void ClearArrows()
    {
        if (_arrows.Count == 0) return;
        _arrows.Clear();
        QueueRedraw();
    }

    /// <summary>
    /// 动态更新箭头的终止位置（如鼠标拖拽跟踪）。
    /// </summary>
    public void SetArrowTo(string key, Vector2 to)
    {
        if (_arrows.TryGetValue(key, out var data))
        {
            data.To = to;
            _arrows[key] = data;
            QueueRedraw();
        }
    }

    /// <summary>
    /// 动态更新箭头的起始位置。
    /// </summary>
    public void SetArrowFrom(string key, Vector2 from)
    {
        if (_arrows.TryGetValue(key, out var data))
        {
            data.From = from;
            _arrows[key] = data;
            QueueRedraw();
        }
    }

    /// <summary>
    /// 检查是否存在指定 key 的箭头。
    /// </summary>
    public bool HasArrow(string key) => _arrows.ContainsKey(key);

    /// <summary>
    /// 获取当前箭头数量。
    /// </summary>
    public int ArrowCount => _arrows.Count;

    // ===== 渲染 =====

    public override void _Draw()
    {
        foreach (var arrow in _arrows.Values)
        {
            DrawArrow(arrow);
        }
    }

    /// <summary>
    /// 绘制单条箭头：直线 + 三角形箭头头部。
    /// </summary>
    private void DrawArrow(ArrowData arrow)
    {
        var from = arrow.From;
        var to = arrow.To;

        // 跳过零长度箭头
        if (from.DistanceSquaredTo(to) < 0.01f)
            return;

        // 绘制箭头杆
        DrawLine(from, to, arrow.Color, arrow.Width, antialiased: true);

        // 计算箭头头部三角形
        var direction = (to - from).Normalized();
        var perpendicular = new Vector2(-direction.Y, direction.X); // 顺时针旋转90度，等价 direction.Rotated(PI/2)

        var tip = to;
        var baseLeft = to - direction * HeadLength + perpendicular * HeadWidth;
        var baseRight = to - direction * HeadLength - perpendicular * HeadWidth;

        // 绘制实心三角形箭头
        var trianglePoints = new Vector2[] { tip, baseLeft, baseRight };
        var colors = new Color[] { arrow.Color, arrow.Color, arrow.Color };
        DrawPolygon(trianglePoints, colors);
    }

    // ===== 生命周期 =====

    public override void _Ready()
    {
        // 设为忽略鼠标，箭头不拦截点击事件
        MouseFilter = MouseFilterEnum.Ignore;
    }
}
