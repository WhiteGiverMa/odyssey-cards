using Godot;

namespace OdysseyCards.UI;

/// <summary>
/// 卡牌打出飞行视觉特效——从屏幕中央沿二次贝塞尔曲线飞向弃牌堆按钮，
/// 途中卡牌缩小/淡化，并生成简单尾迹粒子占位。动画结束后自动 QueueFree。
/// 参考 STS2 NCardFlyVfx 的逐帧贝塞尔动画模式。
/// </summary>
public partial class CardFlyVfx : Control
{
	// ===== 动画常量 =====
	private const float CenterHoldDuration = 0.3f;
	private const float FlyDuration = 0.6f;

	// 贝塞尔控制点 Y 偏移（向上拱起，负值 = 上方）
	private const float BezierControlOffsetY = -120f;

	// 尾迹粒子（简单五毛占位特效）
	private const float TrailParticleSize = 5f;
	private const float TrailParticleLifetime = 0.3f;
	private const int TrailSpawnEveryNFrames = 4;
	private static readonly Color TrailColor = new(1f, 0.85f, 0.3f, 0.7f);

	// ===== 实例字段 =====
	private CardUI _card = null!;
	private int _frameCounter;
	private float _scale = 1.0f; // 来自 UIScaler 的卡牌飞行粒子缩放系数

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

	// ===== 静态工厂 =====

	/// <summary>
	/// 将卡牌从当前位置飞向弃牌堆按钮中心，动画结束后自动销毁。
	/// 调用方负责提前取消卡牌的事件订阅（OnCardDropped、OnDragMove）并调用 CancelDragSilent()。
	/// </summary>
	/// <param name="card">要动画的卡牌 UI（会被 reparent 到此 VFX 节点下）</param>
	/// <param name="targetPos">弃牌堆按钮的屏幕中心位置（GlobalPosition）</param>
	/// <param name="parent">VFX 容器节点</param>
	public static void PlayToDiscard(CardUI card, Vector2 targetPos, Node parent)
	{
		var vfx = new CardFlyVfx();
		// 必须先加入场景树再初始化（Initialize 中调用 GetViewportRect 需要 is_inside_tree）
		parent.AddChild(vfx);
		vfx.Initialize(card, targetPos);
	}

	/// <summary>
	/// 纯装饰性抽牌动画：在 fromPos 处生成一张临时卡牌，沿贝塞尔曲线飞到 toPos，
	/// 到达后缩小淡出并自动销毁。不影响手牌数据模型。
	/// </summary>
	/// <param name="card">要展示的卡牌数据（仅用于渲染，不操作数据）</param>
	/// <param name="fromPos">抽牌堆按钮的屏幕中心位置</param>
	/// <param name="toPos">目标手牌位置的屏幕中心</param>
	/// <param name="parent">VFX 容器节点</param>
	public static void PlayDrawToHand(Card.Card card, Vector2 fromPos, Vector2 toPos, Node parent)
	{
		var tempCardUI = new CardUI();
		tempCardUI.SetCard(card);
		tempCardUI.MouseFilter = MouseFilterEnum.Ignore;
		// 放在 fromPos
		Vector2 cardSize = new(CardUI.DESIGN_WIDTH, CardUI.DESIGN_HEIGHT);
		tempCardUI.Position = fromPos - cardSize * 0.5f;
		tempCardUI.Scale = new Vector2(0.6f, 0.6f);

		var vfx = new CardFlyVfx();
		parent.AddChild(vfx);
		vfx.InitializeDrawAnimation(tempCardUI, fromPos, toPos);
	}

	// ===== 初始化 =====

	private void Initialize(CardUI card, Vector2 targetPos)
	{
		_card = card;

		// 读取卡牌飞行缩放设置
		_scale = UIScaler.Instance?.CardFlyScale ?? 1.0f;
		if (_scale < 0.01f)
			_scale = 1.0f;

		// 全屏覆盖，不拦截鼠标事件
		MouseFilter = MouseFilterEnum.Ignore;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

		// 将卡牌 reparent 到此 VFX 节点（GlobalPosition 由 Godot 自动保持）
		var originalParent = card.GetParent();
		originalParent?.RemoveChild(card);
		AddChild(card);

		// 设置卡牌在 VFX 控件内的本地坐标（全屏锚定后 Position 即等于 GlobalPosition）
		card.Position = card.GlobalPosition;

		AnimateToDiscardPile(targetPos);
	}

	// ===== 动画 =====

	private void AnimateToDiscardPile(Vector2 targetPos)
	{
		Vector2 initialPos = _card.Position;
		Vector2 initialScale = _card.Scale;
		Color initialModulate = _card.Modulate;
		Vector2 centerPos = GetViewportRect().Size / 2f;
		Vector2 startPos = centerPos;

		// 贝塞尔控制点：水平居中偏右，垂直方向向上拱起
		Vector2 ctrlPos = new(
			(startPos.X + targetPos.X) / 2f,
			Mathf.Min(startPos.Y, targetPos.Y) + BezierControlOffsetY
		);

		// === 主补间：到中央 → 贝塞尔飞行 → 自毁 ===
		var tween = CreateTween();

		// 阶段 1：飞到屏幕中央
		tween.TweenMethod(
			Callable.From<float>(t =>
			{
				if (!TryGetLiveCard(out var card))
				{
					StopOrphanedVfx();
					return;
				}

				card.Position = initialPos.Lerp(centerPos, t);
			}),
			0f, 1f, CenterHoldDuration)
			 .SetEase(Tween.EaseType.Out)
			 .SetTrans(Tween.TransitionType.Cubic);

		// 阶段 2：沿贝塞尔曲线飞向弃牌堆
		_frameCounter = 0;
		tween.TweenMethod(
			Callable.From<float>(t =>
			{
				if (!TryGetLiveCard(out var card))
				{
					StopOrphanedVfx();
					return;
				}

				Vector2 pos = QuadraticBezier(t, startPos, ctrlPos, targetPos);
				card.Position = pos;

				// 旋转跟随切线方向
				float nextT = Mathf.Min(t + 0.02f, 1f);
				Vector2 nextPos = QuadraticBezier(nextT, startPos, ctrlPos, targetPos);
				Vector2 dir = nextPos - pos;
				if (dir.LengthSquared() > 0.5f)
				{
					card.Rotation = dir.Angle() + Mathf.Pi / 2f;
				}

				// 每隔 N 帧生成一个尾迹粒子
				_frameCounter++;
				if (_frameCounter >= TrailSpawnEveryNFrames)
				{
					_frameCounter = 0;
					SpawnTrailParticle(pos);
				}
			}),
			0f, 1f, FlyDuration
		).SetEase(Tween.EaseType.InOut).SetTrans(Tween.TransitionType.Cubic);

		// 到达后销毁
		tween.Finished += () =>
		{
			if (TryGetLiveCard(out var card))
				card.QueueFree();
			QueueFree();
		};

		// === 并行补间：变暗 + 缩小（从飞行开始时触发，延迟 CenterHoldDuration） ===
		var fadeTween = CreateTween();
		fadeTween.TweenInterval(CenterHoldDuration);
		fadeTween.TweenMethod(
			Callable.From<float>(t =>
			{
				if (!TryGetLiveCard(out var card))
				{
					StopOrphanedVfx();
					return;
				}

				card.Modulate = initialModulate.Lerp(new Color(0.4f, 0.4f, 0.4f, 0.01f), t);
				card.Scale = initialScale.Lerp(new Vector2(0.2f, 0.2f), t);
			}),
			0f, 1f, FlyDuration)
			.SetEase(Tween.EaseType.InOut)
			.SetTrans(Tween.TransitionType.Quad);
	}

	private bool TryGetLiveCard(out CardUI card)
	{
		card = _card;
		return GodotObject.IsInstanceValid(card) && !card.IsQueuedForDeletion();
	}

	private void StopOrphanedVfx()
	{
		if (!IsQueuedForDeletion())
			QueueFree();
	}

	/// <summary>
	/// 抽牌动画初始化：将临时卡牌加入 VFX 容器，从 fromPos 沿贝塞尔曲线飞到 toPos。
	/// 到达后卡牌缩小淡出并自毁。
	/// </summary>
	private void InitializeDrawAnimation(CardUI tempCard, Vector2 fromPos, Vector2 toPos)
	{
		_card = tempCard;

		// 读取卡牌飞行缩放设置
		_scale = UIScaler.Instance?.CardFlyScale ?? 1.0f;
		if (_scale < 0.01f)
			_scale = 1.0f;
		MouseFilter = MouseFilterEnum.Ignore;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(_card);

		// 贝塞尔控制点：向屏幕上方拱起
		Vector2 ctrlPos = new(
			(fromPos.X + toPos.X) / 2f,
			Mathf.Min(fromPos.Y, toPos.Y) - 180f
		);

		var tween = CreateTween();

		// 缩放弹入（0.6 → 1.0）
		tween.TweenProperty(_card, "scale", new Vector2(0.85f, 0.85f), 0.3)
			 .SetEase(Tween.EaseType.Out)
			 .SetTrans(Tween.TransitionType.Back);

		// 沿贝塞尔曲线飞行
		_frameCounter = 0;
		tween.TweenMethod(
			Callable.From<float>(t =>
			{
				Vector2 pos = QuadraticBezier(t, fromPos, ctrlPos, toPos);
				_card.Position = pos;

				_frameCounter++;
				if (_frameCounter >= TrailSpawnEveryNFrames)
				{
					_frameCounter = 0;
					SpawnTrailParticle(pos);
				}
			}),
			0f, 1f, 0.45f
		).SetEase(Tween.EaseType.InOut).SetTrans(Tween.TransitionType.Cubic);

		// 到达后缩小淡出自毁
		var fadeTween = CreateTween();
		fadeTween.TweenInterval(0.48f);
		fadeTween.SetParallel(true);
		fadeTween.TweenProperty(_card, "modulate", new Color(1f, 1f, 1f, 0f), 0.2f)
				 .SetEase(Tween.EaseType.In);
		fadeTween.TweenProperty(_card, "scale", new Vector2(0.3f, 0.3f), 0.2f)
				 .SetEase(Tween.EaseType.In);

		tween.Finished += () =>
		{
			_card.QueueFree();
			QueueFree();
		};
	}

	// ===== 尾迹粒子（五毛占位特效） =====

	private void SpawnTrailParticle(Vector2 localPos)
	{
		var particleSize = TrailParticleSize * _scale;
		var particle = new ColorRect
		{
			Size = new Vector2(particleSize, particleSize),
			Color = TrailColor,
			Position = localPos - new Vector2(particleSize / 2f, particleSize / 2f),
			MouseFilter = MouseFilterEnum.Ignore,
		};
		AddChild(particle);

		var pTween = CreateTween();
		pTween.SetParallel(true);
		pTween.TweenProperty(particle, "modulate:a", 0f, TrailParticleLifetime)
			   .SetEase(Tween.EaseType.In)
			   .SetTrans(Tween.TransitionType.Quad);
		pTween.TweenProperty(particle, "scale", Vector2.Zero, TrailParticleLifetime)
			   .SetEase(Tween.EaseType.In)
			   .SetTrans(Tween.TransitionType.Quad);
		pTween.Finished += particle.QueueFree;
	}
}
