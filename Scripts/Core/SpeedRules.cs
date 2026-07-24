using System;

namespace OdysseyCards.Core;

/// <summary>
/// 单位速度的数据契约。
/// 速度是追击与被追击能力，不决定回合顺序或攻击次数。
/// </summary>
public static class SpeedRules
{
	public const int Min = 1;
	public const int Max = 5;
	public const int Default = 2;
	public const int Taunt = 3;

	public static int Clamp(int speed) => Math.Clamp(speed, Min, Max);
}
