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
        // 解析目标：有玩家嘲讽→攻击嘲讽随从，否则→攻击玩家英雄
        var playerTaunts = combat.Board.GetTaunts(isEnemy: false);
        IDamageTarget target;
        if (playerTaunts.Count > 0)
            target = playerTaunts[0];
        else
            target = combat.PlayerHero;

        int dmg = DamageResolver.ResolvePreviewDamage(_body.Attack, _body, target);
        string targetName = target switch
        {
            Hero => Localization.Localization.T("intent.target_hero", "英雄"),
            Minion m => m.GetLocalizedName(),
            _ => "目标"
        };

        var intent = new EnemyIntent(IntentType.Attack, dmg,
            Localization.Localization.T("intent.attack_format", "对{target}造成 {damage} 点伤害")
                .Replace("{target}", targetName)
                .Replace("{damage}", dmg.ToString()));
        intent.TargetSelector = _ => target;
        return intent;
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
