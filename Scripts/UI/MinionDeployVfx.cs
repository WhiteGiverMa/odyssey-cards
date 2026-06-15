using System;
using Godot;

namespace OdysseyCards.UI;

/// <summary>
/// 随从部署视觉特效——在棋盘上放置随从时播放。
/// 全链路使用屏幕中心坐标：临时卡牌以 fit 槽位的尺寸渲染，
/// 飞行中放大形成“抬起”感，落地时回到槽位尺寸并生成尘土粒子。
/// </summary>
public partial class MinionDeployVfx : Control
{
	// ===== 动画常量 =====
	private const float LiftDuration = 0.12f;
	private const float FlyBaseDuration = 0.38f;
	private const float LandBaseDuration = 0.22f;

	private const float StartScale = 0.78f;
	private const float LiftScale = 1.22f;
	private const float GlideScale = 1.08f;
	private const float SquashScale = 0.92f;
	private const float RestScale = 1.0f;

	// 贝塞尔控制点垂直偏移。玩家向上拱，敌方向下拱。
	private const float ControlOffsetY = 130f;

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

		// 创建临时卡牌 UI（纯视觉，不参与交互）。RenderScaleOverride 必须在 _Ready 前设置。
		_card = new CardUI
		{
			DisplayOnly = true,
			MouseFilter = MouseFilterEnum.Ignore,
			RenderScaleOverride = renderScale,
			Position = CenterToTopLeft(fromCenter),
			Scale = new Vector2(StartScale, StartScale),
		};
		_card.SetCard(cardData);

		// 全屏覆盖，不拦截鼠标
		MouseFilter = MouseFilterEnum.Ignore;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(_card);

		// 计算各阶段实际时长
		float speed = Mathf.Clamp(speedMultiplier, 0.1f, 3.0f);
		float liftTime = LiftDuration / speed;
		float flyTime = FlyBaseDuration / speed;
		float landTime = LandBaseDuration / speed;

		// 贝塞尔控制点：水平居中，垂直方向按阵营偏移。
		Vector2 ctrlCenter = new(
			(fromCenter.X + toCenter.X) / 2f,
			(fromCenter.Y + toCenter.Y) / 2f + (isPlayerSide ? -ControlOffsetY : ControlOffsetY)
		);

		// === 主位置动画：中心点飞行，内部统一换算为 CardUI 左上角 ===
		var positionTween = CreateTween();
		positionTween.TweenMethod(
			Callable.From<float>(t =>
			{
				Vector2 center = QuadraticBezier(t, fromCenter, ctrlCenter, toCenter);
				_card.Position = CenterToTopLeft(center);
			}),
			0f, 1f, liftTime + flyTime
		).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);

		// === 尺寸动画：抬起变大 → 飞行中收回 → 落地压缩 → 回到槽位 fit 尺寸 ===
		var scaleTween = CreateTween();
		scaleTween.TweenProperty(_card, "scale", new Vector2(LiftScale, LiftScale), liftTime)
			.SetEase(Tween.EaseType.Out)
			.SetTrans(Tween.TransitionType.Back);
		scaleTween.TweenProperty(_card, "scale", new Vector2(GlideScale, GlideScale), flyTime)
			.SetEase(Tween.EaseType.InOut)
			.SetTrans(Tween.TransitionType.Quad);
		scaleTween.TweenProperty(_card, "scale", new Vector2(SquashScale, SquashScale), landTime * 0.35f)
			.SetEase(Tween.EaseType.Out)
			.SetTrans(Tween.TransitionType.Quad);
		scaleTween.TweenProperty(_card, "scale", new Vector2(RestScale, RestScale), landTime * 0.65f)
			.SetEase(Tween.EaseType.Out)
			.SetTrans(Tween.TransitionType.Bounce);
		scaleTween.TweenInterval(Mathf.Max(0f, ParticleLifetime - landTime));
		scaleTween.Finished += () =>
		{
			_card.QueueFree();
			QueueFree();
		};

		// === 并行：落地开始生成尘土粒子 ===
		var particleTrigger = CreateTween();
		particleTrigger.TweenInterval(liftTime + flyTime);
		particleTrigger.Finished += () => SpawnDustParticles(ParticleLifetime);
	}

	private Vector2 CenterToTopLeft(Vector2 center)
	{
		return center - _renderedCardSize * 0.5f;
	}

	// ===== 尘土粒子 =====

	/// <summary>
	/// 在卡牌当前位置生成 5~8 个尘土粒子，向外随机飞出并淡出。
	/// </summary>
	private void SpawnDustParticles(float lifetime)
	{
		Vector2 cardCenter = _card.Position + _renderedCardSize * 0.5f;
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
