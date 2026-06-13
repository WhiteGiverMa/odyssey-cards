using Godot;
using OdysseyCards.Core;
using System;

namespace OdysseyCards.UI;

/// <summary>
/// 浮动伤害/治疗数字——自包含 VFX 节点，动画结束后自动 QueueFree。
/// 参考 STS2 NDamageNumVfx 的动画模式。
/// </summary>
public partial class FloatingDamageNumber : Control
{
	// ===== 常量 =====
	private const float FloatDistance = 50f;        // 上浮距离（像素）
	private const float DamageDuration = 2.0f;      // 伤害数字动画时长
	private const float HealDuration = 1.3f;         // 治疗数字动画时长
	private const float BlockedDuration = 1.5f;      // 格挡文本动画时长
	private const float ArmorDuration = 1.0f;        // 护甲吸收数字动画时长
	private const int FontSize = 28;                  // 主数字字体大小
	private const int ArmorFontSize = 18;             // 护甲数字字体大小
	private const int BlockedFontSize = 20;           // 格挡文本字体大小

	// ===== 颜色常量 =====
	private static readonly Color DamageColor = new(1.0f, 0.25f, 0.15f);       // 橙红
	private static readonly Color HealColor = new(0.15f, 1.0f, 0.30f);         // 绿色
	private static readonly Color ArmorColor = new(0.70f, 0.70f, 0.80f);       // 蓝灰
	private static readonly Color BlockedColor = new(0.55f, 0.60f, 0.75f);     // 深蓝灰

	// ===== 实例字段 =====
	private Label _label = null!;
	private readonly Random _rng = new();

	// ===== 静态工厂方法 =====

	/// <summary>
	/// 在指定屏幕位置创建伤害跳字。
	/// </summary>
	/// <param name="info">伤害事件信息</param>
	/// <param name="screenPosition">生成位置的屏幕坐标</param>
	/// <param name="parent">父节点（应为 CanvasLayer/Control）</param>
	public static void CreateDamage(DamageEventInfo info, Vector2 screenPosition, Node parent)
	{
		if (info.WasFullyBlocked)
		{
			// 完全格挡：显示 "抵挡" 文本
			CreateBlocked(screenPosition, parent);
			if (info.ArmorAbsorbed > 0)
			{
				// 还有护甲吸收数字
				CreateArmorAbsorbed(info.ArmorAbsorbed, screenPosition + new Vector2(0, -18), parent);
			}
			return;
		}

		// HP 伤害数字（主数字）
		if (info.HpLost > 0)
		{
			CreateNumber(info.HpLost, screenPosition, DamageColor, FontSize, DamageDuration, parent);
		}

		// 护甲吸收数字（副数字，显示在主数字右侧）
		if (info.ArmorAbsorbed > 0)
		{
			CreateArmorAbsorbed(info.ArmorAbsorbed, screenPosition + new Vector2(22, 2), parent);
		}
	}

	/// <summary>
	/// 创建治疗跳字。
	/// </summary>
	public static void CreateHeal(int amount, Vector2 screenPosition, Node parent)
	{
		if (amount <= 0)
			return;
		CreateNumber(amount, screenPosition, HealColor, FontSize, HealDuration, parent);
	}

	/// <summary>
	/// 创建单个数字跳字并启动动画。
	/// </summary>
	private static FloatingDamageNumber CreateNumber(int amount, Vector2 position, Color color, int fontSize, float duration, Node parent)
	{
		var fdn = new FloatingDamageNumber();
		fdn.Initialize(amount.ToString(), position, color, fontSize, duration);
		parent.AddChild(fdn);
		return fdn;
	}

	/// <summary>
	/// 创建 "抵挡" 格挡文本。
	/// </summary>
	private static FloatingDamageNumber CreateBlocked(Vector2 position, Node parent)
	{
		var fdn = new FloatingDamageNumber();
		fdn.Initialize("抵挡", position, BlockedColor, BlockedFontSize, BlockedDuration);
		parent.AddChild(fdn);
		return fdn;
	}

	/// <summary>
	/// 创建护甲吸收数字（小字号，灰色）。
	/// </summary>
	private static FloatingDamageNumber CreateArmorAbsorbed(int amount, Vector2 position, Node parent)
	{
		var fdn = new FloatingDamageNumber();
		fdn.Initialize($"(-{amount})", position, ArmorColor, ArmorFontSize, ArmorDuration);
		parent.AddChild(fdn);
		return fdn;
	}

	// ===== 初始化 =====

	private void Initialize(string text, Vector2 screenPosition, Color color, int fontSize, float duration)
	{
		// 读取伤害数字缩放设置
		float vfxScale = UIScaler.Instance?.DamageNumberScale ?? 1.0f;
		if (vfxScale < 0.01f) vfxScale = 1.0f;

		int scaledFontSize = Mathf.RoundToInt(fontSize * vfxScale);
		float scaledFloatDistance = FloatDistance * vfxScale;

		// 随机散射偏移（避免重叠时完全一致），也应用缩放
		float scatterX = (_rng.NextSingle() - 0.5f) * 16f * vfxScale;
		float scatterY = (_rng.NextSingle() - 0.5f) * 8f * vfxScale;

		Position = screenPosition + new Vector2(scatterX, scatterY);
		MouseFilter = MouseFilterEnum.Ignore;

		// 初始缩放
		Scale = new Vector2(1.5f * vfxScale, 1.5f * vfxScale);

		_label = new Label
		{
			Text = text,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
		};
		_label.AddThemeColorOverride("font_color", color);
		_label.AddThemeFontSizeOverride("font_size", scaledFontSize);

		// 添加描边效果使数字更清晰
		_label.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.4f));
		_label.AddThemeConstantOverride("outline_size", Mathf.Max(1, Mathf.RoundToInt(2 * vfxScale)));

		AddChild(_label);

		// 开始动画
		Animate(duration, scaledFloatDistance);
	}

	// ===== 动画 =====

	private void Animate(float duration, float scaledFloatDistance)
	{
		var tween = CreateTween();
		tween.SetParallel(true);

		// 向上浮动
		tween.TweenProperty(this, "position:y", Position.Y - scaledFloatDistance, duration)
			 .SetEase(Tween.EaseType.Out)
			 .SetTrans(Tween.TransitionType.Cubic);

		// 透明度 1.0 → 0.0（后 40% 时间开始淡出）
		tween.TweenProperty(this, "modulate:a", 0.0f, duration * 0.5f)
			 .SetDelay(duration * 0.5f)
			 .SetEase(Tween.EaseType.In)
			 .SetTrans(Tween.TransitionType.Quad);

		// 缩放缩小（仅第一个通道，让进场效果明显）
		var scaleTween = CreateTween();
		scaleTween.TweenProperty(this, "scale", Vector2.One, duration * 0.5f)
				  .SetEase(Tween.EaseType.Out)
				  .SetTrans(Tween.TransitionType.Back);

		// 动画结束后自动销毁
		tween.Finished += QueueFree;
	}
}
