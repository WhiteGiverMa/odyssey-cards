#nullable enable
using System;
using System.Numerics;

#pragma warning disable IDE0130 // 项目命名空间按模块组织，不包含 Scripts 物理目录。
namespace OdysseyCards.UI;
#pragma warning restore IDE0130

/// <summary>以卡面中心为锚的姿态；角度为度，抬升为 0～1，不依赖具体卡面壳。</summary>
public readonly record struct HandPose(Vector2 Center, float Scale, float AngleDegrees, float Elevation);

/// <summary>由静止姿态生成的悬停框。进场用旋转后的牌面；退场有固定的上方迟滞区。</summary>
public readonly record struct HandHitFrame(HandPose Rest, Vector2 HalfSize, float EnterBuffer,
	float ExitHalfWidth, float ExitTop, float Bottom)
{
	public bool ContainsEntry(Vector2 point)
	{
		if (HalfSize.X <= 0f || point.Y >= Bottom)
		{
			return false;
		}

		(float sin, float cos) = MathF.SinCos(Rest.AngleDegrees * (MathF.PI / 180f));
		Vector2 offset = point - Rest.Center;
		float x = (cos * offset.X) + (sin * offset.Y);
		float y = (cos * offset.Y) - (sin * offset.X);
		return MathF.Abs(x) <= HalfSize.X && y >= -HalfSize.Y - EnterBuffer && y < HalfSize.Y;
	}

	public bool ContainsExit(Vector2 point)
	{
		return HalfSize.X > 0f && MathF.Abs(point.X - Rest.Center.X) <= ExitHalfWidth
			&& point.Y >= ExitTop && point.Y < Bottom;
	}
}

/// <summary>桌面微弧的静止站位和聚焦姿态；所有坐标相对于手牌容器。</summary>
public readonly struct HandLayout(int count, float width, float height, float uiScale)
{
	internal const float CardWidth = 140f;
	internal const float CardHeight = 196f;
	public const float CollapsedVisible = 96f;
	internal const float RestScale = 0.85f;
	private const float _focusScale = 1.08f;
	private const float _maxRestAngle = 4f;

	/// <summary>牌数变化只影响同一排的对称间距，宽度不足时压紧间距而不是溢出侧边。</summary>
	public HandPose GetRestPose(int index)
	{
		float middle = (count - 1) * 0.5f;
		float fromMiddle = index - middle;
		float t = middle <= 0f ? 0f : fromMiddle / middle;
		float scaledWidth = CardWidth * uiScale * RestScale;
		float usableSpread = MathF.Max(0f, width - scaledWidth - (64f * uiScale));
		float step = count <= 1 ? 0f : MathF.Min(scaledWidth * 0.81f, usableSpread / (count - 1));
		float y = height + (((CardHeight * RestScale * 0.5f) - CollapsedVisible + (8f * t * t)) * uiScale);
		return new HandPose(new Vector2((width * 0.5f) + (fromMiddle * step), y), RestScale, _maxRestAngle * t, 0f);
	}

	/// <summary>只改变展示目标：聚焦卡拉正、露出整牌，两侧各推 23 设计像素后快速衰减。</summary>
	public HandPose GetFocusPose(int index, int focusedIndex)
	{
		HandPose rest = GetRestPose(index);
		int distance = index - focusedIndex;
		if (focusedIndex < 0)
		{
			return rest;
		}
		if (distance == 0)
		{
			float y = height - (((CardHeight * _focusScale * 0.5f) + 10f) * uiScale);
			return new HandPose(new Vector2(rest.Center.X, y), _focusScale, 0f, 1f);
		}

		float push = Math.Sign(distance) * 23f * uiScale * MathF.Pow(0.32f, Math.Abs(distance) - 1);
		return rest with { Center = rest.Center + new Vector2(push, 0f) };
	}

	/// <summary>进入和离开区域都来自未推开的布局，完全不读取实时动画坐标。</summary>
	public HandHitFrame GetHitFrame(int index)
	{
		HandPose rest = GetRestPose(index);
		Vector2 halfSize = new Vector2(CardWidth, CardHeight) * (rest.Scale * uiScale * 0.5f);
		float exitHalfWidth = ((CardWidth * _focusScale * 0.5f) + 9f) * uiScale;
		float exitTop = height - (((CardHeight * _focusScale) + 24f) * uiScale);
		return new HandHitFrame(rest, halfSize, 9f * uiScale, exitHalfWidth, exitTop, height);
	}
}

/// <summary>与引擎无关的手牌命中和连续运动函数。</summary>
public static class HandMotionMath
{
	public const float DragTiltRate = 26f;
	internal const float LayoutRate = 22f;
	internal const float ReturnRate = 19f;
	internal const float DragPositionRate = 42f;
	internal const float DrawDuration = 0.32f;
	internal const float DrawStagger = 0.055f;
	internal const float DrawArcHeight = 44f;

	/// <summary>由右至左选出正常命中赢家；仅当没有新赢家时尝试保留旧悬停。</summary>
	public static int FindHovered(ReadOnlySpan<HandHitFrame> frames, Vector2 point, int previous, ReadOnlySpan<bool> candidates = default)
	{
		for (int i = frames.Length - 1; i >= 0; i--)
		{
			if ((candidates.IsEmpty || candidates[i]) && frames[i].ContainsEntry(point))
			{
				return i;
			}
		}

		return previous >= 0 && previous < frames.Length && (candidates.IsEmpty || candidates[previous])
			&& frames[previous].ContainsExit(point) ? previous : -1;
	}

	/// <summary>单位时间趋近系数；在目标固定时，任意细分相同时间的结果一致。</summary>
	public static float DampingWeight(float seconds, float rate)
	{
		return 1f - MathF.Exp(-MathF.Max(rate, 0f) * MathF.Max(seconds, 0f));
	}

	public static float Damp(float current, float target, float seconds, float rate)
	{
		return current + ((target - current) * DampingWeight(seconds, rate));
	}

	public static HandPose DampPose(HandPose current, HandPose target, float seconds, float rate)
	{
		float weight = DampingWeight(seconds, rate);
		return new HandPose(Vector2.Lerp(current.Center, target.Center, weight),
			current.Scale + ((target.Scale - current.Scale) * weight),
			current.AngleDegrees + ((target.AngleDegrees - current.AngleDegrees) * weight),
			current.Elevation + ((target.Elevation - current.Elevation) * weight));
	}

	/// <summary>水平速度（设计像素/秒）映射为惯性微倾；停手目标立即回到零。</summary>
	public static float DragTiltTarget(float horizontalVelocity)
	{
		return Math.Clamp(-horizontalVelocity * 0.0075f, -9f, 9f);
	}

	/// <summary>起终点不变的二次弧线；arcHeight 指相对直线中点的最大弓高。</summary>
	public static Vector2 DrawArc(Vector2 start, Vector2 end, float arcHeight, float progress)
	{
		float t = Math.Clamp(progress, 0f, 1f);
		return Vector2.Lerp(start, end, t) - new Vector2(0f, 4f * t * (1f - t) * arcHeight);
	}
}
