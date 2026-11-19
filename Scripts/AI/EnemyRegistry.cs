using System;
using System.Collections.Generic;
using System.Linq;
using OdysseyCards.Core;

namespace OdysseyCards.AI;

/// <summary>
/// 敌人注册表——提供 ID → 敌人实例的映射，供 /fight 命令和测试使用。
///
/// 当前全员同构：所有条目都返回 <see cref="DeckDrivenCommander"/>（真实卡组 + 抽牌 + 法力）。
/// 普通 / 精英 / Boss 的差异只体现在卡组厚度与生命值上。
/// </summary>
public static class EnemyRegistry
{
	private static readonly Dictionary<string, Func<IReadOnlyList<EnemyEncounter>>> _registry = new()
	{
		["cultist"] = () => new EnemyEncounter[] { EnemyDeckProfiles.CreateCultist() },
		["slimy"] = () => new EnemyEncounter[] { EnemyDeckProfiles.CreateSlimy() },
		["wolf"] = () => new EnemyEncounter[] { EnemyDeckProfiles.CreateWolf() },
		["guardian"] = () => new EnemyEncounter[] { EnemyDeckProfiles.CreateGuardian() },
		["zhanglang"] = () => new EnemyEncounter[] { EnemyDeckProfiles.CreateZhangLang() },
		["shanhu"] = () => new EnemyEncounter[] { EnemyDeckProfiles.CreateShanHu() },
		["zhangshan"] = () => new EnemyEncounter[]
		{
			EnemyDeckProfiles.CreateZhangLang(22, 22),
			EnemyDeckProfiles.CreateShanHu(22, 22),
		},
		["apprentice"] = () => new EnemyEncounter[] { EnemyDeckProfiles.CreateApprentice() },
		["cosmonaut"] = () => new EnemyEncounter[] { EnemyDeckProfiles.CreateCosmonaut() },
		["goutansha"] = () => new EnemyEncounter[] { EnemyDeckProfiles.CreateGoutansha() },
		["commander_ayame"] = () => new EnemyEncounter[] { DeckDrivenCommander.CreateElite("ayame") },
		["commander_rie"] = () => new EnemyEncounter[] { DeckDrivenCommander.CreateElite("rie") },
		["commander_sokou"] = () => new EnemyEncounter[] { DeckDrivenCommander.CreateElite("sokou") },
	};

	/// <summary>可选宿敌指挥官英雄 ID（排除玩家当前所选）。</summary>
	public static IReadOnlyList<string> GetRivalCommanderIds(string? playerHeroId)
	{
		var excluded = HeroProfile.Get(playerHeroId).Id;
		return DeckDrivenCommander.CommanderHeroIds
			.Where(id => !string.Equals(id, excluded, StringComparison.OrdinalIgnoreCase))
			.ToList();
	}

	/// <summary>为精英/Boss 房间创建一位宿敌指挥官，自动避开玩家自身英雄。</summary>
	public static EnemyEncounter CreateRivalCommander(bool boss, string? playerHeroId, Random? rng = null)
	{
		var rivals = GetRivalCommanderIds(playerHeroId);
		string heroId = rivals[(rng ?? Random.Shared).Next(rivals.Count)];
		return boss ? DeckDrivenCommander.CreateBoss(heroId) : DeckDrivenCommander.CreateElite(heroId);
	}

	/// <summary>
	/// 根据 ID 创建敌人遭遇列表。支持 "/fight zhangshan" 等命令。
	/// </summary>
	public static IReadOnlyList<EnemyEncounter> Create(string id)
	{
		if (_registry.TryGetValue(id.ToLowerInvariant(), out var factory))
			return factory();

		Godot.GD.PrintErr($"[EnemyRegistry] 未知敌人 ID: {id}，可用: {string.Join(", ", _registry.Keys)}");
		return Array.Empty<EnemyEncounter>();
	}

	/// <summary>
	/// 获取所有可用敌人 ID。
	/// </summary>
	public static IReadOnlyCollection<string> AllIds => _registry.Keys;
}
