using System;
using System.Numerics;
using OdysseyCards.UI;
using Xunit;

namespace OdysseyCards.Tests.Unit;

/// <summary>手牌基线、悬停命中及运动均可在 Godot 场景之外验证。</summary>
public sealed class HandMotionMathTests
{
	private static readonly Vector2 _cardSize = new(140f, 196f);

	[Fact]
	public void GetRestPose_SingleCard_IsCenteredAndFlat()
	{
		var layout = new HandLayout(1, 900f, 110f, 1f);
		HandPose pose = layout.GetRestPose(0);

		Assert.Equal(450f, pose.Center.X);
		Assert.Equal(0f, pose.AngleDegrees);
		Assert.Equal(0.85f, pose.Scale);
		Assert.Equal(110f - HandLayout.CollapsedVisible, pose.Center.Y - _cardSize.Y * pose.Scale / 2f, 4);
	}

	[Theory]
	[InlineData(2)]
	[InlineData(5)]
	[InlineData(9)]
	[InlineData(26)]
	public void GetRestPose_Edges_StaySymmetricAndWithinTiltAndWidth(int count)
	{
		var layout = new HandLayout(count, 700f, 110f, 1f);
		HandPose left = layout.GetRestPose(0);
		HandPose right = layout.GetRestPose(count - 1);

		Assert.Equal(700f, left.Center.X + right.Center.X, 4);
		Assert.Equal(left.Center.Y, right.Center.Y, 4);
		Assert.InRange(right.AngleDegrees, 3f, 5f);
		Assert.Equal(-right.AngleDegrees, left.AngleDegrees);
		Assert.True(left.Center.Y > layout.GetRestPose(count / 2).Center.Y || count == 2);
		Assert.True(left.Center.X - _cardSize.X * left.Scale / 2f >= 0f);
		Assert.True(right.Center.X + _cardSize.X * right.Scale / 2f <= 700f);
	}

	[Fact]
	public void GetFocusPose_Hover_UprightsTheCardAndPushesNeighborsSymmetrically()
	{
		var layout = new HandLayout(5, 900f, 110f, 1f);
		HandPose hovered = layout.GetFocusPose(2, 2);
		HandPose baseline = layout.GetRestPose(2);

		Assert.Equal(0f, layout.GetFocusPose(0, 0).AngleDegrees);
		Assert.Equal(baseline.Center.X, hovered.Center.X);
		Assert.True(hovered.Scale > baseline.Scale);
		Assert.True(hovered.Elevation > baseline.Elevation);
		Assert.True(hovered.Center.Y + (_cardSize.Y * hovered.Scale / 2f) < 110f);
		float leftPush = layout.GetRestPose(1).Center.X - layout.GetFocusPose(1, 2).Center.X;
		float rightPush = layout.GetFocusPose(3, 2).Center.X - layout.GetRestPose(3).Center.X;
		float outerPush = layout.GetRestPose(0).Center.X - layout.GetFocusPose(0, 2).Center.X;
		Assert.Equal(leftPush, rightPush, 4);
		Assert.InRange(leftPush, 10f, 30f);
		Assert.InRange(outerPush, 0.01f, leftPush - 0.01f);
	}

	[Fact]
	public void GetHitFrame_FocusDoesNotMoveTheEntryFrame()
	{
		var layout = new HandLayout(3, 900f, 110f, 1f);
		HandHitFrame before = layout.GetHitFrame(1);
		HandPose preview = layout.GetFocusPose(1, 1);
		HandPose pushedNeighbor = layout.GetFocusPose(2, 1);

		Assert.Equal(before, layout.GetHitFrame(1));
		Assert.NotEqual(preview.Center, before.Rest.Center);
		Assert.NotEqual(layout.GetHitFrame(2).Rest.Center, pushedNeighbor.Center);
		Assert.True(before.ContainsEntry(before.Rest.Center));
		Vector2 previewOnly = preview.Center - new Vector2(0f, 10f);
		Assert.False(before.ContainsEntry(previewOnly));
		Assert.True(before.ContainsExit(previewOnly));
		Assert.False(before.ContainsExit(new Vector2(before.Rest.Center.X, 111f)));
	}

	[Fact]
	public void FindHovered_OverlapPrefersRightmostAndDoesNotLetHysteresisStealIt()
	{
		var layout = new HandLayout(3, 900f, 110f, 1f);
		HandHitFrame[] frames = [layout.GetHitFrame(0), layout.GetHitFrame(1), layout.GetHitFrame(2)];
		var overlap = new Vector2((frames[0].Rest.Center.X + frames[1].Rest.Center.X) / 2f, 45f);
		Assert.True(frames[0].ContainsEntry(overlap));
		Assert.True(frames[1].ContainsEntry(overlap));
		Assert.Equal(1, HandMotionMath.FindHovered(frames, overlap, previous: 0));

		Assert.Equal(0, HandMotionMath.FindHovered(frames, overlap, previous: 1, [true, false, true]));
	}

	[Fact]
	public void FindHovered_ExitCorridor_RetainsOnlyExistingHover()
	{
		var layout = new HandLayout(1, 900f, 110f, 1f);
		HandHitFrame[] frames = [layout.GetHitFrame(0)];
		Vector2 point = layout.GetFocusPose(0, 0).Center;

		Assert.Equal(-1, HandMotionMath.FindHovered(frames, point, previous: -1));
		Assert.Equal(0, HandMotionMath.FindHovered(frames, point, previous: 0));
		Assert.Equal(-1, HandMotionMath.FindHovered(frames, point - new Vector2(100f, 0f), previous: 0));
	}

	[Fact]
	public void DampPose_SubdividingTime_PreservesConvergenceForAllPoseChannels()
	{
		HandPose start = new(new Vector2(100f, 300f), 0.68f, -7f, 0.4f);
		HandPose target = new(new Vector2(300f, -40f), 1.15f, 4f, 1f);
		HandPose single = HandMotionMath.DampPose(start, target, 0.1f, 22f);
		HandPose steps = start;
		for (int i = 0; i < 12; i++)
		{
			steps = HandMotionMath.DampPose(steps, target, 0.1f / 12f, 22f);
		}

		Assert.InRange(Vector2.Distance(single.Center, steps.Center), 0f, 0.001f);
		Assert.Equal(single.Scale, steps.Scale, 5);
		Assert.Equal(single.AngleDegrees, steps.AngleDegrees, 5);
		Assert.Equal(single.Elevation, steps.Elevation, 5);
		Assert.True(Vector2.Distance(single.Center, target.Center) < Vector2.Distance(start.Center, target.Center) * 0.12f);
		Assert.Equal(start, HandMotionMath.DampPose(start, target, 0f, 22f));
	}

	[Theory]
	[InlineData(-4000f, 9f)]
	[InlineData(-600f, 4.5f)]
	[InlineData(0f, 0f)]
	[InlineData(600f, -4.5f)]
	[InlineData(4000f, -9f)]
	public void DragTiltTarget_HorizontalVelocity_ClampsAndSettlesWhenStopped(float velocity, float expected)
	{
		float angle = HandMotionMath.DragTiltTarget(velocity);
		Assert.Equal(expected, angle, 4);
		float stopped = HandMotionMath.Damp(angle, HandMotionMath.DragTiltTarget(0f), 0.16f, HandMotionMath.DragTiltRate);
		Assert.InRange(MathF.Abs(stopped), 0f, 0.2f);
	}

	[Fact]
	public void DrawArc_StartAndEndMatch_AndFlightRisesAboveTheChord()
	{
		Vector2 origin = new(120f, -103f);
		Vector2 destination = new(580f, 82f);

		Assert.Equal(origin, HandMotionMath.DrawArc(origin, destination, 46f, 0f));
		Assert.Equal(destination, HandMotionMath.DrawArc(origin, destination, 46f, 1f));
		Assert.Equal(origin, HandMotionMath.DrawArc(origin, destination, 46f, -1f));
		Assert.Equal(destination, HandMotionMath.DrawArc(origin, destination, 46f, 2f));
		Assert.Equal(Vector2.Lerp(origin, destination, 0.5f) - new Vector2(0f, 46f),
			HandMotionMath.DrawArc(origin, destination, 46f, 0.5f));
	}
}
