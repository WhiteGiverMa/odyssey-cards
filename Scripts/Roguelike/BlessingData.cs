namespace OdysseyCards.Roguelike;

/// <summary>
/// 祝福（金血祝颂）数据模型。后续由用户扩展为完整的英雄级buff系统。
/// 当前为占位符实现，仅提供名称和描述，无实际战斗效果。
/// </summary>
public class BlessingData
{
	/// <summary>祝福唯一标识符。</summary>
	public string Id { get; set; } = "";

	/// <summary>祝福显示名称。</summary>
	public string Name { get; set; } = "";

	/// <summary>祝福效果描述。</summary>
	public string Description { get; set; } = "";
}

/// <summary>
/// 提供占位祝福池（3 个金血祝颂占位符）。
/// 后续版本将接入实际战斗效果逻辑。
/// </summary>
public static class BlessingPool
{
	public static readonly BlessingData[] Placeholders = new[]
	{
		new BlessingData
		{
			Id = "blessing_vigor",
			Name = Localization.Localization.T("blessing.vigor.name", "活力祝颂"),
			Description = Localization.Localization.T("blessing.vigor.desc", "战斗开始时获得2点法力（占位符）"),
		},
		new BlessingData
		{
			Id = "blessing_fortitude",
			Name = Localization.Localization.T("blessing.fortitude.name", "坚韧祝颂"),
			Description = Localization.Localization.T("blessing.fortitude.desc", "获得3点最大生命值（占位符）"),
		},
		new BlessingData
		{
			Id = "blessing_ferocity",
			Name = Localization.Localization.T("blessing.ferocity.name", "狂怒祝颂"),
			Description = Localization.Localization.T("blessing.ferocity.desc", "每回合首次攻击+1伤害（占位符）"),
		},
	};
}
