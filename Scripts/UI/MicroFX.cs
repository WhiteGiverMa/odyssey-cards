#nullable enable
using Godot;

namespace OdysseyCards.UI;

/// <summary>
/// 微交互层——按钮悬停动效、场景淡入、标题呼吸微光。
/// 全部 Tween 驱动，零美术资产；移动端无指针悬停，附加 hover FX 无副作用。
/// </summary>
public static class MicroFX
{
	private static readonly StringName HoverFlagMeta = "microfx_hover";
	private static readonly StringName HoverTweenMeta = "microfx_hover_tween";

	// ===== 按钮悬停 =====

	/// <summary>
	/// 为按钮附加悬停微交互（scale 1.0→1.04 + 微提亮，0.12s Cubic 缓出）。
	/// 通过 metadata 防重复附加；Tween 绑定按钮生命周期，按钮销毁自动停止。
	/// </summary>
	public static void AttachHoverFX(BaseButton btn)
	{
		if (btn.HasMeta(HoverFlagMeta))
			return;
		btn.SetMeta(HoverFlagMeta, true);
		btn.MouseEntered += () => OnHover(btn, entered: true);
		btn.MouseExited += () => OnHover(btn, entered: false);
	}

	private static void OnHover(BaseButton btn, bool entered)
	{
		if (!GodotObject.IsInstanceValid(btn) || !btn.IsInsideTree())
			return;
		// 禁用按钮不响应悬停放大多动（视觉上它已「死」）
		if (entered && btn.Disabled)
			return;

		// 停掉上一次未完成的补间，避免 enter/exit 快速交替时互相打架
		if (btn.HasMeta(HoverTweenMeta))
		{
			if (btn.GetMeta(HoverTweenMeta).AsGodotObject() is Tween old)
				old.Kill();
			btn.RemoveMeta(HoverTweenMeta);
		}

		// 缩放轴心设为按钮中心（Size 在悬停时已布局完成，必然有效）
		btn.PivotOffset = btn.Size / 2f;

		var tween = btn.CreateTween()
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.Out)
			.SetParallel();
		tween.TweenProperty(btn, "scale", entered ? new Vector2(1.04f, 1.04f) : Vector2.One, 0.12);
		tween.TweenProperty(btn, "modulate", entered ? new Color(1.09f, 1.09f, 1.09f) : Colors.White, 0.12);
		btn.SetMeta(HoverTweenMeta, tween);
	}

	// ===== 场景淡入 =====

	/// <summary>
	/// 场景淡入——根控件从透明渐变到不透明。
	/// 对后续创建的弹窗/VFX 无影响（Tween 一次性，播完即恢复纯白）。
	/// </summary>
	public static void FadeIn(CanvasItem root, float duration = 0.3f)
	{
		if (!root.IsInsideTree())
			return;
		root.Modulate = new Color(1f, 1f, 1f, 0f);
		var tween = root.CreateTween();
		tween.TweenProperty(root, "modulate", Colors.White, duration);
	}

	// ===== 呼吸微光 =====

	/// <summary>
	/// 呼吸微光——SelfModulate 在纯白与暗化之间无限循环（正弦缓动）。
	/// 用于主菜单标题等需要「活着的」装饰性元素。
	/// </summary>
	public static void BreathingGlow(CanvasItem item, float minBrightness = 0.8f, float period = 2.8f)
	{
		var dim = new Color(minBrightness, minBrightness, minBrightness);
		var tween = item.CreateTween()
			.SetLoops()
			.SetTrans(Tween.TransitionType.Sine)
			.SetEase(Tween.EaseType.InOut);
		tween.TweenProperty(item, "self_modulate", dim, period / 2f);
		tween.TweenProperty(item, "self_modulate", Colors.White, period / 2f);
	}
}
