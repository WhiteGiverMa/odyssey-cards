using System;

namespace OdysseyCards.AI.Intents;

/// <summary>
/// 法术伤害意图。
/// 使用施法图标，但保留攻击类的数值标签与动态伤害重算能力。
/// </summary>
public sealed class SpellDamageIntent : AttackIntent
{
	private readonly int _repeats;

	public override IntentType Type => IntentType.SpellCast;

	public override string IntentPrefix => "SPELL_CAST";

	public override int Repeats => _repeats;

	public string SpellName { get; }

	public string SpellDescription { get; }

	public SpellDamageIntent(string spellName, string spellDescription, int damage, int repeats = 1)
	{
		SpellName = spellName;
		SpellDescription = spellDescription;
		DamageCalc = _ => damage;
		_repeats = repeats;
	}

	public SpellDamageIntent(string spellName, string spellDescription, Func<Combat.CombatManager, int> damageCalc, int repeats = 1)
	{
		SpellName = spellName;
		SpellDescription = spellDescription;
		DamageCalc = damageCalc;
		_repeats = repeats;
	}

	public override string GetIntentDescription(Combat.CombatManager combat) => SpellDescription;

	public override IntentHoverTip GetHoverTip(Combat.CombatManager combat)
	{
		string title = OdysseyCards.Localization.Localization.T("intents.SPELL_CAST.title", "施法");
		return new IntentHoverTip($"{title}: {SpellName}", SpellDescription, isDebuff: true);
	}
}
