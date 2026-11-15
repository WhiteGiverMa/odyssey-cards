using OdysseyCards.Core;
using OdysseyCards.UI;
using Xunit;

namespace OdysseyCards.Tests.Unit;

/// <summary>M4 战斗 UI 纯逻辑：兵种矢量徽章映射与一步突击 50/50 彩蛋边界。</summary>
public class M4UiLogicTests
{
	[Theory]
	[InlineData(UnitClass.Vanguard, TacticalUnitIcon.Vanguard)]
	[InlineData(UnitClass.Assault, TacticalUnitIcon.Assault)]
	[InlineData(UnitClass.Support, TacticalUnitIcon.Support)]
	[InlineData(UnitClass.Skirmisher, TacticalUnitIcon.Skirmisher)]
	public void GetTacticalUnitIcon_AllClasses_MapsOneToOne(UnitClass unitClass, TacticalUnitIcon expected)
	{
		Assert.Equal(expected, BoardUI.GetTacticalUnitIcon(unitClass));
	}

	[Theory]
	[InlineData(-1, false)]
	[InlineData(0, true)]
	[InlineData(49, true)]
	[InlineData(50, false)]
	[InlineData(99, false)]
	public void IsFireOnMove_RollRange_SplitsFiftyFifty(int roll, bool expected)
	{
		Assert.Equal(expected, AssaultAdvanceVfx.IsFireOnMove(roll));
	}
}
