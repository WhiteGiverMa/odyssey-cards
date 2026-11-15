using System;
using Godot;

namespace OdysseyCards.UI;

/// <summary>视觉层前线归属，不依赖战斗规则层的控制权枚举。</summary>
public enum FrontlineControlSide
{
	/// <summary>中立星灰。</summary>
	None,
	/// <summary>我方青金。</summary>
	Player,
	/// <summary>敌方绯红。</summary>
	Enemy,
}

/// <summary>
/// 三线阵地底板：4 格敌支援、5 格独占前线、4 格我支援。
/// 只表现布局与归属，不保存单位、不参与命中和移动规则；适合放在 BoardUI 最底层。
/// </summary>
[Tool]
public partial class FrontlineArenaBackdrop : Control
{
	private static readonly Color _neutral = new("#6e7391");
	private FrontlineControlSide _owner;
	private Color _displayColor = _neutral;
	private Color _targetColor = _neutral;
	private Rect2 _enemySupport;
	private Rect2 _frontline;
	private Rect2 _playerSupport;
	private bool _explicitLayout;
	private float _phase;
	private float _redrawElapsed;

	/// <summary>当前目标控制方；颜色由显示层平滑过渡。</summary>
	public FrontlineControlSide ControlSide => _owner;
	/// <summary>底板本地坐标中的敌方支援线区域。</summary>
	public Rect2 EnemySupportBounds => _enemySupport;
	/// <summary>底板本地坐标中的中央前线区域。</summary>
	public Rect2 FrontlineBounds => _frontline;
	/// <summary>底板本地坐标中的我方支援线区域。</summary>
	public Rect2 PlayerSupportBounds => _playerSupport;

	/// <summary>兼容编辑器的无参构造；完全穿透输入，默认占用 505 像素棋盘高度。</summary>
	public FrontlineArenaBackdrop()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		CustomMinimumSize = new Vector2(0, 505);
		ClipContents = true;
	}

	/// <summary>
	/// 直接同步三行本地矩形，可精确对齐下一阶段 BoardUI 的实际槽位。
	/// 显式布局不会随本控件尺寸重算，调用者在布局变化后再次同步即可。
	/// </summary>
	public void SetLayout(Rect2 enemySupport, Rect2 frontline, Rect2 playerSupport)
	{
		ValidateRect(enemySupport, nameof(enemySupport));
		ValidateRect(frontline, nameof(frontline));
		ValidateRect(playerSupport, nameof(playerSupport));
		_enemySupport = enemySupport;
		_frontline = frontline;
		_playerSupport = playerSupport;
		_explicitLayout = true;
		QueueRedraw();
	}

	/// <summary>
	/// 以三行 Y 范围（X=上沿，Y=下沿）和宽度同步布局，所有数值均为本地逻辑像素。
	/// 支援宽度默认为前线宽度的 80%，左右居中；left 可对齐外部棋盘边距。
	/// </summary>
	public void SynchronizeLayout(float width, Vector2 enemyYRange, Vector2 frontlineYRange,
		Vector2 playerYRange, float supportWidth = 0f, float left = 0f)
	{
		float support = supportWidth > 0f ? supportWidth : width * 0.8f;
		float supportLeft = left + (width - support) * 0.5f;
		SetLayout(new Rect2(supportLeft, enemyYRange.X, support, enemyYRange.Y - enemyYRange.X),
			new Rect2(left, frontlineYRange.X, width, frontlineYRange.Y - frontlineYRange.X),
			new Rect2(supportLeft, playerYRange.X, support, playerYRange.Y - playerYRange.X));
	}

	/// <summary>恢复按自身尺寸自适应的默认三线布局。</summary>
	public void UseAutomaticLayout()
	{
		_explicitLayout = false;
		UpdateAutomaticLayout();
		QueueRedraw();
	}

	/// <summary>切换控制方；即时模式用于载入战斗快照，常规模式保留平滑过渡。</summary>
	public void SetOwner(FrontlineControlSide owner, bool immediate = false)
	{
		_targetColor = owner switch
		{
			FrontlineControlSide.None => _neutral,
			FrontlineControlSide.Player => UIThemeFactory.CyanGold,
			FrontlineControlSide.Enemy => UIThemeFactory.Crimson,
			_ => throw new ArgumentOutOfRangeException(nameof(owner)),
		};
		_owner = owner;
		if (immediate || Engine.IsEditorHint())
		{
			_displayColor = _targetColor;
		}
		QueueRedraw();
	}

	public override void _Ready()
	{
		if (!_explicitLayout)
		{
			UpdateAutomaticLayout();
		}
	}

	public override void _Notification(int what)
	{
		if (what != NotificationResized)
		{
			return;
		}
		if (!_explicitLayout)
		{
			UpdateAutomaticLayout();
		}
		QueueRedraw();
	}

	public override void _Process(double delta)
	{
		if (Engine.IsEditorHint() || !IsVisibleInTree())
		{
			return;
		}
		float elapsed = (float)delta;
		_displayColor = _displayColor.Lerp(_targetColor, 1f - Mathf.Exp(-elapsed * 8f));
		_phase = Mathf.PosMod(_phase + elapsed * 1.7f, Mathf.Tau);
		_redrawElapsed += elapsed;
		if (_redrawElapsed >= 1f / 30f)
		{
			_redrawElapsed = 0;
			QueueRedraw();
		}
	}

	public override void _Draw()
	{
		if (Size.X < 2f || Size.Y < 2f)
		{
			return;
		}
		DrawSupportLane(_enemySupport, UIThemeFactory.Crimson);
		DrawSupportLane(_playerSupport, UIThemeFactory.CyanGold);
		DrawFrontline();
	}

	private void UpdateAutomaticLayout()
	{
		float gap = Size.Y * 0.04f;
		float top = Size.Y * 0.04f;
		float height = Mathf.Max(0, (Size.Y - top * 2f - gap * 2f) / 3f);
		float width = Mathf.Max(0, Size.X * 0.96f);
		float left = (Size.X - width) * 0.5f;
		float supportWidth = width * 0.8f;
		float supportLeft = left + (width - supportWidth) * 0.5f;
		_enemySupport = new Rect2(supportLeft, top, supportWidth, height);
		_frontline = new Rect2(left, top + height + gap, width, height);
		_playerSupport = new Rect2(supportLeft, top + (height + gap) * 2f, supportWidth, height);
	}

	private void DrawSupportLane(Rect2 rect, Color color)
	{
		if (rect.Size.X < 2f || rect.Size.Y < 2f)
		{
			return;
		}
		DrawCutPanel(rect, new Color(UIThemeFactory.SpaceBg, 0.72f), new Color(color, 0.2f), 1f);
		float cellWidth = rect.Size.X / 4f;
		for (int slot = 0; slot < 4; slot++)
		{
			var cell = new Rect2(rect.Position.X + slot * cellWidth + 5f, rect.Position.Y + 5f,
				Mathf.Max(0, cellWidth - 10f), Mathf.Max(0, rect.Size.Y - 10f));
			DrawRails(cell, color, false);
		}
		for (int i = 0; i < 3; i++)
		{
			float y = rect.Position.Y + rect.Size.Y * (0.25f + i * 0.25f);
			DrawLine(new Vector2(rect.Position.X, y), new Vector2(rect.End.X, y), new Color(color, 0.035f), 0.6f);
		}
	}

	private void DrawFrontline()
	{
		Rect2 rect = _frontline;
		if (rect.Size.X < 2f || rect.Size.Y < 2f)
		{
			return;
		}
		float breath = 0.5f + Mathf.Sin(_phase) * 0.5f;
		DrawCutPanel(rect, new Color(_displayColor.Darkened(0.88f), 0.82f), new Color(_displayColor, 0.65f), 1.2f);
		for (int i = 4; i >= 1; i--)
		{
			DrawCutPanel(rect.Grow(-i * 1.8f), Colors.Transparent,
				new Color(_displayColor, 0.018f + breath * 0.014f), i * 2f);
		}
		float cellWidth = rect.Size.X / 5f;
		for (int slot = 0; slot < 5; slot++)
		{
			var cell = new Rect2(rect.Position.X + slot * cellWidth + 6f, rect.Position.Y + 7f,
				Mathf.Max(0, cellWidth - 12f), Mathf.Max(0, rect.Size.Y - 14f));
			DrawRails(cell, _displayColor, true);
			Vector2 center = cell.GetCenter();
			DrawLine(center - Vector2.Right * 8f, center + Vector2.Right * 8f, new Color(_displayColor, 0.12f), 0.7f);
			DrawLine(center - Vector2.Down * 8f, center + Vector2.Down * 8f, new Color(_displayColor, 0.12f), 0.7f);
		}

		// 左/中/右三组战术标尺，中心的长刻度提供独占前线的视觉轴心。
		for (int group = 0; group < 3; group++)
		{
			float centerX = rect.Position.X + rect.Size.X * (0.12f + group * 0.38f);
			for (int tick = -5; tick <= 5; tick++)
			{
				float x = centerX + tick * 4f;
				float length = tick == 0 ? 8f : tick % 2 == 0 ? 5f : 2.5f;
				DrawLine(new Vector2(x, rect.Position.Y + 2f), new Vector2(x, rect.Position.Y + length),
					new Color(_displayColor, 0.48f), 0.8f, true);
				DrawLine(new Vector2(x, rect.End.Y - 2f), new Vector2(x, rect.End.Y - length),
					new Color(_displayColor, 0.48f), 0.8f, true);
			}
		}
		DrawWarningCorners(rect, breath);
	}

	private void DrawRails(Rect2 cell, Color color, bool front)
	{
		float alpha = front ? 0.2f : 0.12f;
		DrawRect(cell, new Color(color, 0.022f));
		DrawLine(cell.Position + new Vector2(3f, 12f), new Vector2(cell.Position.X + 3f, cell.End.Y - 12f), new Color(color, alpha), 0.8f);
		DrawLine(new Vector2(cell.End.X - 3f, cell.Position.Y + 12f), cell.End - new Vector2(3f, 12f), new Color(color, alpha), 0.8f);
		for (int i = 0; i < 4; i++)
		{
			float sx = (i & 1) == 0 ? 1f : -1f;
			float sy = i < 2 ? 1f : -1f;
			Vector2 corner = new(sx > 0 ? cell.Position.X : cell.End.X, sy > 0 ? cell.Position.Y : cell.End.Y);
			DrawLine(corner + new Vector2(sx * 3f, sy * 9f), corner + new Vector2(sx * 3f, sy * 3f),
				new Color(color, alpha * 2f), 1f, true);
			DrawLine(corner + new Vector2(sx * 3f, sy * 3f), corner + new Vector2(sx * 17f, sy * 3f),
				new Color(color, alpha * 2f), 1f, true);
		}
		for (int tick = 1; tick <= 5; tick++)
		{
			float y = cell.Position.Y + tick * cell.Size.Y / 6f;
			DrawLine(new Vector2(cell.Position.X + 3f, y), new Vector2(cell.Position.X + 7f, y), new Color(color, alpha), 0.6f);
			DrawLine(new Vector2(cell.End.X - 3f, y), new Vector2(cell.End.X - 7f, y), new Color(color, alpha), 0.6f);
		}
	}

	private void DrawWarningCorners(Rect2 rect, float breath)
	{
		for (int i = 0; i < 4; i++)
		{
			float sx = (i & 1) == 0 ? 1f : -1f;
			float sy = i < 2 ? 1f : -1f;
			Vector2 corner = new(sx > 0 ? rect.Position.X : rect.End.X, sy > 0 ? rect.Position.Y : rect.End.Y);
			for (int stripe = 0; stripe < 4; stripe++)
			{
				Vector2 start = corner + new Vector2(sx * (13f + stripe * 5f), sy * 4f);
				DrawLine(start, start + new Vector2(sx * 5f, sy * 5f),
					new Color(_displayColor.Lerp(UIThemeFactory.WarmGold, 0.2f), 0.34f + breath * 0.25f), 1.7f, true);
			}
		}
	}

	private void DrawCutPanel(Rect2 rect, Color fill, Color edge, float width)
	{
		if (rect.Size.X <= 1f || rect.Size.Y <= 1f)
		{
			return;
		}
		float cut = Mathf.Min(12f, Mathf.Min(rect.Size.X, rect.Size.Y) * 0.12f);
		Span<Vector2> path = stackalloc Vector2[9];
		path[0] = rect.Position + new Vector2(cut, 0);
		path[1] = new Vector2(rect.End.X - cut, rect.Position.Y);
		path[2] = new Vector2(rect.End.X, rect.Position.Y + cut);
		path[3] = rect.End - new Vector2(0, cut);
		path[4] = rect.End - new Vector2(cut, 0);
		path[5] = new Vector2(rect.Position.X + cut, rect.End.Y);
		path[6] = new Vector2(rect.Position.X, rect.End.Y - cut);
		path[7] = rect.Position + new Vector2(0, cut);
		path[8] = path[0];
		if (fill.A > 0f)
		{
			DrawColoredPolygon(path[..8], fill, ReadOnlySpan<Vector2>.Empty, null);
		}
		DrawPolyline(path, edge, width, true);
	}

	private static void ValidateRect(Rect2 rect, string name)
	{
		if (!rect.Position.IsFinite() || !rect.Size.IsFinite() || rect.Size.X < 0f || rect.Size.Y < 0f)
		{
			throw new ArgumentOutOfRangeException(name);
		}
	}
}
