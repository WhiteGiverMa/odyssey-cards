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
	public string Id { get; init; } = "qimeng";
	public string NameKey { get; init; } = "hero.qimeng.name";
	public string DisplayName { get; init; } = "绮梦";
	public string RomanizedName { get; init; } = "Qimeng";
	public string DescriptionKey { get; init; } = "hero.qimeng.desc";
	public string DefaultDescription { get; init; } = "初始英雄。30点生命，离子手枪与铁腕。";
	public int MaxHealth { get; init; } = 30;
	public int StartingDefense { get; init; }
	public Func<IHeroPower> CreateHeroPower { get; init; } = () => new IronWillHeroPower();
	public Func<Weapon> CreateWeapon { get; init; } = () => new IonPistol();

	public static readonly IReadOnlyList<HeroProfile> All = new[]
	{
		new HeroProfile(),
		new HeroProfile
		{
			Id = "rie",
			NameKey = "hero.rie.name",
			DisplayName = "理恵",
			RomanizedName = "Rie",
			DescriptionKey = "hero.rie.desc",
			DefaultDescription = "25点生命，SVDS-M338。擅长连射、撕裂与直伤法术检索。",
			MaxHealth = 25,
			StartingDefense = 0,
			CreateHeroPower = () => new RieSuppressingFireHeroPower(),
			CreateWeapon = () => new SvdsM338(),
		},
	};

	public static HeroProfile Get(string? id)
	{
		foreach (var profile in All)
		{
			if (string.Equals(profile.Id, id, StringComparison.OrdinalIgnoreCase))
				return profile;
		}

		return All[0];
	}
}
