using System;
using System.Collections.Generic;
using OdysseyCards.Card;
using OdysseyCards.Card.HeroPowers;
using OdysseyCards.Core;

namespace OdysseyCards.AI;

/// <summary>
/// 敌方牌手预设库——把「同构牌手」的数值、卡组厚度、核心卡与被动集中在一处。
///
/// 分层契约：
///   - 普通（Normal / Monster）：10~20 张，16~26 HP
///   - 精英（Elite）：21~39 张，32~42 HP
///   - Boss：40 张，55~80 HP
///
/// 所有工厂返回 <see cref="DeckDrivenCommander"/>，普通/精英/Boss 在机制上完全同构。
/// 旧意图敌人（Cultist / SlimeBoss / WolfRider / ApprenticeMechanic / GuardianBoss /
/// Goutansha / ZhangLang / ShanHu / Cosmonaut）保留为死代码，不再进入主流程遭遇池。
/// </summary>
public static class EnemyDeckProfiles
{
	/// <summary>普通牌手卡组厚度下限。</summary>
	public const int NormalDeckMin = 10;

	/// <summary>普通牌手卡组厚度上限。</summary>
	public const int NormalDeckMax = 20;

	/// <summary>精英牌手卡组厚度下限。</summary>
	public const int EliteDeckMin = 21;

	/// <summary>精英牌手卡组厚度上限。</summary>
	public const int EliteDeckMax = 39;

	/// <summary>Boss 牌手卡组厚度。</summary>
	public const int BossDeckSize = 40;

	// ===== 普通牌手 =====

	/// <summary>实习机械师——机械铺场与护甲增益，20 HP / 14 张。</summary>
	public static DeckDrivenCommander CreateApprentice() => new(new DeckDrivenCommanderSpec
	{
		Id = "apprentice",
		DisplayName = "实习机械师",
		MaxHealth = 20,
		TargetDeckSize = 14,
		Role = "normal",
		WeaponFactory = () => new RollingLog(),
		HeroPowerFactory = () => new IronWillHeroPower(),
		Profile = new DeckProfile
		{
			HeroId = "apprentice",
			ThemeName = "机械整备",
			TargetDeckSize = 14,
			MaxDuplicatesPerCard = 2,
			MaxDomainCards = 2,
			CoreCardIds = new[] { "minion_mech_lancer", "minion_centipede_aa", "spell_nadao_fangyu", "minion_centurion" },
			TagWeights = new Dictionary<CardMechanicTag, int>
			{
				[CardMechanicTag.Mechanics] = 6,
				[CardMechanicTag.Armor] = 4,
				[CardMechanicTag.Summon] = 3,
				[CardMechanicTag.Draw] = 1,
			},
		},
	});

	/// <summary>劫蛋者——亡语/召唤/直伤，26 HP / 16 张，负防御。</summary>
	public static DeckDrivenCommander CreateGoutansha() => new(new DeckDrivenCommanderSpec
	{
		Id = "goutansha",
		DisplayName = "劫蛋者",
		MaxHealth = 26,
		TargetDeckSize = 16,
		StartingDefense = -1,
		Role = "normal",
		WeaponFactory = () => new LightCrucibleClamp(),
		HeroPowerFactory = () => new IronWillHeroPower(),
		Profile = new DeckProfile
		{
			HeroId = "goutansha",
			ThemeName = "臭蛋劫掠",
			TargetDeckSize = 16,
			MaxDuplicatesPerCard = 2,
			MaxDomainCards = 2,
			CoreCardIds = new[]
			{
				"minion_peregrine_drone", "minion_what_the_dog_doing", "spell_blade_crisis",
				"spell_expose", "spell_nano_corpse_art",
			},
			TagWeights = new Dictionary<CardMechanicTag, int>
			{
				[CardMechanicTag.DirectDamage] = 4,
				[CardMechanicTag.Summon] = 4,
				[CardMechanicTag.Mechanics] = 2,
				[CardMechanicTag.StatusApply] = 2,
				[CardMechanicTag.Heal] = 1,
			},
		},
	});

	/// <summary>软泥牌手——低费铺场与护甲，22 HP / 12 张。</summary>
	public static DeckDrivenCommander CreateSlimy() => new(new DeckDrivenCommanderSpec
	{
		Id = "slimy",
		DisplayName = "软泥牌手",
		MaxHealth = 22,
		TargetDeckSize = 12,
		Role = "normal",
		WeaponFactory = () => new RollingLog(),
		HeroPowerFactory = () => new IronWillHeroPower(),
		Profile = new DeckProfile
		{
			HeroId = "slimy",
			ThemeName = "软泥铺场",
			TargetDeckSize = 12,
			MaxDuplicatesPerCard = 2,
			MaxDomainCards = 2,
			CoreCardIds = new[]
			{
				"minion_113th_garrison", "minion_122nd_bugle_battalion", "minion_18th_regiment",
				"spell_assault", "spell_chansu",
			},
			TagWeights = new Dictionary<CardMechanicTag, int>
			{
				[CardMechanicTag.Summon] = 5,
				[CardMechanicTag.Armor] = 3,
				[CardMechanicTag.Buff] = 2,
			},
		},
	});

	/// <summary>狂热学徒——直伤与增益，18 HP / 12 张。</summary>
	public static DeckDrivenCommander CreateCultist() => new(new DeckDrivenCommanderSpec
	{
		Id = "cultist",
		DisplayName = "狂热学徒",
		MaxHealth = 18,
		TargetDeckSize = 12,
		Role = "normal",
		WeaponFactory = () => new RollingLog(),
		HeroPowerFactory = () => new IronWillHeroPower(),
		Profile = new DeckProfile
		{
			HeroId = "cultist",
			ThemeName = "狂热直伤",
			TargetDeckSize = 12,
			MaxDuplicatesPerCard = 2,
			MaxDomainCards = 2,
			CoreCardIds = new[] { "spell_strike", "spell_heavy_strike", "minion_knight_type1", "spell_shock" },
			TagWeights = new Dictionary<CardMechanicTag, int>
			{
				[CardMechanicTag.DirectDamage] = 5,
				[CardMechanicTag.Buff] = 3,
				[CardMechanicTag.StatusApply] = 1,
				[CardMechanicTag.Draw] = 1,
			},
		},
	});

	/// <summary>速攻骑手——薄卡组抢节奏，15 HP / 10 张。</summary>
	public static DeckDrivenCommander CreateWolf() => new(new DeckDrivenCommanderSpec
	{
		Id = "wolf",
		DisplayName = "速攻骑手",
		MaxHealth = 15,
		TargetDeckSize = 10,
		Role = "normal",
		WeaponFactory = () => new LightCrucibleClamp(),
		HeroPowerFactory = () => new IronWillHeroPower(),
		Profile = new DeckProfile
		{
			HeroId = "wolf",
			ThemeName = "速攻突袭",
			TargetDeckSize = 10,
			MaxDuplicatesPerCard = 2,
			MaxDomainCards = 1,
			CoreCardIds = new[] { "spell_assault", "spell_adrenaline", "minion_lianshu_scout", "spell_frag_bullet" },
			TagWeights = new Dictionary<CardMechanicTag, int>
			{
				[CardMechanicTag.DirectDamage] = 4,
				[CardMechanicTag.WeaponSynergy] = 3,
				[CardMechanicTag.Draw] = 1,
			},
			KeywordWeights = new Dictionary<Keyword, int>
			{
				[Keyword.Charge] = 6,
				[Keyword.Windfury] = 2,
			},
		},
	});

	// ===== 精英牌手 =====

	/// <summary>张郎——机械族铺场与武器多段攻击，被动「固璋(3)」。</summary>
	public static DeckDrivenCommander CreateZhangLang(int maxHealth = 24, int targetDeckSize = 24) => new(new DeckDrivenCommanderSpec
	{
		Id = "zhanglang",
		DisplayName = "张郎",
		MaxHealth = maxHealth,
		TargetDeckSize = targetDeckSize,
		DamageCap = 3,
		Role = "elite",
		WeaponFactory = () => new RollingLog(),
		HeroPowerFactory = () => new IronWillHeroPower(),
		Profile = new DeckProfile
		{
			HeroId = "zhanglang",
			ThemeName = "机械蠊群",
			TargetDeckSize = targetDeckSize,
			MaxDuplicatesPerCard = 2,
			MaxDomainCards = 3,
			CoreCardIds = new[]
			{
				"minion_centipede_aa", "minion_centurion", "minion_centipede_siege", "minion_mech_lancer",
			},
			TagWeights = new Dictionary<CardMechanicTag, int>
			{
				[CardMechanicTag.Mechanics] = 5,
				[CardMechanicTag.WeaponSynergy] = 3,
				[CardMechanicTag.Summon] = 2,
				[CardMechanicTag.Armor] = 1,
			},
		},
	});

	/// <summary>珊胡——护甲与守护续航，被动「不破(1)」。</summary>
	public static DeckDrivenCommander CreateShanHu(int maxHealth = 24, int targetDeckSize = 24) => new(new DeckDrivenCommanderSpec
	{
		Id = "shanhu",
		DisplayName = "珊胡",
		MaxHealth = maxHealth,
		TargetDeckSize = targetDeckSize,
		Unbreakable = true,
		Role = "elite",
		WeaponFactory = () => new RollingLog(),
		HeroPowerFactory = () => new IronWillHeroPower(),
		Profile = new DeckProfile
		{
			HeroId = "shanhu",
			ThemeName = "不破守护",
			TargetDeckSize = targetDeckSize,
			MaxDuplicatesPerCard = 2,
			MaxDomainCards = 3,
			CoreCardIds = new[] { "minion_centurion", "spell_nadao_fangyu", "spell_chansu", "spell_alert" },
			TagWeights = new Dictionary<CardMechanicTag, int>
			{
				[CardMechanicTag.Armor] = 5,
				[CardMechanicTag.Heal] = 3,
				[CardMechanicTag.Buff] = 2,
				[CardMechanicTag.Summon] = 1,
			},
		},
	});

	// ===== Boss 牌手 =====

	/// <summary>宇宙员——控制与直伤，70 HP / 40 张，磁轨手枪。</summary>
	public static DeckDrivenCommander CreateCosmonaut() => new(new DeckDrivenCommanderSpec
	{
		Id = "cosmonaut",
		DisplayName = "宇宙员",
		MaxHealth = 70,
		TargetDeckSize = BossDeckSize,
		Role = "boss",
		WeaponFactory = () => new RailPistol(),
		HeroPowerFactory = () => new IronWillHeroPower(),
		Profile = new DeckProfile
		{
			HeroId = "cosmonaut",
			ThemeName = "宇宙冷漠",
			TargetDeckSize = BossDeckSize,
			MaxDuplicatesPerCard = 2,
			MaxDomainCards = 4,
			// 核心池只放非衍生构筑卡：状态牌（如「解释」）由生成器的合法池过滤掉，不再点名。
			CoreCardIds = new[]
			{
				"spell_boundless_darkness", "spell_heavy_strike", "spell_mamba_missile",
				"spell_sutaraito_spirit", "spell_longtermism",
			},
			TagWeights = new Dictionary<CardMechanicTag, int>
			{
				[CardMechanicTag.StatusApply] = 4,
				[CardMechanicTag.DirectDamage] = 4,
				[CardMechanicTag.Draw] = 2,
				[CardMechanicTag.ManaRamp] = 2,
				[CardMechanicTag.Heal] = 1,
			},
		},
	});

	/// <summary>守护者——重甲坦克与大牌压制，60 HP / 40 张。</summary>
	public static DeckDrivenCommander CreateGuardian() => new(new DeckDrivenCommanderSpec
	{
		Id = "guardian",
		DisplayName = "守护者",
		MaxHealth = 60,
		TargetDeckSize = BossDeckSize,
		Role = "boss",
		WeaponFactory = () => new RollingLog(),
		HeroPowerFactory = () => new IronWillHeroPower(),
		Profile = new DeckProfile
		{
			HeroId = "guardian",
			ThemeName = "重装防线",
			TargetDeckSize = BossDeckSize,
			MaxDuplicatesPerCard = 2,
			MaxDomainCards = 4,
			CoreCardIds = new[]
			{
				"minion_40_main_battle_tank", "minion_40a_main_battle_tank", "minion_40b_main_battle_tank",
				"minion_tombstone", "spell_heavy_strike",
			},
			TagWeights = new Dictionary<CardMechanicTag, int>
			{
				[CardMechanicTag.Armor] = 5,
				[CardMechanicTag.Mechanics] = 3,
				[CardMechanicTag.DirectDamage] = 2,
				[CardMechanicTag.Buff] = 2,
				[CardMechanicTag.Draw] = 1,
			},
		},
	});

	// ===== 随机遭遇 =====

	/// <summary>普通房间遭遇池（全部为同构牌手）。</summary>
	public static IReadOnlyList<Func<DeckDrivenCommander>> MonsterFactories { get; } = new Func<DeckDrivenCommander>[]
	{
		CreateApprentice,
		CreateGoutansha,
		CreateSlimy,
		CreateCultist,
		CreateWolf,
	};

	/// <summary>精英房间非宿敌遭遇池（张郎 / 珊胡 / 双人组合）。</summary>
	public static IReadOnlyList<Func<IReadOnlyList<DeckDrivenCommander>>> EliteFactories { get; } =
		new Func<IReadOnlyList<DeckDrivenCommander>>[]
		{
			() => new[] { CreateZhangLang() },
			() => new[] { CreateShanHu() },
			() => new[] { CreateZhangLang(22, 22), CreateShanHu(22, 22) },
		};

	/// <summary>Boss 房间非宿敌遭遇池（宇宙员 / 守护者）。</summary>
	public static IReadOnlyList<Func<DeckDrivenCommander>> BossFactories { get; } = new Func<DeckDrivenCommander>[]
	{
		CreateCosmonaut,
		CreateGuardian,
	};

	/// <summary>随机取一位普通牌手。</summary>
	public static DeckDrivenCommander CreateRandomMonster(Random? rng = null)
		=> MonsterFactories[(rng ?? Random.Shared).Next(MonsterFactories.Count)]();

	/// <summary>随机取一组非宿敌精英。</summary>
	public static IReadOnlyList<DeckDrivenCommander> CreateRandomElite(Random? rng = null)
		=> EliteFactories[(rng ?? Random.Shared).Next(EliteFactories.Count)]();

	/// <summary>随机取一位非宿敌 Boss。</summary>
	public static DeckDrivenCommander CreateRandomBoss(Random? rng = null)
		=> BossFactories[(rng ?? Random.Shared).Next(BossFactories.Count)]();
}
