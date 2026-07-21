#nullable enable
using Godot;

namespace OdysseyCards.UI;

/// <summary>
/// 法力水晶条——六边形水晶阵列替代「法力 n/m」纯文本。
/// 点亮水晶 = 当前法力，空水晶 = 已消耗/未解锁；超上限时自动缩小水晶尺寸。
/// 纯 _Draw 绘制，零图片资产。
/// </summary>
[Tool]
public partial class ManaCrystalBar : Control
{
	private int _current;
	private int _max = 1;

	private static readonly Color CrystalFill = new("#3d9bd8");
	private static readonly Color CrystalHighlight = new("#9fdcff");
	private static readonly Color CrystalEdge = new("#1a4a6b");
	private static readonly Color EmptyFill = new(0.12f, 0.14f, 0.2f, 0.8f);
	private static readonly Color EmptyEdge = new(0.3f, 0.35f, 0.5f, 0.5f);

	public ManaCrystalBar()
	{
		CustomMinimumSize = new Vector2(120, 26);
		MouseFilter = MouseFilterEnum.Ignore;
	}

	/// <summary>
	/// 设置法力值并触发重绘。
	/// </summary>
	public void SetMana(int current, int max)
	{
		_current = Mathf.Max(0, current);
		_max = Mathf.Max(1, max);
		QueueRedraw();
	}

	public override void _Draw()
	{
		var size = Size;
		if (size.X < 1f || size.Y < 1f || _max < 1)
			return;

		// 水晶尺寸自适应：水晶 + 间距总宽不超控件
		float diameter = size.Y;
		float spacing = 2f;
		float totalWidth = _max * (diameter + spacing) - spacing;
		if (totalWidth > size.X)
		{
			diameter = (size.X - (_max - 1) * spacing) / _max;
		}
		float r = diameter * 0.5f;
		float cy = size.Y * 0.5f;

		for (int i = 0; i < _max; i++)
		{
			float cx = r + i * (diameter + spacing);
			var center = new Vector2(cx, cy);
			bool filled = i < _current;

			// 六边形（尖顶）
			var hex = HexPoints(center, r);
			if (filled)
			{
				DrawColoredPolygon(hex, CrystalFill);
				DrawPolyline(ClosedLoop(hex), CrystalHighlight, Mathf.Max(1.2f, r * 0.14f), true);
				// 顶部高光点
				DrawCircle(center + new Vector2(-r * 0.22f, -r * 0.3f), r * 0.16f, new Color(1f, 1f, 1f, 0.75f));
			}
			else
			{
				DrawColoredPolygon(hex, EmptyFill);
				DrawPolyline(ClosedLoop(hex), EmptyEdge, Mathf.Max(1f, r * 0.1f), true);
			}
		}
	}

	private static Vector2[] HexPoints(Vector2 center, float r)
	{
		var pts = new Vector2[6];
		for (int i = 0; i < 6; i++)
		{
			float ang = -Mathf.Pi * 0.5f + Mathf.Tau * i / 6f;
			pts[i] = center + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r;
		}
		return pts;
	}

	private static Vector2[] ClosedLoop(Vector2[] pts)
	{
		var loop = new Vector2[pts.Length + 1];
		pts.CopyTo(loop, 0);
		loop[pts.Length] = pts[0];
		return loop;
	}
}
