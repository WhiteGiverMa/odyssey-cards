using Godot;

namespace OdysseyCards.Infrastructure;

/// <summary>
/// 移动端触控输入助手 — Autoload 单例。
/// 集中管理触控事件，为 CardUI/CombatUI 等组件的 _Process 轮询提供触控状态查询，
/// 替代桌面端 Input.IsMouseButtonPressed/GetGlobalMousePosition。
///
/// 设计原则：
///   - 不合成鼠标事件（避免 Godot 移动端的鼠标+触控双触发问题）
///   - 仅处理主手指（index=0），忽略多指手势
///   - 组件通过 IsTouchPressed / TouchScreenPosition 查询状态，与桌面端鼠标 API 对称
/// </summary>
public partial class MobileInputHelper : Node
{
	public static MobileInputHelper Instance { get; private set; } = null!;

	/// <summary>是否运行在移动平台</summary>
	public static bool IsMobile => OS.HasFeature("mobile");

	/// <summary>主手指（index=0）当前是否按下</summary>
	public static bool IsTouchPressed { get; private set; }

	/// <summary>主手指当前屏幕坐标</summary>
	public static Vector2 TouchScreenPosition { get; private set; }

	/// <summary>主手指按下时的初始屏幕坐标（用于拖拽阈值计算）</summary>
	public static Vector2 TouchStartPosition { get; private set; }

	/// <summary>最近一次触控事件的时间戳（用于区分点击/长按）</summary>
	public static ulong TouchPressTimeMsec { get; private set; }

	/// <summary>最近一次触控松手时的屏幕坐标</summary>
	public static Vector2 TouchReleasePosition { get; private set; }

	/// <summary>是否有触控松手事件待消费（由 _Process 消费后清除）</summary>
	public static bool HasTouchRelease { get; private set; }

	/// <summary>本次触控按下后手指是否移动过（超出阈值）</summary>
	public static bool HasTouchMoved { get; private set; }

	private static int _activeFingerIndex = -1;

	public override void _Ready()
	{
		Instance = this;
		Name = "MobileInputHelper";
		GD.Print($"[MobileInputHelper] 初始化 — IsMobile={IsMobile}");
	}

	public override void _Input(InputEvent @event)
	{
		if (!IsMobile)
			return;

		if (@event is InputEventScreenTouch touch)
		{
			if (touch.Index != 0)
				return; // 仅追踪主手指

			if (touch.Pressed)
			{
				IsTouchPressed = true;
				HasTouchMoved = false;
				HasTouchRelease = false;
				_activeFingerIndex = touch.Index;
				TouchStartPosition = touch.Position;
				TouchScreenPosition = touch.Position;
				TouchPressTimeMsec = Time.GetTicksMsec();
			}
			else
			{
				// 手指抬起
				IsTouchPressed = false;
				HasTouchRelease = true;
				TouchReleasePosition = touch.Position;
				_activeFingerIndex = -1;
			}
		}
		else if (@event is InputEventScreenDrag drag)
		{
			if (drag.Index != 0 || _activeFingerIndex != 0)
				return;

			HasTouchMoved = true;
			TouchScreenPosition = drag.Position;
		}
	}

	/// <summary>
	/// 消费触控松手事件（调用后 HasTouchRelease 复位为 false）。
	/// </summary>
	public static void ConsumeTouchRelease()
	{
		HasTouchRelease = false;
	}
}
