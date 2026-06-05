using Godot;
using OdysseyCards.Combat;

namespace OdysseyCards.Relic;

/// <summary>
/// 战术核显卡：「微妙」藏品。
/// 每回合内首次累计打出5张牌时，对全体敌人造成3点伤害。
/// 下回合开始时失去2点法力。
/// </summary>
public sealed class TacticalNukeRelic : AbstractRelic
{
    public override string Id => "tactical_nuke";
    public override string Name => "战术核显卡";
    public override string Description => "每回合内首次累计打出5张牌时，对全体敌人造成3点伤害。下回合开始时失去2点法力。";

    public override bool IsBeneficial => false;
    public override bool IsSubtle => true;

    private int _cardsPlayedThisTurn;
    private bool _triggeredThisTurn;
    private bool _loseManaNextTurn;

    public override void OnTurnStart(CombatManager combat)
    {
        // 上回合触发了 → 本回合开始失去法力
        if (_loseManaNextTurn)
        {
            int newMana = System.Math.Max(0, combat.PlayerHero.CurrentMana - 2);
            combat.PlayerHero.SetMana(newMana, combat.PlayerHero.MaxMana);
            GD.Print($"[TacticalNuke] 失去2点法力（剩余 {newMana}）");
            _loseManaNextTurn = false;
        }

        _cardsPlayedThisTurn = 0;
        _triggeredThisTurn = false;
    }

    public override void OnCardPlayed(CombatManager combat, OdysseyCards.Card.Card card, int actualCost)
    {
        _cardsPlayedThisTurn++;

        if (_cardsPlayedThisTurn >= 5 && !_triggeredThisTurn)
        {
            _triggeredThisTurn = true;
            _loseManaNextTurn = true;

            // 对全体敌人造成3点伤害
            const int damage = 3;
            var enemyMinions = combat.Board.GetEnemyMinions();
            foreach (var minion in enemyMinions)
            {
                minion.TakeDamage(damage, null, Core.DamageKind.Effect);
            }

            // 对敌方英雄造成伤害
            foreach (var enemyUnit in combat.EnemyUnits)
            {
                enemyUnit.Body.TakeDamage(damage, null, Core.DamageKind.Effect);
            }

            GD.Print($"[TacticalNuke] 第5张牌触发！对全体敌人造成 {damage} 点伤害");
        }
    }
}
