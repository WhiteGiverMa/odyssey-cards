using System;
using System.Collections.Generic;
using Godot;

namespace OdysseyCards.Infrastructure;

/// <summary>
/// 移动端统一输入路由器 — Autoload 单例，替换 MobileInputHelper。
///
/// 设计目标：
///   1. 手势所有权 — 一次触摸从按下到抬起，由唯一 owner 独占处理
///   2. 模态优先级栈 — 弹窗/覆盖层打开时自动阻断下层输入
///   3. 生命周期安全 — 场景切换时自动释放所有手势
///   4. 桌面端兼容 — IsMobile=false 时完全透传，不影响现有桌面逻辑
///
/// 与 MobileInputHelper 的关键差异：
///   - 不再使用全局共享的 HasTouchRelease（会被多个组件竞争消费）
///   - 不再需要组件手动 ConsumeTouchRelease()
///   - 引入 GestureOwner 概念：组件通过注册优先级的 TouchZone 来声明自己处理触摸
///   - 支持模态覆盖层（Dialog > Overlay > Scene base）
///
/// 用法示例（场景中）：
///   // 注册一个可点击按钮
///   MobileInputRouter.Instance.RegisterTouchZone(this, _button.GetGlobalRect(),
///       priority: 10, onTap: () => OnButtonPressed());
///
///   // 注册拖拽区域
///   MobileInputRouter.Instance.RegisterTouchZone(this, dragRect,
///       priority: 10, onDragStart: ..., onDragMove: ..., onDragEnd: ...);
///
///   // 打开模态弹窗
///   MobileInputRouter.Instance.PushModalLayer(myDialog);
///   // ... 关闭时
///   MobileInputRouter.Instance.PopModalLayer(myDialog);
/// </summary>
public partial class MobileInputRouter : Node
{
	// ===== 单例 =====

	public static MobileInputRouter Instance { get; private set; } = null!;

	/// <summary>是否运行在移动平台。</summary>
	public static bool IsMobile => OS.HasFeature("mobile");

	// ===== 手势所有权 =====

	/// <summary>当前活跃手势的 owner（Control 引用）。null 表示无活跃手势。</summary>
	public Control? ActiveGestureOwner => _activeTouchZone?.Owner;

	/// <summary>当前触摸是否活跃（手指按下未抬起）。</summary>
	public bool IsTouchActive => _activeTouchZone != null;

	/// <summary>当前触摸屏幕坐标（手指按下时初始位置）。</summary>
	public Vector2 TouchStartPosition { get; private set; }

	/// <summary>当前触摸屏幕坐标（持续更新）。</summary>
	public Vector2 TouchPosition { get; private set; }

	/// <summary>触摸已移动的距离。</summary>
	public float TouchTravelDistance => IsTouchActive
		? TouchPosition.DistanceTo(TouchStartPosition)
		: 0f;

	/// <summary>触摸持续时间（毫秒）。</summary>
	public ulong TouchDurationMsec => IsTouchActive
		? Time.GetTicksMsec() - _touchStartMsec
		: 0;

	/// <summary>触摸松手时的屏幕坐标。</summary>
	public Vector2 TouchReleasePosition { get; private set; }

	// ===== 私有状态 =====

	/// <summary>所有已注册的触控区域（按 priority 降序排列）。</summary>
	private readonly List<TouchZone> _touchZones = new();

	/// <summary>当前活跃的触控区域（手指按下时命中并锁定的）。</summary>
	private TouchZone? _activeTouchZone;

	/// <summary>模态层叠栈——栈顶的元素有最高优先级，阻断下层输入。</summary>
	private readonly Stack<Control> _modalStack = new();

	/// <summary>触摸按下的时间戳。</summary>
	private ulong _touchStartMsec;

	/// <summary>拖拽阈值（像素）。移动超过此距离后从 Tap 切换为 Drag。</summary>
	public const float DragThreshold = 20f;

	/// <summary>长按阈值（毫秒）。按住超过此时间触发 LongPress。</summary>
	public const ulong LongPressThresholdMsec = 500;

	// 手势类型标记
	private bool _isDragging;
	private bool _hasLongPressFired;

	// ===== 事件（供 _Process 轮询的组件迁移到事件驱动） =====

	/// <summary>触摸按下时触发。</summary>
	public event Action<Vector2>? OnTouchBegan;

	/// <summary>触摸移动时触发（仅当 owner 是自己的 zone 时）。</summary>
	public event Action<Vector2>? OnTouchMoved;

	/// <summary>触摸松手时触发（仅当 owner 是自己的 zone 时）。</summary>
	public event Action<Vector2>? OnTouchEnded;

	// ===== Godot 生命周期 =====

	public override void _Ready()
	{
		Instance = this;
		Name = "MobileInputRouter";
		GD.Print($"[MobileInputRouter] 初始化 — IsMobile={IsMobile}");
	}

	public override void _Input(InputEvent @event)
	{
		if (!IsMobile)
			return;

		switch (@event)
		{
			case InputEventScreenTouch touch when touch.Index == 0:
				HandleTouchEvent(touch);
				break;

			case InputEventScreenDrag drag when drag.Index == 0:
				HandleDragEvent(drag);
				break;

			case InputEventKey keyEvent:
				// Android 返回键 → ESC
				if (keyEvent.Keycode == Key.Back && keyEvent.Pressed)
				{
					HandleBackKey();
				}
				break;
		}
	}

	public override void _Process(double delta)
	{
		if (!IsMobile || _activeTouchZone == null)
			return;
		if (!IsActiveZoneValid())
		{ _activeTouchZone = null; return; }

		// 长按检测
		if (!_hasLongPressFired && !_isDragging && TouchDurationMsec >= LongPressThresholdMsec)
		{
			_hasLongPressFired = true;
			_activeTouchZone.OnLongPress?.Invoke();
		}
	}

	// ===== 触控事件处理 =====

	private void HandleTouchEvent(InputEventScreenTouch touch)
	{
		try
		{
			if (touch.Pressed)
			{
				// 手指按下 → 查找命中的最高优先级 zone
				TouchStartPosition = touch.Position;
				TouchPosition = touch.Position;
				_touchStartMsec = Time.GetTicksMsec();
				_isDragging = false;
				_hasLongPressFired = false;

				_activeTouchZone = FindTopTouchZone(touch.Position);
				if (_activeTouchZone != null && IsActiveZoneValid())
				{
					_activeTouchZone.IsActive = true;
					GD.Print($"[MobileInputRouter] 手势开始 — owner={_activeTouchZone.Owner.Name} pos={touch.Position}");
				}
				else
				{
					_activeTouchZone = null;
				}

				OnTouchBegan?.Invoke(touch.Position);
			}
			else
			{
				// 手指抬起
				TouchReleasePosition = touch.Position;

				if (_activeTouchZone != null)
				{
					if (!IsActiveZoneValid())
					{
						_activeTouchZone = null;
					}
					else if (_isDragging)
					{
						_activeTouchZone.OnDragEnd?.Invoke(touch.Position);
					}
					else
					{
						_activeTouchZone.OnTap?.Invoke();
					}

					if (_activeTouchZone != null)
					{
						_activeTouchZone.IsActive = false;
						GD.Print($"[MobileInputRouter] 手势结束 — owner={_activeTouchZone.Owner.Name} " +
								 $"type={(_isDragging ? "drag" : "tap")} pos={touch.Position}");
					}
				}

				OnTouchEnded?.Invoke(touch.Position);
				_activeTouchZone = null;
			}
		}
		catch (System.Exception ex)
		{
			GD.PushError($"[MobileInputRouter] HandleTouchEvent 异常: {ex.GetType().Name} — {ex.Message}\n{ex.StackTrace}");
			_activeTouchZone = null;
		}
	}

	private void HandleDragEvent(InputEventScreenDrag drag)
	{
		TouchPosition = drag.Position;

		if (_activeTouchZone == null)
			return;
		if (!IsActiveZoneValid())
		{ _activeTouchZone = null; return; }

		// 拖拽阈值检测
		if (!_isDragging && TouchTravelDistance >= DragThreshold)
		{
			_isDragging = true;
			_activeTouchZone.OnDragStart?.Invoke(TouchStartPosition);
		}

		if (_isDragging)
		{
			_activeTouchZone.OnDragMove?.Invoke(drag.Position, drag.Relative);
		}

		OnTouchMoved?.Invoke(drag.Position);
	}

	// ===== 模态层管理 =====

	/// <summary>
	/// 压入模态层。模态层存在时，只有模态层内的 TouchZone 可以响应触摸。
	/// </summary>
	public void PushModalLayer(Control overlay)
	{
		if (!IsMobile)
			return;
		_modalStack.Push(overlay);
		GD.Print($"[MobileInputRouter] 压入模态层 — {overlay.Name} (depth={_modalStack.Count})");
	}

	/// <summary>
	/// 弹出模态层。传入的 overlay 必须与栈顶匹配。
	/// </summary>
	public void PopModalLayer(Control overlay)
	{
		if (!IsMobile)
			return;
		if (_modalStack.Count > 0 && _modalStack.Peek() == overlay)
		{
			_modalStack.Pop();
			GD.Print($"[MobileInputRouter] 弹出模态层 — {overlay.Name} (depth={_modalStack.Count})");
		}
		else
		{
			GD.PushWarning($"[MobileInputRouter] PopModalLayer 不匹配 — got={overlay.Name}");
		}
	}

	/// <summary>
	/// 当前是否有模态层活跃。
	/// </summary>
	public bool HasModalLayer => _modalStack.Count > 0;

	// ===== TouchZone 注册 =====

	/// <summary>
	/// 注册一个轻触区域（Tap-only，不处理拖拽）。
	/// 返回 IDisposable token，Dispose 时自动注销。
	/// </summary>
	public IDisposable RegisterTapZone(Control owner, Rect2 globalRect, int priority, Action onTap)
	{
		var zone = new TouchZone(owner, globalRect, priority, onTap: onTap);
		RegisterZone(zone);
		return new TouchZoneToken(this, zone);
	}

	/// <summary>
	/// 注册一个拖拽区域（支持 tap + drag）。所有回调可选。
	/// </summary>
	public IDisposable RegisterDragZone(
		Control owner,
		Rect2 globalRect,
		int priority,
		Action? onTap = null,
		Action<Vector2>? onDragStart = null,
		Action<Vector2, Vector2>? onDragMove = null,
		Action<Vector2>? onDragEnd = null,
		Action? onLongPress = null)
	{
		var zone = new TouchZone(owner, globalRect, priority,
			onTap: onTap,
			onDragStart: onDragStart,
			onDragMove: onDragMove,
			onDragEnd: onDragEnd,
			onLongPress: onLongPress);
		RegisterZone(zone);
		return new TouchZoneToken(this, zone);
	}

	private void RegisterZone(TouchZone zone)
	{
		// 按 priority 降序插入（高 priority 在前）
		int insertIndex = 0;
		for (int i = 0; i < _touchZones.Count; i++)
		{
			if (_touchZones[i].Priority < zone.Priority)
			{
				insertIndex = i;
				break;
			}
			insertIndex = i + 1;
		}
		_touchZones.Insert(insertIndex, zone);
	}

	internal void UnregisterZone(TouchZone zone)
	{
		if (_activeTouchZone == zone)
		{
			_activeTouchZone = null; // 安全释放
		}
		_touchZones.Remove(zone);
	}

	/// <summary>
	/// 检查当前活跃 zone 的 owner 是否仍然有效。
	/// 场景切换时 owner 可能已被释放，此时应清除 _activeTouchZone。
	/// </summary>
	private bool IsActiveZoneValid()
	{
		if (_activeTouchZone == null)
			return false;
		if (!SceneLifecycleGuard.IsNodeValid(_activeTouchZone.Owner))
			return false;
		if (!_activeTouchZone.Owner.IsInsideTree())
			return false;
		return true;
	}

	// ===== 命中测试 =====

	/// <summary>
	/// 在已注册的 TouchZone 中查找命中给定坐标的最高优先级 zone。
	/// 模态层存在时，只搜索模态层内的 zone。
	/// </summary>
	private TouchZone? FindTopTouchZone(Vector2 screenPos)
	{
		Control? modalTop = _modalStack.Count > 0 ? _modalStack.Peek() : null;

		for (int i = 0; i < _touchZones.Count; i++)
		{
			var zone = _touchZones[i];

			// 检查 zone 的 owner 是否仍然有效
			if (!SceneLifecycleGuard.IsNodeValid(zone.Owner))
				continue;
			if (!zone.Owner.IsInsideTree())
				continue;

			// 模态层过滤：如果有模态层，只接受模态层内的 zone（或模态层本身）
			if (modalTop != null)
			{
				if (!IsDescendantOf(zone.Owner, modalTop) && zone.Owner != modalTop)
					continue;
			}

			// 命中测试（使用缓存的 rect，但需要更新因为布局可能变化）
			Rect2 currentRect = zone.Owner is Control c ? c.GetGlobalRect() : zone.CachedRect;
			if (currentRect.HasPoint(screenPos))
			{
				return zone;
			}
		}

		return null;
	}

	private static bool IsDescendantOf(Node? node, Node? ancestor)
	{
		if (node == null || ancestor == null)
			return false;
		Node? current = node;
		while (current != null)
		{
			if (current == ancestor)
				return true;
			current = current.GetParentOrNull<Node>();
		}
		return false;
	}

	// ===== 查询 API（供不能注册 TouchZone 的现有 _Process 轮询组件使用） =====

	/// <summary>
	/// 检查指定 Control 是否拥有当前手势。
	/// 用于迁移期：现有 _Process 轮询的组件可以通过此方法检查所有权，
	/// 避免与其他组件竞争全局触控状态。
	/// </summary>
	public bool OwnsGesture(Control? control)
	{
		return control != null && _activeTouchZone?.Owner == control;
	}

	/// <summary>
	/// 当前手势是否已被任何组件拥有。
	/// </summary>
	public bool IsGestureOwned => _activeTouchZone != null;

	// ===== Android 返回键 =====

	private void HandleBackKey()
	{
		// 优先处理模态层
		if (_modalStack.Count > 0)
		{
			var top = _modalStack.Peek();
			GD.Print($"[MobileInputRouter] 返回键 → 弹出模态层 {top.Name}");
			// 模态层的关闭由各层自己处理（通过 OnBackPressed 事件或类似机制）
			// 这里只触发 ESC 输入动作，让场景的 _UnhandledInput 处理
			return;
		}

		// 无模态层 → 模拟 ESC 键（由 CombatUI._UnhandledInput 等处理）
		GetViewport().SetInputAsHandled();
	}
}

/// <summary>
/// 触控区域定义。封装一个可交互的矩形区域及其回调。
/// </summary>
internal class TouchZone
{
	public Control Owner { get; }
	public Rect2 CachedRect { get; }
	public int Priority { get; }
	public bool IsActive { get; set; }

	public Action? OnTap { get; }
	public Action<Vector2>? OnDragStart { get; }
	public Action<Vector2, Vector2>? OnDragMove { get; }
	public Action<Vector2>? OnDragEnd { get; }
	public Action? OnLongPress { get; }

	public TouchZone(
		Control owner,
		Rect2 rect,
		int priority,
		Action? onTap = null,
		Action<Vector2>? onDragStart = null,
		Action<Vector2, Vector2>? onDragMove = null,
		Action<Vector2>? onDragEnd = null,
		Action? onLongPress = null)
	{
		Owner = owner;
		CachedRect = rect;
		Priority = priority;
		OnTap = onTap;
		OnDragStart = onDragStart;
		OnDragMove = onDragMove;
		OnDragEnd = onDragEnd;
		OnLongPress = onLongPress;
	}
}

/// <summary>
/// TouchZone 的注销 token。Dispose 时自动从 MobileInputRouter 注销。
/// </summary>
internal class TouchZoneToken : IDisposable
{
	private MobileInputRouter? _router;
	private TouchZone? _zone;

	public TouchZoneToken(MobileInputRouter router, TouchZone zone)
	{
		_router = router;
		_zone = zone;
	}

	public void Dispose()
	{
		if (_zone != null && _router != null)
		{
			_router.UnregisterZone(_zone);
		}
		_router = null;
		_zone = null;
	}
}
