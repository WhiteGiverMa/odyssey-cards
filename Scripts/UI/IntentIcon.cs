using Godot;
using System;

namespace OdysseyCards.UI;

/// <summary>
/// 意图图标——纯代码绘制几何图标，支持 bob 浮动动画、悬停检测、冻结状态。
/// 不依赖外部贴图，所有图标通过 DrawRect/DrawCircle/DrawPolygon/DrawLine 绘制。
/// </summary>
public partial class IntentIcon : Control
{
	// ===== 常量 =====
	private const float IconSize = 56f;
	private const float HitBoxHeight = IconSize + BobAmplitude * 2f;
	private const float BobSpeed = 3.14f;        // π rad/s，完整周期约 2 秒
	private const float BobAmplitude = 6f;        // 浮动像素幅度
	private const float HoverScale = 1.1f;
	private const float PerformScale = 1.3f;
	private const float PerformDuration = 0.3f;
	private const int ValueFontSize = 12;

	// ===== 内部状态 =====
	private int _intentTypeId;
	private string _labelText;
	private int _intentValue;
	private bool _isFrozen;
	private Label _valueLabel = null!;
	private float _bobPhase;
	private float _iconVisualOffsetY = BobAmplitude;
	private float _valueVisualOffsetY = BobAmplitude;
	private bool _isHovering;
	private Tween? _hoverScaleTween;

	// ===== 公共属性 =====
	/// <summary>当前是否处于悬停状态（供父卡片检查兄弟图标是否仍 hovered）。</summary>
	public bool IsHovering => _isHovering;

	// ===== 公共事件 =====
	/// <summary>鼠标进入图标时触发，用于外部显示 tooltip。</summary>
	public event Action<IntentIcon> OnHovered;
	/// <summary>鼠标离开图标时触发。</summary>
	public event Action<IntentIcon> OnUnhovered;

	// ===== 构造 =====

	public IntentIcon(int typeId, string labelText, int value = 0)
	{
		_intentTypeId = typeId;
		_labelText = labelText;
		_intentValue = value;

		CustomMinimumSize = new Vector2(IconSize, HitBoxHeight);
		MouseFilter = MouseFilterEnum.Stop;

		// 数值标签（底端居中）
		_valueLabel = new Label
		{
			Text = labelText,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = MouseFilterEnum.Ignore,
			Visible = !string.IsNullOrEmpty(labelText),
		};
		_valueLabel.AddThemeColorOverride("font_color", Colors.White);
		_valueLabel.AddThemeFontSizeOverride("font_size", ValueFontSize);
		// 伤害数字使用固定手动布局，避免被 68px 稳定命中框撑到图标下方。
		_valueLabel.Size = new Vector2(IconSize, 20f);
		AddChild(_valueLabel);
	}

	// ===== 生命周期 =====

	public override void _Ready()
	{
		if (UIScaler.Instance != null)
			UIScaler.Instance.OnIntentVisualSettingsChanged += OnIntentVisualSettingsChanged;

		UpdateVisualOffsets(0f);
		UpdateValueLabelOffset();
		QueueRedraw();
	}

	public override void _ExitTree()
	{
		if (UIScaler.Instance != null)
			UIScaler.Instance.OnIntentVisualSettingsChanged -= OnIntentVisualSettingsChanged;
	}

	public override void _Process(double delta)
	{
		// 始终推进 bob 相位——保持多个图标相位同步。悬停/冻结时跳过视觉输出。
		float dt = (float)delta;
		bool iconFloating = UIScaler.Instance?.IntentIconFloatingEnabled ?? true;
		bool valueFloating = iconFloating && (UIScaler.Instance?.IntentValueFloatingEnabled ?? true);
		if (iconFloating || valueFloating)
			_bobPhase += dt * BobSpeed;

		if (_isFrozen || _isHovering)
			return;

		ApplyBobVisuals(iconFloating, valueFloating);
		UpdateValueLabelOffset();
		QueueRedraw();
	}

	private void ApplyBobVisuals(bool iconFloating, bool valueFloating)
	{
		float bobOffsetY = BobAmplitude + Mathf.Sin(_bobPhase) * BobAmplitude;
		_iconVisualOffsetY = iconFloating ? bobOffsetY : BobAmplitude;
		_valueVisualOffsetY = valueFloating ? bobOffsetY : BobAmplitude;
	}

	public override void _Draw()
	{
		DrawSetTransform(new Vector2(0f, _iconVisualOffsetY), 0f, Vector2.One);

		switch (_intentTypeId)
		{
			case 0:
				DrawAttackIcon();
				break;
			case 1:
				DrawMultiAttackIcon();
				break;
			case 2:
				DrawDefendIcon();
				break;
			case 3:
				DrawBuffIcon();
				break;
			case 4:
				DrawDebuffIcon();
				break;
			case 5:
				DrawHealIcon();
				break;
			case 6:
				DrawSummonIcon();
				break;
			case 7:
				DrawSleepIcon();
				break;
			case 8:
				DrawStunIcon();
				break;
			case 9:
				DrawEscapeIcon();
				break;
			case 10:
				DrawStatusCardIcon();
				break;
			case 11:
				DrawUnknownIcon();
				break;
			case 12: /* Hidden — 不绘制 */
				break;
			case 13:
				DrawSpellCastIcon();
				break;
			default:
				DrawUnknownIcon();
				break;
		}

		DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
	}

	public override void _Notification(int what)
	{
		if (what == NotificationMouseEnter)
		{
			OnMouseEntered();
		}
		else if (what == NotificationMouseExit)
		{
			OnMouseExited();
		}
	}

	// ===== 公共方法 =====

	/// <summary>设置冻结状态。冻结时停止动画、半透明显示。</summary>
	public void SetFrozen(bool frozen)
	{
		_isFrozen = frozen;
		_hoverScaleTween?.Kill();
		_hoverScaleTween = null;
		if (frozen)
		{
			Modulate = new Color(1f, 1f, 1f, 0.5f);
			_iconVisualOffsetY = BobAmplitude;
			_valueVisualOffsetY = BobAmplitude;
			UpdateValueLabelOffset();
			QueueRedraw();
		}
		else
		{
			Modulate = Colors.White;
		}
	}

	/// <summary>更新图标数值与类型，无需销毁重建。</summary>
	public void UpdateIntent(int typeId, string labelText, int value)
	{
		_intentTypeId = typeId;
		_labelText = labelText;
		_intentValue = value;

		_valueLabel.Text = labelText;
		_valueLabel.Visible = !string.IsNullOrEmpty(labelText);
		QueueRedraw();
	}

	/// <summary>执行动画：缩放脉冲 + 亮度闪白。</summary>
	public void PlayPerform()
	{
		var scaleTween = CreateTween();
		scaleTween.TweenProperty(this, "scale", new Vector2(PerformScale, PerformScale), PerformDuration * 0.4f);
		scaleTween.TweenProperty(this, "scale", Vector2.One, PerformDuration * 0.6f);

		var flashTween = CreateTween();
		Color targetRestore = _isFrozen ? new Color(1f, 1f, 1f, 0.5f) : Colors.White;
		flashTween.TweenProperty(this, "modulate", new Color(1.5f, 1.5f, 1.5f, 1f), PerformDuration * 0.3f);
		flashTween.TweenProperty(this, "modulate", targetRestore, PerformDuration * 0.3f);
	}

	/// <summary>设置悬停状态（由父卡片手动 hover 检测调用，替代不可靠的 _Notification）。</summary>
	public void SetHovering(bool hovering)
	{
		if (_isHovering == hovering)
			return;
		_isHovering = hovering;
		if (hovering)
		{
			_hoverScaleTween?.Kill();
			_hoverScaleTween = CreateTween();
			_hoverScaleTween.TweenProperty(this, "scale", new Vector2(HoverScale, HoverScale), 0.1f);
		}
		else
		{
			_hoverScaleTween?.Kill();
			_hoverScaleTween = CreateTween();
			_hoverScaleTween.TweenProperty(this, "scale", Vector2.One, 0.1f);
		}
	}

	public int GetIntentTypeId() => _intentTypeId;
	public int GetValue() => _intentValue;
	public string GetLabelText() => _labelText;

	// ===== 悬停处理 =====

	private void OnMouseEntered()
	{
		_isHovering = true;
		OnHovered?.Invoke(this);

		_hoverScaleTween?.Kill();
		_hoverScaleTween = CreateTween();
		_hoverScaleTween.TweenProperty(this, "scale", new Vector2(HoverScale, HoverScale), 0.1f);
	}

	private void OnMouseExited()
	{
		_isHovering = false;
		OnUnhovered?.Invoke(this);

		_hoverScaleTween?.Kill();
		_hoverScaleTween = CreateTween();
		_hoverScaleTween.TweenProperty(this, "scale", Vector2.One, 0.1f);
	}

	private void OnIntentVisualSettingsChanged()
	{
		UpdateVisualOffsets(0f);
		UpdateValueLabelOffset();
		QueueRedraw();
	}

	private void UpdateVisualOffsets(float delta)
	{
		bool iconFloating = UIScaler.Instance?.IntentIconFloatingEnabled ?? true;
		bool valueFloating = iconFloating && (UIScaler.Instance?.IntentValueFloatingEnabled ?? true);
		if (iconFloating || valueFloating)
			_bobPhase += delta * BobSpeed;

		ApplyBobVisuals(iconFloating, valueFloating);
	}

	/// <summary>同步数值标签的视觉浮动；父 Control 自身保持稳定命中区域。</summary>
	private void UpdateValueLabelOffset()
	{
		_valueLabel.Position = new Vector2(0f, _valueVisualOffsetY + IconSize - 20f);
		_valueLabel.Size = new Vector2(IconSize, 20f);
	}

	// ===== 绘制辅助 =====

	/// <summary>绘制半透明背景圆（所有非Hidden图标共用）。</summary>
	private void DrawBgCircle(Color tint)
	{
		DrawCircle(new Vector2(IconSize * 0.5f, IconSize * 0.5f), 22f,
			new Color(tint.R, tint.G, tint.B, 0.15f));
	}

	// ===== 各意图类型绘制 =====

	/// <summary>Attack(0)：红色剑形。伤害 &gt;15 时加粗。</summary>
	private void DrawAttackIcon()
	{
		Color c = new(1f, 0.15f, 0.15f);
		DrawBgCircle(c);

		float sf = _intentValue > 15 ? 1.2f : 1f;
		float cx = IconSize * 0.5f;
		int bw = (int)(8 * sf), bh = (int)(24 * sf);
		int gw = (int)(18 * sf), gh = (int)(4 * sf);
		int hw = (int)(6 * sf), hh = (int)(10 * sf);

		// 剑身（垂直矩形）
		DrawRect(new Rect2(cx - bw * 0.5f, 6f, bw, bh), c);
		// 护手（水平矩形）
		DrawRect(new Rect2(cx - gw * 0.5f, 6f + bh, gw, gh), c);
		// 剑柄（垂直矩形）
		DrawRect(new Rect2(cx - hw * 0.5f, 6f + bh + gh, hw, hh), c);
	}

	/// <summary>MultiAttack(1)：三把交叠红色剑形。</summary>
	private void DrawMultiAttackIcon()
	{
		Color c = new(1f, 0.15f, 0.15f, 0.85f);
		DrawBgCircle(new Color(1f, 0.15f, 0.15f));

		float cx = IconSize * 0.5f;
		// 三把剑水平错开
		float[] offsets = { -7f, 0f, 7f };
		foreach (float ox in offsets)
		{
			float x = cx + ox;
			DrawRect(new Rect2(x - 3f, 6f, 6f, 22f), c);
			DrawRect(new Rect2(x - 9f, 28f, 18f, 4f), c);
			DrawRect(new Rect2(x - 2.5f, 32f, 5f, 10f), c);
		}
	}

	/// <summary>Defend(2)：蓝色盾牌。</summary>
	private void DrawDefendIcon()
	{
		Color c = new(0.3f, 0.5f, 1f);
		DrawBgCircle(c);

		Vector2 center = new(IconSize * 0.5f, IconSize * 0.5f);
		// 实心盾体（填充圆）
		DrawCircle(center, 15f, c);
		// 中央竖线（剑刃暗影穿过盾牌）
		DrawLine(center + new Vector2(0, -16f), center + new Vector2(0, 16f),
			new Color(0.15f, 0.25f, 0.6f), 3f);
	}

	/// <summary>Buff(3)：绿色上箭头。</summary>
	private void DrawBuffIcon()
	{
		Color c = new(0.2f, 0.9f, 0.3f);
		DrawBgCircle(c);

		float cx = IconSize * 0.5f;
		// 箭头主体（竖直矩形）
		DrawRect(new Rect2(cx - 3.5f, 24f, 7f, 22f), c);
		// 箭头尖（上三角形）
		Vector2[] head = {
			new(cx - 10f, 24f), new(cx + 10f, 24f), new(cx, 10f)
		};
		DrawPolygon(head, new[] { c, c, c });
	}

	/// <summary>Debuff(4)：紫色下箭头。</summary>
	private void DrawDebuffIcon()
	{
		Color c = new(0.7f, 0.2f, 1f);
		DrawBgCircle(c);

		float cx = IconSize * 0.5f;
		// 箭头主体
		DrawRect(new Rect2(cx - 3.5f, 10f, 7f, 22f), c);
		// 箭头尖（下三角形）
		Vector2[] head = {
			new(cx - 10f, 32f), new(cx + 10f, 32f), new(cx, 46f)
		};
		DrawPolygon(head, new[] { c, c, c });
	}

	/// <summary>Heal(5)：绿色十字。</summary>
	private void DrawHealIcon()
	{
		Color c = new(0.3f, 0.9f, 0.4f);
		DrawBgCircle(c);

		float cx = IconSize * 0.5f;
		float cy = IconSize * 0.5f;
		// 水平条
		DrawRect(new Rect2(cx - 9f, cy - 2.5f, 18f, 5f), c);
		// 垂直条
		DrawRect(new Rect2(cx - 2.5f, cy - 9f, 5f, 18f), c);
	}

	/// <summary>Summon(6)：黄色五角星。</summary>
	private void DrawSummonIcon()
	{
		Color c = new(1f, 0.85f, 0.1f);
		DrawBgCircle(c);

		float cx = IconSize * 0.5f;
		float cy = IconSize * 0.5f;
		float outerR = 15f, innerR = 6.5f;
		var starPoints = new Vector2[10];
		for (int i = 0; i < 10; i++)
		{
			float angle = (i * 36f - 90f) * Mathf.Pi / 180f;
			float r = (i % 2 == 0) ? outerR : innerR;
			starPoints[i] = new Vector2(cx + Mathf.Cos(angle) * r, cy + Mathf.Sin(angle) * r);
		}
		// 填充星形需要三角剖分，这里用多层绘制模拟
		DrawCircle(new Vector2(cx, cy), outerR, c); // 实心圆底
													// 五道星芒线（内外交替）
		for (int i = 0; i < 10; i += 2)
		{
			DrawLine(starPoints[i], starPoints[(i + 5) % 10], c, 2.5f);
		}
	}

	/// <summary>Sleep(7)：浅蓝色 Z 字形。</summary>
	private void DrawSleepIcon()
	{
		Color c = new(0.5f, 0.7f, 1f);
		DrawBgCircle(c);

		// Z 字三段
		DrawLine(new Vector2(16f, 15f), new Vector2(34f, 15f), c, 3.5f);
		DrawLine(new Vector2(34f, 15f), new Vector2(18f, 35f), c, 3.5f);
		DrawLine(new Vector2(18f, 35f), new Vector2(36f, 35f), c, 3.5f);
	}

	/// <summary>Stun(8)：黄色双圆 + 感叹号。</summary>
	private void DrawStunIcon()
	{
		Color c = new(1f, 0.9f, 0.2f);
		DrawBgCircle(c);

		Vector2 center = new(IconSize * 0.5f, 26f);
		// 双圆环
		DrawArc(center, 14f, 0f, Mathf.Tau, 32, c, 2.5f);
		DrawArc(center, 9f, 0f, Mathf.Tau, 24, c, 2f);
		// 感叹号竖线
		DrawLine(new Vector2(28f, 17f), new Vector2(28f, 28f), Colors.White, 3.5f);
		// 感叹号圆点
		DrawCircle(new Vector2(28f, 33f), 2.5f, Colors.White);
	}

	/// <summary>Escape(9)：灰色门框 + 右箭头。</summary>
	private void DrawEscapeIcon()
	{
		Color c = new(0.5f, 0.5f, 0.5f);
		DrawBgCircle(c);

		// 门框
		DrawRect(new Rect2(14f, 12f, 22f, 30f), c, false, 2f);
		// 右侧三角箭头
		Vector2[] arrow = {
			new(24f, 19f), new(36f, 27f), new(24f, 35f)
		};
		DrawPolygon(arrow, new[] { c });
	}

	/// <summary>StatusCard(10)：青色卡牌带折角。</summary>
	private void DrawStatusCardIcon()
	{
		Color c = new(0.2f, 0.8f, 0.9f);
		DrawBgCircle(c);

		// 卡牌主体（切去右上折角）
		Vector2[] card = {
			new(16f, 16f), new(34f, 16f), new(38f, 20f), new(38f, 36f), new(16f, 36f)
		};
		DrawPolygon(card, new[] { c, c, c, c, c });
		// 折角线
		DrawLine(new Vector2(34f, 16f), new Vector2(38f, 20f), new Color(1f, 1f, 1f, 0.35f), 1.5f);
	}

	/// <summary>Unknown(11)：灰色问号。</summary>
	private void DrawUnknownIcon()
	{
		Color c = new(0.4f, 0.4f, 0.4f);
		DrawBgCircle(c);

		// 问号顶部弧线（从左到右）
		DrawArc(new Vector2(28f, 22f), 8f, Mathf.Pi, 0f, 16, c, 3f);
		// 中间竖线
		DrawLine(new Vector2(28f, 30f), new Vector2(28f, 37f), c, 3f);
		// 底部圆点
		DrawCircle(new Vector2(28f, 41f), 2.5f, c);
	}

	/// <summary>SpellCast(13)：紫粉色卡牌 + 魔法星芒。</summary>
	private void DrawSpellCastIcon()
	{
		Color c = new(0.9f, 0.3f, 0.85f);
		DrawBgCircle(c);

		float cx = IconSize * 0.5f;
		float cy = IconSize * 0.5f;
		// 小卡牌矩形
		DrawRect(new Rect2(cx - 10f, cy - 12f, 20f, 24f), c);
		// 卡牌内星形（魔法符号）
		DrawCircle(new Vector2(cx, cy - 2f), 6f, new Color(1f, 0.9f, 0.3f));
		// 星芒四射线
		float r = 10f;
		DrawLine(new Vector2(cx, cy - 2f - r), new Vector2(cx, cy - 2f + r), Colors.White, 1.5f);
		DrawLine(new Vector2(cx - r, cy - 2f), new Vector2(cx + r, cy - 2f), Colors.White, 1.5f);
	}
}
