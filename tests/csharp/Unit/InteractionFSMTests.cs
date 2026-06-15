using System;
using Godot;
using Xunit;
using OdysseyCards.UI;

namespace OdysseyCards.Tests.Unit;

/// <summary>
/// 单元测试 — InteractionFsm 交互状态机。
/// 覆盖全部状态转换、拖拽阈值、PlayZone/CancelZone 区域判定、
/// 并发交互防护与边界情况。
/// 当前 InteractionFsm.cs 骨架为桩实现（Tick 无逻辑、事件永不触发），
/// 所有测试应显示 FAIL（RED 阶段确认）。
/// </summary>
public class InteractionFsmTests
{
	// ===== 状态转换测试 =====

	[Fact]
	public void IdleToCardPickedUp_OnPickUpCard()
	{
		var fsm = new InteractionFsm();
		int phaseChanged = 0;
		fsm.OnPhaseChanged += (from, to) => phaseChanged++;

		fsm.PickUpCard(new Vector2(100, 500), isClickSelect: false, isMobile: false);

		Assert.Equal(InteractionPhase.CardPickedUp, fsm.CurrentPhase);
		Assert.True(phaseChanged > 0, "PickUpCard 应触发 OnPhaseChanged");
	}

	[Fact]
	public void CardPickedUpToIdle_OnCancel()
	{
		var fsm = new InteractionFsm();
		int phaseChanged = 0;
		fsm.OnPhaseChanged += (from, to) => phaseChanged++;
		fsm.PickUpCard(new Vector2(100, 500), isClickSelect: false, isMobile: false);

		fsm.ForceReset();

		Assert.Equal(InteractionPhase.Idle, fsm.CurrentPhase);
		Assert.True(phaseChanged >= 2, "PickUpCard 和 ForceReset 各应触发一次 OnPhaseChanged");
	}

	[Fact]
	public void CardPickedUpToTargeting_OnDrag()
	{
		var fsm = new InteractionFsm();
		fsm.PickUpCard(new Vector2(100, 500), isClickSelect: false, isMobile: false);

		// 模拟拖拽到棋盘区域——应触发 BoardDrag 阶段（拖拽超过阈值时自动切换）
		fsm.Tick(new Vector2(200, 300), isPointerDown: true, isRightDown: false, 1080f, 500f);

		// 跨越多帧拖拽，阶段应切换为 BoardDrag
		fsm.Tick(new Vector2(200, 100), isPointerDown: true, isRightDown: false, 1080f, 500f);

		Assert.Equal(InteractionPhase.BoardDrag, fsm.CurrentPhase);
	}

	[Fact]
	public void TargetingToIdle_OnCancel()
	{
		var fsm = new InteractionFsm();
		int phaseChanged = 0;
		fsm.OnPhaseChanged += (from, to) => phaseChanged++;
		fsm.EnterTargeting();
		Assert.Equal(InteractionPhase.Targeting, fsm.CurrentPhase);

		fsm.ForceReset();

		Assert.Equal(InteractionPhase.Idle, fsm.CurrentPhase);
		Assert.True(phaseChanged >= 2, "EnterTargeting 和 ForceReset 各应触发一次 OnPhaseChanged");
	}

	[Fact]
	public void ForceResetFromAnyState_ReturnsToIdle()
	{
		// BoardDrag 阶段尚无公开入口方法，改用 Targeting 阶段验证 ForceReset 通用性。
		var fsm = new InteractionFsm();
		int phaseChanged = 0;
		fsm.OnPhaseChanged += (from, to) => phaseChanged++;

		// 从 Targeting 阶段 ForceReset
		fsm.EnterTargeting();
		Assert.Equal(InteractionPhase.Targeting, fsm.CurrentPhase);
		fsm.ForceReset();
		Assert.Equal(InteractionPhase.Idle, fsm.CurrentPhase);

		// 从 CardPickedUp 阶段 ForceReset
		fsm.PickUpCard(new Vector2(0, 0), false, false);
		fsm.ForceReset();
		Assert.Equal(InteractionPhase.Idle, fsm.CurrentPhase);

		// ForceReset 应触发 OnPhaseChanged（目标阶段→Idle）
		Assert.True(phaseChanged >= 3, "EnterTargeting、PickUpCard、两次 ForceReset 各应触发 OnPhaseChanged");
	}

	[Fact]
	public void PhaseChangedEvent_FiresOnTransition()
	{
		var fsm = new InteractionFsm();
		InteractionPhase? lastFrom = null;
		InteractionPhase? lastTo = null;
		int fireCount = 0;
		fsm.OnPhaseChanged += (from, to) =>
		{
			lastFrom = from;
			lastTo = to;
			fireCount++;
		};

		fsm.PickUpCard(new Vector2(100, 500), isClickSelect: true, isMobile: false);

		Assert.True(fireCount > 0, "OnPhaseChanged 应在 PickUpCard 时至少触发一次");
		Assert.Equal(InteractionPhase.Idle, lastFrom);
		Assert.Equal(InteractionPhase.CardPickedUp, lastTo);
	}

	// ===== 拖拽阈值测试 =====

	[Fact]
	public void DesktopDrag_9px_DoesNotTrigger()
	{
		var fsm = new InteractionFsm();
		int dragMoveCount = 0;
		fsm.OnDragMove += (pos, inPlay, inCancel) => dragMoveCount++;
		fsm.PickUpCard(new Vector2(100, 500), isClickSelect: false, isMobile: false);

		// 移动 9px（X 方向）→ DistanceSquared = 81 < 100（桌面阈值 10px²）
		fsm.Tick(new Vector2(109, 500), isPointerDown: true, isRightDown: false, 1080f, 500f);

		Assert.False(fsm.HasDragged, "移动 9px 未超过桌面阈值 10px，不应触发拖拽");
		Assert.True(dragMoveCount > 0, "任何指针移动都应触发 OnDragMove");
	}

	[Fact]
	public void DesktopDrag_11px_TriggersDragged()
	{
		var fsm = new InteractionFsm();
		fsm.PickUpCard(new Vector2(100, 500), isClickSelect: false, isMobile: false);

		// 移动 11px（X 方向）→ DistanceSquared = 121 > 100（桌面阈值 10px²）
		fsm.Tick(new Vector2(111, 500), isPointerDown: true, isRightDown: false, 1080f, 500f);

		Assert.True(fsm.HasDragged, "移动 11px 超过桌面阈值 10px，应触发 HasDragged");
	}

	[Fact]
	public void ClickSelect_MouseMoveWithoutPointerDown_DoesNotTriggerDragged()
	{
		var fsm = new InteractionFsm();
		fsm.PickUpCard(new Vector2(100, 500), isClickSelect: true, isMobile: false);

		// 点击选中后，玩家会把鼠标移动到目标上再第二击；未按住主按钮时不应被判定为拖拽。
		fsm.Tick(new Vector2(400, 200), isPointerDown: false, isRightDown: false, 1080f, 500f);

		Assert.False(fsm.HasDragged, "未按住主按钮时移动鼠标不应触发拖拽状态");
		Assert.Equal(InteractionPhase.CardPickedUp, fsm.CurrentPhase);
	}

	[Fact]
	public void MobileDrag_19px_DoesNotTrigger()
	{
		var fsm = new InteractionFsm();
		int dragMoveCount = 0;
		fsm.OnDragMove += (pos, inPlay, inCancel) => dragMoveCount++;
		fsm.PickUpCard(new Vector2(100, 500), isClickSelect: false, isMobile: true);

		// 移动 19px（X 方向）→ DistanceSquared = 361 < 400（移动端阈值 20px²）
		fsm.Tick(new Vector2(119, 500), isPointerDown: true, isRightDown: false, 1080f, 500f);

		Assert.False(fsm.HasDragged, "移动 19px 未超过移动端阈值 20px，不应触发拖拽");
		Assert.True(dragMoveCount > 0, "任何指针移动都应触发 OnDragMove");
	}

	[Fact]
	public void MobileDrag_21px_TriggersDragged()
	{
		var fsm = new InteractionFsm();
		fsm.PickUpCard(new Vector2(100, 500), isClickSelect: false, isMobile: true);

		// 移动 21px（X 方向）→ DistanceSquared = 441 > 400（移动端阈值 20px²）
		fsm.Tick(new Vector2(121, 500), isPointerDown: true, isRightDown: false, 1080f, 500f);

		Assert.True(fsm.HasDragged, "移动 21px 超过移动端阈值 20px，应触发 HasDragged");
	}

	// ===== 拖拽完成测试 =====

	[Fact]
	public void DragRelease_WithDragged_TriggersOnDrop()
	{
		var fsm = new InteractionFsm();
		int dropCount = 0;
		bool lastWasDrag = false;
		fsm.OnDrop += (pos, wasDrag) => { dropCount++; lastWasDrag = wasDrag; };
		fsm.PickUpCard(new Vector2(100, 500), isClickSelect: false, isMobile: false);

		// 拖拽超过阈值
		fsm.Tick(new Vector2(115, 400), isPointerDown: true, isRightDown: false, 1080f, 500f);
		// 松手
		fsm.Tick(new Vector2(115, 400), isPointerDown: false, isRightDown: false, 1080f, 500f);

		Assert.True(dropCount > 0, "拖拽松手应触发 OnDrop");
		Assert.True(lastWasDrag, "wasDrag 应为 true");
	}

	[Fact]
	public void ClickSelectRelease_WithoutMove_StaysInClickSelect()
	{
		var fsm = new InteractionFsm();
		int dropCount = 0;
		int cancelCount = 0;
		fsm.OnDrop += (pos, wasDrag) => dropCount++;
		fsm.OnCancel += () => cancelCount++;
		fsm.PickUpCard(new Vector2(200, 600), isClickSelect: true, isMobile: false);

		// 点击选中后直接松手（未移动）→ FSM 保持在 clickSelect 模式，
		// 不触发 OnDrop 也不触发 OnCancel
		// 需要先建立按下状态再松手（边缘触发）
		fsm.Tick(new Vector2(200, 600), isPointerDown: true, isRightDown: false, 1080f, 600f);
		fsm.Tick(new Vector2(200, 600), isPointerDown: false, isRightDown: false, 1080f, 600f);

		Assert.True(dropCount == 0, "点击选中无位移松手不应触发 OnDrop");
		Assert.True(cancelCount == 0, "点击选中无位移松手不应触发 OnCancel");
		Assert.Equal(InteractionPhase.CardPickedUp, fsm.CurrentPhase);
	}

	[Fact]
	public void DragRelease_WithoutDragged_TriggersOnCancel()
	{
		var fsm = new InteractionFsm();
		int cancelCount = 0;
		int dropCount = 0;
		fsm.OnCancel += () => cancelCount++;
		fsm.OnDrop += (pos, wasDrag) => dropCount++;
		fsm.PickUpCard(new Vector2(100, 500), isClickSelect: false, isMobile: false);

		// 非点击选中模式，按住但未移动即松手 → 取消
		// 需要先建立按下状态再松手（边缘触发）
		fsm.Tick(new Vector2(105, 500), isPointerDown: true, isRightDown: false, 1080f, 500f);
		fsm.Tick(new Vector2(105, 500), isPointerDown: false, isRightDown: false, 1080f, 500f);

		Assert.True(cancelCount > 0, "非点击选中且未拖拽时松手应触发 OnCancel");
		Assert.Equal(0, dropCount);
	}

	// ===== 右键取消测试 =====

	[Fact]
	public void RightClickDuringDrag_TriggersOnCancel()
	{
		var fsm = new InteractionFsm();
		int cancelCount = 0;
		fsm.OnCancel += () => cancelCount++;
		fsm.PickUpCard(new Vector2(100, 500), isClickSelect: false, isMobile: false);

		// 拖拽中右键按下
		fsm.Tick(new Vector2(100, 500), isPointerDown: true, isRightDown: true, 1080f, 500f);

		Assert.True(cancelCount > 0, "拖拽中右键应触发 OnCancel");
	}

	[Fact]
	public void RightClickDoesNotFireOnDrop()
	{
		var fsm = new InteractionFsm();
		int dropCount = 0;
		int cancelCount = 0;
		fsm.OnDrop += (pos, wasDrag) => dropCount++;
		fsm.OnCancel += () => cancelCount++;
		fsm.PickUpCard(new Vector2(100, 500), isClickSelect: false, isMobile: false);

		// 右键按下
		fsm.Tick(new Vector2(100, 500), isPointerDown: true, isRightDown: true, 1080f, 500f);

		Assert.Equal(0, dropCount);
		Assert.True(cancelCount > 0, "右键应触发 OnCancel 而非 OnDrop");
	}

	// ===== PlayZone 测试 =====

	[Fact]
	public void PlayZone_DragStartBelowBaseThreshold_UsesMinFormula()
	{
		var fsm = new InteractionFsm();
		// viewportH=1080, dragStartY=400, baseThreshold=648
		// dragStartY(400) < baseThreshold(648) → threshold = Min(648, 350) = 350
		fsm.PickUpCard(new Vector2(200, 400), isClickSelect: false, isMobile: false);

		// Y=300 < 350 → 在出牌区域
		fsm.Tick(new Vector2(200, 300), isPointerDown: true, isRightDown: false, 1080f, 400f);
		Assert.True(fsm.IsInPlayZone, "Y=300 应在出牌区域内");

		// Y=400 ≥ 350 → 不在出牌区域
		fsm.Tick(new Vector2(200, 400), isPointerDown: true, isRightDown: false, 1080f, 400f);
		Assert.False(fsm.IsInPlayZone, "Y=400 应不在出牌区域内");
	}

	[Fact]
	public void PlayZone_DragStartAboveBaseThreshold_UsesMaxFormula()
	{
		var fsm = new InteractionFsm();
		// viewportH=1080, dragStartY=700, baseThreshold=648
		// dragStartY(700) > baseThreshold(648) → threshold = Max(648, 600) = 648
		fsm.PickUpCard(new Vector2(200, 700), isClickSelect: false, isMobile: false);

		// Y=600 < 648 → 在出牌区域
		fsm.Tick(new Vector2(200, 600), isPointerDown: true, isRightDown: false, 1080f, 700f);
		Assert.True(fsm.IsInPlayZone, "Y=600 应在出牌区域内");

		// Y=700 ≥ 648 → 不在出牌区域
		fsm.Tick(new Vector2(200, 700), isPointerDown: true, isRightDown: false, 1080f, 700f);
		Assert.False(fsm.IsInPlayZone, "Y=700 应不在出牌区域内");
	}

	[Fact]
	public void PlayZone_NotInDrag_ReturnsFalse()
	{
		var fsm = new InteractionFsm();

		// Idle 阶段——未拿起卡牌
		Assert.False(fsm.IsInPlayZone, "Idle 阶段 IsInPlayZone 应始终为 false");

		// 拿起后仍未调用 Tick，不应返回 true
		fsm.PickUpCard(new Vector2(200, 100), isClickSelect: false, isMobile: false);
		// 未调用 Tick 时 IsInPlayZone 应保持 false（依赖 Tick 计算）
		Assert.False(fsm.IsInPlayZone, "未调用 Tick 时 IsInPlayZone 应为 false");
	}

	// ===== CancelZone 测试 =====

	[Fact]
	public void CancelZone_StartInBottom_NoTrigger()
	{
		var fsm = new InteractionFsm();
		int dragMoveCount = 0;
		fsm.OnDragMove += (pos, inPlay, inCancel) => dragMoveCount++;
		// cancelThreshold = 1080 * 0.95 = 1026
		// 拿起位置在底部（Y=1050 > 1026），初始 _hasLeftCancelZone=false
		fsm.PickUpCard(new Vector2(200, 1050), isClickSelect: false, isMobile: false);

		// 即使指针在取消区域，因为从未离开过，不应触发
		fsm.Tick(new Vector2(200, 1050), isPointerDown: true, isRightDown: false, 1080f, 1050f);

		Assert.False(fsm.IsInCancelZone,
			"起点已在取消区域内但未离开过，IsInCancelZone 应为 false");
		Assert.True(dragMoveCount > 0, "任何指针移动都应触发 OnDragMove");
	}

	[Fact]
	public void CancelZone_LeaveThenEnter_Triggers()
	{
		var fsm = new InteractionFsm();
		// cancelThreshold = 1080 * 0.95 = 1026
		fsm.PickUpCard(new Vector2(200, 500), isClickSelect: false, isMobile: false);

		// 第一步：离开取消区域（Y=100 ≤ 1026 → _hasLeftCancelZone=true）
		fsm.Tick(new Vector2(200, 100), isPointerDown: true, isRightDown: false, 1080f, 500f);
		Assert.False(fsm.IsInCancelZone, "离开取消区域后 IsInCancelZone 应为 false");

		// 第二步：回到取消区域（Y=1040 > 1026 且 _hasLeftCancelZone=true）
		fsm.Tick(new Vector2(200, 1040), isPointerDown: true, isRightDown: false, 1080f, 500f);
		Assert.True(fsm.IsInCancelZone,
			"离开后再进入取消区域，IsInCancelZone 应为 true");
	}

	[Fact]
	public void CancelZone_ResetOnNewPickup()
	{
		var fsm = new InteractionFsm();
		// cancelThreshold = 1080 * 0.95 = 1026
		fsm.PickUpCard(new Vector2(200, 500), isClickSelect: false, isMobile: false);

		// 离开取消区域 → _hasLeftCancelZone=true
		fsm.Tick(new Vector2(200, 100), isPointerDown: true, isRightDown: false, 1080f, 500f);
		// 回到取消区域 → IsInCancelZone=true
		fsm.Tick(new Vector2(200, 1040), isPointerDown: true, isRightDown: false, 1080f, 500f);
		Assert.True(fsm.IsInCancelZone);

		// 重新拿起卡牌 → _hasLeftCancelZone 应重置为 false
		fsm.PickUpCard(new Vector2(200, 500), isClickSelect: false, isMobile: false);

		// 即使指针在取消区域，因为 _hasLeftCancelZone 已重置，不应触发
		fsm.Tick(new Vector2(200, 1050), isPointerDown: true, isRightDown: false, 1080f, 500f);
		Assert.False(fsm.IsInCancelZone,
			"PickUpCard 后 _hasLeftCancelZone 应重置，IsInCancelZone 应为 false");
	}

	// ===== 并发交互防护测试 =====

	[Fact]
	public void ForceReset_DoesNotFireEvents()
	{
		var fsm = new InteractionFsm();
		int dropCount = 0;
		int cancelCount = 0;
		fsm.OnDrop += (pos, wasDrag) => dropCount++;
		fsm.OnCancel += () => cancelCount++;
		fsm.PickUpCard(new Vector2(100, 500), isClickSelect: false, isMobile: false);

		// ForceReset 应静默重置，不触发 OnDrop 或 OnCancel
		fsm.ForceReset();

		Assert.Equal(0, dropCount);
		Assert.Equal(0, cancelCount);
	}

	[Fact]
	public void DoublePickUpCard_AutoResets()
	{
		var fsm = new InteractionFsm();
		int phaseChanged = 0;
		fsm.OnPhaseChanged += (from, to) => phaseChanged++;

		// 第一次拿起（移动端模式）
		fsm.PickUpCard(new Vector2(100, 500), isClickSelect: true, isMobile: true);
		Assert.Equal(InteractionPhase.CardPickedUp, fsm.CurrentPhase);
		Assert.Equal(20f, fsm.DragThreshold);

		// 在已拿起状态下再次拿起（桌面端模式）→ 应先 ForceReset 再进入新状态
		fsm.PickUpCard(new Vector2(200, 600), isClickSelect: false, isMobile: false);

		Assert.Equal(InteractionPhase.CardPickedUp, fsm.CurrentPhase);
		// 第二次拿起的 isMobile=false 应生效（DragThreshold 回到桌面端 10px）
		Assert.Equal(10f, fsm.DragThreshold);
		// 内部 ForceReset + 第二次 PickUpCard 各应触发 OnPhaseChanged
		Assert.True(phaseChanged >= 3, "两次 PickUpCard + 内部 ForceReset 各应触发 OnPhaseChanged");
	}

	// ===== 移动端 vs 桌面端测试 =====

	[Fact]
	public void MobileTick_DoesNotCheckRightClick()
	{
		var fsm = new InteractionFsm();
		int cancelCount = 0;
		int dragMoveCount = 0;
		fsm.OnCancel += () => cancelCount++;
		fsm.OnDragMove += (pos, inPlay, inCancel) => dragMoveCount++;
		fsm.PickUpCard(new Vector2(100, 500), isClickSelect: false, isMobile: true);

		// 移动端模式下右键应被忽略
		fsm.Tick(new Vector2(100, 500), isPointerDown: true, isRightDown: true, 1080f, 500f);

		Assert.True(cancelCount == 0, "移动端模式下右键不应触发取消");
		Assert.True(dragMoveCount > 0, "移动端指针应触发 OnDragMove");
	}

	// ===== 边界情况测试 =====

	[Fact]
	public void DragThreshold_DefaultsToDesktop()
	{
		var fsm = new InteractionFsm();

		// 默认构造后拿起桌面端卡牌
		fsm.PickUpCard(new Vector2(0, 0), isClickSelect: false, isMobile: false);

		Assert.Equal(10f, fsm.DragThreshold);
	}

	[Fact]
	public void Tick_WhileIdle_DoesNotChangeState()
	{
		var fsm = new InteractionFsm();

		// Idle 状态下 Tick 不应改变阶段
		fsm.Tick(new Vector2(200, 300), isPointerDown: true, isRightDown: false, 1080f, 0f);

		Assert.Equal(InteractionPhase.Idle, fsm.CurrentPhase);
		Assert.False(fsm.HasDragged);
		Assert.False(fsm.IsInPlayZone);
		Assert.False(fsm.IsInCancelZone);
	}
}
