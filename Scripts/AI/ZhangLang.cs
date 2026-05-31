using System;
using Godot;
using OdysseyCards.Card;
using OdysseyCards.Combat;
using OdysseyCards.Core;

namespace OdysseyCards.AI;

/// <summary>
/// 张郎 — 第2层精英敌人。
/// 被动：固璋（3）— 单次受到的生命伤害最高 3（由 CombatManager 初始化时注入 DamageCapModifier）。
/// 意图：A(3~4伤害) / B(1伤害×3) 随机交换 → C(武器攻击+1) → 循环。
/// D 每 2~4 回合替代主意图：召唤机械小蠊。
/// </summary>
public class ZhangLang : EnemyEncounter
{
    private int _dTurnsRemaining;
    private bool _dJustExecuted;
    private bool _abAFirst;
    private int _cycleStep; // 0=first-AB, 1=second-AB, 2=C

    public ZhangLang()
        : base("张郎", 20, new EnemyIntent[]
        {
            new(IntentType.Attack, 3, "造成 3~4 点伤害"),
            new(IntentType.Attack, 3, "造成 1 点伤害 ×3"),
            new(IntentType.Buff, 1, "武器攻击力 +1"),
        })
    {
        _dTurnsRemaining = Random.Shared.Next(2, 5);
        _abAFirst = Random.Shared.Next(2) == 0;
    }

    public override EnemyIntent GetCurrentIntent(CombatManager combat, Hero self)
    {
        if (_dTurnsRemaining <= 0)
        {
            return new EnemyIntent(
                IntentType.Summon, 1, "召唤 机械小蠊 (1/3)",
                summonName: "机械小蠊", summonAttack: 1, summonHealth: 3);
        }

        if (_cycleStep == 2)
            return new EnemyIntent(IntentType.Buff, 1, "武器攻击力 +1");

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
            var intent = new EnemyIntent(IntentType.Attack, 3, "造成 1 点伤害 ×3");
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
            ExecuteSummon(combat);
            _dTurnsRemaining = Random.Shared.Next(2, 5);
            _dJustExecuted = true;
            GD.Print($"[张郎] 召唤机械小蠊！下个D={_dTurnsRemaining}");
            return;
        }

        _dJustExecuted = false;
        var intent = GetCurrentIntent(combat, self);
        GD.Print($"[张郎] 执行意图：{intent.Description}");

        switch (intent.Type)
        {
            case IntentType.Attack:
                if (intent.Value <= 5) // B: multi-hit
                    ExecuteMultiHit(combat, self, intent, 1, 3);
                else
                    ExecuteAttackIntent(combat, self);
                break;

            case IntentType.Buff:
                if (self.Weapon != null)
                {
                    self.Weapon.Attack += intent.Value;
                    GD.Print($"[张郎] 武器攻击力 +{intent.Value} → {self.Weapon.Attack}");
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
            if (target is Minion m)
            {
                bool ambush = m.HasAmbush && !m.AmbushUsedThisTurn;
                if (ambush) m.AmbushUsedThisTurn = true;
                self.SuppressWeaponCounter = true;
                self.TakeDamage(m.Attack, m);
                self.SuppressWeaponCounter = false;
                if (ambush && self.IsDead) return;
                m.TakeDamage(perHit + Attack, null);
            }
            else if (target is Hero h)
            {
                h.TakeDamage(perHit + Attack, self);
            }
        }
    }

    private void ExecuteSummon(CombatManager combat)
    {
        if (!combat.Board.CanPlaceMinion(isPlayerSide: false)) return;
        const string path = "res://Resources/Cards/Minion_Roach.tres";
        if (!ResourceLoader.Exists(path))
        {
            GD.PrintErr($"[张郎] 未找到机械小蠊卡牌资源：{path}");
            return;
        }
        var data = GD.Load<CardData>(path);
        if (data == null) return;
        var roach = new Minion(data, isPlayerSide: false);
        roach.IntentBrain = new MechanicalRoachBrain(roach);
        int slot = combat.Board.GetEmptySlotIndex(isPlayerSide: false);
        combat.Board.PlaceMinion(roach, slot);
        GD.Print($"[张郎] 槽位{slot} 召唤机械小蠊 ({roach.Attack}/{roach.CurrentHealth})，已挂载意图大脑");
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
