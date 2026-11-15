using System;
using Godot;

namespace OdysseyCards.UI;

/// <summary>视觉层自有兵种索引；不引用尚未合并的战斗层枚举。</summary>
public enum TacticalUnitIcon
{
	/// <summary>先锋：重盾与双斜杠。</summary>
	Vanguard,
	/// <summary>突击：履带装甲与长身管。</summary>
	Assault,
	/// <summary>支援：仰角火炮与测距环。</summary>
	Support,
	/// <summary>游击：后掠翼飞梭。</summary>
	Skirmisher,
}

/// <summary>
/// 战术微标矢量库。仅可在调用者的 _Draw 中使用；size 表示图标外接正方形边长。
/// 所有几何使用栈上缓冲，保持调用者变换不变，无外部纹理与逐帧托管分配。
/// </summary>
public static class TacticalIconDrawer
{
	/// <summary>按视觉兵种索引绘制图标，主色与轮廓色由调用方指定。</summary>
	public static void DrawUnitClass(CanvasItem canvas, TacticalUnitIcon unitClass,
		Vector2 center, float size, Color primary, Color outline)
	{
		switch (unitClass)
		{
			case TacticalUnitIcon.Vanguard:
				DrawVanguard(canvas, center, size, primary, outline);
				break;
			case TacticalUnitIcon.Assault:
				DrawAssault(canvas, center, size, primary, outline);
				break;
			case TacticalUnitIcon.Support:
				DrawSupport(canvas, center, size, primary, outline);
				break;
			case TacticalUnitIcon.Skirmisher:
				DrawSkirmisher(canvas, center, size, primary, outline);
				break;
			default:
				throw new ArgumentOutOfRangeException(nameof(unitClass));
		}
	}

	/// <summary>先锋：棱角重盾，左右切面与两条锐利的斜杠军衔。</summary>
	public static void DrawVanguard(CanvasItem canvas, Vector2 center, float size, Color primary, Color outline)
	{
		ArgumentNullException.ThrowIfNull(canvas);
		if (size <= 0f)
		{ return; }
		float r = size * 0.48f;
		ReadOnlySpan<Vector2> shield = stackalloc Vector2[]
		{
			new(-0.68f, -0.8f), new(0.68f, -0.8f), new(0.76f, -0.52f),
			new(0.58f, 0.5f), new(0, 0.96f), new(-0.58f, 0.5f), new(-0.76f, -0.52f),
		};
		DrawPlate(canvas, shield, center, r, primary, outline);
		Line(canvas, center, r, new(-0.48f, -0.56f), new(0.45f, -0.56f), primary.Lerp(Colors.White, 0.5f));
		Line(canvas, center, r, new(-0.4f, 0.3f), new(0, -0.3f), primary, 0.14f);
		Line(canvas, center, r, new(0.02f, 0.36f), new(0.42f, -0.24f), primary, 0.14f);
		Line(canvas, center, r, new(0, 0.6f), new(0, 0.77f), outline);
	}

	/// <summary>突击：倾斜装甲车体、分段履带轮与长身管主炮。</summary>
	public static void DrawAssault(CanvasItem canvas, Vector2 center, float size, Color primary, Color outline)
	{
		ArgumentNullException.ThrowIfNull(canvas);
		if (size <= 0f)
		{ return; }
		float r = size * 0.48f;
		ReadOnlySpan<Vector2> track = stackalloc Vector2[]
		{
			new(-0.88f, 0.23f), new(0.64f, 0.18f), new(0.85f, 0.44f),
			new(0.58f, 0.72f), new(-0.72f, 0.72f), new(-0.94f, 0.48f),
		};
		DrawPlate(canvas, track, center, r, new Color(primary, primary.A * 0.55f), outline);
		for (int i = 0; i < 4; i++)
		{
			Vector2 wheel = center + new Vector2(-0.6f + i * 0.36f, 0.47f) * r;
			canvas.DrawArc(wheel, r * 0.12f, 0, Mathf.Tau, 12, primary, Mathf.Max(0.7f, r * 0.045f), true);
		}
		ReadOnlySpan<Vector2> hull = stackalloc Vector2[]
		{
			new(-0.86f, 0.18f), new(-0.56f, -0.28f), new(0.42f, -0.35f),
			new(0.76f, 0.04f), new(0.6f, 0.27f), new(-0.7f, 0.33f),
		};
		DrawPlate(canvas, hull, center, r, primary, outline);
		ReadOnlySpan<Vector2> turret = stackalloc Vector2[]
		{
			new(-0.4f, -0.29f), new(-0.2f, -0.6f), new(0.22f, -0.6f), new(0.4f, -0.29f),
		};
		DrawPlate(canvas, turret, center, r, primary, outline);
		Line(canvas, center, r, new(0.08f, -0.48f), new(0.94f, -0.76f), outline, 0.17f);
		Line(canvas, center, r, new(0.08f, -0.5f), new(0.94f, -0.78f), primary.Lerp(Colors.White, 0.25f), 0.08f);
		Line(canvas, center, r, new(-0.48f, -0.1f), new(0.36f, -0.14f), primary);
	}

	/// <summary>支援：独立仰角炮管与十字测距分划环。</summary>
	public static void DrawSupport(CanvasItem canvas, Vector2 center, float size, Color primary, Color outline)
	{
		ArgumentNullException.ThrowIfNull(canvas);
		if (size <= 0f)
		{ return; }
		float r = size * 0.48f;
		Vector2 sight = center + new Vector2(0.34f, -0.35f) * r;
		canvas.DrawArc(sight, r * 0.48f, 0, Mathf.Tau, 40, new Color(primary, primary.A * 0.6f), Mathf.Max(0.6f, r * 0.035f), true);
		for (int i = 0; i < 12; i++)
		{
			var axis = Vector2.FromAngle(i * Mathf.Tau / 12f);
			canvas.DrawLine(sight + axis * r * 0.42f, sight + axis * r * (i % 3 == 0 ? 0.63f : 0.51f), primary,
				Mathf.Max(0.6f, r * 0.035f), true);
		}
		Line(canvas, center, r, new(-0.58f, 0.34f), new(0.4f, -0.72f), outline, 0.22f);
		Line(canvas, center, r, new(-0.58f, 0.31f), new(0.4f, -0.75f), primary, 0.1f);
		ReadOnlySpan<Vector2> mount = stackalloc Vector2[]
		{
			new(-0.87f, 0.7f), new(-0.5f, 0.1f), new(-0.15f, 0.12f), new(0.45f, 0.68f),
		};
		DrawPlate(canvas, mount, center, r, primary, outline);
		Line(canvas, center, r, new(-0.94f, 0.77f), new(0.62f, 0.77f), primary, 0.09f);
		canvas.DrawCircle(center + new Vector2(-0.35f, 0.34f) * r, r * 0.1f, primary.Lerp(Colors.White, 0.4f));
	}

	/// <summary>游击：后掠双翼破空梭，偏置尾迹强调机动方向。</summary>
	public static void DrawSkirmisher(CanvasItem canvas, Vector2 center, float size, Color primary, Color outline)
	{
		ArgumentNullException.ThrowIfNull(canvas);
		if (size <= 0f)
		{ return; }
		float r = size * 0.48f;
		ReadOnlySpan<Vector2> wings = stackalloc Vector2[]
		{
			new(0, -0.98f), new(0.22f, -0.15f), new(0.95f, 0.53f), new(0.9f, 0.72f),
			new(0.19f, 0.37f), new(0.12f, 0.76f), new(-0.12f, 0.76f), new(-0.19f, 0.37f),
			new(-0.9f, 0.72f), new(-0.95f, 0.53f), new(-0.22f, -0.15f),
		};
		DrawPlate(canvas, wings, center, r, primary, outline);
		Line(canvas, center, r, new(0, -0.65f), new(0, 0.42f), primary.Lerp(Colors.White, 0.55f), 0.07f);
		Line(canvas, center, r, new(-0.32f, 0.7f), new(-0.5f, 0.98f), new Color(primary, primary.A * 0.55f));
		Line(canvas, center, r, new(0.32f, 0.7f), new(0.5f, 0.98f), new Color(primary, primary.A * 0.55f));
		Line(canvas, center, r, new(0, 0.83f), new(0, 1.02f), primary, 0.08f);
	}

	/// <summary>嘲讽：厚重八角金铁巨盾与中心十字星；外轮廓封闭，强调全局阻挡。</summary>
	public static void DrawTaunt(CanvasItem canvas, Vector2 center, float size, Color primary, Color outline)
	{
		ArgumentNullException.ThrowIfNull(canvas);
		if (size <= 0f)
		{ return; }
		float r = size * 0.47f;
		ReadOnlySpan<Vector2> shield = stackalloc Vector2[]
		{
			new(-0.5f, -0.95f), new(0.5f, -0.95f), new(0.86f, -0.52f), new(0.86f, 0.42f),
			new(0.42f, 0.94f), new(-0.42f, 0.94f), new(-0.86f, 0.42f), new(-0.86f, -0.52f),
		};
		DrawPlate(canvas, shield, center, r, primary, outline, 0.12f);
		DrawOutline(canvas, shield, center, r * 0.72f, primary, Mathf.Max(0.8f, r * 0.065f));
		ReadOnlySpan<Vector2> star = stackalloc Vector2[]
		{
			new(0, -0.59f), new(0.13f, -0.13f), new(0.5f, 0), new(0.13f, 0.13f),
			new(0, 0.56f), new(-0.13f, 0.13f), new(-0.5f, 0), new(-0.13f, -0.13f),
		};
		Fill(canvas, star, center, r, primary.Lerp(Colors.White, 0.5f));
	}

	/// <summary>守护：左右张开的双翼掩体与小型中心盾，明确表达仅邻格掩护。</summary>
	public static void DrawGuard(CanvasItem canvas, Vector2 center, float size, Color primary, Color outline)
	{
		ArgumentNullException.ThrowIfNull(canvas);
		if (size <= 0f)
		{ return; }
		float r = size * 0.48f;
		ReadOnlySpan<Vector2> wing = stackalloc Vector2[]
		{
			new(-0.3f, -0.2f), new(-0.96f, -0.66f), new(-0.86f, 0.22f), new(-0.35f, 0.58f),
		};
		DrawPlate(canvas, wing, center, r, primary, outline);
		Span<Vector2> reflected = stackalloc Vector2[4];
		for (int i = 0; i < wing.Length; i++)
		{ reflected[i] = new Vector2(-wing[i].X, wing[i].Y); }
		DrawPlate(canvas, reflected, center, r, primary, outline);
		ReadOnlySpan<Vector2> shield = stackalloc Vector2[]
		{
			new(-0.28f, -0.44f), new(0.28f, -0.44f), new(0.3f, 0.3f), new(0, 0.68f), new(-0.3f, 0.3f),
		};
		DrawPlate(canvas, shield, center, r, primary, outline);
		Line(canvas, center, r, new(-0.78f, -0.28f), new(-0.45f, -0.04f), primary);
		Line(canvas, center, r, new(0.78f, -0.28f), new(0.45f, -0.04f), primary);
		Line(canvas, center, r, new(0, -0.22f), new(0, 0.35f), primary.Lerp(Colors.White, 0.6f), 0.08f);
	}

	/// <summary>情报：全息菱形扫描眼与左右脉冲波纹。</summary>
	public static void DrawIntel(CanvasItem canvas, Vector2 center, float size, Color primary, Color outline)
	{
		ArgumentNullException.ThrowIfNull(canvas);
		if (size <= 0f)
		{ return; }
		float r = size * 0.46f;
		ReadOnlySpan<Vector2> diamond = stackalloc Vector2[]
		{
			new(0, -0.83f), new(0.82f, 0), new(0, 0.83f), new(-0.82f, 0),
		};
		DrawPlate(canvas, diamond, center, r, primary, outline);
		ReadOnlySpan<Vector2> eye = stackalloc Vector2[]
		{
			new(-0.59f, 0), new(-0.2f, -0.29f), new(0.2f, -0.29f),
			new(0.59f, 0), new(0.2f, 0.29f), new(-0.2f, 0.29f),
		};
		DrawOutline(canvas, eye, center, r, primary, Mathf.Max(0.8f, r * 0.05f));
		canvas.DrawCircle(center, r * 0.15f, primary.Lerp(Colors.White, 0.6f));
		for (int i = 0; i < 2; i++)
		{
			float wave = r * (0.95f + i * 0.18f);
			canvas.DrawArc(center, wave, -0.42f, 0.42f, 12, new Color(primary, primary.A * (0.7f - i * 0.2f)), 0.8f, true);
			canvas.DrawArc(center, wave, Mathf.Pi - 0.42f, Mathf.Pi + 0.42f, 12,
				new Color(primary, primary.A * (0.7f - i * 0.2f)), 0.8f, true);
		}
	}

	/// <summary>行动油费：红晶菱标；可传入其他主色用于禁用或阵营态。</summary>
	public static void DrawActionCost(CanvasItem canvas, Vector2 center, float size, Color primary, Color outline)
	{
		ArgumentNullException.ThrowIfNull(canvas);
		if (size <= 0f)
		{ return; }
		float r = size * 0.46f;
		ReadOnlySpan<Vector2> diamond = stackalloc Vector2[]
		{
			new(0, -1), new(0.68f, 0), new(0, 1), new(-0.68f, 0),
		};
		DrawPlate(canvas, diamond, center, r, primary, outline);
		ReadOnlySpan<Vector2> facet = stackalloc Vector2[] { new(0, -1), new(0.68f, 0), new(0, 0.21f) };
		Fill(canvas, facet, center, r, new Color(primary.Lerp(Colors.White, 0.65f), primary.A * 0.8f));
		Line(canvas, center, r, new(0, -0.68f), new(0, 0.72f), primary.Lerp(Colors.White, 0.5f), 0.045f);
		Line(canvas, center, r, new(-0.49f, 0), new(0, 0.21f), primary, 0.05f);
	}

	private static void DrawPlate(CanvasItem canvas, ReadOnlySpan<Vector2> shape, Vector2 center, float radius,
		Color primary, Color outline, float thickness = 0.075f)
	{
		Fill(canvas, shape, center, radius, new Color(primary.Darkened(0.72f), primary.A));
		DrawOutline(canvas, shape, center, radius, outline, Mathf.Max(1f, radius * thickness * 1.9f));
		DrawOutline(canvas, shape, center, radius, primary, Mathf.Max(0.65f, radius * thickness));
	}

	private static void Fill(CanvasItem canvas, ReadOnlySpan<Vector2> shape, Vector2 center, float radius, Color color)
	{
		Span<Vector2> points = stackalloc Vector2[shape.Length];
		for (int i = 0; i < shape.Length; i++)
		{ points[i] = center + shape[i] * radius; }
		canvas.DrawColoredPolygon(points, color, ReadOnlySpan<Vector2>.Empty, null);
	}

	private static void DrawOutline(CanvasItem canvas, ReadOnlySpan<Vector2> shape, Vector2 center, float radius,
		Color color, float width)
	{
		Span<Vector2> points = stackalloc Vector2[shape.Length + 1];
		for (int i = 0; i < shape.Length; i++)
		{ points[i] = center + shape[i] * radius; }
		points[^1] = points[0];
		canvas.DrawPolyline(points, color, width, true);
	}

	private static void Line(CanvasItem canvas, Vector2 center, float radius, Vector2 from, Vector2 to,
		Color color, float thickness = 0.055f)
	{
		canvas.DrawLine(center + from * radius, center + to * radius, color, Mathf.Max(0.65f, radius * thickness), true);
	}
}
