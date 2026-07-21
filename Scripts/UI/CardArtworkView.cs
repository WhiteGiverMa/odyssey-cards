#nullable enable
using System;
using System.Collections.Generic;
using Godot;
using OdysseyCards.Core;

namespace OdysseyCards.UI;

/// <summary>
/// 程序化卡图视图——0 美术资产下的卡面渲染组件。
/// 按 <see cref="CardArtworkSpec"/> 用 _Draw 绘制：类型渐变底 + 星点散布 +
/// 几何符号（按英雄主题风格）+ 稀有度光晕 + 边缘暗角。
/// 真实 Artwork 存在时优先显示真图，为未来接入美术资产留口。
/// </summary>
[Tool]
public partial class CardArtworkView : Control
{
	private CardArtworkSpec _spec;
	private bool _hasSpec;
	private Texture2D? _realArtwork;

	/// <summary>渐变底纹理缓存（baseColor+accentColor → 2×64 垂直渐变）。</summary>
	private static readonly Dictionary<string, ImageTexture> _gradientCache = new();

	/// <summary>
	/// 绑定卡牌数据并触发重绘。data 为 null 时清空。
	/// </summary>
	public void Setup(CardData? data)
	{
		if (data == null)
		{
			_hasSpec = false;
			_realArtwork = null;
			QueueRedraw();
			return;
		}

		_realArtwork = data.Artwork;
		string? theme = CardArtworkGenerator.ResolveHeroTheme(data.Id);
		_spec = CardArtworkGenerator.ResolveSpec(data.Id, data.Type, data.Rarity,
			data.MechanicTags, data.DomainId, theme);
		_hasSpec = true;
		QueueRedraw();
	}

	public override void _Ready()
	{
		// 编辑器预览：无数据时给一张演示卡面（大师金晕五角星），方便调布局。
		if (Engine.IsEditorHint() && !_hasSpec)
		{
			_spec = CardArtworkGenerator.ResolveSpec("preview_card", CardType.Spell,
				CardRarity.Master, CardMechanicTag.None, "", "ayame");
			_hasSpec = true;
		}
	}

	public override void _Draw()
	{
		var size = Size;
		if (size.X < 1f || size.Y < 1f)
			return;

		// 真图优先：未来接入美术资产的通道。
		if (_realArtwork != null)
		{
			DrawTextureRect(_realArtwork, new Rect2(Vector2.Zero, size), false);
			DrawEdgeVignette(size);
			return;
		}

		if (!_hasSpec)
		{
			DrawRect(new Rect2(Vector2.Zero, size), new Color("#3a3a40"));
			return;
		}

		DrawGradientBase(size);
		DrawStars(size);
		DrawSymbol(size);
		DrawRarityGlow(size);
		DrawEdgeVignette(size);
	}

	// ===== 分层绘制 =====

	/// <summary>类型渐变底（中部微亮、上下压暗，缓存纹理拉伸）。</summary>
	private void DrawGradientBase(Vector2 size)
	{
		string key = $"{_spec.BaseColor.ToHtml()}_{_spec.AccentColor.ToHtml()}";
		if (!_gradientCache.TryGetValue(key, out var tex))
		{
			// 顶部色先向底色压暗一档，避免亮底吞掉亮色符号
			var topColor = _spec.AccentColor.Lerp(_spec.BaseColor, 0.42f);
			var img = Image.CreateEmpty(2, 64, false, Image.Format.Rgba8);
			for (int y = 0; y < 64; y++)
			{
				float t = y / 63f;
				float k = Mathf.SmoothStep(0f, 1f, Mathf.Max(0f, (t - 0.18f) / 0.82f));
				var c = topColor.Lerp(_spec.BaseColor, k);
				img.SetPixel(0, y, c);
				img.SetPixel(1, y, c);
			}
			tex = ImageTexture.CreateFromImage(img);
			_gradientCache[key] = tex;
		}
		DrawTextureRect(tex, new Rect2(Vector2.Zero, size), false);
	}

	/// <summary>星点散布——种子驱动，大小与亮度随机。</summary>
	private void DrawStars(Vector2 size)
	{
		var rng = new Random(_spec.Seed);
		for (int i = 0; i < _spec.StarCount; i++)
		{
			float x = (float)rng.NextDouble() * size.X;
			float y = (float)rng.NextDouble() * size.Y;
			float r = 0.8f + (float)rng.NextDouble() * 1.6f;
			float a = 0.25f + (float)rng.NextDouble() * 0.6f;

			// 70% 白星，20% 青金，10% 星粉
			double pick = rng.NextDouble();
			var c = pick < 0.7 ? new Color(1f, 1f, 1f, a)
				: pick < 0.9 ? new Color(0.5f, 0.85f, 1f, a)
				: new Color(1f, 0.62f, 0.82f, a);
			DrawCircle(new Vector2(x, y), r, c);
		}
	}

	/// <summary>中央几何符号——12 种符号库 × 3 种风格，带深色衬底保证对比度。</summary>
	private void DrawSymbol(Vector2 size)
	{
		var center = new Vector2(size.X * 0.5f, size.Y * 0.44f);
		float r = Mathf.Min(size.X, size.Y) * 0.27f;
		float lineW = Mathf.Max(1.5f, size.X * 0.014f);
		var color = _spec.AccentColor.Lerp(Colors.White, 0.25f);

		// 深色衬底（描边效果）：先画一遍加粗的半透明暗符号
		DrawSymbolShape(center, r, lineW + size.X * 0.016f, new Color(0, 0, 0, 0.55f));
		// 主符号
		DrawSymbolShape(center, r, lineW, color);

		// 风格装饰层
		switch (_spec.Style)
		{
			case ArtworkSymbolStyle.Rune:
				// 内层缩版双线 + 环绕四小星
				DrawSymbolShape(center, r * 0.72f, lineW * 0.7f, color.Lerp(Colors.White, 0.35f));
				for (int i = 0; i < 4; i++)
				{
					float ang = Mathf.Pi * 0.5f * i + Mathf.Pi * 0.25f;
					var p = center + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r * 1.45f;
					DrawTinyStar(p, r * 0.1f, new Color(1f, 1f, 1f, 0.85f));
				}
				break;
			case ArtworkSymbolStyle.Mecha:
				// 四角 L 形准线框
				float boxR = r * 1.5f;
				float arm = r * 0.35f;
				var mechaColor = new Color(color, 0.55f);
				for (int i = 0; i < 4; i++)
				{
					float sx = (i & 1) == 0 ? -1f : 1f;
					float sy = i < 2 ? -1f : 1f;
					var corner = center + new Vector2(sx * boxR, sy * boxR * 0.82f);
					DrawLine(corner, corner + new Vector2(-sx * arm, 0), mechaColor, lineW * 0.8f);
					DrawLine(corner, corner + new Vector2(0, -sy * arm), mechaColor, lineW * 0.8f);
				}
				break;
			case ArtworkSymbolStyle.Abstract:
			default:
				break;
		}
	}

	/// <summary>按符号索引绘制线稿形状（统一入口，Rune 风格会复用画内层）。</summary>
	private void DrawSymbolShape(Vector2 center, float r, float lineW, Color color)
	{
		Vector2 Transform(Vector2 p) => center + (p - center).Rotated(_spec.SymbolRotation);

		switch (_spec.SymbolIndex)
		{
			case 0: // 五角星
				DrawPolylineRotated(StarPoints(center, r, 5), color, lineW, true, Transform);
				break;
			case 1: // 剑
			{
				var blade = new Vector2[]
				{
					center + new Vector2(0, -r),
					center + new Vector2(r * 0.22f, r * 0.1f),
					center + new Vector2(-r * 0.22f, r * 0.1f),
				};
				DrawPolygonRotated(blade, color, Transform);
				DrawLine(Transform(center + new Vector2(-r * 0.55f, r * 0.22f)),
					Transform(center + new Vector2(r * 0.55f, r * 0.22f)), color, lineW);
				DrawLine(Transform(center + new Vector2(0, r * 0.22f)),
					Transform(center + new Vector2(0, r * 0.85f)), color, lineW);
				DrawCircle(Transform(center + new Vector2(0, r * 0.95f)), r * 0.09f, color);
				break;
			}
			case 2: // 盾（六边形）
				DrawPolylineRotated(RegularPolygon(center, r, 6, 0f), color, lineW, true, Transform);
				break;
			case 3: // 圆环
				DrawArc(Transform(center), r, 0, Mathf.Tau, 40, color, lineW);
				DrawArc(Transform(center), r * 0.55f, 0, Mathf.Tau, 32, new Color(color, 0.6f), lineW * 0.7f);
				break;
			case 4: // 菱形
			{
				var pts = new Vector2[]
				{
					center + new Vector2(0, -r), center + new Vector2(r * 0.68f, 0),
					center + new Vector2(0, r), center + new Vector2(-r * 0.68f, 0),
				};
				DrawPolylineRotated(pts, color, lineW, true, Transform);
				break;
			}
			case 5: // 闪电
			{
				var pts = new Vector2[]
				{
					center + new Vector2(r * 0.25f, -r),
					center + new Vector2(-r * 0.35f, r * 0.12f),
					center + new Vector2(r * 0.08f, r * 0.12f),
					center + new Vector2(-r * 0.25f, r),
					center + new Vector2(r * 0.5f, -r * 0.18f),
					center + new Vector2(r * 0.05f, -r * 0.18f),
				};
				DrawPolylineRotated(pts, color, lineW, true, Transform);
				break;
			}
			case 6: // 准星
				DrawArc(Transform(center), r * 0.7f, 0, Mathf.Tau, 36, color, lineW);
				DrawLine(Transform(center + new Vector2(-r, 0)), Transform(center + new Vector2(r, 0)), color, lineW * 0.8f);
				DrawLine(Transform(center + new Vector2(0, -r)), Transform(center + new Vector2(0, r)), color, lineW * 0.8f);
				break;
			case 7: // 齿轮
				DrawPolylineRotated(GearPoints(center, r, 8), color, lineW, true, Transform);
				DrawArc(Transform(center), r * 0.32f, 0, Mathf.Tau, 24, color, lineW * 0.8f);
				break;
			case 8: // 眼睛
			{
				var pts = new Vector2[33];
				for (int i = 0; i < 32; i++)
				{
					float t = Mathf.Tau * i / 32f;
					pts[i] = center + new Vector2(Mathf.Cos(t) * r, Mathf.Sin(t) * r * 0.55f);
				}
				pts[32] = pts[0];
				DrawPolylineRotated(pts, color, lineW, false, Transform);
				DrawCircle(Transform(center), r * 0.22f, color);
				break;
			}
			case 9: // 心形（参数方程近似）
			{
				var pts = new Vector2[33];
				for (int i = 0; i < 32; i++)
				{
					float t = Mathf.Tau * i / 32f;
					float x = 16f * Mathf.Pow(Mathf.Sin(t), 3f);
					float y = 13f * Mathf.Cos(t) - 5f * Mathf.Cos(2f * t) - 2f * Mathf.Cos(3f * t) - Mathf.Cos(4f * t);
					pts[i] = center + new Vector2(x / 17f * r, -y / 17f * r);
				}
				pts[32] = pts[0];
				DrawPolylineRotated(pts, color, lineW, false, Transform);
				break;
			}
			case 10: // 三角形
				DrawPolylineRotated(RegularPolygon(center, r, 3, -Mathf.Pi * 0.5f), color, lineW, true, Transform);
				break;
			case 11: // 飞鸟（海鸥双线）
			default:
			{
				var pts = new Vector2[]
				{
					center + new Vector2(-r, -r * 0.15f),
					center + new Vector2(-r * 0.5f, r * 0.3f),
					center + new Vector2(0, -r * 0.05f),
					center + new Vector2(r * 0.5f, r * 0.3f),
					center + new Vector2(r, -r * 0.15f),
				};
				DrawPolylineRotated(pts, color, lineW, false, Transform);
				break;
			}
		}
	}

	/// <summary>稀有度光晕——圆环 + 大师级双环角星。</summary>
	private void DrawRarityGlow(Vector2 size)
	{
		if (_spec.GlowColor.A < 0.01f)
			return;

		var center = new Vector2(size.X * 0.5f, size.Y * 0.44f);
		float r = Mathf.Min(size.X, size.Y) * 0.27f;
		float lineW = Mathf.Max(1.5f, size.X * 0.012f);

		DrawArc(center, r * 1.32f, 0, Mathf.Tau, 48, _spec.GlowColor, lineW);

		// 大师级：外环 + 四角闪星
		if (_spec.GlowColor.R > 0.95f && _spec.GlowColor.G > 0.8f)
		{
			DrawArc(center, r * 1.5f, 0, Mathf.Tau, 48, new Color(_spec.GlowColor, 0.45f), lineW * 0.7f);
			for (int i = 0; i < 4; i++)
			{
				float ang = Mathf.Pi * 0.5f * i;
				var p = center + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r * 1.42f;
				DrawTinyStar(p, r * 0.12f, _spec.GlowColor);
			}
		}
	}

	/// <summary>边缘暗角——底部压暗带 + 四周五边暗线，增强卡面纵深。</summary>
	private void DrawEdgeVignette(Vector2 size)
	{
		// 底部压暗（与描述区衔接）
		DrawRect(new Rect2(0, size.Y * 0.78f, size.X, size.Y * 0.22f), new Color(0, 0, 0, 0.28f));
		// 四周内描边
		var edge = new Color(0, 0, 0, 0.45f);
		float w = 1.5f;
		DrawLine(Vector2.Zero, new Vector2(size.X, 0), edge, w);
		DrawLine(new Vector2(0, size.Y), new Vector2(size.X, size.Y), edge, w);
		DrawLine(Vector2.Zero, new Vector2(0, size.Y), edge, w);
		DrawLine(new Vector2(size.X, 0), size, edge, w);
	}

	// ===== 绘制辅助 =====

	private void DrawPolylineRotated(Vector2[] points, Color color, float width, bool closed, Func<Vector2, Vector2> transform)
	{
		var pts = new Vector2[points.Length + (closed ? 1 : 0)];
		for (int i = 0; i < points.Length; i++)
			pts[i] = transform(points[i]);
		if (closed)
			pts[points.Length] = transform(points[0]);
		DrawPolyline(pts, color, width, true);
	}

	private void DrawPolygonRotated(Vector2[] points, Color color, Func<Vector2, Vector2> transform)
	{
		var pts = new Vector2[points.Length];
		for (int i = 0; i < points.Length; i++)
			pts[i] = transform(points[i]);
		DrawColoredPolygon(pts, color);
	}

	/// <summary>正多边形顶点（startAngle 起始弧度）。</summary>
	private static Vector2[] RegularPolygon(Vector2 center, float r, int sides, float startAngle)
	{
		var pts = new Vector2[sides];
		for (int i = 0; i < sides; i++)
		{
			float ang = startAngle + Mathf.Tau * i / sides;
			pts[i] = center + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r;
		}
		return pts;
	}

	/// <summary>星形顶点（外内交替）。</summary>
	private static Vector2[] StarPoints(Vector2 center, float r, int points)
	{
		var pts = new Vector2[points * 2];
		for (int i = 0; i < points * 2; i++)
		{
			float ang = -Mathf.Pi * 0.5f + Mathf.Pi * i / points;
			float radius = (i & 1) == 0 ? r : r * 0.42f;
			pts[i] = center + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * radius;
		}
		return pts;
	}

	/// <summary>齿轮顶点（齿顶齿根交替）。</summary>
	private static Vector2[] GearPoints(Vector2 center, float r, int teeth)
	{
		var pts = new Vector2[teeth * 2];
		for (int i = 0; i < teeth * 2; i++)
		{
			float ang = Mathf.Tau * i / (teeth * 2);
			float radius = (i & 1) == 0 ? r : r * 0.74f;
			pts[i] = center + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * radius;
		}
		return pts;
	}

	/// <summary>四角小星（闪光点缀）。</summary>
	private void DrawTinyStar(Vector2 center, float r, Color color)
	{
		DrawLine(center + new Vector2(-r, 0), center + new Vector2(r, 0), color, Mathf.Max(1f, r * 0.35f));
		DrawLine(center + new Vector2(0, -r), center + new Vector2(0, r), color, Mathf.Max(1f, r * 0.35f));
	}
}
