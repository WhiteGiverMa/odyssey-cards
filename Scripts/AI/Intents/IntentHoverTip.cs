namespace OdysseyCards.AI;

/// <summary>
/// 意图悬浮提示数据。
/// 用于 UI 展示敌人意图的详细信息，包含标题、描述和是否为减益的标记。
/// </summary>
public readonly struct IntentHoverTip
{
    /// <summary>提示标题（可为 null，表示无标题）。</summary>
    public string? Title { get; }

    /// <summary>提示描述文本。</summary>
    public string Description { get; }

    /// <summary>是否为减益意图（影响 UI 颜色呈现）。</summary>
    public bool IsDebuff { get; }

    /// <summary>
    /// 创建意图悬浮提示实例。
    /// </summary>
    /// <param name="title">提示标题</param>
    /// <param name="description">提示描述</param>
    /// <param name="isDebuff">是否为减益</param>
    public IntentHoverTip(string? title, string description, bool isDebuff = false)
    {
        Title = title;
        Description = description;
        IsDebuff = isDebuff;
    }
}
