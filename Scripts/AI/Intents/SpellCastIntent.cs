namespace OdysseyCards.AI.Intents;

/// <summary>
/// 施法意图——敌人打出一张具名卡牌/法术。
/// 本项目特有类型，用于「无边黑暗」等敌人专属法术的意图显示。
/// 塔2中无对应类型（塔2用 DebuffIntent 表示类似效果）。
/// </summary>
public sealed class SpellCastIntent : AbstractIntent
{
	/// <inheritdoc />
	public override IntentType Type => IntentType.SpellCast;

	/// <inheritdoc />
	public override string IntentPrefix => "SPELL_CAST";

	/// <summary>
	/// 施放的法术名称。用于 UI 描述和标签。
	/// </summary>
	public string SpellName { get; }

	/// <summary>
	/// 施放的法术描述。用于悬停提示。
	/// </summary>
	public string SpellDescription { get; }

	/// <summary>
	/// 创建施法意图。
	/// </summary>
	/// <param name="spellName">法术名称（如「无边黑暗」）</param>
	/// <param name="spellDescription">法术描述</param>
	public SpellCastIntent(string spellName, string spellDescription)
	{
		SpellName = spellName;
		SpellDescription = spellDescription;
	}

	/// <inheritdoc />
	public override string GetIntentLabel(Combat.CombatManager combat)
	{
		// 施法不显示数字标签
		return "";
	}

	/// <inheritdoc />
	public override string GetIntentDescription(Combat.CombatManager combat)
	{
		return SpellDescription;
	}

	/// <inheritdoc />
	public override IntentHoverTip GetHoverTip(Combat.CombatManager combat)
	{
		string titleKey = $"intents.SPELL_CAST.title";
		string title = OdysseyCards.Localization.Localization.T(titleKey, "施法");
		return new IntentHoverTip($"{title}: {SpellName}", SpellDescription, isDebuff: true);
	}
}
