using OdysseyCards.Card;
using OdysseyCards.Card.HeroPowers;
using OdysseyCards.Character;
using OdysseyCards.Core;
using Xunit;

namespace OdysseyCards.Tests.Unit;

/// <summary>
/// 溯光英雄配置与核心技能单元测试。
/// </summary>
public class SokouHeroTests
{
	[Fact]
	public void HeroProfile_GetSokou_ReturnsConfiguredHero()
	{
		var profile = HeroProfile.Get("sokou");

		Assert.Equal("溯光", profile.DisplayName);
		Assert.Equal("Sokou", profile.RomanizedName);
		Assert.Equal(25, profile.MaxHealth);
		Assert.Equal(0, profile.StartingDefense);
		Assert.IsType<SokouRestructureHeroPower>(profile.CreateHeroPower());
		Assert.IsType<RayPistol>(profile.CreateWeapon());
	}

	[Fact]
	public void RayPistol_AndBlindShot_UseExpectedCostsAndCharges()
	{
		var weapon = new RayPistol();
		var skill = new BlindShot();
		var hero = CreateHeroWithMana(2);

		Assert.Equal(1, weapon.Attack);
		Assert.Equal(2, weapon.AttackCost);
		Assert.IsType<RememberPassive>(weapon.PassiveSkill);
		Assert.IsType<BlindShot>(weapon.ActiveSkill);
		Assert.True(skill.CanUse(hero));
		Assert.Equal(1, skill.Charges);
		Assert.Equal(3, skill.MaxCharges);
		Assert.Equal(1, skill.Cooldown);
	}

	private static Hero CreateHeroWithMana(int mana)
	{
		var core = new CommanderCore();
		core.InitializeHealth(25, 25);
		core.SetMana(mana, mana);
		return new Hero(core, isPlayerSide: true);
	}
}
