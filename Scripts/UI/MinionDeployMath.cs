using System;
using System.Numerics;
using OdysseyCards.Audio;

#pragma warning disable IDE0130 // 命名空间按模块组织，不包含 Scripts 物理目录。
namespace OdysseyCards.UI;
#pragma warning restore IDE0130

/// <summary>部署的视觉来源，不参与战斗规则。</summary>
public enum DeployOriginKind
{
	PlayerDrag,
	PlayerClick,
	EnemyHand,
	OffscreenSummon,
}

/// <summary>拍桌力度；位移以设计像素计，压缩和光尘为无量纲强度。</summary>
internal readonly record struct DeployImpactProfile(float Lift, float StrikeDepth, float Sink, float Squash,
	float Shake, float RingStrength, float DustStrength, float SfxIntensity = ProceduralSfxMath.StandardImpact)
{
	public static DeployImpactProfile Standard { get; } = new(8f, 14f, 3.8f, 0.06f, 1.7f, 0.84f, 0.76f);
}

/// <summary>屏幕中心姿态；Scale 指设计尺寸到屏幕尺寸的倍率，不是某个节点的局部 Scale。</summary>
internal readonly record struct DeployPose(Vector2 Center, float Scale, float AngleDegrees, float Elevation);

/// <summary>逻辑视口的左上角和尺寸，所有路径输入输出使用同一坐标系。</summary>
internal readonly record struct DeployViewport(Vector2 Min, Vector2 Size);

/// <summary>以基础秒数计的飞行分段；快慢只由播放层缩放时间。</summary>
internal readonly record struct DeployTiming(float Approach, float Hold, float Strike)
{
	public float HoldEnd => Approach + Hold;
	public float Touchdown => HoldEnd + Strike;
}

/// <summary>触桌后的局部表现变换，不移动槽位锚点和命中区。</summary>
internal readonly record struct DeploySettlement(Vector2 Offset, Vector2 Scale);

/// <summary>一条从来源到槽中心的连续轨迹。临时卡面只采样到触桌为止。</summary>
internal readonly record struct DeployFlight(DeployOriginKind Kind, DeployTiming Timing, DeployPose Launch,
	DeployPose ReadyToStrike, DeployPose Contact, Vector2 FirstControl, Vector2 SecondControl)
{
	public DeployPose Sample(float seconds)
	{
		if (seconds <= 0f)
		{
			return Launch;
		}
		if (seconds >= Timing.Touchdown)
		{
			return Contact;
		}
		if (seconds < Timing.Approach)
		{
			float approach = MinionDeployMath.EaseOutCubic(seconds / Timing.Approach);
			return Interpolate(Launch, ReadyToStrike, approach);
		}
		if (seconds <= Timing.HoldEnd)
		{
			return ReadyToStrike;
		}

		float t = (seconds - Timing.HoldEnd) / Timing.Strike;
		float strike = Kind switch
		{
			DeployOriginKind.PlayerDrag or DeployOriginKind.EnemyHand => t * t,
			DeployOriginKind.PlayerClick => t * (0.32f + (0.68f * t)),
			DeployOriginKind.OffscreenSummon => t * (0.65f + (0.35f * t)),
			_ => throw new InvalidOperationException("未知部署来源。"),
		};
		Vector2 center = Kind == DeployOriginKind.PlayerDrag
			? Vector2.Lerp(ReadyToStrike.Center, Contact.Center, strike)
			: MinionDeployMath.CubicBezier(strike, ReadyToStrike.Center, FirstControl, SecondControl, Contact.Center);
		return Interpolate(ReadyToStrike, Contact, strike) with { Center = center };
	}

	private static DeployPose Interpolate(DeployPose from, DeployPose to, float weight)
	{
		return new DeployPose(Vector2.Lerp(from.Center, to.Center, weight),
			float.Lerp(from.Scale, to.Scale, weight), float.Lerp(from.AngleDegrees, to.AngleDegrees, weight),
			float.Lerp(from.Elevation, to.Elevation, weight));
	}
}

/// <summary>与 Godot 无关的部署路径、视口安全区与沉降曲线。</summary>
internal static class MinionDeployMath
{
	public const float SettlementDuration = 0.11f;
	public const float ImpactDuration = 0.26f;
	private const float _readableScale = 0.90f;
	private static readonly DeployTiming _enemyTiming = new(0.20f, 0.41f, 0.18f);

	public static Vector2 CardDesignSize => new(HandLayout.CardWidth, HandLayout.CardHeight);
	public static float EnemyStepDelay => _enemyTiming.Touchdown + SettlementDuration + 0.02f;

	public static DeployTiming TimingFor(DeployOriginKind kind)
	{
		return kind switch
		{
			DeployOriginKind.PlayerDrag => new DeployTiming(0.05f, 0f, 0.19f),
			DeployOriginKind.PlayerClick => new DeployTiming(0f, 0f, 0.31f),
			DeployOriginKind.EnemyHand => _enemyTiming,
			DeployOriginKind.OffscreenSummon => new DeployTiming(0f, 0f, 0.30f),
			_ => throw new ArgumentOutOfRangeException(nameof(kind)),
		};
	}

	/// <summary>已知的手牌姿态原样起飞；只有无手牌来源才从对应阵营的场外解析起点。</summary>
	public static DeployFlight CreateFlight(DeployOriginKind kind, DeployPose? handPose, Vector2 slotCenter,
		float slotScale, DeployViewport viewport, float uiScale, bool playerSide, DeployImpactProfile impact)
	{
		var contact = new DeployPose(slotCenter, slotScale, 0f, 0f);
		DeployPose launch = kind == DeployOriginKind.OffscreenSummon
			? ResolveOffscreenLaunch(slotCenter, slotScale, viewport, uiScale, playerSide)
			: handPose ?? DefaultHandPose(kind, viewport, uiScale);
		DeployTiming timing = TimingFor(kind);
		DeployPose ready = kind switch
		{
			DeployOriginKind.PlayerDrag => launch with
			{
				Center = launch.Center - new Vector2(0f, impact.Lift * uiScale),
				Elevation = MathF.Max(launch.Elevation, 0.9f),
			},
			DeployOriginKind.EnemyHand => ResolveEnemyHover(launch, contact, viewport, uiScale),
			DeployOriginKind.PlayerClick or DeployOriginKind.OffscreenSummon => launch,
			_ => throw new ArgumentOutOfRangeException(nameof(kind)),
		};

		// 横向控制点始终在起终点之间。缩短尾段横移，最后几帧更像向下压，而非横着滑过槽位。
		Vector2 first = Vector2.Lerp(ready.Center, slotCenter, 0.30f) - new Vector2(0f, 2f * impact.Lift * uiScale);
		Vector2 second = new(float.Lerp(ready.Center.X, slotCenter.X, 0.93f), slotCenter.Y - (impact.StrikeDepth * uiScale));
		if (kind == DeployOriginKind.EnemyHand && ready.Center.Y < slotCenter.Y)
		{
			// 亮牌之后只能继续下压，不先反向抬头；狭窄视口也不跨过起点。
			second.Y = MathF.Max(second.Y, ready.Center.Y);
			first.Y = float.Lerp(ready.Center.Y, second.Y, 0.25f);
		}
		return new DeployFlight(kind, timing, launch, ready, contact, first, second);
	}

	private static DeployPose DefaultHandPose(DeployOriginKind kind, DeployViewport viewport, float uiScale)
	{
		bool enemy = kind == DeployOriginKind.EnemyHand;
		Vector2 center = viewport.Min + (viewport.Size * new Vector2(0.5f, enemy ? 0.05f : 0.88f));
		return new DeployPose(center, (enemy ? 0.36f : 0.80f) * uiScale, 0f, enemy ? 0.20f : 0.60f);
	}

	private static DeployPose ResolveEnemyHover(DeployPose launch, DeployPose contact, DeployViewport viewport, float uiScale)
	{
		float margin = MathF.Min(16f * uiScale, MathF.Min(viewport.Size.X, viewport.Size.Y) * 0.1f);
		Vector2 safeSize = viewport.Size - new Vector2(2f * margin);
		float maxScale = CalculateFitScale(safeSize);
		float readable = MathF.Min(MathF.Max(_readableScale * uiScale, contact.Scale), maxScale);
		var halfExtent = Vector2.Min((CardDesignSize * (readable * 0.5f)) + new Vector2(margin), viewport.Size * 0.5f);
		Vector2 center = new(float.Lerp(launch.Center.X, contact.Center.X, 0.43f),
			MathF.Min(float.Lerp(launch.Center.Y, contact.Center.Y, 0.54f), contact.Center.Y - (52f * uiScale)));
		center = Vector2.Clamp(center, viewport.Min + halfExtent, viewport.Min + viewport.Size - halfExtent);
		return new DeployPose(center, readable, 0f, 0.86f);
	}

	private static DeployPose ResolveOffscreenLaunch(Vector2 target, float slotScale, DeployViewport viewport,
		float uiScale, bool playerSide)
	{
		float scale = MathF.Max(0.73f * uiScale, slotScale * 0.9f);
		// 用旋转后也能保证不露边的半径，把整张牌置于视口外。
		float clearance = (CardDesignSize.Length() * scale * 0.5f) + (12f * uiScale);
		Vector2 max = viewport.Min + viewport.Size;
		float left = target.X - viewport.Min.X;
		float right = max.X - target.X;
		float vertical = playerSide ? max.Y - target.Y : target.Y - viewport.Min.Y;
		bool nearerLeft = left <= right;
		float lean = nearerLeft ? -1f : 1f;
		Vector2 center;
		if (vertical <= MathF.Min(left, right))
		{
			float drift = MathF.Min(viewport.Size.X * 0.16f, 106f * uiScale);
			center = new Vector2(target.X + (lean * drift), playerSide ? max.Y + clearance : viewport.Min.Y - clearance);
		}
		else
		{
			float y = Math.Clamp(target.Y + ((playerSide ? 64f : -64f) * uiScale), viewport.Min.Y, max.Y);
			center = new Vector2(nearerLeft ? viewport.Min.X - clearance : max.X + clearance, y);
		}
		return new DeployPose(center, scale, lean * 8f, 0.84f);
	}

	/// <summary>碰桌时达到最大压缩，随后单调回正；横向微震仅一次短促侧移。</summary>
	public static DeploySettlement SampleSettlement(float seconds, DeployImpactProfile impact)
	{
		float t = Math.Clamp(seconds / SettlementDuration, 0f, 1f);
		float remaining = 1f - t;
		float recover = remaining * remaining * remaining;
		float shake = 4f * t * remaining;
		return new DeploySettlement(new Vector2(impact.Shake * shake * shake, impact.Sink * recover),
			new Vector2(1f + (impact.Squash * 0.5f * recover), 1f - (impact.Squash * recover)));
	}

	public static float EaseOutCubic(float t)
	{
		float u = 1f - t;
		return 1f - (u * u * u);
	}

	public static float CalculateFitScale(Vector2 targetSize)
	{
		return targetSize.X <= 0f || targetSize.Y <= 0f
			? 1f : MathF.Min(targetSize.X / CardDesignSize.X, targetSize.Y / CardDesignSize.Y);
	}

	public static Vector2 QuadraticBezier(float t, Vector2 start, Vector2 control, Vector2 end)
	{
		float u = 1f - t;
		return (u * u * start) + (2f * u * t * control) + (t * t * end);
	}

	public static Vector2 CubicBezier(float t, Vector2 start, Vector2 first, Vector2 second, Vector2 end)
	{
		return Vector2.Lerp(QuadraticBezier(t, start, first, second), QuadraticBezier(t, first, second, end), t);
	}
}
