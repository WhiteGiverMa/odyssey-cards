namespace OdysseyCards.Core;

/// <summary>
/// 伤害计算上下文，包含所有相关信息。
/// </summary>
public readonly struct DamageContext
{
	/// <summary>
	/// 伤害来源（攻击者/效果来源）。
	/// </summary>
	public IDamageSource Source { get; }

	/// <summary>
	/// 伤害目标（防御者）。
	/// </summary>
	public IDamageTarget Target { get; }

	/// <summary>
	/// 伤害结算类型。
	/// </summary>
	public DamageKind Kind { get; }

	/// <summary>
	/// 是否跳过目标防御力减免。
	/// </summary>
	public bool IgnoresDefense => Kind == DamageKind.Effect;

	/// <summary>
	/// 创建 DamageContext。
	/// </summary>
	public DamageContext(IDamageSource source, IDamageTarget target, DamageKind kind = DamageKind.Attack)
	{
		Source = source;
		Target = target;
		Kind = kind;
	}

	/// <summary>
	/// 创建预览模式的 DamageContext（无目标）。
	/// </summary>
	public static DamageContext ForPreview(IDamageSource source)
	{
		return new DamageContext(source, null);
	}
}
