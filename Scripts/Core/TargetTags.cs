using System;

namespace OdysseyCards.Core;

/// <summary>
/// 目标标签标志枚举。
/// 每个游戏实体（英雄/随从）计算其标签掩码；
/// 卡牌定义目标过滤条件，通过子集匹配判定合法性。
/// 空集（None）表示无限制——对任意目标有效。
/// </summary>
[Flags]
public enum TargetTags
{
	/// <summary>空集 = 任意目标均可选择</summary>
	None = 0,

	/// <summary>我方（玩家方）</summary>
	Friendly = 1 << 0,

	/// <summary>敌方</summary>
	Enemy = 1 << 1,

	/// <summary>英雄单位</summary>
	Hero = 1 << 2,

	/// <summary>随从单位</summary>
	Minion = 1 << 3,
}

/// <summary>
/// 目标标签静态辅助方法。
/// </summary>
public static class TargetTagsHelper
{
	/// <summary>
	/// 判断实体标签是否满足卡牌的目标过滤条件。
	/// 验证两步：
	/// 1. 必须匹配：require 是 entityTags 的子集（None 表示不限制）
	/// 2. 排除匹配：entityTags 不能同时拥有 exclude 中的所有标签（None 表示不排除）
	/// 两者都通过才算合法目标。
	/// </summary>
	/// <param name="entityTags">目标实体的标签掩码</param>
	/// <param name="require">必须包含的标签</param>
	/// <param name="exclude">不能同时包含的标签</param>
	/// <returns>true 表示该实体是卡牌的合法目标</returns>
	public static bool IsValidTarget(TargetTags entityTags, TargetTags require, TargetTags exclude)
	{
		bool hasRequired = require == TargetTags.None || (entityTags & require) == require;
		bool notExcluded = exclude == TargetTags.None || (entityTags & exclude) != exclude;
		return hasRequired && notExcluded;
	}

	/// <summary>
	/// 简化版验证（无排除条件），向后兼容。
	/// </summary>
	public static bool IsValidTarget(TargetTags entityTags, TargetTags filter)
	{
		return IsValidTarget(entityTags, filter, TargetTags.None);
	}
}
