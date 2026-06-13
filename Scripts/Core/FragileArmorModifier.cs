namespace OdysseyCards.Core;

/// <summary>
/// 脆弱——护甲获得量 × 倍率。
/// 基础倍率 0.75（STS2 FrailPower），可被「总观效应」降至 0.5。
/// </summary>
public class FragileArmorModifier
{
	public bool IsActive { get; set; }
	public float BaseMultiplier { get; set; } = 0.75f;
	public float ExtraMultiplier { get; set; }
	private float EffectiveMultiplier => System.Math.Max(0f, BaseMultiplier + ExtraMultiplier);

	public int ModifyArmorGain(int amount)
	{
		if (!IsActive || amount <= 0)
			return amount;
		return (int)System.MathF.Round(amount * EffectiveMultiplier, System.MidpointRounding.AwayFromZero);
	}
}
