using System;
using System.Numerics;
using OdysseyCards.UI;
using Xunit;

namespace OdysseyCards.Tests.Unit;

/// <summary>部署飞行与触桌沉降的纯数学回归，不依赖 Godot 节点。</summary>
public sealed class MinionDeployMathTests
{
	private static readonly DeployViewport _viewport = new(new Vector2(20f, 14f), new Vector2(1600f, 900f));

	[Theory]
	[InlineData(DeployOriginKind.PlayerDrag, 0.22f, 0.28f)]
	[InlineData(DeployOriginKind.PlayerClick, 0.28f, 0.35f)]
	[InlineData(DeployOriginKind.EnemyHand, 0.75f, 0.85f)]
	[InlineData(DeployOriginKind.OffscreenSummon, 0.26f, 0.34f)]
	public void Flight_AllOrigins_ReachExactSlotWithForwardProgress(DeployOriginKind kind, float minSeconds, float maxSeconds)
	{
		bool player = kind != DeployOriginKind.EnemyHand;
		var start = new DeployPose(new Vector2(610f, player ? 720f : 102f), player ? 0.92f : 0.3f, -7f, 0.64f);
		var center = new Vector2(960f, player ? 650f : 294f);
		DeployFlight flight = CreateFlight(kind, kind == DeployOriginKind.OffscreenSummon ? null : start, center, player);
		DeployTiming timing = flight.Timing;

		Assert.InRange(timing.Touchdown, minSeconds, maxSeconds);
		Assert.Equal(flight.Launch, flight.Sample(-1f));
		Assert.Equal(flight.Launch, flight.Sample(0f));
		Assert.Equal(new DeployPose(center, 0.67f, 0f, 0f), flight.Sample(timing.Touchdown));
		Assert.Equal(flight.Contact, flight.Sample(timing.Touchdown + 1f));
		if (kind != DeployOriginKind.OffscreenSummon)
		{
			Assert.Equal(start, flight.Launch);
		}

		float direction = Math.Sign(center.X - flight.Launch.Center.X);
		float previousX = flight.Launch.Center.X;
		for (int i = 1; i <= 80; i++)
		{
			DeployPose sampled = flight.Sample(timing.Touchdown * i / 80f);
			Assert.True((sampled.Center.X - previousX) * direction >= -0.001f);
			Assert.InRange(sampled.Elevation, 0f, 1f);
			Assert.True(sampled.Scale > 0f);
			previousX = sampled.Center.X;
		}
	}

	[Fact]
	public void PlayerDrag_StartsAtReleasedPose_ThenLiftsBeforeAccelerating()
	{
		var start = new DeployPose(new Vector2(728.25f, 650.7f), 0.973f, -6.2f, 0.81f);
		var center = new Vector2(731f, 655f);
		DeployFlight flight = CreateFlight(DeployOriginKind.PlayerDrag, start, center, player: true);
		DeployTiming timing = flight.Timing;

		DeployPose first = flight.Sample(0.00001f);
		Assert.True(Vector2.Distance(first.Center, start.Center) < 0.02f);
		Assert.Equal(start.Scale, first.Scale);
		Assert.Equal(start.AngleDegrees, first.AngleDegrees);
		Assert.True(MathF.Abs(first.Elevation - start.Elevation) < 0.001f);
		DeployPose lifted = flight.Sample(timing.Approach);
		Assert.Equal(start.Center.X, lifted.Center.X);
		Assert.InRange(start.Center.Y - lifted.Center.Y, 4f, 12f);
		Assert.Equal(0f, timing.Hold);
		float firstStep = flight.Sample(timing.Approach + (timing.Strike * 0.25f)).Center.Y - lifted.Center.Y;
		float lastStep = center.Y - flight.Sample(timing.Approach + (timing.Strike * 0.75f)).Center.Y;
		Assert.InRange(firstStep, 0f, lastStep - 0.01f);
	}

	[Fact]
	public void PlayerClick_UsesDirectLowArcWithoutStopping()
	{
		var start = new DeployPose(new Vector2(800f, 820f), 0.75f, 0f, 0.7f);
		var center = new Vector2(620f, 595f);
		DeployFlight flight = CreateFlight(DeployOriginKind.PlayerClick, start, center, player: true);

		Assert.Equal(0f, flight.Timing.HoldEnd);
		Assert.NotEqual(start.Center, flight.Sample(0.01f).Center);
		Vector2 middle = flight.Sample(flight.Timing.Touchdown * 0.5f).Center;
		float pathT = (middle.X - start.Center.X) / (center.X - start.Center.X);
		float arcHeight = Vector2.Lerp(start.Center, center, pathT).Y - middle.Y;
		Assert.InRange(arcHeight, 5f, 28f);
	}

	[Theory]
	[InlineData(1600f, 900f, 1f)]
	[InlineData(420f, 230f, 0.7f)]
	[InlineData(47f, 29f, 1f)]
	public void EnemyHand_HoldsReadableCardInsideViewport(float width, float height, float uiScale)
	{
		var viewport = new DeployViewport(new Vector2(30f, 45f), new Vector2(width, height));
		var start = new DeployPose(viewport.Min - new Vector2(140, 80), 0.29f, 0f, 0.1f);
		DeployFlight flight = MinionDeployMath.CreateFlight(DeployOriginKind.EnemyHand, start,
			viewport.Min + new Vector2(width - 1f, height - 1f), 0.67f, viewport, uiScale, false, DeployImpactProfile.Standard);
		DeployTiming timing = flight.Timing;
		DeployPose held = flight.Sample(timing.Approach);
		Vector2 halfSize = MinionDeployMath.CardDesignSize * (held.Scale * 0.5f);
		Vector2 lastPixel = viewport.Min + viewport.Size;

		Assert.Equal(held, flight.Sample(timing.Approach + (timing.Hold * 0.5f)));
		Assert.Equal(held, flight.Sample(timing.HoldEnd));
		Assert.InRange(timing.Hold, 0.38f, 0.45f);
		Assert.InRange(held.Center.X, viewport.Min.X + halfSize.X - 0.001f, lastPixel.X - halfSize.X + 0.001f);
		Assert.InRange(held.Center.Y, viewport.Min.Y + halfSize.Y - 0.001f, lastPixel.Y - halfSize.Y + 0.001f);
		if (height >= 230f)
		{
			Assert.True(held.Scale > start.Scale);
		}
		Assert.NotEqual(held, flight.Sample(timing.HoldEnd + (timing.Strike * 0.25f)));
		float previousY = held.Center.Y;
		for (int i = 1; i <= 50; i++)
		{
			float y = flight.Sample(timing.HoldEnd + (timing.Strike * i / 50f)).Center.Y;
			Assert.InRange(y, previousY - 0.001f, flight.Contact.Center.Y + 0.001f);
			previousY = y;
		}
	}

	[Theory]
	[InlineData(true, 800f, 730f, 0, 1)]
	[InlineData(false, 800f, 190f, 0, -1)]
	[InlineData(true, 110f, 630f, -1, 0)]
	[InlineData(false, 1540f, 350f, 1, 0)]
	public void OffscreenSummon_PicksNearbySideFacingEdge(bool player, float x, float y, int edgeX, int edgeY)
	{
		var center = new Vector2(x, y);
		DeployFlight flight = CreateFlight(DeployOriginKind.OffscreenSummon, null, center, player);
		Vector2 start = flight.Launch.Center;
		Vector2 max = _viewport.Min + _viewport.Size;
		float radius = MinionDeployMath.CardDesignSize.Length() * flight.Launch.Scale * 0.5f;

		Assert.Equal(edgeX, start.X + radius < _viewport.Min.X ? -1 : start.X - radius > max.X ? 1 : 0);
		Assert.Equal(edgeY, start.Y + radius < _viewport.Min.Y ? -1 : start.Y - radius > max.Y ? 1 : 0);
		Assert.True(Vector2.Distance(flight.Sample(0.1f).Center, center) < Vector2.Distance(start, center));
	}

	[Fact]
	public void Settlement_ReturnsFromSingleCompression_ToExactRest()
	{
		DeployImpactProfile impact = DeployImpactProfile.Standard;
		DeploySettlement previous = MinionDeployMath.SampleSettlement(0f, impact);
		Assert.Equal(impact.Sink, previous.Offset.Y);
		Assert.Equal(1f - impact.Squash, previous.Scale.Y);
		Assert.InRange(previous.Offset.Y, 3f, 4f);

		for (int i = 1; i <= 100; i++)
		{
			DeploySettlement pose = MinionDeployMath.SampleSettlement(MinionDeployMath.SettlementDuration * i / 100f, impact);
			Assert.InRange(pose.Offset.X, 0f, impact.Shake + 0.001f);
			Assert.InRange(pose.Offset.Y, 0f, previous.Offset.Y);
			Assert.InRange(pose.Scale.Y, previous.Scale.Y, 1f);
			Assert.InRange(pose.Scale.X, 1f, previous.Scale.X);
			previous = pose;
		}
		Assert.Equal(new DeploySettlement(Vector2.Zero, Vector2.One), previous);
		Assert.Equal(previous, MinionDeployMath.SampleSettlement(1f, impact));
	}

	private static DeployFlight CreateFlight(DeployOriginKind kind, DeployPose? start, Vector2 target, bool player)
	{
		return MinionDeployMath.CreateFlight(kind, start, target, 0.67f, _viewport, 1f, player, DeployImpactProfile.Standard);
	}
}
