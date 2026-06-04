namespace OdysseyCards.AI.Intents;

/// <summary>
/// 减益意图——对敌方单位施加负面效果（如易伤、虚弱等）。
/// 支持普通减益和强力减益两种变体。
/// </summary>
public sealed class DebuffIntent : AbstractIntent
{
    private readonly bool _strong;

    /// <inheritdoc />
    public override IntentType Type => _strong ? IntentType.DebuffStrong : IntentType.Debuff;

    /// <inheritdoc />
    public override string IntentPrefix => "DEBUFF";

    /// <inheritdoc />
    public override string? SpritePath => "res://Assets/Intents/debuff.png";

    /// <summary>
    /// 创建减益意图。
    /// </summary>
    /// <param name="strong">是否为强力减益（影响意图类型和 UI 呈现）</param>
    public DebuffIntent(bool strong = false)
    {
        _strong = strong;
    }

    /// <inheritdoc />
    public override IntentHoverTip GetHoverTip(Combat.CombatManager combat)
    {
        var baseTip = base.GetHoverTip(combat);
        return new IntentHoverTip(baseTip.Title, baseTip.Description, isDebuff: true);
    }
}
