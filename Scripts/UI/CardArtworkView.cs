#nullable enable
using System;
using System.Collections.Generic;
using Godot;
using OdysseyCards.Core;

namespace OdysseyCards.UI;

/// <summary>
/// 零资产精密卡图：双焦点星云、主题测绘底纹、复合徽记与切角稀有度流光。
/// 星尘与符号拓扑只在绑定时准备；绘制热路径复用缓冲，不创建随机数或几何数组。
/// 真实 Artwork 仍优先于程序化卡图。
/// </summary>
[Tool]
public partial class CardArtworkView : Control
{
	private readonly struct StarSample
	{
		public readonly Vector2 Position;
		public readonly float Radius;
		public readonly float Alpha;
		public readonly float Tint;

		public StarSample(Random random)
		{
			Position = new Vector2((float)random.NextDouble(), (float)random.NextDouble());
			Radius = 0.25f + (float)random.NextDouble() * 1.15f;
			Alpha = 0.15f + (float)random.NextDouble() * 0.65f;
			Tint = (float)random.NextDouble();
		}
	}

	private static readonly Dictionary<(Color Base, Color Accent), ImageTexture> _gradientCache = new();
	private static readonly Vector2[][] _symbolPaths = CreateSymbolPaths();
	private static ImageTexture? _softGlow;
	private readonly Vector2[] _triangle = new Vector2[3];
	private readonly Vector2[] _panel = new Vector2[4];
	private StarSample[] _stars = Array.Empty<StarSample>();
	private CardArtworkSpec _spec;
	private bool _hasSpec;
	private Texture2D? _realArtwork;
	private ImageTexture? _gradient;

	/// <summary>创建可安全用于编辑器预览的无参视图。</summary>
	public CardArtworkView()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		ClipContents = true;
	}

	/// <summary>绑定卡牌数据；空值清除显示，真实卡图保留优先级。</summary>
	public void Setup(CardData? data)
	{
		_realArtwork = data?.Artwork;
		_hasSpec = data != null;
		if (data != null)
		{
			CardArtworkSpec spec = CardArtworkGenerator.ResolveSpec(data.Id, data.Type, data.Rarity,
				data.MechanicTags, data.DomainId, CardArtworkGenerator.ResolveHeroTheme(data.Id));
			if (_stars.Length == 0 || _spec.Seed != spec.Seed || _spec.Style != spec.Style || _spec.BaseColor != spec.BaseColor)
			{
				BindSpec(spec);
			}
			else
			{
				_spec = spec;
			}
		}
		QueueRedraw();
	}

	public override void _Ready()
	{
		if (Engine.IsEditorHint() && !_hasSpec)
		{
			BindSpec(CardArtworkGenerator.ResolveSpec("preview_card", CardType.Spell,
				CardRarity.Master, CardMechanicTag.None, "", "ayame"));
		}
	}

	public override void _Notification(int what)
	{
		if (what == NotificationResized)
		{
			QueueRedraw();
		}
	}

	public override void _Draw()
	{
		Vector2 size = Size;
		if (size.X < 1f || size.Y < 1f)
		{
			return;
		}
		if (_realArtwork != null)
		{
			DrawTextureRect(_realArtwork, new Rect2(Vector2.Zero, size), false);
			DrawEdgeVignette(size);
			return;
		}
		if (!_hasSpec || _gradient == null)
		{
			DrawRect(new Rect2(Vector2.Zero, size), UIThemeFactory.PanelBg);
			return;
		}

		DrawTextureRect(_gradient, new Rect2(Vector2.Zero, size), false);
		DrawNebula(size);
		DrawSurveyLines(size);
		DrawStars(size);
		DrawSymbol(size);
		DrawEdgeVignette(size);
		DrawRarityFrame(size);
	}

	private void BindSpec(CardArtworkSpec spec)
	{
		_spec = spec;
		_hasSpec = true;
		var random = new Random(spec.Seed);
		_stars = new StarSample[spec.StarCount * 3];
		for (int i = 0; i < _stars.Length; i++)
		{
			_stars[i] = new StarSample(random);
		}
		(Color BaseColor, Color AccentColor) key = (spec.BaseColor, spec.AccentColor);
		if (!_gradientCache.TryGetValue(key, out _gradient))
		{
			using var image = Image.CreateEmpty(64, 128, false, Image.Format.Rgba8);
			for (int y = 0; y < 128; y++)
			{
				for (int x = 0; x < 64; x++)
				{
					float t = y / 127f;
					float grain = ((x * 73 + y * 37) % 17 - 8) / 2048f;
					Color color = spec.BaseColor.Lerp(new Color("#090c19"), t * 0.65f);
					color = color.Lerp(spec.AccentColor, 0.055f + grain);
					image.SetPixel(x, y, color);
				}
			}
			_gradient = ImageTexture.CreateFromImage(image);
			_gradientCache.Add(key, _gradient);
		}
		_ = GetSoftGlow();
	}

	/// <summary>双焦点柔光与偏心尘带；径向贴图由内存生成并全局复用。</summary>
	private void DrawNebula(Vector2 size)
	{
		float offset = _spec.NebulaOffset;
		Vector2 focus = size * new Vector2(0.28f + offset * 0.15f, 0.28f);
		Vector2 second = size * new Vector2(0.76f, 0.62f - offset * 0.16f);
		DrawGlow(focus, size * new Vector2(1.1f, 1.35f), _spec.AccentColor, 0.24f);
		DrawGlow(second, size * new Vector2(1.0f, 1.1f), new Color("#bd7bde"), 0.2f);
		DrawGlow(size * new Vector2(0.5f, 0.46f), size * 0.92f, _spec.AccentColor, 0.11f);
		for (int i = 0; i < 7; i++)
		{
			Vector2 point = size * new Vector2(0.06f + i * 0.15f, 0.75f - i * 0.075f);
			DrawGlow(point, size * new Vector2(0.32f, 0.22f), _spec.BaseColor, 0.2f);
		}
	}

	private void DrawSurveyLines(Vector2 size)
	{
		Vector2 center = size * new Vector2(0.5f, 0.46f);
		float radius = Mathf.Min(size.X, size.Y) * 0.38f;
		Color ink = new(_spec.AccentColor, 0.15f);
		switch (_spec.Style)
		{
			case ArtworkSymbolStyle.Rune:
				for (int ring = 0; ring < 4; ring++)
				{
					float r = radius * (0.72f + ring * 0.24f);
					DrawArc(center, r, 0f, Mathf.Tau, 96, new Color(ink, 0.08f + ring * 0.018f), 0.6f, true);
					DrawTicks(center, r, _spec.OrbitTickCount, ring * 0.17f, ink, radius * 0.05f);
				}
				for (int i = 0; i < 8; i++)
				{
					var axis = Vector2.FromAngle(i * Mathf.Tau / 8f + _spec.SymbolRotation);
					DrawLine(center + axis * radius * 0.48f, center + axis * radius * 1.6f,
						new Color(ink, 0.09f), 0.6f, true);
				}
				break;
			case ArtworkSymbolStyle.Mecha:
				float step = Mathf.Max(10f, size.X / 12f);
				for (float x = step * 0.5f; x < size.X; x += step)
				{
					DrawLine(new Vector2(x, 0), new Vector2(x, size.Y), new Color(ink, 0.08f), 0.5f);
				}
				for (float y = step * 0.5f; y < size.Y; y += step)
				{
					DrawLine(new Vector2(0, y), new Vector2(size.X, y), new Color(ink, 0.08f), 0.5f);
				}
				for (int i = 0; i < 4; i++)
				{
					float side = (i & 1) == 0 ? -1f : 1f;
					float height = i < 2 ? 0.24f : 0.71f;
					Vector2 start = size * new Vector2(0.5f + side * 0.46f, height);
					Vector2 bend = start + size * new Vector2(-side * 0.14f, 0);
					Vector2 end = bend + size * new Vector2(-side * 0.1f, -0.12f);
					DrawLine(start, bend, ink, 0.9f, true);
					DrawLine(bend, end, ink, 0.9f, true);
					DrawCircle(end, 1.1f, ink);
					for (int mark = 0; mark < 6; mark++)
					{
						Vector2 p = start + new Vector2(-side * mark * 2.5f, 4f);
						DrawLine(p, p + new Vector2(0, mark % 3 == 0 ? 4f : 2f), ink, 0.6f);
					}
				}
				break;
			case ArtworkSymbolStyle.Abstract:
			default:
				int sides = 5 + _spec.SecondaryGeometryIndex % 3;
				for (int ring = 0; ring < 7; ring++)
				{
					DrawRegularOutline(center, radius * (0.55f + ring * 0.17f), sides,
						_spec.SymbolRotation + ring * 0.07f, new Color(ink, 0.05f + ring * 0.012f), 0.65f);
				}
				break;
		}
	}

	private void DrawStars(Vector2 size)
	{
		float scale = Mathf.Clamp(size.X / 140f, 0.65f, 2.2f);
		for (int i = 0; i < _stars.Length; i++)
		{
			StarSample star = _stars[i];
			Vector2 position = star.Position * size;
			bool near = i % 7 == 0;
			float r = star.Radius * scale * (near ? 1.2f : 0.55f);
			Color tint = star.Tint > 0.8f ? UIThemeFactory.StarPink : _spec.AccentColor.Lerp(Colors.White, 0.68f);
			DrawCircle(position, r, new Color(tint, star.Alpha * (near ? 0.85f : 0.6f)));
			if (near && star.Alpha > 0.5f)
			{
				DrawGlow(position, Vector2.One * r * 9f, tint, 0.22f);
				DrawTinyStar(position, r * 2.7f, new Color(tint, star.Alpha * 0.65f));
			}
			if (i % 6 == 0 && i + 6 < _stars.Length)
			{
				Vector2 next = _stars[i + 6].Position * size;
				if (position.DistanceSquaredTo(next) < size.LengthSquared() * 0.13f)
				{
					DrawLine(position, next, new Color(tint, 0.13f), 0.6f, true);
				}
			}
		}
	}

	/// <summary>十二种徽记共用三重轮廓、体积光、内嵌刻度与能量核，主题分别追加星盘/装甲/棱镜。</summary>
	private void DrawSymbol(Vector2 size)
	{
		Vector2 center = size * new Vector2(0.5f, 0.46f);
		float radius = Mathf.Min(size.X, size.Y) * 0.265f;
		float width = Mathf.Max(0.85f, size.X * 0.008f);
		Color color = _spec.AccentColor.Lerp(Colors.White, 0.42f);
		Vector2[] path = _symbolPaths[_spec.SymbolIndex];
		DrawGlow(center, Vector2.One * radius * 3.8f, _spec.AccentColor, 0.28f * _spec.CoreGlowStrength);
		DrawRegularOutline(center, radius * 1.15f, 6 + _spec.SecondaryGeometryIndex,
			-_spec.SymbolRotation, new Color(color, 0.25f), width * 0.6f);

		// 每一片均为独立三角切面，不对凹星形直接填充以避免原生三角剖分歧义。
		for (int i = 0; i < path.Length - 1; i++)
		{
			_triangle[0] = center;
			_triangle[1] = center + path[i].Rotated(_spec.SymbolRotation) * radius;
			_triangle[2] = center + path[i + 1].Rotated(_spec.SymbolRotation) * radius;
			if (Mathf.Abs((_triangle[1] - center).Cross(_triangle[2] - center)) < 0.01f)
			{
				continue;
			}
			DrawColoredPolygon(_triangle, new Color(_spec.AccentColor, i % 2 == 0 ? 0.12f : 0.045f));
		}
		DrawPath(path, center, radius, new Color(0.01f, 0.02f, 0.06f, 0.8f), width * 4f);
		DrawPath(path, center, radius, new Color(_spec.AccentColor, 0.24f), width * 3f);
		DrawPath(path, center, radius, color, width);
		DrawPath(path, center, radius * 0.77f, new Color(color, 0.65f), width * 0.65f);
		DrawPath(path, center + new Vector2(-width * 0.45f, -width * 0.65f), radius,
			new Color(Colors.White, 0.25f), width * 0.45f);
		if (_spec.SymbolIndex == 6)
		{
			for (int i = 0; i < 4; i++)
			{
				var axis = Vector2.FromAngle(i * Mathf.Pi * 0.5f + _spec.SymbolRotation);
				DrawLine(center + axis * radius * 0.48f, center + axis * radius * 1.18f, color, width, true);
				Vector2 cursor = center + axis * radius * 1.18f;
				DrawLine(cursor - axis.Orthogonal() * radius * 0.09f, cursor + axis.Orthogonal() * radius * 0.09f,
					new Color(color, 0.65f), width * 0.7f, true);
			}
		}

		switch (_spec.Style)
		{
			case ArtworkSymbolStyle.Rune:
				DrawArc(center, radius * 1.3f, 0, Mathf.Tau, 96, new Color(color, 0.6f), width * 0.7f, true);
				DrawArc(center, radius * 1.39f, 0, Mathf.Tau, 96, new Color(color, 0.3f), width * 0.45f, true);
				DrawTicks(center, radius * 1.32f, _spec.OrbitTickCount, _spec.SymbolRotation,
					new Color(color, 0.72f), radius * 0.065f);
				for (int i = 0; i < 6; i++)
				{
					Vector2 p = center + Vector2.FromAngle(i * Mathf.Tau / 6f) * radius * 1.39f;
					DrawTinyStar(p, radius * 0.085f, color);
				}
				break;
			case ArtworkSymbolStyle.Mecha:
				DrawArmorPanels(center, radius, color, width);
				break;
			case ArtworkSymbolStyle.Abstract:
			default:
				for (int i = 0; i < 6; i++)
				{
					Vector2 p = center + Vector2.FromAngle(i * Mathf.Tau / 6f + _spec.SymbolRotation) * radius * 1.2f;
					DrawLine(p, center + Vector2.FromAngle((i + 2) * Mathf.Tau / 6f + _spec.SymbolRotation)
						* radius * 1.2f, new Color(color, 0.19f), width * 0.6f, true);
					DrawCircle(p, width * 1.1f, new Color(color, 0.85f));
				}
				break;
		}

		DrawArc(center, radius * 0.25f, 0, Mathf.Tau, 32, new Color(color, 0.75f), width * 0.7f, true);
		DrawGlow(center, Vector2.One * radius * 1.2f, color, 0.55f * _spec.CoreGlowStrength);
		DrawTinyStar(center, radius * 0.14f, new Color(Colors.White, 0.95f));
		DrawCircle(center, width * 1.2f, Colors.White);
	}

	private void DrawArmorPanels(Vector2 center, float radius, Color color, float width)
	{
		for (int i = 0; i < 4; i++)
		{
			float sx = (i & 1) == 0 ? -1f : 1f;
			float sy = i < 2 ? -1f : 1f;
			Vector2 corner = center + new Vector2(sx * radius * 1.35f, sy * radius * 0.95f);
			_panel[0] = corner;
			_panel[1] = corner + new Vector2(-sx * radius * 0.48f, 0);
			_panel[2] = corner + new Vector2(-sx * radius * 0.2f, -sy * radius * 0.19f);
			_panel[3] = corner + new Vector2(0, -sy * radius * 0.38f);
			DrawColoredPolygon(_panel, new Color(color, i % 2 == 0 ? 0.16f : 0.08f));
			DrawLine(_panel[0], _panel[1], new Color(color, 0.9f), width, true);
			DrawLine(_panel[0], _panel[3], new Color(color, 0.6f), width, true);
			for (int notch = 0; notch < 4; notch++)
			{
				Vector2 p = corner + new Vector2(-sx * radius * (0.12f + notch * 0.085f), sy * width * 3f);
				DrawLine(p, p + new Vector2(0, sy * width * 2f), new Color(color, 0.5f), width * 0.6f);
			}
		}
		DrawArc(center, radius * 0.49f, -0.8f, 0.8f, 24, color, width, true);
		DrawArc(center, radius * 0.49f, Mathf.Pi - 0.8f, Mathf.Pi + 0.8f, 24, color, width, true);
	}

	private void DrawRarityFrame(Vector2 size)
	{
		Color glow = _spec.GlowColor;
		if (glow.A < 0.01f)
		{
			return;
		}
		float inset = Mathf.Max(2f, size.X * 0.028f);
		float arm = Mathf.Min(size.X, size.Y) * (0.12f + _spec.CornerOrnamentIndex * 0.012f);
		float cut = arm * 0.42f;
		for (int i = 0; i < 4; i++)
		{
			float sx = (i & 1) == 0 ? 1f : -1f;
			float sy = i < 2 ? 1f : -1f;
			Vector2 corner = new(sx > 0 ? inset : size.X - inset, sy > 0 ? inset : size.Y - inset);
			Vector2 a = corner + new Vector2(sx * arm, 0);
			Vector2 b = corner + new Vector2(sx * cut, 0);
			Vector2 c = corner + new Vector2(0, sy * cut);
			Vector2 d = corner + new Vector2(0, sy * arm);
			DrawGlow(corner, Vector2.One * arm * 3f, glow, glow.A * 0.22f);
			for (int layer = 3; layer >= 1; layer--)
			{
				Color ink = new(glow, layer == 1 ? glow.A : glow.A * 0.08f);
				DrawLine(a, b, ink, layer * 0.85f, true);
				DrawLine(b, c, ink, layer * 0.85f, true);
				DrawLine(c, d, ink, layer * 0.85f, true);
			}
			DrawTinyStar(corner + new Vector2(sx * cut * 0.5f, sy * cut * 0.5f), inset * 0.42f,
				new Color(glow.Lerp(Colors.White, 0.4f), glow.A));
		}
		DrawLine(new Vector2(inset + arm, inset), new Vector2(size.X - inset - arm, inset),
			new Color(glow, glow.A * 0.16f), 0.65f, true);
		DrawLine(new Vector2(inset, size.Y - inset), new Vector2(size.X - inset, size.Y - inset),
			new Color(glow, glow.A * 0.2f), 0.65f, true);
	}

	private void DrawEdgeVignette(Vector2 size)
	{
		for (int i = 0; i < 12; i++)
		{
			float t = i / 12f;
			float height = size.Y * 0.025f;
			DrawRect(new Rect2(0, size.Y - height * (i + 1), size.X, height + 0.5f),
				new Color(0.015f, 0.015f, 0.04f, (1f - t) * 0.56f));
			DrawRect(new Rect2(i, i, Mathf.Max(0, size.X - i * 2f), Mathf.Max(0, size.Y - i * 2f)),
				new Color(0.005f, 0.008f, 0.02f, (1f - t) * 0.17f), false, 1f);
		}
	}

	private void DrawPath(Vector2[] path, Vector2 center, float radius, Color color, float width)
	{
		Span<Vector2> points = stackalloc Vector2[path.Length];
		for (int i = 0; i < path.Length; i++)
		{
			points[i] = center + path[i].Rotated(_spec.SymbolRotation) * radius;
		}
		DrawPolyline(points, color, width, true);
	}

	private void DrawTicks(Vector2 center, float radius, int count, float rotation, Color color, float length)
	{
		for (int i = 0; i < count; i++)
		{
			var axis = Vector2.FromAngle(rotation + i * Mathf.Tau / count);
			float tick = i % 4 == 0 ? length * 1.8f : length;
			DrawLine(center + axis * radius, center + axis * (radius + tick), color, 0.65f, true);
		}
	}

	private void DrawRegularOutline(Vector2 center, float radius, int sides, float rotation, Color color, float width)
	{
		for (int i = 0; i < sides; i++)
		{
			DrawLine(center + Vector2.FromAngle(rotation + i * Mathf.Tau / sides) * radius,
				center + Vector2.FromAngle(rotation + (i + 1) * Mathf.Tau / sides) * radius, color, width, true);
		}
	}

	private void DrawTinyStar(Vector2 center, float radius, Color color)
	{
		DrawLine(center - Vector2.Right * radius, center + Vector2.Right * radius, color, 0.7f, true);
		DrawLine(center - Vector2.Down * radius, center + Vector2.Down * radius, color, 0.7f, true);
	}

	private void DrawGlow(Vector2 center, Vector2 size, Color color, float alpha)
	{
		DrawTextureRect(GetSoftGlow(), new Rect2(center - size * 0.5f, size), false, new Color(color, alpha));
	}

	private static ImageTexture GetSoftGlow()
	{
		if (_softGlow != null)
		{
			return _softGlow;
		}
		using var image = Image.CreateEmpty(128, 128, false, Image.Format.Rgba8);
		for (int y = 0; y < 128; y++)
		{
			for (int x = 0; x < 128; x++)
			{
				float d = new Vector2((x - 63.5f) / 63.5f, (y - 63.5f) / 63.5f).Length();
				float alpha = Mathf.Pow(Mathf.Max(0, 1f - d), 2.4f);
				image.SetPixel(x, y, new Color(1, 1, 1, alpha));
			}
		}
		_softGlow = ImageTexture.CreateFromImage(image);
		return _softGlow;
	}

	/// <summary>归一化轮廓在类初始化时生成一次，末顶点闭合；飞翼、准星使用折线路径。</summary>
	private static Vector2[][] CreateSymbolPaths()
	{
		return new[]
		{
			CreateRadialPath(10, true),
			new[] { new Vector2(0, -1), new Vector2(0.23f, 0.12f), new Vector2(0.55f, 0.25f),
				new Vector2(0.12f, 0.32f), new Vector2(0.12f, 0.88f), new Vector2(0, 1),
				new Vector2(-0.12f, 0.88f), new Vector2(-0.12f, 0.32f), new Vector2(-0.55f, 0.25f),
				new Vector2(-0.23f, 0.12f), new Vector2(0, -1) },
			CreateRadialPath(6, false),
			CreateRadialPath(64, false),
			new[] { new Vector2(0, -1), new Vector2(0.7f, 0), new Vector2(0, 1), new Vector2(-0.7f, 0), new Vector2(0, -1) },
			new[] { new Vector2(0.25f, -1), new Vector2(-0.4f, 0.12f), new Vector2(0.04f, 0.12f),
				new Vector2(-0.25f, 1), new Vector2(0.5f, -0.18f), new Vector2(0.05f, -0.18f), new Vector2(0.25f, -1) },
			CreateRadialPath(48, false),
			CreateGearPath(),
			CreateCurvedPath(false),
			CreateCurvedPath(true),
			CreateRadialPath(3, false),
			new[] { new Vector2(-1, -0.3f), new Vector2(-0.45f, 0.35f), new Vector2(0, -0.08f),
				new Vector2(0.45f, 0.35f), new Vector2(1, -0.3f), new Vector2(0.4f, 0.05f),
				new Vector2(0, -0.3f), new Vector2(-0.4f, 0.05f), new Vector2(-1, -0.3f) },
		};
	}

	private static Vector2[] CreateRadialPath(int count, bool star)
	{
		var points = new Vector2[count + 1];
		for (int i = 0; i < count; i++)
		{
			float r = star && (i & 1) != 0 ? 0.43f : 1f;
			points[i] = Vector2.FromAngle(-Mathf.Pi * 0.5f + i * Mathf.Tau / count) * r;
		}
		points[count] = points[0];
		return points;
	}

	private static Vector2[] CreateGearPath()
	{
		var points = new Vector2[33];
		for (int i = 0; i < 32; i++)
		{
			points[i] = Vector2.FromAngle(i * Mathf.Tau / 32f) * (i % 4 < 2 ? 1f : 0.78f);
		}
		points[32] = points[0];
		return points;
	}

	private static Vector2[] CreateCurvedPath(bool heart)
	{
		var points = new Vector2[65];
		for (int i = 0; i < 64; i++)
		{
			float t = i * Mathf.Tau / 64f;
			float sin = Mathf.Sin(t);
			points[i] = heart
				? new Vector2(16f * sin * sin * sin / 17f,
					-(13f * Mathf.Cos(t) - 5f * Mathf.Cos(2f * t) - 2f * Mathf.Cos(3f * t) - Mathf.Cos(4f * t)) / 17f)
				: new Vector2(Mathf.Cos(t), sin * 0.55f);
		}
		points[64] = points[0];
		return points;
	}
}
