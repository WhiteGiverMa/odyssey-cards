#nullable enable
using System;
using Godot;

namespace OdysseyCards.UI;

/// <summary>
/// 六边形棱镜法力阵列：外壳、六向折射面、内核与顶缘焦散。
/// 点亮表示当前法力，空壳表示已消耗法力；栈上顶点不产生逐帧托管垃圾。
/// </summary>
[Tool]
public partial class ManaCrystalBar : Control
{
	private int _current;
	private int _max = 1;
	private static readonly Color _crystalFill = new("#3d9bd8");
	private static readonly Color _crystalHighlight = new("#9fdcff");
	private static readonly Color _crystalEdge = new("#1a4a6b");
	private static readonly Color _emptyFill = new(0.08f, 0.11f, 0.17f, 0.9f);
	private static readonly Color _emptyEdge = new(0.3f, 0.35f, 0.5f, 0.5f);

	/// <summary>创建鼠标穿透的法力微标条。</summary>
	public ManaCrystalBar()
	{
		CustomMinimumSize = new Vector2(120, 26);
		MouseFilter = MouseFilterEnum.Ignore;
	}

	/// <summary>设置法力；允许零上限和超上限临时法力，不截断真实资源数量。</summary>
	public void SetMana(int current, int max)
	{
		_current = Mathf.Max(0, current);
		_max = Mathf.Max(0, max);
		QueueRedraw();
	}

	public override void _Notification(int what)
	{
		if (what == NotificationResized)
		{
			QueueRedraw();
		}
	}

	public override void _Draw()
	{
		Vector2 size = Size;
		int count = Mathf.Max(_current, _max);
		if (size.X < 1f || size.Y < 1f || count < 1)
		{
			return;
		}

		// 极端法力值以可见像素容量为上界，避免小尺寸出现负半径或无界绘制。
		int visible = Mathf.Min(count, Mathf.Max(1, (int)(size.X / 3f)));
		float spacing = Mathf.Min(3f, size.X / visible * 0.14f);
		float diameter = Mathf.Min(size.Y - 2f, (size.X - (visible - 1) * spacing) / visible);
		if (diameter <= 0f)
		{
			return;
		}
		float radius = diameter * 0.48f;
		for (int i = 0; i < visible; i++)
		{
			Vector2 center = new(diameter * 0.5f + i * (diameter + spacing), size.Y * 0.5f);
			DrawCrystal(this, center, radius, i < _current);
		}
	}

	/// <summary>在任意 CanvasItem 的 _Draw 中绘制独立六边形棱镜，供紧凑 HUD 复用。</summary>
	public static void DrawCrystal(CanvasItem canvas, Vector2 center, float radius, bool filled)
	{
		ArgumentNullException.ThrowIfNull(canvas);
		if (radius <= 0f)
		{
			return;
		}
		Span<Vector2> hex = stackalloc Vector2[7];
		for (int i = 0; i < 6; i++)
		{
			hex[i] = center + Vector2.FromAngle(-Mathf.Pi * 0.5f + Mathf.Tau * i / 6f) * radius;
		}
		hex[6] = hex[0];
		canvas.DrawColoredPolygon(hex[..6], filled ? _crystalEdge : _emptyFill, ReadOnlySpan<Vector2>.Empty, null);
		Vector2 core = center + new Vector2(-0.12f, -0.14f) * radius;
		Span<Vector2> facet = stackalloc Vector2[3];
		facet[0] = core;
		for (int i = 0; i < 6; i++)
		{
			facet[1] = hex[i];
			facet[2] = hex[i + 1];
			Color color = filled
				? _crystalFill.Lerp(i is 0 or 5 ? _crystalHighlight : _crystalEdge, i is 0 or 5 ? 0.65f : 0.32f)
				: new Color(_emptyEdge, i is 0 or 5 ? 0.12f : 0.035f);
			canvas.DrawColoredPolygon(facet, color, ReadOnlySpan<Vector2>.Empty, null);
			canvas.DrawLine(core, hex[i], new Color(filled ? _crystalHighlight : _emptyEdge, filled ? 0.32f : 0.1f), 0.65f, true);
		}
		canvas.DrawPolyline(hex, filled ? _crystalHighlight : _emptyEdge, Mathf.Max(0.75f, radius * 0.08f), true);
		if (!filled)
		{
			return;
		}
		canvas.DrawLine(hex[5].Lerp(core, 0.12f), hex[0].Lerp(core, 0.12f),
			new Color(Colors.White, 0.85f), Mathf.Max(0.8f, radius * 0.08f), true);
		canvas.DrawLine(core, hex[2].Lerp(core, 0.3f), new Color(_crystalHighlight, 0.7f), 0.65f, true);
		canvas.DrawCircle(core, Mathf.Max(0.45f, radius * 0.09f), new Color(Colors.White, 0.8f));
	}
}
