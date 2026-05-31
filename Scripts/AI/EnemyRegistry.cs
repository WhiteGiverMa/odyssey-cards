using System;
using System.Collections.Generic;

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
    };

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
