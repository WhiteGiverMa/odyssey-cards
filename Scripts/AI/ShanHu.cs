using System;
using System.Linq;
using Godot;
using OdysseyCards.Card;
using OdysseyCards.Combat;
using OdysseyCards.Core;

namespace OdysseyCards.AI;

/// <summary>
/// 珊胡 — 第2层精英敌人。
/// 被动：不破（1）— 每回合只能受到 1 次生命伤害（0 点不计）。
/// 意图：A(3~4伤害) / B(2伤害×2) 随机交换 → C(武器攻击+2) → 循环。
/// D 每 2~4 回合替代主意图：给随机友方目标 5 点护甲。
/// </summary>
public class ShanHu : EnemyEncounter
{
    private int _dTurnsRemaining;
    private bool _dJustExecuted;
    private bool _abAFirst;
    private int _cycleStep;

    public ShanHu()
        : base("珊胡", 20, new EnemyIntent[]
        {
            new(IntentType.Attack, 3, "造成 3~4 点伤害"),
            new(IntentType.Attack, 4, "造成 2 点伤害 ×2"),
            new(IntentType.Buff, 2, "武器攻击力 +2"),
        })
    {
        _dTurnsRemaining = Random.Shared.Next(2, 5);
        _abAFirst = Random.Shared.Next(2) == 0;
    }

    public override EnemyIntent GetCurrentIntent(CombatManager combat, Hero self)
    {
        if (_dTurnsRemaining <= 0)
        {
            return new EnemyIntent(IntentType.Defend, 5, "给随机友方 5 点护甲");
        }

        if (_cycleStep == 2)
            return new EnemyIntent(IntentType.Buff, 2, "武器攻击力 +2");

        bool isA = _cycleStep == 0 ? _abAFirst : !_abAFirst;
        return BuildABIntent(combat, self, isA);
    }

    private EnemyIntent BuildABIntent(CombatManager combat, Hero self, bool isA)
    {
        if (isA)
        {
            int dmg = Random.Shared.Next(3, 5);
            var intent = new EnemyIntent(IntentType.Attack, dmg, $"造成 {dmg} 点伤害");
            return InjectLambda(combat, self, intent);
        }
        else
        {
            var intent = new EnemyIntent(IntentType.Attack, 4, "造成 2 点伤害 ×2");
            return InjectLambda(combat, self, intent);
        }
    }

    /// <summary>注入目标选择器和伤害计算器到意图中（返回修改后的结构体以避免 struct 值拷贝问题）。</summary>
    private EnemyIntent InjectLambda(CombatManager combat, Hero self, EnemyIntent intent)
    {
        // 始终基于当前战场状态重新解析目标（不缓存，确保嘲讽等动态变化生效）
        var t = ResolveAttackTarget(combat);
        intent.TargetSelector = _ => t;
        intent.DamageCalc = (c) =>
            DamageResolver.ResolvePreviewDamage(intent.Value + Attack, self, t);
        return intent;
    }

    public override void ExecuteIntent(CombatManager combat, Hero self)
    {
        _cachedAttackTarget = null; // 基于当前战场状态重新解析目标

        if (_dTurnsRemaining <= 0)
        {
            ExecuteArmorGrant(combat);
            _dTurnsRemaining = Random.Shared.Next(2, 5);
            _dJustExecuted = true;
            GD.Print($"[珊胡] 给友方护甲！下个D={_dTurnsRemaining}");
            return;
        }

        _dJustExecuted = false;
        var intent = GetCurrentIntent(combat, self);
        GD.Print($"[珊胡] 执行意图：{intent.Description}");

        bool isA = _cycleStep == 0 ? _abAFirst : !_abAFirst;

        switch (intent.Type)
        {
            case IntentType.Attack:
                if (!isA) // B: multi-hit
                    ExecuteMultiHit(combat, self, intent, 2, 2);
                else
                    ExecuteAttackIntent(combat, self);
                break;

            case IntentType.Buff:
                if (self.Weapon != null)
                {
                    self.Weapon.Attack += intent.Value;
                    GD.Print($"[珊胡] 武器攻击力 +{intent.Value} → {self.Weapon.Attack}");
                }
                break;
        }
    }

    private void ExecuteMultiHit(CombatManager combat, Hero self, EnemyIntent intent, int perHit, int hits)
    {
        var target = intent.GetTarget(combat);
        for (int i = 0; i < hits; i++)
        {
            if (self.IsDead) break;
            GD.Print($"[珊胡] 多段攻击 {i + 1}/{hits}：{intent.Description}");
            if (target is Minion m)
            {
                combat.TriggerBaitTacticsOnAttacked(m);

                bool ambush = m.HasAmbush && !m.AmbushUsedThisTurn;
                if (ambush) m.AmbushUsedThisTurn = true;
                self.SuppressWeaponCounter = true;
                self.TakeDamage(m.Attack, m);
                self.SuppressWeaponCounter = false;
                if (ambush && self.IsDead) return;
                m.TakeDamage(perHit + Attack, self);
            }
            else if (target is Hero h)
            {
                h.TakeDamage(perHit + Attack, self);
            }
        }
    }

    private void ExecuteArmorGrant(CombatManager combat)
    {
        // 随机友方目标：自己或其他友方敌人英雄
        var candidates = combat.EnemyUnits
            .Select(u => u.Body)
            .Where(b => !b.IsDead)
            .ToList();

        if (candidates.Count == 0) return;
        var target = candidates[Random.Shared.Next(candidates.Count)];
        target.GainArmor(5);
        GD.Print($"[珊胡] 给 {target} 5 点护甲（当前 {target.CurrentArmor}）");
    }

    public override void AdvanceIntent()
    {
        if (_dJustExecuted)
        {
            _dJustExecuted = false;
        }
        else
        {
            _cycleStep = (_cycleStep + 1) % 3;
            if (_cycleStep == 0) _abAFirst = Random.Shared.Next(2) == 0;
        }

        if (_dTurnsRemaining > 0) _dTurnsRemaining--;
        _cachedAttackTarget = null;
    }
}
