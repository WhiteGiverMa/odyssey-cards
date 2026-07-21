using System;
using Godot;
using OdysseyCards.Combat;

namespace OdysseyCards.UI;

/// <summary>
/// 程序化攻击弹道特效。
/// 不依赖美术资产：用贝塞尔曲线、尾迹线段和命中闪光表现从来源到目标的打击。
/// 自包含、非阻塞，动画结束后自动 QueueFree。
/// </summary>
public partial class AttackProjectileVfx : Control
{
	private const float AttackDuration = 0.22f;
	private const float SpellDuration = 0.34f;
	private const float CombatDuration = 0.18f;
	private const float ArcHeight = 42f;
	private const int TrailSegments = 7;

	private Vector2 _from;
	private Vector2 _to;
	private Vector2 _control;
	private float _progress;
	private float _impactProgress;
	private Color _coreColor;
	private Color _trailColor;
	private CombatDamageVfxKind _kind;
	private float _scale = 1.0f; // 来自 UIScaler 的弹道缩放系数

	public static void Play(Vector2 from, Vector2 to, CombatDamageVfxKind kind, Node parent)
	{
		if (parent == null || from == Vector2.Zero || to == Vector2.Zero || from.DistanceSquaredTo(to) < 1f)
			return;

		var vfx = new AttackProjectileVfx();
		parent.AddChild(vfx);
		vfx.Initialize(from, to, kind);
	}

	private void Initialize(Vector2 from, Vector2 to, CombatDamageVfxKind kind)
	{
		MouseFilter = MouseFilterEnum.Ignore;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

		_from = from;
		_to = to;
		_kind = kind;

		// 读取弹道缩放设置
		_scale = UIScaler.Instance?.ProjectileScale ?? 1.0f;
		if (_scale < 0.01f)
			_scale = 1.0f;

		var mid = (_from + _to) * 0.5f;
		var direction = _to - _from;
		var normal = direction.LengthSquared() > 0.01f
			? new Vector2(-direction.Y, direction.X).Normalized()
			: Vector2.Up;
		_control = mid + normal * ArcHeight * _scale;

		(_coreColor, _trailColor) = kind switch
		{
			CombatDamageVfxKind.Spell => (new Color(0.65f, 0.35f, 1f, 1f), new Color(0.9f, 0.55f, 1f, 0.55f)),
			CombatDamageVfxKind.Combat => (new Color(1f, 0.95f, 0.35f, 1f), new Color(1f, 0.55f, 0.12f, 0.55f)),
			_ => (new Color(1f, 0.32f, 0.12f, 1f), new Color(1f, 0.7f, 0.18f, 0.5f)),
		};

		float duration = kind switch
		{
			CombatDamageVfxKind.Spell => SpellDuration,
			CombatDamageVfxKind.Combat => CombatDuration,
			_ => AttackDuration,
		};

		var tween = CreateTween();
		tween.TweenMethod(Callable.From<float>(SetProgress), 0f, 1f, duration)
			.SetEase(Tween.EaseType.Out)
			.SetTrans(Tween.TransitionType.Cubic);
		tween.TweenMethod(Callable.From<float>(SetImpactProgress), 0f, 1f, 0.16f)
			.SetDelay(duration * 0.78f)
			.SetEase(Tween.EaseType.Out)
			.SetTrans(Tween.TransitionType.Quad);
		tween.Finished += QueueFree;
	}

	private void SetProgress(float progress)
	{
		_progress = progress;
		QueueRedraw();
	}

	private void SetImpactProgress(float progress)
	{
		_impactProgress = progress;
		QueueRedraw();
	}

	public override void _Draw()
	{
		if (_progress <= 0f)
			return;

		DrawTrail();
		DrawProjectileCore();
		DrawImpactFlash();
	}

	private void DrawTrail()
	{
		float headT = Mathf.Clamp(_progress, 0f, 1f);
		Vector2 previous = QuadraticBezier(headT);

		for (int i = 1; i <= TrailSegments; i++)
		{
			float t = Mathf.Max(0f, headT - i * 0.035f);
			Vector2 current = QuadraticBezier(t);
			float alpha = Mathf.Clamp(1f - i / (float)(TrailSegments + 1), 0f, 1f) * _trailColor.A;
			var color = new Color(_trailColor.R, _trailColor.G, _trailColor.B, alpha);
			DrawLine(previous, current, color, Mathf.Lerp(5f, 1f, i / (float)TrailSegments) * _scale, antialiased: true);
			previous = current;
		}
	}

	private void DrawProjectileCore()
	{
		Vector2 pos = QuadraticBezier(Mathf.Clamp(_progress, 0f, 1f));
		float baseRadius = _kind == CombatDamageVfxKind.Spell ? 6f : 4.5f;
		float radius = baseRadius * _scale;
		DrawCircle(pos, radius + 4f * _scale, new Color(_coreColor.R, _coreColor.G, _coreColor.B, 0.16f));
		DrawCircle(pos, radius, _coreColor);
		DrawCircle(pos, radius * 0.45f, Colors.White);
	}

	private void DrawImpactFlash()
	{
		if (_impactProgress <= 0f)
			return;

		float alpha = 1f - _impactProgress;
		float radius = Mathf.Lerp(5f, 22f, _impactProgress) * _scale;
		var color = new Color(_coreColor.R, _coreColor.G, _coreColor.B, alpha * 0.55f);
		DrawArc(_to, radius, 0f, MathF.Tau, 24, color, 2.5f * _scale, antialiased: true);
		DrawCircle(_to, radius * 0.35f, new Color(1f, 1f, 1f, alpha * 0.28f));
	}

	private Vector2 QuadraticBezier(float t)
	{
		float u = 1f - t;
		return u * u * _from + 2f * u * t * _control + t * t * _to;
	}
}
