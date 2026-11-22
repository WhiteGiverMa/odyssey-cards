#nullable enable
using Godot;
using OdysseyCards.Card;

namespace OdysseyCards.UI;

/// <summary>
/// 随从普通推进前线的平滑位移动画：在部署层生成一张只读替身卡，
/// 沿轻微抬起的曲线从原槽位滑到新槽位，并带阵营色流光尾迹。
/// 一步突击若由 <see cref="AssaultAdvanceVfx"/> 接管，会调用 <see cref="CancelAndFree"/> 撤掉替身，避免重影。
/// </summary>
public partial class MinionMoveVfx : Control
{
	private const float MoveBaseDuration = 0.34f;
	private const float LiftHeight = 16f;
	private const float PeakScale = 1.08f;
	private const float FadeBaseDuration = 0.12f;
	private const float TrailSize = 5f;
	private const float TrailLifetime = 0.26f;

	private CardUI _card = null!;
	private Tween? _tween;
	private Vector2 _fromCenter;
	private Vector2 _toCenter;
	private Color _accent;
	private float _streakSize = TrailSize;

	/// <summary>动画所属随从；用于突击接管时精确撤销。</summary>
	public Minion? SourceMinion { get; private set; }

	/// <summary>推进起点/终点屏幕中心之间的中点，供 QA 与单测校验路径。</summary>
	public static Vector2 Midpoint(Vector2 fromCenter, Vector2 toCenter) => fromCenter.Lerp(toCenter, 0.5f);

	/// <summary>二次贝塞尔插值（与部署/飞牌演出保持同一套数学）。</summary>
	public static Vector2 QuadraticBezier(float t, Vector2 start, Vector2 control, Vector2 end)
	{
		float u = 1f - t;
		return u * u * start + 2f * u * t * control + t * t * end;
	}

	public static MinionMoveVfx? Play(Minion minion, Rect2 fromRect, Rect2 toRect, bool isPlayerSide,
		float speedMultiplier, Node parent)
	{
		if (minion == null || parent == null || !GodotObject.IsInstanceValid(parent))
			return null;
		if (fromRect.Size == Vector2.Zero || toRect.Size == Vector2.Zero)
			return null;
		var vfx = new MinionMoveVfx();
		parent.AddChild(vfx);
		vfx.Initialize(minion, fromRect, toRect, isPlayerSide, speedMultiplier);
		return vfx;
	}

	private void Initialize(Minion minion, Rect2 fromRect, Rect2 toRect, bool isPlayerSide, float speedMultiplier)
	{
		SourceMinion = minion;
		MouseFilter = MouseFilterEnum.Ignore;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

		float uiScale = UIScaler.Instance?.CurrentScale ?? 1f;
		// 取两槽位中较小的一边作为渲染比例，滑行途中不会溢出棋盘。
		float renderScale = Mathf.Min(
			MinionDeployVfx.CalculateFitRenderScale(fromRect.Size),
			MinionDeployVfx.CalculateFitRenderScale(toRect.Size));
		Vector2 cardSize = new Vector2(CardUI.DESIGN_WIDTH, CardUI.DESIGN_HEIGHT) * renderScale;
		_fromCenter = fromRect.Position + fromRect.Size * 0.5f;
		_toCenter = toRect.Position + toRect.Size * 0.5f;
		_accent = isPlayerSide ? UIThemeFactory.CyanGold : UIThemeFactory.Crimson;
		_streakSize = TrailSize * Mathf.Max(uiScale, 0.5f);

		_card = new CardUI
		{
			DisplayOnly = true,
			MouseFilter = MouseFilterEnum.Ignore,
			RenderScaleOverride = renderScale,
			Position = _fromCenter - cardSize * 0.5f,
			PivotOffset = cardSize * 0.5f,
			Modulate = new Color(1f, 1f, 1f, 0.92f),
		};
		_card.SetCard(minion.ToRuntimeCard());
		AddChild(_card);

		float speed = Mathf.Clamp(speedMultiplier, UIScaler.MinDeployAnimationSpeed, UIScaler.MaxDeployAnimationSpeed);
		float moveTime = MoveBaseDuration / speed;
		float fadeTime = FadeBaseDuration / speed;
		Vector2 control = (_fromCenter + _toCenter) * 0.5f + new Vector2(0f, -LiftHeight);

		// === 主补间：轻微抬起的滑行 → 到达后淡出 ===
		_tween = CreateTween();
		_tween.TweenMethod(
			Callable.From<float>(t =>
			{
				if (!TryGetLiveCard(out CardUI card))
					return;
				Vector2 center = QuadraticBezier(t, _fromCenter, control, _toCenter);
				card.Position = center - cardSize * 0.5f;
			}),
			0f, 1f, moveTime
		).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
		_tween.TweenProperty(_card, "modulate:a", 0f, fadeTime).SetEase(Tween.EaseType.In);
		_tween.Finished += Finish;

		// === 并行：先抬后落的缩放，制造“滑行”而非“瞬移”的体感 ===
		var scaleTween = CreateTween();
		scaleTween.TweenProperty(_card, "scale", Vector2.One * PeakScale, moveTime * 0.45f)
			.SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Quad);
		scaleTween.TweenProperty(_card, "scale", Vector2.One, moveTime * 0.55f)
			.SetEase(Tween.EaseType.InOut).SetTrans(Tween.TransitionType.Quad);

		// === 并行：三簇流光尾迹 ===
		var trailTween = CreateTween();
		trailTween.SetParallel(false);
		for (int i = 0; i < 3; i++)
		{
			trailTween.TweenInterval(moveTime / 4f);
			trailTween.TweenCallback(Callable.From(SpawnStreak));
		}
	}

	/// <summary>被一步突击接管时立即撤销替身，避免与突击演出叠影。</summary>
	public void CancelAndFree()
	{
		_tween?.Kill();
		_tween = null;
		if (TryGetLiveCard(out CardUI card))
			card.QueueFree();
		if (!IsQueuedForDeletion())
			QueueFree();
	}

	private bool TryGetLiveCard(out CardUI card)
	{
		card = _card;
		return GodotObject.IsInstanceValid(card) && !card.IsQueuedForDeletion() && !IsQueuedForDeletion();
	}

	private void SpawnStreak()
	{
		if (!TryGetLiveCard(out CardUI card))
			return;
		Vector2 size = new(_streakSize, _streakSize);
		var streak = new ColorRect
		{
			Size = size,
			Color = new Color(_accent, 0.55f),
			Position = card.Position + new Vector2(card.Size.X * 0.5f, card.Size.Y * 0.5f) - size * 0.5f,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		AddChild(streak);
		var tween = CreateTween();
		tween.SetParallel(true);
		tween.TweenProperty(streak, "position", streak.Position + new Vector2(0f, 12f), TrailLifetime)
			.SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Quad);
		tween.TweenProperty(streak, "modulate:a", 0f, TrailLifetime).SetEase(Tween.EaseType.In);
		tween.Finished += streak.QueueFree;
	}

	private void Finish()
	{
		if (TryGetLiveCard(out CardUI card))
			card.QueueFree();
		if (!IsQueuedForDeletion())
			QueueFree();
	}

	public override void _ExitTree()
	{
		_tween?.Kill();
		_tween = null;
	}
}
