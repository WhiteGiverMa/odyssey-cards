using Godot;
using OdysseyCards.Combat;

namespace OdysseyCards.Relic;

/// <summary>
/// 冰袋：「微妙」藏品。
/// 战斗开始时少获得2个法力水晶。抑制敌人20%的热力值。
/// </summary>
public sealed class IceBagRelic : AbstractRelic
{
	public override string Id => "ice_bag";
	public override string Name => "冰袋";
	public override string Description => "战斗开始时少获得2个法力水晶。抑制敌人20%的热力值。";

	public override bool IsBeneficial => false;
	public override bool IsSubtle => true;

	private const float HeatReduction = 0.2f; // 20个百分点

	public override void OnBattleStart(CombatManager combat)
	{
		// 少获得2个法力水晶：降低当前和最大法力
		int currentMana = combat.PlayerHero.CurrentMana;
		int maxMana = combat.PlayerHero.MaxMana;

		int newCurrent = System.Math.Max(0, currentMana - 2);
		int newMax = System.Math.Max(0, maxMana - 2);

		combat.PlayerHero.SetMana(newCurrent, newMax);
		GD.Print($"[IceBag] 法力水晶：{currentMana}/{maxMana} → {newCurrent}/{newMax}");
	}

	public override void ModifyHeatSystem(Heat.HeatSystem heat)
	{
		heat.ApplyFlatReduction(HeatReduction);
		GD.Print($"[IceBag] 热力值降低 {HeatReduction * 100f} 个百分点");
	}
}
