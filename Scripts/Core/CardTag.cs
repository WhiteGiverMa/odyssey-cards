using System;

namespace OdysseyCards.Core;

/// <summary>
/// 卡牌多标签系统（[Flags]）。
/// 用于种族、阵营、机制等任意语义标签，不限于「种族」一种概念。
/// 当前已定义的标签：
///   Mechanics(1) — 机械族
/// </summary>
[Flags]
public enum CardTag
{
	/// <summary>无标签</summary>
	None = 0,

	/// <summary>机械族</summary>
	Mechanics = 1,
}
