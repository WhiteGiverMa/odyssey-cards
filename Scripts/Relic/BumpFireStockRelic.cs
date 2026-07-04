namespace OdysseyCards.Relic;
using OdysseyCards.Combat;

/// <summary>
/// 撞火枪托——英雄武器每回合可攻击次数 +1。
/// 在战斗开始时通过修改 <c>Weapon.AttacksPerTurn</c> 实现。
/// </summary>
public sealed class BumpFireStockRelic : AbstractRelic
{
	public override string Id => "bump_fire_stock";
	public override string Name => "撞火枪托";
	public override string Description => "你的英雄武器每回合可攻击次数+1。";

	public override void OnBattleStart(CombatManager combat)
	{
		if (combat.PlayerHero.Weapon != null)
			combat.PlayerHero.Weapon.AttacksPerTurn += 1;
	}
}
