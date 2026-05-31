using System;
using System.Collections.Generic;
using System.Linq;
using OdysseyCards.Card;
using OdysseyCards.Combat;
using OdysseyCards.Core;

namespace OdysseyCards.AI;

/// <summary>
/// 默认敌方随从意图大脑——占位实现，行为等同于现有 EnemyMinionsAttack。
/// 每回合意图 = 攻击随机合法敌方目标（玩家英雄或玩家随从，遵守嘲讽）。
/// </summary>
public class DefaultAttackMinionBrain : IIntentActor
{
    private readonly Minion _body;

    public Hero? OwnerHero => null;

    public DefaultAttackMinionBrain(Minion body)
    {
        _body = body ?? throw new ArgumentNullException(nameof(body));
    }

    public EnemyIntent GetCurrentIntent(CombatManager combat)
    {
        int dmg = DamageResolver.ResolvePreviewDamage(_body.Attack, _body, combat.PlayerHero);
        return new EnemyIntent(IntentType.Attack, dmg, $"攻击造成 {dmg} 点伤害");
    }

    public void ExecuteIntent(CombatManager combat)
    {
        // 现有逻辑：有嘲讽→随机嘲讽，无嘲讽→玩家英雄
        var playerTaunts = combat.Board.GetTaunts(isEnemy: false);
        if (playerTaunts.Count > 0)
        {
            var tauntTargets = playerTaunts.Where(t => !t.IsDead).ToList();
            if (tauntTargets.Count > 0)
            {
                var defender = tauntTargets[Random.Shared.Next(tauntTargets.Count)];
                defender.TakeDamage(_body.Attack, _body);
                // 反击
                if (!_body.IsDead && !defender.IsDead)
                    _body.TakeDamage(defender.Attack, defender);
            }
        }
        else
        {
            combat.PlayerHero.TakeDamage(_body.Attack, _body);
        }
    }

    public void AdvanceIntent()
    {
        // 默认随从意图不变，每次都是攻击
    }
}
