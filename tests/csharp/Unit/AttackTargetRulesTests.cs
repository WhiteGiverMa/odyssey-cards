using OdysseyCards.Combat;
using Xunit;

namespace OdysseyCards.Tests.Unit;

/// <summary>
/// 单元测试 — 普通攻击的速度与嘲讽拦截规则。
/// </summary>
public class AttackTargetRulesTests
{
	[Fact]
	public void CanReach_EqualOrSlowerTarget_ReturnsTrue()
	{
		Assert.True(AttackTargetRules.CanReach(attackerSpeed: 2, targetSpeed: 2));
		Assert.True(AttackTargetRules.CanReach(attackerSpeed: 3, targetSpeed: 2));
	}

	[Fact]
	public void CanReach_FasterTarget_ReturnsFalse()
	{
		Assert.False(AttackTargetRules.CanReach(attackerSpeed: 2, targetSpeed: 3));
	}

	[Fact]
	public void CanAttackTarget_DirectTaunt_BypassesSpeed()
	{
		Assert.True(AttackTargetRules.CanAttackTarget(
			attackerSpeed: 1,
			targetSpeed: 5,
			targetIsTaunt: true,
			tauntSpeeds: new[] { 5 }));
	}

	[Fact]
	public void CanAttackTarget_FasterNonTaunt_IsStillIllegalWhenTauntIsPresent()
	{
		Assert.False(AttackTargetRules.CanAttackTarget(
			attackerSpeed: 2,
			targetSpeed: 4,
			targetIsTaunt: false,
			tauntSpeeds: new[] { 5 }));
	}

	[Fact]
	public void CanAttackTarget_FastTaunt_InterceptsReachableNonTaunt()
	{
		Assert.False(AttackTargetRules.CanAttackTarget(
			attackerSpeed: 3,
			targetSpeed: 2,
			targetIsTaunt: false,
			tauntSpeeds: new[] { 4 }));
	}

	[Fact]
	public void CanAttackTarget_SlowerTaunt_DoesNotInterceptFasterAttacker()
	{
		Assert.True(AttackTargetRules.CanAttackTarget(
			attackerSpeed: 4,
			targetSpeed: 2,
			targetIsTaunt: false,
			tauntSpeeds: new[] { 3 }));
	}

	[Fact]
	public void CanAttackTarget_TauntMustKeepUpWithProtectedTarget()
	{
		Assert.True(AttackTargetRules.CanAttackTarget(
			attackerSpeed: 5,
			targetSpeed: 5,
			targetIsTaunt: false,
			tauntSpeeds: new[] { 4 }));
	}
}
