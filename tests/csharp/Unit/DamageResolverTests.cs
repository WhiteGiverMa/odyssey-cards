using System;
using System.Collections.Generic;
using OdysseyCards.Core;
using Xunit;

namespace OdysseyCards.Tests.Unit;

/// <summary>
/// 单元测试 — DamageResolver 三阶段伤害管线。
/// 使用纯 C# 测试替身，避免触碰 CardData/Resource/Godot 运行时。
/// </summary>
public class DamageResolverTests
{
    private static readonly string[] ExpectedPipelineOrder =
    {
        "source additive",
        "target additive",
        "source multiplicative",
        "target multiplicative",
        "source capping",
        "target capping",
    };

    [Fact]
    public void ResolveDamage_WithoutModifiers_ReturnsBaseDamage()
    {
        int damage = DamageResolver.ResolvePreviewDamage(7, source: null, target: null);

        Assert.Equal(7, damage);
    }

    [Fact]
    public void ResolveDamage_ClampsNegativeDamageToZero()
    {
        var target = new TestDamageTarget(
            defense: 0,
            new TestDamageModifier(DamagePhase.ADDITIVE, taken: damage => damage - 10));

        int damage = DamageResolver.ResolvePreviewDamage(3, source: null, target);

        Assert.Equal(0, damage);
    }

    [Fact]
    public void ResolveDamage_AppliesPhasesInStableOrder()
    {
        var calls = new List<string>();
        var source = new TestDamageSource(
            new TestDamageModifier(DamagePhase.ADDITIVE, dealt: damage =>
            {
                calls.Add("source additive");
                return damage + 2;
            }),
            new TestDamageModifier(DamagePhase.MULTIPLICATIVE, dealt: damage =>
            {
                calls.Add("source multiplicative");
                return damage * 2;
            }),
            new TestDamageModifier(DamagePhase.CAPPING, dealt: damage =>
            {
                calls.Add("source capping");
                return Math.Min(damage, 5);
            }));
        var target = new TestDamageTarget(
            defense: 0,
            new TestDamageModifier(DamagePhase.ADDITIVE, taken: damage =>
            {
                calls.Add("target additive");
                return damage - 3;
            }),
            new TestDamageModifier(DamagePhase.MULTIPLICATIVE, taken: damage =>
            {
                calls.Add("target multiplicative");
                return damage / 3;
            }),
            new TestDamageModifier(DamagePhase.CAPPING, taken: damage =>
            {
                calls.Add("target capping");
                return Math.Min(damage, 4);
            }));

        int damage = DamageResolver.ResolveDamage(10, source, target);

        Assert.Equal(4, damage);
        Assert.Equal(ExpectedPipelineOrder, calls);
    }

    [Fact]
    public void ResolveDamage_AppliesSourceBeforeTargetWithinSamePhase()
    {
        var source = new TestDamageSource(
            new TestDamageModifier(DamagePhase.ADDITIVE, dealt: damage => damage + 5));
        var target = new TestDamageTarget(
            defense: 0,
            new TestDamageModifier(DamagePhase.ADDITIVE, taken: damage => damage * 2));

        int damage = DamageResolver.ResolveDamage(10, source, target);

        Assert.Equal(30, damage);
    }

    [Fact]
    public void DefenseModifier_AttackDamage_ReducesByDefense()
    {
        var target = new TestDamageTarget(
            defense: 3,
            new DefenseModifier(() => 3));

        int damage = DamageResolver.ResolvePreviewDamage(10, source: null, target, DamageKind.Attack);

        Assert.Equal(7, damage);
    }

    [Fact]
    public void DefenseModifier_EffectDamage_IgnoresDefense()
    {
        var target = new TestDamageTarget(
            defense: 3,
            new DefenseModifier(() => 3));

        int damage = DamageResolver.ResolvePreviewDamage(10, source: null, target, DamageKind.Effect);

        Assert.Equal(10, damage);
    }

    [Fact]
    public void DefenseModifier_NegativeDefense_IncreasesAttackDamage()
    {
        var target = new TestDamageTarget(
            defense: -2,
            new DefenseModifier(() => -2));

        int damage = DamageResolver.ResolvePreviewDamage(5, source: null, target, DamageKind.Attack);

        Assert.Equal(7, damage);
    }

    [Fact]
    public void DamageCapModifier_CapsIncomingDamage()
    {
        var target = new TestDamageTarget(
            defense: 0,
            new DamageCapModifier(3));

        int damage = DamageResolver.ResolvePreviewDamage(10, source: null, target);

        Assert.Equal(3, damage);
    }

    [Fact]
    public void DefendedTargetDamageBonusModifier_AddsDamageWhenTargetDefenseMeetsThreshold()
    {
        var source = new TestDamageSource(new DefendedTargetDamageBonusModifier(bonusDamage: 4));
        var target = new TestDamageTarget(defense: 2);

        int damage = DamageResolver.ResolveDamage(6, source, target);

        Assert.Equal(10, damage);
    }

    [Fact]
    public void DefendedTargetDamageBonusModifier_DoesNotAddDamageWithoutTarget()
    {
        var source = new TestDamageSource(new DefendedTargetDamageBonusModifier(bonusDamage: 4));

        int damage = DamageResolver.ResolvePreviewDamage(6, source, target: null);

        Assert.Equal(6, damage);
    }

    [Fact]
    public void DefendedTargetDamageBonusModifier_DoesNotAddDamageBelowDefenseThreshold()
    {
        var source = new TestDamageSource(new DefendedTargetDamageBonusModifier(bonusDamage: 4, minimumDefense: 2));
        var target = new TestDamageTarget(defense: 1);

        int damage = DamageResolver.ResolveDamage(6, source, target);

        Assert.Equal(6, damage);
    }

    private sealed class TestDamageSource : IDamageSource
    {
        public TestDamageSource(params IDamageModifier[] modifiers)
        {
            DamageModifiers = modifiers;
        }

        public IReadOnlyList<IDamageModifier> DamageModifiers { get; }

        public int BaseAttack => 0;

        public bool IsPlayerSide => false;
    }

    private sealed class TestDamageTarget : IDamageTarget
    {
        public TestDamageTarget(int defense, params IDamageModifier[] modifiers)
        {
            Defense = defense;
            DamageModifiers = modifiers;
        }

        public int Defense { get; }

        public IReadOnlyList<IDamageModifier> DamageModifiers { get; }

        public void TakeDamage(int baseDamage, IDamageSource? source)
        {
        }

        public void TakeDamage(int baseDamage, IDamageSource? source, DamageKind kind)
        {
        }

        public void ApplyDamage(int finalDamage, IDamageSource source)
        {
        }
    }

    private sealed class TestDamageModifier : IDamageModifier
    {
        private readonly Func<int, int> _dealt;
        private readonly Func<int, int> _taken;

        public TestDamageModifier(
            DamagePhase phase,
            Func<int, int>? dealt = null,
            Func<int, int>? taken = null)
        {
            Phase = phase;
            _dealt = dealt ?? (damage => damage);
            _taken = taken ?? (damage => damage);
        }

        public DamagePhase Phase { get; }

        public int ModifyDamageDealt(int currentDamage, DamageContext context) => _dealt(currentDamage);

        public int ModifyDamageTaken(int currentDamage, DamageContext context) => _taken(currentDamage);
    }
}
