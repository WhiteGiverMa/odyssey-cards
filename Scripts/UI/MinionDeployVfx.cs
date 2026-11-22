using System;
using Godot;

namespace OdysseyCards.UI;

/// <summary>
/// 随从部署视觉特效——在棋盘上放置随从时播放。
/// 全链路使用屏幕中心坐标：临时卡牌先从来源飞向亮牌悬停点（放大到可读卡面），
/// 悬停片刻让玩家认清“这张卡”，再俯冲落入目标槽位、压缩回弹并生成尘土粒子。
/// </summary>
public partial class MinionDeployVfx : Control
{
	// ===== 动画常量（基础时长，实际时长 = 基础时长 / speed） =====
	private const float ApproachBaseDuration = 0.26f;
	private const float PlayerHoldBaseDuration = 0.42f;
	private const float EnemyHoldBaseDuration = 0.65f;
	private const float DiveBaseDuration = 0.20f;
	private const float LandBaseDuration = 0.24f;

	/// <summary>起点缩放：比槽位尺寸略小，先快速“捏起”再放大。</summary>
	private const float StartScale = 0.62f;

	/// <summary>悬停亮牌的目标像素比例（相对设计尺寸），保证卡面可读。</summary>
	private const float ReadableCardScale = 0.90f;

	private const float SquashScale = 0.92f;
	private const float RestScale = 1.0f;

	// 俯冲贝塞尔控制点垂直偏移。玩家向上拱，敌方向下拱。
	private const float ControlOffsetY = 46f;

	// 尘土粒子
	private const float ParticleSize = 4f;
	private const int ParticleBaseCount = 6;
	private const float ParticleLifetime = 0.4f;
	private static readonly Color DustColor = new(0.78f, 0.66f, 0.31f, 1f);

	// ===== 实例字段 =====
	private CardUI _card = null!;
	private Vector2 _renderedCardSize;

	// ===== 静态数学工具 =====

	/// <summary>
	/// 二次贝塞尔曲线插值。
	/// B(t) = (1−t)²·P₀ + 2(1−t)t·P₁ + t²·P₂
	/// </summary>
	public static Vector2 QuadraticBezier(float t, Vector2 start, Vector2 ctrl, Vector2 end)
	{
		float u = 1f - t;
		return u * u * start + 2f * u * t * ctrl + t * t * end;
	}

	/// <summary>
	/// 计算 CardUI 在目标矩形内完整显示的渲染缩放。
	/// </summary>
	public static float CalculateFitRenderScale(Vector2 targetSize)
	{
		if (targetSize.X <= 0f || targetSize.Y <= 0f)
			return 1f;
		return Mathf.Min(targetSize.X / CardUI.DESIGN_WIDTH, targetSize.Y / CardUI.DESIGN_HEIGHT);
	}

	/// <summary>把渲染缩放换算为“能看清卡面”的悬停缩放；不允许比槽位更小。</summary>
	public static float CalculateHoverScale(float renderScale, float uiScale)
	{
		if (renderScale <= 0f)
			return 1f;
		return Mathf.Clamp(ReadableCardScale * Mathf.Max(uiScale, 0.01f) / renderScale, 1f, 3.4f);
	}

	/// <summary>悬停亮牌位：起点与槽位中点再向屏幕中央抬起，并夹紧在视口安全边距内。</summary>
	public static Vector2 ResolveHoverCenter(Vector2 fromCenter, Vector2 toCenter, Vector2 viewportSize, Vector2 cardSize)
	{
		Vector2 midpoint = (fromCenter + toCenter) * 0.5f;
		Vector2 viewportCenter = viewportSize * 0.5f;
		Vector2 hover = midpoint.Lerp(viewportCenter, 0.35f);
		Vector2 half = cardSize * 0.5f + new Vector2(16f, 16f);
		hover.X = Mathf.Clamp(hover.X, half.X, Mathf.Max(half.X, viewportSize.X - half.X));
		hover.Y = Mathf.Clamp(hover.Y, half.Y, Mathf.Max(half.Y, viewportSize.Y - half.Y));
		return hover;
	}

	// ===== 静态工厂 =====

	/// <summary>
	/// 播放随从部署动画。
	/// </summary>
	/// <param name="card">卡牌数据（仅用于渲染，不操作数据模型）</param>
	/// <param name="fromCenter">起始屏幕中心位置</param>
	/// <param name="toCenter">目标槽位屏幕中心位置</param>
	/// <param name="targetSize">目标槽位屏幕尺寸</param>
	/// <param name="isPlayerSide">true=玩家侧（曲线向上拱起），false=敌方侧（向下拱起）</param>
	/// <param name="speedMultiplier">速度倍率（actualDuration = baseDuration / speedMultiplier）</param>
	/// <param name="parent">VFX 容器节点</param>
	public static void Play(Card.Card card, Vector2 fromCenter, Vector2 toCenter, Vector2 targetSize, bool isPlayerSide, float speedMultiplier, Node parent)
	{
		var vfx = new MinionDeployVfx();
		parent.AddChild(vfx);
		vfx.Initialize(card, fromCenter, toCenter, targetSize, isPlayerSide, speedMultiplier);
	}

	// ===== 初始化 =====

	private void Initialize(Card.Card cardData, Vector2 fromCenter, Vector2 toCenter, Vector2 targetSize, bool isPlayerSide, float speedMultiplier)
	{
		float renderScale = CalculateFitRenderScale(targetSize);
		_renderedCardSize = new Vector2(CardUI.DESIGN_WIDTH, CardUI.DESIGN_HEIGHT) * renderScale;
		float uiScale = UIScaler.Instance?.CurrentScale ?? 1f;
		float hoverScale = CalculateHoverScale(renderScale, uiScale);

		// 创建临时卡牌 UI（纯视觉，不参与交互）。RenderScaleOverride 必须在 _Ready 前设置。
		_card = new CardUI
		{
			DisplayOnly = true,
			MouseFilter = MouseFilterEnum.Ignore,
			RenderScaleOverride = renderScale,
			Position = CenterToTopLeft(fromCenter),
			// 以卡面中心为缩放轴，位置公式在任意缩放阶段都仍然成立。
			PivotOffset = _renderedCardSize * 0.5f,
			Scale = new Vector2(StartScale, StartScale),
		};
		_card.SetCard(cardData);

		// 全屏覆盖，不拦截鼠标
		MouseFilter = MouseFilterEnum.Ignore;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(_card);

		// 计算各阶段实际时长
		float speed = Mathf.Clamp(speedMultiplier, UIScaler.MinDeployAnimationSpeed, UIScaler.MaxDeployAnimationSpeed);
		float approachTime = ApproachBaseDuration / speed;
		float holdTime = (isPlayerSide ? PlayerHoldBaseDuration : EnemyHoldBaseDuration) / speed;
		float diveTime = DiveBaseDuration / speed;
		float landTime = LandBaseDuration / speed;

		Vector2 hoverCenter = ResolveHoverCenter(fromCenter, toCenter, GetViewportRect().Size,
			_renderedCardSize * hoverScale);
		Vector2 diveControl = new(
			(hoverCenter.X + toCenter.X) * 0.5f,
			(hoverCenter.Y + toCenter.Y) * 0.5f + (isPlayerSide ? -ControlOffsetY : ControlOffsetY));

		// === 阶段 1+2：飞向亮牌悬停点并停留 ===
		var positionTween = CreateTween();
		positionTween.TweenMethod(
			Callable.From<float>(t =>
			{
				if (!TryGetLiveCard(out CardUI card))
					return;
				card.Position = CenterToTopLeft(fromCenter.Lerp(hoverCenter, t));
			}),
			0f, 1f, approachTime
		).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
		positionTween.TweenInterval(holdTime);

		// === 阶段 3：俯冲落入槽位 ===
		positionTween.TweenMethod(
			Callable.From<float>(t =>
			{
				if (!TryGetLiveCard(out CardUI card))
					return;
				card.Position = CenterToTopLeft(QuadraticBezier(t, hoverCenter, diveControl, toCenter));
			}),
			0f, 1f, diveTime
		).SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Quad);

		// === 并行缩放：放大到可读 → 悬停 → 落位压缩回弹 ===
		var scaleTween = CreateTween();
		scaleTween.TweenProperty(_card, "scale", Vector2.One * hoverScale, approachTime)
			.SetEase(Tween.EaseType.Out)
			.SetTrans(Tween.TransitionType.Back);
		scaleTween.TweenInterval(holdTime);
		scaleTween.TweenProperty(_card, "scale", Vector2.One, diveTime)
			.SetEase(Tween.EaseType.In)
			.SetTrans(Tween.TransitionType.Quad);
		scaleTween.TweenProperty(_card, "scale", new Vector2(SquashScale, SquashScale), landTime * 0.35f)
			.SetEase(Tween.EaseType.Out)
			.SetTrans(Tween.TransitionType.Quad);
		scaleTween.TweenProperty(_card, "scale", new Vector2(RestScale, RestScale), landTime * 0.65f)
			.SetEase(Tween.EaseType.Out)
			.SetTrans(Tween.TransitionType.Bounce);
		scaleTween.TweenInterval(Mathf.Max(0f, ParticleLifetime - landTime));
		scaleTween.Finished += Finish;

		// === 并行：落地开始生成尘土粒子 ===
		var particleTrigger = CreateTween();
		particleTrigger.TweenInterval(approachTime + holdTime + diveTime);
		particleTrigger.Finished += () => SpawnDustParticles(ParticleLifetime);
	}

	private Vector2 CenterToTopLeft(Vector2 center)
	{
		return center - _renderedCardSize * 0.5f;
	}

	private bool TryGetLiveCard(out CardUI card)
	{
		card = _card;
		return GodotObject.IsInstanceValid(card) && !card.IsQueuedForDeletion();
	}

	private void Finish()
	{
		if (TryGetLiveCard(out CardUI card))
			card.QueueFree();
		if (!IsQueuedForDeletion())
			QueueFree();
	}

	// ===== 尘土粒子 =====

	/// <summary>
	/// 在卡牌当前位置生成 5~8 个尘土粒子，向外随机飞出并淡出。
	/// </summary>
	private void SpawnDustParticles(float lifetime)
	{
		if (!TryGetLiveCard(out CardUI card))
			return;
		Vector2 cardCenter = card.Position + _renderedCardSize * 0.5f;
		int count = Random.Shared.Next(ParticleBaseCount - 1, ParticleBaseCount + 3); // 5~8

		for (int i = 0; i < count; i++)
		{
			var particle = new ColorRect
			{
				Size = new Vector2(ParticleSize, ParticleSize),
				Color = DustColor,
				Position = cardCenter - new Vector2(ParticleSize / 2f, ParticleSize / 2f),
				MouseFilter = MouseFilterEnum.Ignore,
			};
			AddChild(particle);

			// 随机方向 + 随机距离（20~40px）
			float angle = Random.Shared.NextSingle() * MathF.PI * 2f;
			float distance = 20f + Random.Shared.NextSingle() * 20f;
			Vector2 velocity = new(Mathf.Cos(angle) * distance, Mathf.Sin(angle) * distance);

			var pTween = CreateTween();
			pTween.SetParallel(true);
			pTween.TweenProperty(particle, "position", particle.Position + velocity, lifetime)
				.SetEase(Tween.EaseType.Out)
				.SetTrans(Tween.TransitionType.Quad);
			pTween.TweenProperty(particle, "modulate:a", 0f, lifetime)
				.SetEase(Tween.EaseType.In)
				.SetTrans(Tween.TransitionType.Quad);
			pTween.Finished += particle.QueueFree;
		}
	}
}
