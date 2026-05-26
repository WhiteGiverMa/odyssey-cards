using Xunit;
using OdysseyCards.Combat;
using OdysseyCards.Card;
using OdysseyCards.Core;

namespace OdysseyCards.Tests.Integration;

/// <summary>
/// 集成测试 — 需要 Godot 运行时的多组件交互测试。
/// CardData 继承 Resource，必须通过 Godot 无头模式运行。
/// </summary>
public class CombatIntegrationTests
{
    [Fact(Skip = "需要 Godot 运行时 — CardData 继承 Resource")]
    public void PlaceAndRemoveMinionIntegrationFlow()
    {
        var board = new Board();
        var cardData = new CardData
        {
            Id = "test_minion",
            CardName = "测试随从",
            Attack = 2,
            Health = 3
        };
        var minion = new Minion(cardData, isPlayerSide: true);

        board.PlaceMinion(minion, slotIndex: 0);
        Assert.Same(minion, board.PlayerSlots[0]);

        board.RemoveMinion(minion);
        Assert.Null(board.PlayerSlots[0]);
    }

    [Fact(Skip = "需要 Godot 运行时 — CardData 继承 Resource")]
    public void DamageResolverPipelineWithMinion()
    {
        var attackerData = new CardData { CardName = "攻击者", Attack = 3, Health = 2 };
        var defenderData = new CardData { CardName = "防御者", Attack = 1, Health = 4 };

        var attacker = new Minion(attackerData, isPlayerSide: true);
        var defender = new Minion(defenderData, isPlayerSide: false);

        int damage = DamageResolver.ResolveDamage(attacker.Attack, attacker, defender);
        defender.TakeDamage(damage, attacker);

        Assert.True(defender.CurrentHealth < defender.MaxHealth);
        Assert.False(defender.IsDead);
    }
}
