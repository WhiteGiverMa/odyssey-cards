using System;

namespace OdysseyCards.Core;

/// <summary>
/// 伤害事件信息——传递给 UI 层的伤害/护甲吸收数据。
/// 纯 C# 结构体，不依赖 Godot。
/// </summary>
public readonly struct DamageEventInfo
{
	/// <summary>最终生命值减少量（所有减免后的 HP 损失）。完全格挡时为 0。</summary>
	public int HpLost { get; }

	/// <summary>被护甲吸收的伤害量。</summary>
	public int ArmorAbsorbed { get; }

	/// <summary>伤害是否被完全格挡（无 HP 损失）。</summary>
	public bool WasFullyBlocked { get; }

	public DamageEventInfo(int hpLost, int armorAbsorbed, bool wasFullyBlocked)
	{
		HpLost = hpLost;
		ArmorAbsorbed = armorAbsorbed;
		WasFullyBlocked = wasFullyBlocked;
	}
}
