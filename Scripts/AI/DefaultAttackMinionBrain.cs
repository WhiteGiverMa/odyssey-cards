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
        var playerTaunts = combat.Board.GetTaunts(ofEnemy: false);
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
        // 解析目标：有嘲讽→攻击嘲讽随从，无嘲讽→攻击玩家英雄
        var playerTaunts = combat.Board.GetTaunts(ofEnemy: false);
        if (playerTaunts.Count > 0)
        {
            // 攻击随机嘲讽随从——走统一战斗序列（自动处理伏击/冲击/反击）
            var tauntTargets = playerTaunts.Where(t => !t.IsDead).ToList();
            if (tauntTargets.Count > 0)
            {
                var defender = tauntTargets[Random.Shared.Next(tauntTargets.Count)];
                combat.ResolveMinionCombat(_body, defender);

                // 清理死亡随从（Board.RemoveMinion 自动触发亡语和牌堆回收事件）
                if (defender.IsDead)
                    combat.Board.RemoveMinion(defender);
                if (_body.IsDead)
                    combat.Board.RemoveMinion(_body);
            }
        }
        else
        {
            // 攻击玩家英雄——英雄攻击不支持统一战斗序列，直接造成伤害
            combat.PlayerHero.TakeDamage(_body.Attack, _body);
        }
    }

    public void AdvanceIntent()
    {
        // 默认随从意图不变，每次都是攻击
    }
}
