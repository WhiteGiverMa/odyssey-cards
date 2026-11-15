using OdysseyCards.AI;
using OdysseyCards.Card;
using Xunit;

namespace OdysseyCards.Tests.Unit;

public class WeaponRangeTests
{
	[Fact]
	public void InitialWeapons_UseAssignedRangeAndCounterPolicy()
	{
		Assert.Equal(WeaponRange.Melee, new IonPistol().Range);
		Assert.Equal(WeaponRange.Melee, new RollingLog().Range);
		Assert.Equal(WeaponRange.Melee, new LightCrucibleClamp().Range);
		Assert.Equal(WeaponRange.FriendlySupport, new MagicWand().Range);
		Assert.Equal(WeaponRange.Ranged, new SvdsM338().Range);
		Assert.True(new SvdsM338().IgnoreCounterAttack);
		Assert.Equal(WeaponRange.Ranged, new RayPistol().Range);
		Assert.False(new RayPistol().IgnoreCounterAttack);
		var bossWeapon = new Cosmonaut().CreateWeapon();
		Assert.IsType<RailPistol>(bossWeapon);
		Assert.Equal(WeaponRange.Ranged, bossWeapon.Range);
		Assert.IsType<RailPistolPassive>(bossWeapon.PassiveSkill);
	}
}
