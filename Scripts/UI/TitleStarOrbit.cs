#nullable enable
using Godot;

namespace OdysseyCards.UI;

/// <summary>
/// 标题环绕星——主菜单标题的装饰性环绕小星星。
/// Track(target) 后每帧同步目标全局区域，星星沿其外沿椭圆轨道环绕 + 闪烁。
/// 全部 _Draw 绘制，零美术资产。
/// </summary>
public partial class TitleStarOrbit : Control
{
	private Control? _target;
	private float _time;

	public TitleStarOrbit()
	{
		MouseFilter = MouseFilterEnum.Ignore;
	}

	/// <summary>跟踪目标控件（星星沿目标外沿的椭圆轨道环绕）。</summary>
	public void Track(Control target)
	{
		_target = target;
	}

	public override void _Process(double delta)
	{
		_time += (float)delta;

		// 每帧同步目标区域（容器布局完成后目标位置才稳定，逐帧同步最稳）
		if (_target != null && IsInstanceValid(_target))
		{
			Rect2 rect = _target.GetGlobalRect();
			GlobalPosition = rect.Position - new Vector2(30, 16);
			Size = rect.Size + new Vector2(60, 32);
		}

		QueueRedraw();
	}

	public override void _Draw()
	{
		if (Size.X < 10f || Size.Y < 10f)
		{
			return;
		}

		Vector2 center = Size / 2f;
		float rx = Size.X / 2f;
		float ry = Size.Y / 2f;

		for (int i = 0; i < 5; i++)
		{
			// 椭圆轨道，五星相位均匀错开
			float phase = _time * 0.45f + i * Mathf.Tau / 5f;
			Vector2 pos = center + new Vector2(Mathf.Cos(phase) * rx, Mathf.Sin(phase) * ry);

			// 闪烁（每星独立相位）
			float twinkle = 0.45f + 0.55f * (0.5f + 0.5f * Mathf.Sin(_time * 2.2f + i * 1.7f));
			Color color = i % 2 == 0
				? new Color(1f, 0.62f, 0.82f, 0.9f * twinkle)   // 星粉
				: new Color(0.5f, 0.85f, 1f, 0.9f * twinkle);   // 青金
			DrawFourPointStar(pos, 4.5f + (i % 3), color);
		}
	}

	/// <summary>画一颗四角星（尖角 + 内凹的 8 顶点 polygon）。</summary>
	private void DrawFourPointStar(Vector2 center, float size, Color color)
	{
		float w = size * 0.3f;
		var points = new Vector2[]
		{
			center + new Vector2(0, -size),
			center + new Vector2(w, -w),
			center + new Vector2(size, 0),
			center + new Vector2(w, w),
			center + new Vector2(0, size),
			center + new Vector2(-w, w),
			center + new Vector2(-size, 0),
			center + new Vector2(-w, -w),
		};
		DrawColoredPolygon(points, color);
	}
}
