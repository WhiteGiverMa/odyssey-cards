using Godot;
using OdysseyCards.Combat;

namespace OdysseyCards.Relic;

/// <summary>
/// 小风扇：每回合打出的第3张牌耗能-1。
/// </summary>
public sealed class SmallFanRelic : AbstractRelic
{
    public override string Id => "small_fan";
    public override string Name => "小风扇";
    public override string Description => "每回合打出的第3张牌耗能-1。";

    private int _cardsPlayedThisTurn;

    public override void OnTurnStart(CombatManager combat)
    {
        _cardsPlayedThisTurn = 0;
    }

    public override int ModifyCost(OdysseyCards.Card.Card card, int originalCost)
    {
        // 第3张牌（已打出2张）减费
        if (_cardsPlayedThisTurn == 2 && originalCost > 0)
        {
            GD.Print($"[SmallFan] 第3张牌「{card.CardName}」耗能 {originalCost} → {originalCost - 1}");
            return originalCost - 1;
        }
        return originalCost;
    }

    public override void OnCardPlayed(CombatManager combat, OdysseyCards.Card.Card card, int actualCost)
    {
        _cardsPlayedThisTurn++;
    }
}
