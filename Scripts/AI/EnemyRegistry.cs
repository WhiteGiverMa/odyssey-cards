using System;
using System.Collections.Generic;
using System.Linq;
using OdysseyCards.Core;

namespace OdysseyCards.AI;

/// <summary>
/// 敌人注册表——提供 ID → 敌人实例的映射，供 /fight 命令和测试使用。
/// </summary>
public static class EnemyRegistry
{
	private static readonly Dictionary<string, Func<IReadOnlyList<EnemyEncounter>>> _registry = new()
	{
		["cultist"] = () => new EnemyEncounter[] { new Cultist() },
		["slimy"] = () => new EnemyEncounter[] { new SlimeBoss() },
		["wolf"] = () => new EnemyEncounter[] { new WolfRider() },
		["guardian"] = () => new EnemyEncounter[] { new GuardianBoss() },
		["zhanglang"] = () => new EnemyEncounter[] { new ZhangLang() },
		["shanhu"] = () => new EnemyEncounter[] { new ShanHu() },
		["zhangshan"] = () => new EnemyEncounter[] { new ZhangLang(), new ShanHu() },
		["apprentice"] = () => new EnemyEncounter[] { new ApprenticeMechanic() },
		["cosmonaut"] = () => new EnemyEncounter[] { new Cosmonaut() },
		["goutansha"] = () => new EnemyEncounter[] { new Goutansha() },
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
