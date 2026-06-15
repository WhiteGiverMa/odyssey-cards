using OdysseyCards.Card;
using OdysseyCards.Card.HeroPowers;
using OdysseyCards.Core;
using Xunit;

namespace OdysseyCards.Tests.Unit;

/// <summary>
/// 绮梦/红裤衩英雄配置与魔法棒核心规则单元测试。
/// </summary>
public class AyameHeroTests
{
	[Fact]
	public void HeroProfile_GetAyame_ReturnsRealAyameConfiguration()
	{
		var profile = HeroProfile.Get("ayame");

		Assert.Equal("绮梦", profile.DisplayName);
		Assert.Equal("Ayame", profile.RomanizedName);
		Assert.Equal(30, profile.MaxHealth);
		Assert.Equal(0, profile.StartingDefense);
		Assert.IsType<AyameStarlightSupplyHeroPower>(profile.CreateHeroPower());
		Assert.IsType<MagicWand>(profile.CreateWeapon());
	}

	[Fact]
	public void HeroProfile_LegacyQimeng_MapsToAyame()
	{
		Assert.Equal("ayame", HeroProfile.Get("qimeng").Id);
	}

	[Fact]
	public void HeroProfile_GetRedPantsu_ReturnsDemoConfiguration()
	{
		var profile = HeroProfile.Get("redpantsu");

		Assert.Equal("红裤衩", profile.DisplayName);
		Assert.Equal("RedPantsu", profile.RomanizedName);
		Assert.IsType<IronWillHeroPower>(profile.CreateHeroPower());
		Assert.IsType<IonPistol>(profile.CreateWeapon());
	}

	[Fact]
	public void MagicWand_UsesExpectedCostsAndSkills()
	{
		var weapon = new MagicWand();

		Assert.Equal(0, weapon.Attack);
		Assert.Equal(2, weapon.AttackCost);
		Assert.IsType<MagicFilmPassive>(weapon.PassiveSkill);
		Assert.IsType<StarlightCleanse>(weapon.ActiveSkill);
		Assert.Equal(2, weapon.ActiveSkill!.Cost);
		Assert.Equal(1, weapon.ActiveSkill.Cooldown);
	}

	[Fact]
	public void MagicFilmPassive_HealsByWeaponDamagePlusTwo()
	{
		var passive = new MagicFilmPassive();

		Assert.Equal(2, passive.GetFriendlyMinionHealAmount(0));
		Assert.Equal(5, passive.GetFriendlyMinionHealAmount(3));
	}

	[Fact]
	public void StatusEffect_DefaultsKnownDebuffsToNegative()
	{
		Assert.True(new StatusEffect("vulnerable", 1, TickTiming.EnemyTurnEnd).IsNegative);
		Assert.True(new StatusEffect("damage_over_time", 1, TickTiming.PlayerTurnStart).IsNegative);
		Assert.False(new StatusEffect("custom_non_negative", 1, TickTiming.PlayerTurnEnd).IsNegative);
	}
}
