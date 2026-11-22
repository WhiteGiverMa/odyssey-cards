#nullable enable
using Godot;

namespace OdysseyCards.UI;

/// <summary>
/// KARDS 式出牌亮牌展示：指令 / 领域 / 反制触发时在飞行层完成
/// 「弹入定格 → 阅读停留（可带目标指示弧线）→ 收尾飞出」三段演出。
/// 纯表现，不参与战斗结算；所有 Tween 回调都检查实例有效性，场景切换安全。
/// </summary>
public partial class CardShowcaseVfx : Control
{
	// ===== 动画常量（基础时长，实际时长 = 基础时长 / speed） =====
	private const float EnterBaseDuration = 0.22f;
	private const float PlayerHoldBaseDuration = 0.65f;
	private const float EnemyHoldBaseDuration = 0.95f;
	private const float ExitBaseDuration = 0.34f;

	private const float ShowcaseScale = 0.98f;
	private const float StartScale = 0.55f;
	private const float ExitScale = 0.62f;

	/// <summary>
	/// 展示位：嵌在左侧展开态战报抽屉（320px + 34px 把手 = 354px）与中央前线左缘之间的空隙，
	/// 0.27f 保证放大字号后也不会与抽屉右缘重叠。
	/// </summary>
	private const float ShowcaseXProportion = 0.27f;
	private const float ShowcaseYProportion = 0.46f;

	private const int BezierSegments = 26;
	private const float GlowPadding = 6f;
	private const float BadgeGap = 8f;

	// ===== 实例字段 =====
	private Control _visual = null!;
	private ColorRect _glow = null!;
	private CardUI _card = null!;
	private Vector2 _renderedCardSize;
	private Color _accent;
	private Vector2 _targetCenter;
	private bool _drawTargetLine;
	private float _elapsed;
	private Tween? _tween;

	/// <summary>我方/敌方阵营色；反制触发或已被反制时用暖金强调。</summary>
	public static Color ResolveAccent(bool isPlayerSide, bool highlight)
	{
		if (highlight)
			return UIThemeFactory.WarmGold;
		return isPlayerSide ? UIThemeFactory.CyanGold : UIThemeFactory.Crimson;
	}

	/// <summary>展示位中心：左侧偏中并按视口与卡片尺寸夹紧，保证整张卡可见。</summary>
	public static Vector2 ResolveShowcaseCenter(Vector2 viewportSize, Vector2 cardSize)
	{
		float x = Mathf.Clamp(viewportSize.X * ShowcaseXProportion,
			cardSize.X * 0.5f + 16f, Mathf.Max(cardSize.X * 0.5f + 16f, viewportSize.X - cardSize.X * 0.5f - 16f));
		float y = Mathf.Clamp(viewportSize.Y * ShowcaseYProportion,
			cardSize.Y * 0.5f + 16f, Mathf.Max(cardSize.Y * 0.5f + 16f, viewportSize.Y - cardSize.Y * 0.5f - 16f));
		return new Vector2(x, y);
	}

	/// <summary>二次贝塞尔插值；供收尾飞行与指示弧线共用。</summary>
	public static Vector2 QuadraticBezier(float t, Vector2 start, Vector2 control, Vector2 end)
	{
		float u = 1f - t;
		return u * u * start + 2f * u * t * control + t * t * end;
	}

	/// <summary>
	/// 播放一次亮牌展示。
	/// </summary>
	/// <param name="card">卡牌快照（只读渲染）</param>
	/// <param name="isPlayerSide">施法/触发方阵营</param>
	/// <param name="originCenter">弹入起点（拖拽卡 / 敌方手牌栏的屏幕中心）</param>
	/// <param name="targetCenter">目标屏幕中心；<see cref="Vector2.Zero"/> 表示无单目标指示线</param>
	/// <param name="endCenter">收尾飞向位置；<see cref="Vector2.Zero"/> 表示原地淡出</param>
	/// <param name="badgeText">卡牌下方纯色标签（本地化后的文本，不含 Emoji）</param>
	/// <param name="highlight">反制触发等需要暖金强调的场合</param>
	/// <param name="speedMultiplier">动画速度倍率</param>
	/// <param name="parent">VFX 容器节点</param>
	public static CardShowcaseVfx? Play(
		Card.Card card,
		bool isPlayerSide,
		Vector2 originCenter,
		Vector2 targetCenter,
		Vector2 endCenter,
		string badgeText,
		bool highlight,
		float speedMultiplier,
		Node parent)
	{
		if (card == null || parent == null || !GodotObject.IsInstanceValid(parent))
			return null;
		var vfx = new CardShowcaseVfx();
		parent.AddChild(vfx);
		vfx.Initialize(card, isPlayerSide, originCenter, targetCenter, endCenter, badgeText, highlight, speedMultiplier);
		return vfx;
	}

	private void Initialize(
		Card.Card cardData,
		bool isPlayerSide,
		Vector2 originCenter,
		Vector2 targetCenter,
		Vector2 endCenter,
		string badgeText,
		bool highlight,
		float speedMultiplier)
	{
		MouseFilter = MouseFilterEnum.Ignore;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

		float uiScale = UIScaler.Instance?.CurrentScale ?? 1f;
		float renderScale = ShowcaseScale * uiScale;
		_renderedCardSize = new Vector2(CardUI.DESIGN_WIDTH, CardUI.DESIGN_HEIGHT) * renderScale;
		_accent = ResolveAccent(isPlayerSide, highlight);
		_targetCenter = targetCenter;

		Vector2 viewport = GetViewportRect().Size;
		Vector2 showcaseCenter = ResolveShowcaseCenter(viewport, _renderedCardSize);
		Vector2 safeOrigin = originCenter == Vector2.Zero ? showcaseCenter : originCenter;

		// 视觉根：所有子元素以它的原点为中心，缩放/淡出只作用于这一层。
		_visual = new Control { MouseFilter = MouseFilterEnum.Ignore };
		_visual.Position = safeOrigin;
		_visual.PivotOffset = Vector2.Zero;
		_visual.Scale = new Vector2(StartScale, StartScale);
		_visual.Modulate = new Color(1f, 1f, 1f, 0f);
		AddChild(_visual);

		// 阵营色柔和边框光晕：比卡面略大，垫在卡牌之后。
		_glow = new ColorRect
		{
			Color = new Color(_accent, 0.26f),
			Position = -_renderedCardSize * 0.5f - new Vector2(GlowPadding, GlowPadding) * uiScale,
			Size = _renderedCardSize + new Vector2(GlowPadding * 2f, GlowPadding * 2f) * uiScale,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		_visual.AddChild(_glow);

		_card = new CardUI
		{
			DisplayOnly = true,
			MouseFilter = MouseFilterEnum.Ignore,
			RenderScaleOverride = renderScale,
			Position = -_renderedCardSize * 0.5f,
			PivotOffset = Vector2.Zero,
		};
		_card.SetCard(cardData);
		_visual.AddChild(_card);

		PanelContainer badge = CreateBadge(badgeText, uiScale);
		_visual.AddChild(badge);

		float speed = Mathf.Clamp(speedMultiplier, UIScaler.MinDeployAnimationSpeed, UIScaler.MaxDeployAnimationSpeed);
		float enterTime = EnterBaseDuration / speed;
		// 敌方指令与任何反制触发都留足阅读时间；我方主动指令干脆利落。
		bool longRead = !isPlayerSide || highlight;
		float holdTime = (longRead ? EnemyHoldBaseDuration : PlayerHoldBaseDuration) / speed;
		float exitTime = ExitBaseDuration / speed;

		// === 阶段 1：弹入定格（Back Out 放大） ===
		_tween = CreateTween();
		_tween.TweenMethod(
			Callable.From<float>(t =>
			{
				if (!TryGetVisual(out Control visual))
					return;
				visual.Position = safeOrigin.Lerp(showcaseCenter, t);
				visual.Scale = Vector2.One * Mathf.Lerp(StartScale, 1f, t);
				visual.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(t * 2.5f, 0f, 1f));
			}),
			0f, 1f, enterTime
		).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Back);

		// === 阶段 2：定格阅读 + 目标指示线脉冲 ===
		_tween.TweenCallback(Callable.From(() =>
		{
			if (!TryGetVisual(out _))
				return;
			_drawTargetLine = _targetCenter != Vector2.Zero;
			SetProcess(_drawTargetLine);
			QueueRedraw();
		}));
		_tween.TweenInterval(holdTime);

		// === 阶段 3：指示线淡出 + 沿贝塞尔飞向收尾点并缩小淡出 ===
		_tween.TweenCallback(Callable.From(() =>
		{
			if (!TryGetVisual(out _))
				return;
			_drawTargetLine = false;
			SetProcess(false);
			QueueRedraw();
		}));
		_tween.TweenMethod(
			Callable.From<float>(t =>
			{
				if (!TryGetVisual(out Control visual))
					return;
				if (endCenter == Vector2.Zero)
				{
					visual.Modulate = new Color(1f, 1f, 1f, 1f - t);
					visual.Scale = Vector2.One * Mathf.Lerp(1f, ExitScale, t);
					return;
				}
				Vector2 control = BuildExitControl(showcaseCenter, endCenter);
				visual.Position = QuadraticBezier(t, showcaseCenter, control, endCenter);
				visual.Scale = Vector2.One * Mathf.Lerp(1f, ExitScale, t);
				visual.Modulate = new Color(1f, 1f, 1f, 1f - t);
			}),
			0f, 1f, exitTime
		).SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Quad);

		_tween.Finished += Finish;
	}

	/// <summary>收尾贝塞尔控制点：向屏幕上方拱起，避免穿过棋盘中心。</summary>
	private static Vector2 BuildExitControl(Vector2 start, Vector2 end)
	{
		float lift = Mathf.Min(start.Y, end.Y) - 90f;
		return new Vector2((start.X + end.X) * 0.5f, lift);
	}

	/// <summary>卡牌下方纯色底条 + 简短标签；宽度与卡面一致。</summary>
	private PanelContainer CreateBadge(string text, float uiScale)
	{
		var panel = new PanelContainer
		{
			MouseFilter = MouseFilterEnum.Ignore,
			Position = new Vector2(-_renderedCardSize.X * 0.5f, _renderedCardSize.Y * 0.5f + BadgeGap * uiScale),
			CustomMinimumSize = new Vector2(_renderedCardSize.X, 0f),
		};
		var style = new StyleBoxFlat
		{
			BgColor = new Color(_accent, 0.88f),
			CornerRadiusTopLeft = 4,
			CornerRadiusTopRight = 4,
			CornerRadiusBottomLeft = 4,
			CornerRadiusBottomRight = 4,
			ContentMarginLeft = 6,
			ContentMarginRight = 6,
			ContentMarginTop = 3,
			ContentMarginBottom = 3,
		};
		panel.AddThemeStyleboxOverride("panel", style);
		var label = new Label
		{
			Text = text,
			HorizontalAlignment = HorizontalAlignment.Center,
			MouseFilter = MouseFilterEnum.Ignore,
			ClipText = true,
		};
		label.AddThemeColorOverride("font_color", new Color(0.06f, 0.05f, 0.10f));
		// 底部指令标签跟随界面字体缩放；uiScale 已处理分辨率，FontScale 处理玩家偏好。
		float fontScale = UIScaler.Instance?.FontScale ?? UIScaler.DefaultFontScale;
		label.AddThemeFontSizeOverride("font_size", Mathf.Max(8, Mathf.RoundToInt(13f * uiScale * fontScale)));
		panel.AddChild(label);
		return panel;
	}

	public override void _Process(double delta)
	{
		if (!_drawTargetLine)
			return;
		_elapsed += (float)delta;
		QueueRedraw();
	}

	public override void _Draw()
	{
		if (!_drawTargetLine || !TryGetVisual(out Control visual))
			return;

		// 从展示卡右缘指向目标，画一条轻微脉冲的阵营色指示弧线。
		Vector2 start = visual.Position + new Vector2(_renderedCardSize.X * 0.42f, 0f);
		Vector2 end = _targetCenter;
		Vector2 dir = end - start;
		float distance = dir.Length();
		if (distance < 8f)
			return;
		Vector2 normal = new Vector2(-dir.Y, dir.X).Normalized();
		float arc = Mathf.Clamp(distance * 0.16f, 20f, 110f);
		Vector2 control = (start + end) * 0.5f + normal * arc;

		var points = new Vector2[BezierSegments + 1];
		for (int i = 0; i <= BezierSegments; i++)
			points[i] = QuadraticBezier(i / (float)BezierSegments, start, control, end);
		Color lineColor = new(_accent, 0.72f);
		DrawPolyline(points, lineColor, 2.5f, true);

		// 光点沿弧线循环移动，强化“这张指令打向了谁”的因果感。
		float pulse = Mathf.PosMod(_elapsed * 0.85f, 1f);
		Vector2 dot = QuadraticBezier(pulse, start, control, end);
		DrawCircle(dot, 4.5f, new Color(_accent, 0.95f));

		// 目标处收缩靶环。
		float ringT = Mathf.PosMod(_elapsed * 1.35f, 1f);
		float radius = Mathf.Lerp(26f, 9f, ringT);
		DrawArc(end, radius, 0f, Mathf.Tau, 28, new Color(_accent, 0.75f - ringT * 0.35f), 2f, true);
	}

	private bool TryGetVisual(out Control visual)
	{
		visual = _visual;
		return GodotObject.IsInstanceValid(visual) && !visual.IsQueuedForDeletion();
	}

	/// <summary>被后续亮牌取代时立即收尾，避免两张卡叠在同一展示位。</summary>
	public void CancelAndFree()
	{
		_tween?.Kill();
		_tween = null;
		Finish();
	}

	private void Finish()
	{
		_drawTargetLine = false;
		SetProcess(false);
		if (GodotObject.IsInstanceValid(_card) && !_card.IsQueuedForDeletion())
			_card.QueueFree();
		if (!IsQueuedForDeletion())
			QueueFree();
	}

	public override void _ExitTree()
	{
		_drawTargetLine = false;
		SetProcess(false);
	}
}
