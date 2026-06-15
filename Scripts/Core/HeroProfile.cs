using System;
using System.Collections.Generic;
using OdysseyCards.Card;
using OdysseyCards.Card.HeroPowers;

namespace OdysseyCards.Core;

/// <summary>
/// 可选英雄的静态配置。
/// </summary>
public sealed class HeroProfile
{
	public string Id { get; init; } = "redpantsu";
	public string NameKey { get; init; } = "hero.redpantsu.name";
	public string DisplayName { get; init; } = "红裤衩";
	public string RomanizedName { get; init; } = "RedPantsu";
	public string DescriptionKey { get; init; } = "hero.redpantsu.desc";
	public string DefaultDescription { get; init; } = "演示配置英雄。30点生命，离子手枪与铁腕。";
	public int MaxHealth { get; init; } = 30;
	public int StartingDefense { get; init; }
	public Func<IHeroPower> CreateHeroPower { get; init; } = () => new IronWillHeroPower();
	public Func<Weapon> CreateWeapon { get; init; } = () => new IonPistol();

	public static readonly IReadOnlyList<HeroProfile> All = new[]
	{
		new HeroProfile(),
		new HeroProfile
		{
			Id = "ayame",
			NameKey = "hero.ayame.name",
			DisplayName = "绮梦",
			RomanizedName = "Ayame",
			DescriptionKey = "hero.ayame.desc",
			DefaultDescription = "30点生命，魔法棒。用星光补给抽牌回血，并用魔法棒为随从贴膜、净化负面效果。",
			MaxHealth = 30,
			StartingDefense = 0,
			CreateHeroPower = () => new AyameStarlightSupplyHeroPower(),
			CreateWeapon = () => new MagicWand(),
		},
		new HeroProfile
		{
			Id = "rie",
			NameKey = "hero.rie.name",
			DisplayName = "理惠",
			RomanizedName = "Rie",
			DescriptionKey = "hero.rie.desc",
			DefaultDescription = "25点生命，SVDS-M338。擅长连射、撕裂与直伤法术检索。",
			MaxHealth = 25,
			StartingDefense = 0,
			CreateHeroPower = () => new RieSuppressingFireHeroPower(),
			CreateWeapon = () => new SvdsM338(),
		},
		new HeroProfile
		{
			Id = "sokou",
			NameKey = "hero.sokou.name",
			DisplayName = "溯光",
			RomanizedName = "Sokou",
			DescriptionKey = "hero.sokou.desc",
			DefaultDescription = "25点生命，射线手枪。依靠轮战牌积蓄火力，并用重整重排牌堆。",
			MaxHealth = 25,
			StartingDefense = 0,
			CreateHeroPower = () => new SokouRestructureHeroPower(),
			CreateWeapon = () => new RayPistol(),
		},
	};

	public static HeroProfile Get(string? id)
	{
		if (string.Equals(id, "qimeng", StringComparison.OrdinalIgnoreCase))
			id = "ayame";

		foreach (var profile in All)
		{
			if (string.Equals(profile.Id, id, StringComparison.OrdinalIgnoreCase))
				return profile;
		}

		return All[0];
	}
}
