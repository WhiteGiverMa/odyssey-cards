#nullable enable
using Godot;

namespace OdysseyCards.UI;

/// <summary>
/// 英雄底座光环——同心圆环 + 旋转弧线的程序化英雄标识。
/// 敌方绯红 / 玩家青金，为战场双方英雄提供「对弈感」视觉锚点。
/// 全部 _Draw 绘制，零美术资产。
/// </summary>
[Tool]
public partial class HeroAuraRing : Control
{
	/// <summary>光环主色（敌方绯红 / 玩家青金）。</summary>
	[Export] public Color RingColor { get; set; } = new("#ff6b7a");

	/// <summary>旋转弧速度（弧度/秒）。</summary>
	[Export] public float SpinSpeed { get; set; } = 1.1f;

	private float _angle;

	public HeroAuraRing()
	{
		CustomMinimumSize = new Vector2(38, 38);
		MouseFilter = MouseFilterEnum.Ignore;
	}

	public override void _Process(double delta)
	{
		// 编辑器预览保持静态
		if (Engine.IsEditorHint())
		{
			return;
		}

		_angle += (float)delta * SpinSpeed;
		QueueRedraw();
	}

	public override void _Draw()
	{
		float diameter = Mathf.Min(Size.X, Size.Y);
		if (diameter < 4f)
		{
			return;
		}

		Vector2 center = Size / 2f;
		float r = diameter / 2f - 2f;

		// 底盘（深空半透明圆底，让光环从身份卡背景中浮出）
		DrawCircle(center, r, new Color(0.05f, 0.05f, 0.1f, 0.55f));

		// 外环
		DrawArc(center, r, 0, Mathf.Tau, 48, new Color(RingColor, 0.9f), 2f, true);

		// 内环细线
		DrawArc(center, r * 0.68f, 0, Mathf.Tau, 40, new Color(RingColor, 0.35f), 1f, true);

		// 旋转弧 ×2（对称两段，缓慢旋转营造「能量环绕」感）
		for (int i = 0; i < 2; i++)
		{
			float start = _angle + i * Mathf.Pi;
			DrawArc(center, r * 0.84f, start, start + 1.5f, 20, new Color(RingColor, 0.75f), 2.5f, true);
		}

		// 中心菱形徽记（英雄身份标记）
		float d = r * 0.3f;
		var diamond = new Vector2[]
		{
			center + new Vector2(0, -d),
			center + new Vector2(d, 0),
			center + new Vector2(0, d),
			center + new Vector2(-d, 0),
		};
		DrawColoredPolygon(diamond, new Color(RingColor, 0.9f));
	}
}
