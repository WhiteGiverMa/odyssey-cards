#nullable enable
using System;
using Godot;

namespace OdysseyCards.UI;

/// <summary>
/// 一步突击的两种随机演出：停稳射击与行进间射击。
/// 只创建临时 CardUI 替身和矢量弹道，不操作战斗数据；所有 Tween 回调均检查节点有效性。
/// </summary>
public partial class AssaultAdvanceVfx : Control
{
	private const float FireOnMoveThreshold = 0.58f;

	private CardUI? _card;
	private MuzzleFlash? _muzzle;
	private Tween? _tween;
	private Vector2 _renderedCardSize;
	private Vector2 _originCenter;
	private Vector2 _targetCenter;
	private Vector2 _shotTarget;
	private bool _fireOnMove;
	private bool _shotFired;
	private bool _finished;
	private Action? _onFinished;

	/// <summary>50/50 彩蛋判定；独立成静态纯函数以便单元测试锁定边界。</summary>
	internal static bool IsFireOnMove(int roll)
	{
		return roll is >= 0 and < 50;
	}

	/// <summary>
	/// 播放突击演出。parent 必须位于场景树中；失败时也执行 onFinished，避免调用方残留抑制状态。
	/// </summary>
	public static AssaultAdvanceVfx? Play(
		OdysseyCards.Card.Card card,
		Rect2 originRect,
		Rect2 targetRect,
		Vector2 shotTarget,
		bool fireOnMove,
		Node parent,
		Action? onFinished = null)
	{
		if (card == null
			|| originRect.Size.X <= 1f || originRect.Size.Y <= 1f
			|| targetRect.Size.X <= 1f || targetRect.Size.Y <= 1f
			|| parent == null || !GodotObject.IsInstanceValid(parent) || !parent.IsInsideTree())
		{
			onFinished?.Invoke();
			return null;
		}

		var vfx = new AssaultAdvanceVfx();
		parent.AddChild(vfx);
		vfx.Initialize(card, originRect, targetRect, shotTarget, fireOnMove, onFinished);
		return vfx;
	}

	/// <summary>伤害事件到达后校正真实弹着点；停稳阶段会使用最新目标。</summary>
	public void SetShotTarget(Vector2 target)
	{
		if (!GodotObject.IsInstanceValid(this) || target == Vector2.Zero)
			return;
		_shotTarget = target;
	}

	private void Initialize(
		OdysseyCards.Card.Card card,
		Rect2 originRect,
		Rect2 targetRect,
		Vector2 shotTarget,
		bool fireOnMove,
		Action? onFinished)
	{
		Name = "AssaultAdvanceVfx";
		MouseFilter = MouseFilterEnum.Ignore;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		_fireOnMove = fireOnMove;
		_shotTarget = shotTarget;
		_onFinished = onFinished;
		_originCenter = originRect.Position + originRect.Size * 0.5f;
		_targetCenter = targetRect.Position + targetRect.Size * 0.5f;

		float renderScale = MinionDeployVfx.CalculateFitRenderScale(targetRect.Size);
		_renderedCardSize = new Vector2(CardUI.DESIGN_WIDTH, CardUI.DESIGN_HEIGHT) * renderScale;
		_card = new CardUI
		{
			DisplayOnly = true,
			MouseFilter = MouseFilterEnum.Ignore,
			RenderScaleOverride = renderScale,
			Position = CenterToTopLeft(_originCenter),
			Scale = new Vector2(0.9f, 0.9f),
		};
		_card.SetCard(card);
		AddChild(_card);

		_muzzle = new MuzzleFlash
		{
			MouseFilter = MouseFilterEnum.Ignore,
			Visible = false,
		};
		AddChild(_muzzle);

		_tween = CreateTween();
		if (_fireOnMove)
			BuildFireOnMoveSequence();
		else
			BuildStationarySequence();
		_tween.Finished += Finish;
	}

	private void BuildStationarySequence()
	{
		if (_tween == null)
			return;
		_tween.TweenMethod(Callable.From<float>(SetStationaryProgress), 0f, 1f, 0.24f)
			.SetEase(Tween.EaseType.Out)
			.SetTrans(Tween.TransitionType.Cubic);
		_tween.TweenInterval(0.12f);
		_tween.TweenCallback(Callable.From(Fire));
		_tween.TweenInterval(0.08f);
		_tween.TweenMethod(Callable.From<float>(SetFadeProgress), 1f, 0f, 0.18f)
			.SetEase(Tween.EaseType.In);
		_tween.TweenCallback(Callable.From(Finish));
	}

	private void BuildFireOnMoveSequence()
	{
		if (_tween == null)
			return;
		_tween.TweenMethod(Callable.From<float>(SetGlideProgress), 0f, 1f, 0.34f)
			.SetEase(Tween.EaseType.In)
			.SetTrans(Tween.TransitionType.Quad);
		_tween.TweenProperty(_card, "position", CenterToTopLeft(_targetCenter) + Vector2.Up * 7f, 0.08f)
			.SetEase(Tween.EaseType.Out);
		_tween.TweenProperty(_card, "position", CenterToTopLeft(_targetCenter), 0.12f)
			.SetEase(Tween.EaseType.Out)
			.SetTrans(Tween.TransitionType.Back);
		_tween.TweenInterval(0.08f);
		_tween.TweenMethod(Callable.From<float>(SetFadeProgress), 1f, 0f, 0.16f)
			.SetEase(Tween.EaseType.In);
		_tween.TweenCallback(Callable.From(Finish));
	}

	private void SetStationaryProgress(float progress)
	{
		if (!GodotObject.IsInstanceValid(this) || !GodotObject.IsInstanceValid(_card))
			return;
		Vector2 control = (_originCenter + _targetCenter) * 0.5f + Vector2.Up * 32f;
		Vector2 center = MinionDeployVfx.QuadraticBezier(progress, _originCenter, control, _targetCenter);
		_card.Position = CenterToTopLeft(center);
		_card.Scale = Vector2.One * Mathf.Lerp(0.9f, 1f, progress);
	}

	private void SetGlideProgress(float progress)
	{
		if (!GodotObject.IsInstanceValid(this) || !GodotObject.IsInstanceValid(_card))
			return;
		Vector2 center = _originCenter.Lerp(_targetCenter, progress) + Vector2.Up * Mathf.Sin(progress * Mathf.Pi) * 18f;
		_card.Position = CenterToTopLeft(center);
		_card.Scale = Vector2.One * Mathf.Lerp(0.86f, 1.04f, progress);
		if (!_shotFired && progress >= FireOnMoveThreshold)
			Fire();
	}

	private void SetFadeProgress(float alpha)
	{
		if (!GodotObject.IsInstanceValid(this) || !GodotObject.IsInstanceValid(_card))
			return;
		_card.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(alpha, 0f, 1f));
	}

	private void Fire()
	{
		if (_shotFired || !GodotObject.IsInstanceValid(this))
			return;
		_shotFired = true;
		if (!GodotObject.IsInstanceValid(_card) || !GodotObject.IsInstanceValid(_muzzle))
			return;
		Vector2 muzzlePoint = _card.Position + _renderedCardSize * new Vector2(0.78f, 0.22f);
		_muzzle.Flash(muzzlePoint);
		var tracer = new AssaultTracerVfx { MouseFilter = MouseFilterEnum.Ignore };
		AddChild(tracer);
		tracer.Play(muzzlePoint, _shotTarget);
	}

	private void Finish()
	{
		if (_finished || !GodotObject.IsInstanceValid(this))
			return;
		_finished = true;
		if (_tween != null && _tween.IsValid())
			_tween.Kill();
		_tween = null;
		if (GodotObject.IsInstanceValid(_card))
			_card.QueueFree();
		if (GodotObject.IsInstanceValid(_muzzle))
			_muzzle.QueueFree();
		_onFinished?.Invoke();
		_onFinished = null;
		QueueFree();
	}

	private Vector2 CenterToTopLeft(Vector2 center) => center - _renderedCardSize * 0.5f;

	public override void _ExitTree()
	{
		if (_tween != null && _tween.IsValid())
			_tween.Kill();
		_tween = null;
		_onFinished?.Invoke();
		_onFinished = null;
	}

	/// <summary>炮口闪光：短促十字星与扩散圆环。</summary>
	private partial class MuzzleFlash : Control
	{
		private Vector2 _position;
		private float _progress;
		private Tween? _tween;

		public void Flash(Vector2 position)
		{
			if (!GodotObject.IsInstanceValid(this))
				return;
			_position = position;
			_progress = 0f;
			Show();
			_tween?.Kill();
			_tween = CreateTween();
			_tween.TweenMethod(Callable.From<float>(SetProgress), 0f, 1f, 0.16f)
				.SetEase(Tween.EaseType.Out);
			_tween.TweenCallback(Callable.From(Hide));
		}

		private void SetProgress(float progress)
		{
			if (!GodotObject.IsInstanceValid(this))
				return;
			_progress = progress;
			QueueRedraw();
		}

		public override void _Draw()
		{
			if (_progress <= 0f || _progress >= 1f)
				return;
			float alpha = 1f - _progress;
			float radius = Mathf.Lerp(4f, 18f, _progress);
			DrawCircle(_position, radius * 0.45f, new Color(1f, 0.92f, 0.55f, alpha * 0.75f));
			DrawArc(_position, radius, 0f, Mathf.Tau, 20, new Color(1f, 0.65f, 0.2f, alpha), 2f, true);
			for (int i = 0; i < 4; i++)
			{
				Vector2 direction = Vector2.FromAngle(i * Mathf.Pi * 0.5f);
				DrawLine(_position + direction * radius * 0.3f, _position + direction * radius,
					new Color(1f, 0.95f, 0.65f, alpha), 1.5f, true);
			}
		}

		public override void _ExitTree()
		{
			_tween?.Kill();
			_tween = null;
		}
	}

	/// <summary>高速曳光弹：短线尾迹 + 高亮弹头，命中端由普通伤害闪光收尾。</summary>
	private partial class AssaultTracerVfx : Control
	{
		private Vector2 _from;
		private Vector2 _to;
		private float _progress;
		private Tween? _tween;

		public void Play(Vector2 from, Vector2 to)
		{
			if (!GodotObject.IsInstanceValid(this) || from.DistanceSquaredTo(to) < 1f)
			{
				QueueFree();
				return;
			}
			SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			_from = from;
			_to = to;
			_tween = CreateTween();
			_tween.TweenMethod(Callable.From<float>(SetProgress), 0f, 1f, 0.14f)
				.SetEase(Tween.EaseType.Out)
				.SetTrans(Tween.TransitionType.Quad);
			_tween.Finished += QueueFree;
		}

		private void SetProgress(float progress)
		{
			if (!GodotObject.IsInstanceValid(this))
				return;
			_progress = progress;
			QueueRedraw();
		}

		public override void _Draw()
		{
			if (_progress <= 0f)
				return;
			Vector2 head = _from.Lerp(_to, Mathf.Clamp(_progress, 0f, 1f));
			Vector2 direction = (_to - _from).Normalized();
			float length = Mathf.Lerp(18f, 92f, _progress);
			Vector2 tail = head - direction * length;
			DrawLine(tail, head, new Color(1f, 0.72f, 0.22f, 0.75f), 4f, true);
			DrawLine(tail, head, new Color(1f, 0.98f, 0.82f, 0.95f), 1.4f, true);
			DrawCircle(head, 4.5f, new Color(1f, 0.95f, 0.55f, 0.95f));
			DrawCircle(head, 9f, new Color(1f, 0.55f, 0.12f, 0.14f));
		}

		public override void _ExitTree()
		{
			_tween?.Kill();
			_tween = null;
		}
	}
}
