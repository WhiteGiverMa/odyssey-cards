using OdysseyCards.Combat;

namespace OdysseyCards.AI.Intents;

/// <summary>
/// 意图抽象基类。
/// 参考《杀戮尖塔2》的 AbstractIntent 设计，所有具体意图类型均继承此类。
/// 每个意图包含类型、图标路径、悬浮提示文本等 UI 展示所需的元数据。
/// </summary>
public abstract class AbstractIntent
{
    /// <summary>意图类型。</summary>
    public abstract IntentType Type { get; }

    /// <summary>意图图标精灵路径（可为 null 表示无图标）。</summary>
    public virtual string? SpritePath => null;

    /// <summary>是否有意图提示（隐藏意图返回 false）。</summary>
    public virtual bool HasIntentTip => true;

    /// <summary>意图前缀，用于本地化 key 查找（如 "ATTACK" → "intents.ATTACK.title"）。</summary>
    public abstract string IntentPrefix { get; }

    /// <summary>
    /// 获取意图标签文本（UI 中意图图标旁的数字或标记）。
    /// 默认返回空字符串——非攻击意图不显示数字。
    /// </summary>
    /// <param name="combat">战斗管理器</param>
    /// <returns>意图标签文本</returns>
    public virtual string GetIntentLabel(CombatManager combat)
    {
        return "";
    }

    /// <summary>
    /// 获取意图描述文本（已本地化）。
    /// 默认从本地化表中查找 intents.{IntentPrefix}.description。
    /// </summary>
    /// <param name="combat">战斗管理器</param>
    /// <returns>意图描述文本</returns>
    public virtual string GetIntentDescription(CombatManager combat)
    {
        return OdysseyCards.Localization.Localization.T($"intents.{IntentPrefix}.description", "");
    }

    /// <summary>
    /// 获取意图悬浮提示数据。
    /// 合并标题和描述，供 UI 层展示。
    /// </summary>
    /// <param name="combat">战斗管理器</param>
    /// <returns>意图悬浮提示</returns>
    public virtual IntentHoverTip GetHoverTip(CombatManager combat)
    {
        string? title = HasIntentTip
            ? OdysseyCards.Localization.Localization.T($"intents.{IntentPrefix}.title", IntentPrefix)
            : null;
        string description = GetIntentDescription(combat);
        return new IntentHoverTip(title, description);
    }

    /// <summary>将意图类型映射为 IntentIcon 所需的 typeId。</summary>
    public static int GetIconTypeId(IntentType type) => type switch
    {
        IntentType.Attack or IntentType.DeathBlow => 0,
        IntentType.MultiAttack => 1,
        IntentType.Defend => 2,
        IntentType.Buff => 3,
        IntentType.Debuff or IntentType.DebuffStrong => 4,
        IntentType.Heal => 5,
        IntentType.Summon => 6,
        IntentType.Sleep => 7,
        IntentType.Stun => 8,
        IntentType.Escape => 9,
        IntentType.StatusCard => 10,
        IntentType.Unknown => 11,
        IntentType.Hidden => 12,
        IntentType.SpellCast => 13,
        _ => 11
    };
}
