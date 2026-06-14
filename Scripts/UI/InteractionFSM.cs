using System;
using Godot;

namespace OdysseyCards.UI;

/// <summary>
/// 交互阶段枚举。
/// 定义 UI 交互状态机的各个阶段，从空闲到卡牌拖拽/选择/目标选取。
/// </summary>
public enum InteractionPhase
{
	/// <summary>空闲状态——无卡牌被选中或拖拽。</summary>
	Idle,

	/// <summary>卡牌已被拿起（点击选中或拖拽开始）。</summary>
	CardPickedUp,

	/// <summary>目标选择模式——等待选择目标（法术/攻击目标）。</summary>
	Targeting,

	/// <summary>棋盘拖拽——随从/卡牌在棋盘上方拖拽。</summary>
	BoardDrag,
}

/// <summary>
/// 输入模式枚举。
/// 区分桌面端点击、桌面端拖拽、移动端触控三种交互路径。
/// </summary>
public enum InputMode
{
	/// <summary>桌面端点击选中→第二击目标。</summary>
	DesktopClick,

	/// <summary>桌面端按住拖拽→松手打出。</summary>
	DesktopDrag,

	/// <summary>移动端触控交互。</summary>
	MobileTouch,
}

/// <summary>
/// 交互状态机——管理卡牌拖拽/点击交互和战斗攻击拖拽交互的阶段转换。
/// 纯 C#，不继承 Godot Node，便于单元测试。
/// </summary>
public sealed class InteractionFsm
{
	// ===== 常量 =====

	/// <summary>桌面端拖拽阈值（像素），超过此距离视为拖拽而非点击。</summary>
	private const float DragThresholdDesktop = 10f;

	/// <summary>移动端拖拽阈值（像素），超过此距离视为拖拽而非点击。</summary>
	private const float DragThresholdMobile = 20f;

	/// <summary>出牌区域基准比例——视口高度乘以此值得到基准 Y 阈值。</summary>
	private const float PlayZoneBaseRatio = 0.60f;

	/// <summary>取消区域屏幕比例——视口高度乘以此值得到取消区域 Y 阈值。</summary>
	private const float CancelZoneScreenProportion = 0.95f;

	/// <summary>脱离原点的最小距离平方（5px²），用于判定指针是否已移动。</summary>
	private const float MinMoveFromOriginThresholdSq = 5f * 5f;

	// ===== 事件 =====

	/// <summary>卡牌/随从放下时触发。参数为放下位置和是否为拖拽操作。</summary>
	public event Action<Vector2, bool>? OnDrop;

	/// <summary>交互取消时触发（右键/拖回底部/ESC）。</summary>
	public event Action? OnCancel;

	/// <summary>拖拽移动时触发，携带位置、是否在出牌区域、是否在取消区域。</summary>
	public event Action<Vector2, bool, bool>? OnDragMove;

	/// <summary>交互阶段变更时触发，参数为旧阶段和新阶段。</summary>
	public event Action<InteractionPhase, InteractionPhase>? OnPhaseChanged;

	// ===== 属性 =====

	/// <summary>当前交互阶段。初始值为 <see cref="InteractionPhase.Idle"/>。</summary>
	public InteractionPhase CurrentPhase
	{
		get => _currentPhase;
		private set => _currentPhase = value;
	}

	/// <summary>
	/// 当前指针位置是否位于「出牌区域」（屏幕上方 Y 阈值区域）。
	/// </summary>
	public bool IsInPlayZone { get; private set; }

	/// <summary>
	/// 当前指针位置是否位于「取消区域」（屏幕底部或指定取消区域）。
	/// </summary>
	public bool IsInCancelZone { get; private set; }

	/// <summary>
	/// 在当前交互中是否已发生拖拽（超过拖拽阈值）。
	/// </summary>
	public bool HasDragged { get; private set; }

	/// <summary>
	/// 根据当前输入模式获取对应的拖拽阈值。
	/// </summary>
	public float DragThreshold => _isMobile ? DragThresholdMobile : DragThresholdDesktop;

	// ===== 私有字段 =====

	private InteractionPhase _currentPhase = InteractionPhase.Idle;
	private bool _isClickSelect;
	private bool _isMobile;
	private Vector2 _anchorPos;
	private bool _hasMovedFromOrigin;
	private bool _hasLeftCancelZone;
	private bool _wasPointerDownLastFrame;
	private bool _wasRightDownLastFrame;

	// ===== 公共 API 方法 =====

	/// <summary>
	/// 每帧更新——根据指针位置和按键状态驱动状态转换。
	/// Idle 阶段直接返回，不执行任何逻辑。
	/// </summary>
	/// <param name="inputPosition">当前指针/触控在屏幕空间的位置。</param>
	/// <param name="isPointerDown">主按钮（左键/触控）是否按下。</param>
	/// <param name="isRightDown">右键是否按下。</param>
	/// <param name="viewportHeight">视口高度，用于计算出牌/取消 Y 阈值。</param>
	/// <param name="dragStartY">本次拖拽起始位置的 Y 坐标，用于自适应出牌阈值。</param>
	public void Tick(Vector2 inputPosition, bool isPointerDown, bool isRightDown, float viewportHeight, float dragStartY)
	{
		if (_currentPhase == InteractionPhase.Idle)
			return;

		// 右键取消（仅桌面端，边缘触发）
		if (isRightDown && !_wasRightDownLastFrame && !_isMobile)
		{
			_wasRightDownLastFrame = true;
			OnCancel?.Invoke();
			ForceReset();
			return;
		}
		_wasRightDownLastFrame = isRightDown;

		// 拖拽阈值检测
		float distSq = DistanceSquaredTo(_anchorPos, inputPosition);
		float dragThresholdSq = DragThreshold * DragThreshold;

		if (distSq > dragThresholdSq && !HasDragged)
		{
			HasDragged = true;
			_hasMovedFromOrigin = true;
			if (_currentPhase == InteractionPhase.CardPickedUp)
				SetPhase(InteractionPhase.BoardDrag);
		}
		else if (distSq > MinMoveFromOriginThresholdSq)
		{
			_hasMovedFromOrigin = true;
		}

		// 出牌区域计算
		float baseThreshold = viewportHeight * PlayZoneBaseRatio;
		float threshold;
		if (dragStartY > baseThreshold)
			threshold = MathF.Max(baseThreshold, dragStartY - 100f);
		else
			threshold = MathF.Min(baseThreshold, dragStartY - 50f);

		IsInPlayZone = inputPosition.Y < threshold;

		// 取消区域计算（状态机：必须先离开再进入才触发）
		float cancelThreshold = viewportHeight * CancelZoneScreenProportion;
		if (inputPosition.Y <= cancelThreshold)
			_hasLeftCancelZone = true;

		IsInCancelZone = _hasLeftCancelZone && inputPosition.Y > cancelThreshold;

		// 每帧触发拖拽移动事件
		OnDragMove?.Invoke(inputPosition, IsInPlayZone, IsInCancelZone);

		// 松手检测（边缘触发：仅在本帧首次检测到松手的帧处理）
		bool releasedThisFrame = _wasPointerDownLastFrame && !isPointerDown;
		_wasPointerDownLastFrame = isPointerDown;

		if (releasedThisFrame)
		{
			if (HasDragged)
			{
				OnDrop?.Invoke(inputPosition, true);
				ForceReset();
			}
			else if (_isClickSelect && _hasMovedFromOrigin)
			{
				OnDrop?.Invoke(inputPosition, false);
				ForceReset();
			}
			else if (_isClickSelect)
			{
				// 点击选中模式下首次松手无位移：保持 clickSelect，等待后续点击
				// 不触发任何事件，不重置 Machine
			}
			else
			{
				OnCancel?.Invoke();
				ForceReset();
			}
		}
	}

	/// <summary>
	/// 拿起一张卡牌——从手牌点击/拖拽开始时调用。
	/// 将阶段切换为 <see cref="InteractionPhase.CardPickedUp"/>。
	/// 若当前非 Idle 阶段，先执行 ForceReset 再进入新交互。
	/// </summary>
	/// <param name="anchorPos">卡牌被拿起时的锚定位置。</param>
	/// <param name="isClickSelect">是否为点击选中模式（而非拖拽）。</param>
	/// <param name="isMobile">是否为移动端触控输入。</param>
	public void PickUpCard(Vector2 anchorPos, bool isClickSelect, bool isMobile)
	{
		if (_currentPhase != InteractionPhase.Idle)
			ForceReset();

		_anchorPos = anchorPos;
		_isClickSelect = isClickSelect;
		_isMobile = isMobile;
		_hasLeftCancelZone = false;
		HasDragged = false;
		IsInPlayZone = false;
		IsInCancelZone = false;
		_hasMovedFromOrigin = false;

		SetPhase(InteractionPhase.CardPickedUp);
	}

	/// <summary>
	/// 进入目标选择模式——法术/攻击需要选择目标时调用。
	/// 将阶段切换为 <see cref="InteractionPhase.Targeting"/>。
	/// </summary>
	public void EnterTargeting()
	{
		SetPhase(InteractionPhase.Targeting);
	}

	/// <summary>
	/// 取消当前交互——先重置 FSM 内部状态，再触发 <see cref="OnCancel"/>
	/// 通知订阅者清理 UI。仅在非 Idle 阶段生效。
	/// 调用后 FSM 回到 Idle 阶段，订阅者的 OnCancel 回调不会引发重入。
	/// </summary>
	public void Cancel()
	{
		if (_currentPhase == InteractionPhase.Idle)
			return;
		ForceReset();
		OnCancel?.Invoke();
	}

	/// <summary>
	/// 强制重置状态机——将阶段重置为 <see cref="InteractionPhase.Idle"/>，
	/// 清除所有内部状态。会触发 <see cref="OnPhaseChanged"/> 但不会触发 OnDrop 或 OnCancel。
	/// </summary>
	public void ForceReset()
	{
		HasDragged = false;
		IsInPlayZone = false;
		IsInCancelZone = false;
		_isClickSelect = false;
		_isMobile = false;
		_anchorPos = default;
		_hasLeftCancelZone = false;
		_hasMovedFromOrigin = false;

		SetPhase(InteractionPhase.Idle);
	}

	// ===== 私有辅助方法 =====

	/// <summary>
	/// 设置当前阶段，若新旧阶段相同则不触发事件。
	/// </summary>
	private void SetPhase(InteractionPhase newPhase)
	{
		if (newPhase == _currentPhase)
			return;

		InteractionPhase oldPhase = _currentPhase;
		_currentPhase = newPhase;
		OnPhaseChanged?.Invoke(oldPhase, newPhase);
	}

	/// <summary>
	/// 计算两点之间距离的平方（避免开方开销）。
	/// </summary>
	private static float DistanceSquaredTo(Vector2 a, Vector2 b)
	{
		float dx = a.X - b.X;
		float dy = a.Y - b.Y;
		return dx * dx + dy * dy;
	}
}
