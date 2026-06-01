using System;
using Godot;
using OdysseyCards.Card;
using OdysseyCards.Combat;
using OdysseyCards.Core;

namespace OdysseyCards.AI;

/// <summary>
/// 机械小蠊 — 随从意图大脑。
/// 意图：A.部署回合沉睡 → B.攻击随机敌方目标 → C.友方有空槽时复制自身 → 循环。
/// "随机敌方目标"使用 TargetTags，目标包括敌方英雄与敌方随从，并遵守嘲讽。
/// </summary>
public class MechanicalRoachBrain : IIntentActor
{
    private readonly Minion _body;
    private int _cycleStep; // 0=sleep, 1=attack, 2=copy (if available)
    private bool _hasSlept;
    private IDamageTarget? _cachedTarget;
    private int _cachedDamage;

    public Hero? OwnerHero => null;

    /// <summary>
    /// 创建机械小蠊意图大脑。
    /// </summary>
    /// <param name="body">该大脑控制的随从身体</param>
    public MechanicalRoachBrain(Minion body)
    {
        _body = body ?? throw new ArgumentNullException(nameof(body));
    }

    public EnemyIntent GetCurrentIntent(CombatManager combat)
    {
        if (!_hasSlept)
        {
            return new EnemyIntent(IntentType.Buff, 0, "沉睡中…");
        }

        if (_cycleStep == 2 && combat.Board.CanPlaceMinion(isPlayerSide: false))
        {
            return new EnemyIntent(IntentType.Buff, 1, "分裂复制");
        }

        // Attack: resolve target
        var target = ResolveRoachTarget(combat);
        _cachedTarget = target;
        _cachedDamage = DamageResolver.ResolvePreviewDamage(_body.Attack, _body, target);
        string targetName = target switch
        {
            Hero => "英雄",
            Minion m => m.GetLocalizedName(),
            _ => "目标"
        };
        return new EnemyIntent(IntentType.Attack, _cachedDamage, $"攻击 {targetName} 造成 {_cachedDamage} 点伤害");
    }

    public void ExecuteIntent(CombatManager combat)
    {
        if (!_hasSlept)
        {
            _hasSlept = true;
            _cycleStep = 1;
            GD.Print($"[机械小蠊] 部署回合：沉睡");
            return;
        }

        if (_cycleStep == 2 && combat.Board.CanPlaceMinion(isPlayerSide: false))
        {
            ExecuteCopy(combat);
            _cycleStep = 1;
            return;
        }

        // Attack
        var target = _cachedTarget ?? ResolveRoachTarget(combat);
        if (target != null && _body.Attack > 0)
        {
            GD.Print($"[机械小蠊] 攻击目标，造成 {_body.Attack} 点伤害");
            if (target is Hero hero)
                hero.TakeDamage(_body.Attack, _body);
            else if (target is Minion minionTarget)
            {
                combat.TriggerBaitTacticsOnAttacked(minionTarget);

                bool ambush = minionTarget.HasAmbush && !minionTarget.AmbushUsedThisTurn;
                if (ambush) minionTarget.AmbushUsedThisTurn = true;

                if (ambush)
                {
                    bool wasSuppressed = _body.IsPlayerSide;
                    _body.TakeDamage(minionTarget.Attack, minionTarget);
                }

                if (_body.IsDead) return;
                minionTarget.TakeDamage(_body.Attack, _body);
            }
        }
    }

    public void AdvanceIntent()
    {
        _cachedTarget = null;
        _cycleStep = (_cycleStep + 1) % 3;
    }

    private IDamageTarget ResolveRoachTarget(CombatManager combat)
    {
        // 尊重嘲讽：玩家方有嘲讽随从则优先从嘲讽中随机选
        var playerTaunts = combat.Board.GetTaunts(ofEnemy: false);
        if (playerTaunts.Count > 0)
            return playerTaunts[Random.Shared.Next(playerTaunts.Count)];

        // 无嘲讽：从玩家英雄 + 玩家随从中随机选
        var candidates = new System.Collections.Generic.List<IDamageTarget>();
        candidates.Add(combat.PlayerHero);
        foreach (var m in combat.Board.GetPlayerMinions())
        {
            if (!m.IsDead && !ReferenceEquals(m, _body))
                candidates.Add(m);
        }

        if (candidates.Count == 0)
            return combat.PlayerHero;

        return candidates[Random.Shared.Next(candidates.Count)];
    }

    private void ExecuteCopy(CombatManager combat)
    {
        int slot = combat.Board.GetEmptySlotIndex(isPlayerSide: false);
        if (slot < 0) return;

        // Clone the card data and create a new Minion
        var clone = new Minion(_body.Data, isPlayerSide: false);
        combat.Board.PlaceMinion(clone, slot);
        GD.Print($"[机械小蠊] 在敌方槽位 {slot} 分裂复制！");
    }
}
