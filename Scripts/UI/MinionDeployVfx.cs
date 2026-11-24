#nullable enable
using System;
using Godot;
using OdysseyCards.Infrastructure;
using MotionVector = System.Numerics.Vector2;
using RuntimeCard = OdysseyCards.Card.Card;

#pragma warning disable IDE0130 // 命名空间按模块组织，不包含 Scripts 物理目录。
namespace OdysseyCards.UI;
#pragma warning restore IDE0130

/// <summary>只显示飞行卡面。到达槽位中心时先藏起自己，再把画面交给真实槽位。</summary>
public partial class MinionDeployVfx : Control
{
	private CardUI _card = null!;
	private DeployFlight _flight;
	private float _elapsed;
	private float _speed;
	private bool _sampledFirstFrame;
	private Func<Rect2>? _getSlotRect;
	private Action<bool>? _onComplete;

	/// <summary>二次贝塞尔插值，也供一步突击等其他演出使用。</summary>
	public static Vector2 QuadraticBezier(float t, Vector2 start, Vector2 ctrl, Vector2 end)
	{
		return ToGodot(MinionDeployMath.QuadraticBezier(t, ToMotion(start), ToMotion(ctrl), ToMotion(end)));
	}

	/// <summary>计算 CardUI 在目标矩形内完整显示的渲染缩放。</summary>
	public static float CalculateFitRenderScale(Vector2 targetSize)
	{
		return MinionDeployMath.CalculateFitScale(ToMotion(targetSize));
	}

	/// <summary>读取正在显示的真卡姿态；画布基向量同时包含牌面自身和父层的缩放与旋转。</summary>
	internal static DeployPose CapturePose(CardUI view)
	{
		Transform2D transform = view.GetGlobalTransformWithCanvas();
		return new DeployPose(ToMotion(view.ScreenCenter), view.FaceRenderScale * transform.X.Length(),
			Mathf.RadToDeg(transform.Rotation), view.Elevation);
	}

	/// <summary>把屏幕坐标放入部署层；首帧保留来源姿态，末帧重新校准布局后的槽中心。</summary>
	internal static MinionDeployVfx? Play(RuntimeCard source, DeployOriginKind kind, DeployPose? handPose,
		bool playerSide, DeployImpactProfile impact, float speed, Control parent, Func<Rect2> getSlotRect,
		Action<bool> onComplete, CardUI? sourceView)
	{
		if (SceneLifecycleGuard.ShouldSkip(parent))
		{
			return null;
		}
		Rect2 target = getSlotRect();
		Rect2 viewport = parent.GetViewportRect();
		if (!target.HasArea() || !viewport.HasArea())
		{
			return null;
		}

		var vfx = new MinionDeployVfx
		{
			Name = $"Deploy{kind}",
			MouseFilter = MouseFilterEnum.Ignore,
			_speed = speed,
			_getSlotRect = getSlotRect,
			_onComplete = onComplete,
		};
		float fitScale = CalculateFitRenderScale(target.Size);
		float canvasScale = parent.GetGlobalTransformWithCanvas().X.Length();
		float uiScale = (UIScaler.Instance?.CurrentScale ?? 1f) * canvasScale;
		vfx._flight = MinionDeployMath.CreateFlight(kind, handPose, ToMotion(target.GetCenter()), fitScale,
			new DeployViewport(ToMotion(viewport.Position), ToMotion(viewport.Size)), uiScale, playerSide, impact);
		parent.AddChild(vfx, forceReadableName: true);

		// 用原脸的渲染精度建牌（字体/圆角随比例变化），动画才改节点 Scale。
		float renderScale = sourceView?.FaceRenderScale ?? (Mathf.Max(fitScale, vfx._flight.ReadyToStrike.Scale) / canvasScale);
		vfx._card = new CardUI
		{
			Name = "FlyingCard",
			DisplayOnly = true,
			MouseFilter = MouseFilterEnum.Ignore,
			RenderScaleOverride = renderScale,
		};
		vfx._card.SetCard(source);
		if (sourceView is not null)
		{
			vfx._card.CopyDeployFaceState(sourceView);
		}
		vfx.AddChild(vfx._card);
		vfx.ApplyPose(vfx._flight.Launch);
		return vfx;
	}

	public override void _Process(double delta)
	{
		if (SceneLifecycleGuard.ShouldSkip(this) || _onComplete is null)
		{
			return;
		}
		if (SceneLifecycleGuard.ShouldSkip(_card) || _getSlotRect is not { } getSlotRect)
		{
			Complete(touchedDown: false);
			return;
		}
		Rect2 target = getSlotRect();
		if (!target.HasArea())
		{
			Complete(touchedDown: false);
			return;
		}

		// 即使创建于输入处理或延迟队列，至少有一帧完整的接棒姿态。
		if (!_sampledFirstFrame)
		{
			_sampledFirstFrame = true;
			return;
		}
		float seconds = (float)delta * _speed;
		_elapsed += seconds;
		float touchdown = _flight.Timing.Touchdown;
		var contact = new DeployPose(ToMotion(target.GetCenter()), CalculateFitRenderScale(target.Size), 0f, 0f);
		if (_elapsed >= touchdown)
		{
			ApplyPose(contact);
			Complete(touchedDown: true);
			return;
		}

		// 窗口/布局中途变动时只渐进补偿终点偏差，既不动首帧也不跳到过期槽位。
		DeployPose pose = _flight.Sample(_elapsed);
		float progress = _elapsed / touchdown;
		float correction = progress * progress;
		ApplyPose(pose with
		{
			Center = pose.Center + ((contact.Center - _flight.Contact.Center) * correction),
			Scale = pose.Scale + ((contact.Scale - _flight.Contact.Scale) * correction),
		});
		_card.FadeDeployGlow(seconds);
	}

	private void ApplyPose(DeployPose pose)
	{
		Transform2D parentTransform = GetGlobalTransformWithCanvas();
		_card.Rotation = Mathf.DegToRad(pose.AngleDegrees) - parentTransform.Rotation;
		_card.Scale = Vector2.One * (pose.Scale / (_card.FaceRenderScale * parentTransform.X.Length()));
		_card.SetScreenCenter(ToGodot(pose.Center));
		_card.SetElevation(pose.Elevation);
	}

	/// <summary>移除飞行外观而不揭幕；清掉回调，避免失效的动画碰到后来占据同槽的单位。</summary>
	internal void CancelAndFree()
	{
		_onComplete = null;
		_getSlotRect = null;
		if (!IsInstanceValid(this) || IsQueuedForDeletion())
		{
			return;
		}
		Hide();
		// 藏起立即生效；实际释放放到延迟边界，避免同栈帧的嵌套召唤挂新节点。
		_ = CallDeferred(nameof(FreeRetiredFlight));
	}

	private void FreeRetiredFlight()
	{
		if (IsInstanceValid(this) && !IsQueuedForDeletion())
		{
			QueueFree();
		}
	}

	private void Complete(bool touchedDown)
	{
		Action<bool>? complete = _onComplete;
		CancelAndFree();
		complete?.Invoke(touchedDown);
	}

	public override void _ExitTree()
	{
		Action<bool>? complete = _onComplete;
		_onComplete = null;
		_getSlotRect = null;
		complete?.Invoke(false);
	}

	internal static MotionVector ToMotion(Vector2 point)
	{
		return new MotionVector(point.X, point.Y);
	}

	private static Vector2 ToGodot(MotionVector point)
	{
		return new Vector2(point.X, point.Y);
	}
}
