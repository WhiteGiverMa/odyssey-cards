namespace OdysseyCards.Core;

/// <summary>
/// 效果类别，用于 UI 层排序和显示优先级。
/// StatusEffect(0) < Domain(1) < StatBuff(2) < Keyword(3) < Modifier(4)
/// </summary>
public enum EffectCategory
{
	/// <summary>计时衰减的状态效果（attack_zero、meltdown 等）</summary>
	StatusEffect,
	/// <summary>持久领域效果（unlimited_potential、flying_away 等）</summary>
	Domain,
	/// <summary>数值增益/减益（攻击力、防御力变化）</summary>
	StatBuff,
	/// <summary>授予的关键词（按来源分组，如诱饵战术）</summary>
	Keyword,
	/// <summary>其他运行时修饰（亡语替换等）</summary>
	Modifier,
}

/// <summary>
/// 统一的效果显示数据。
/// 由 Minion.GetDisplayableEffects() / Hero.GetDisplayableEffects() 聚合生成，
/// UI 层直接消费，不理解效果来源。
/// </summary>
public readonly struct DisplayableEffect
{
	/// <summary>Emoji 图标字符（如 "🔒"、"🔥"、"🛡"）</summary>
	public string Icon { get; init; }

	/// <summary>本地化显示名称</summary>
	public string Name { get; init; }

	/// <summary>层数/数值。0 表示不显示数字标签。</summary>
	public int Stacks { get; init; }

	/// <summary>Tooltip 描述文本</summary>
	public string Description { get; init; }

	/// <summary>true = buff（绿色），false = debuff（红色）</summary>
	public bool IsBuff { get; init; }

	/// <summary>效果类别</summary>
	public EffectCategory Category { get; init; }

	/// <summary>来源标识（如 "bait_tactics"），用于同源效果分组</summary>
	public string? SourceId { get; init; }

	/// <summary>
	/// 显示排序键。数值越小越靠左。
	/// </summary>
	public int SortOrder => (int)Category * 10;
}
