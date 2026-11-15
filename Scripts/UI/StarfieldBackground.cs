#nullable enable
using System;
using Godot;

namespace OdysseyCards.UI;

/// <summary>
/// 星空背景——可复用的程序化深空背景组件。
/// 垂直渐变底 + 三层视差星点（漂移 + 闪烁）+ 偶发流星 + 可选中央光晕。
/// 主菜单（密星）、地图（中密度）、战斗（稀疏 + 战场光晕）共用调参。
/// 全部 _Draw 绘制，零图片资产；星点一次数组预生成，每帧仅重绘。
/// </summary>
[Tool]
public partial class StarfieldBackground : Control
{
	/// <summary>星点总数（三层合计）。</summary>
	[Export] public int StarCount { get; set; } = 120;

	/// <summary>是否启用流星。</summary>
	[Export] public bool EnableMeteors { get; set; } = true;

	/// <summary>是否绘制中央微光晕（战斗场景用）。</summary>
	[Export] public bool CenterGlow { get; set; }

	/// <summary>中央光晕颜色。</summary>
	[Export] public Color GlowColor { get; set; } = new("#7fd8ff");

	private struct Star
	{
		public float X, Y;
		public float Size;
		public float Phase;
		public float BlinkSpeed;
		public int Layer; // 0远 1中 2近
	}

	private struct Meteor
	{
		public Vector2 From, To;
		public float T, Duration;
		public bool Active;
	}

	private Star[] _stars = Array.Empty<Star>();
	private readonly Meteor[] _meteors = new Meteor[4];
	private readonly Random _meteorRng = new();
	private float _time;
	private float _nextMeteorAt = 2.5f;
	private float _redrawElapsed;

	private static readonly Color _skyTop = new("#1c1930");
	private static readonly Color _skyBottom = new("#12101f");
	private static ImageTexture? _bgTexture;
	private static ImageTexture? _glowTexture;

	/// <summary>无参构造保证编辑器中也能正确穿透输入。</summary>
	public StarfieldBackground()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		ClipContents = true;
	}

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		// 固定种子：星空布局跨会话稳定，避免每次进场景星星乱跳
		var rng = new Random(20260721);
		_stars = new Star[Mathf.Max(0, StarCount)];
		for (int i = 0; i < _stars.Length; i++)
		{
			int layer = rng.Next(3);
			_stars[i] = new Star
			{
				X = (float)rng.NextDouble(),
				Y = (float)rng.NextDouble(),
				Size = 0.8f + layer * 0.55f + (float)rng.NextDouble() * 0.7f,
				Phase = (float)rng.NextDouble() * Mathf.Tau,
				BlinkSpeed = 0.6f + (float)rng.NextDouble() * 1.8f,
				Layer = layer,
			};
		}
		QueueRedraw();
	}

	public override void _Notification(int what)
	{
		if (what == NotificationResized)
		{
			QueueRedraw();
		}
	}

	public override void _Process(double delta)
	{
		// 编辑器预览保持静态星空，不启动动画与流星
		if (Engine.IsEditorHint() || !IsVisibleInTree())
		{
			return;
		}

		_time += (float)delta;

		if (EnableMeteors && _time >= _nextMeteorAt)
		{
			SpawnMeteor();
			_nextMeteorAt = _time + 4f + (float)_meteorRng.NextDouble() * 7f;
		}

		for (int i = 0; i < _meteors.Length; i++)
		{
			if (!_meteors[i].Active)
			{
				continue;
			}
			_meteors[i].T += (float)delta / _meteors[i].Duration;
			if (_meteors[i].T >= 1f)
			{
				_meteors[i].Active = false;
			}
		}

		_redrawElapsed += (float)delta;
		if (_redrawElapsed >= 1f / 30f)
		{
			_redrawElapsed = 0;
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

		DrawTextureRect(GetBgTexture(), new Rect2(Vector2.Zero, size), false);

		if (CenterGlow)
		{
			DrawCenterGlow(size);
		}

		DrawStars(size);
		DrawConstellations(size);
		DrawMeteors();
	}

	// ===== 分层绘制 =====

	private void DrawStars(Vector2 size)
	{
		foreach (Star star in _stars)
		{
			// 视差漂移：近层快、远层慢，水平向右循环
			float drift = _time * (1.5f + star.Layer * 2.5f);
			float x = Mathf.PosMod(star.X * size.X + drift, size.X);
			float y = star.Y * size.Y;
			float alpha = 0.3f + 0.5f * (0.5f + 0.5f * Mathf.Sin(_time * star.BlinkSpeed + star.Phase));

			// 85% 白星，15% 星粉点缀
			Color c = star.Layer == 2 && star.Phase > Mathf.Pi
				? new Color(1f, 0.62f, 0.82f, alpha)
				: new Color(1f, 1f, 1f, alpha);
			DrawCircle(new Vector2(x, y), star.Size, c);
			if (star.Layer == 2 && star.Phase < 1.2f)
			{
				Vector2 position = new(x, y);
				DrawCircle(position, star.Size * 2.5f, new Color(c, alpha * 0.05f));
				DrawLine(position - new Vector2(star.Size * 2.5f, 0), position + new Vector2(star.Size * 2.5f, 0),
					new Color(c, alpha * 0.45f), 0.65f, true);
				DrawLine(position - new Vector2(0, star.Size * 3.5f), position + new Vector2(0, star.Size * 3.5f),
					new Color(c, alpha * 0.45f), 0.65f, true);
				DrawCircle(position, star.Size * 0.4f, new Color(Colors.White, alpha));
			}
		}
	}

	private void DrawConstellations(Vector2 size)
	{
		for (int i = 0; i + 1 < _stars.Length; i += 11)
		{
			Star a = _stars[i];
			Star b = _stars[i + 1];
			if (a.Layer != b.Layer)
			{
				continue;
			}
			float drift = _time * (1.5f + a.Layer * 2.5f);
			Vector2 from = new(Mathf.PosMod(a.X * size.X + drift, size.X), a.Y * size.Y);
			Vector2 to = new(Mathf.PosMod(b.X * size.X + drift, size.X), b.Y * size.Y);
			if (from.DistanceSquaredTo(to) < size.LengthSquared() * 0.02f)
			{
				DrawLine(from, to, new Color(0.5f, 0.75f, 1f, 0.065f), 0.65f, true);
			}
		}
	}

	private void DrawMeteors()
	{
		foreach (Meteor meteor in _meteors)
		{
			if (!meteor.Active)
			{
				continue;
			}
			Vector2 head = meteor.From.Lerp(meteor.To, meteor.T);
			Vector2 tail = meteor.From.Lerp(meteor.To, Mathf.Max(0f, meteor.T - 0.12f));
			float fade = 1f - meteor.T * meteor.T; // 尾段渐隐
			DrawLine(tail, head, new Color(1f, 1f, 1f, 0.85f * fade), 2f, true);
			DrawLine(tail, head, new Color(0.5f, 0.85f, 1f, 0.08f * fade), 6f, true);
			DrawCircle(head, 2.2f, new Color(1f, 1f, 1f, 0.9f * fade));
		}
	}

	private void DrawCenterGlow(Vector2 size)
	{
		var center = new Vector2(size.X * 0.5f, size.Y * 0.52f);
		Vector2 extent = size * new Vector2(0.95f, 1.15f);
		DrawTextureRect(GetGlowTexture(), new Rect2(center - extent * 0.5f, extent), false, new Color(GlowColor, 0.13f));
		Vector2 second = size * new Vector2(0.72f, 0.32f);
		DrawTextureRect(GetGlowTexture(), new Rect2(second - extent * 0.3f, extent * 0.6f), false,
			new Color(UIThemeFactory.StarPink, 0.055f));
	}

	private void SpawnMeteor()
	{
		Vector2 size = Size;
		if (size.X < 1f || size.Y < 1f)
		{
			return;
		}

		float startX = (float)_meteorRng.NextDouble() * size.X * 0.7f;
		float startY = (float)_meteorRng.NextDouble() * size.Y * 0.3f;
		float length = size.X * (0.25f + (float)_meteorRng.NextDouble() * 0.3f);
		// 斜向右下划过
		int index = Array.FindIndex(_meteors, static meteor => !meteor.Active);
		if (index < 0)
		{
			return;
		}
		_meteors[index] = new Meteor
		{
			From = new Vector2(startX, startY),
			To = new Vector2(startX + length, startY + length * 0.45f),
			T = 0f,
			Duration = 0.9f + (float)_meteorRng.NextDouble() * 0.5f,
			Active = true,
		};
	}

	private static ImageTexture GetBgTexture()
	{
		if (_bgTexture != null)
		{
			return _bgTexture;
		}

		// 星云底纹：低频单噪声调制垂直渐变——既根除 8bit 色阶断层（噪声天然
		// 打散量化边界），又让深空有星云状的明暗流动，而非死板渐变。
		using var noise = new FastNoiseLite
		{
			NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth,
			Frequency = 0.012f,
			FractalOctaves = 3,
			Seed = 7,
		};
		using var img = Image.CreateEmpty(384, 256, false, Image.Format.Rgba8);
		for (int y = 0; y < 256; y++)
		{
			float vertical = y / 255f;
			for (int x = 0; x < 384; x++)
			{
				float n = noise.GetNoise2D(x, y) * 0.5f + 0.5f; // 0..1
				float fine = noise.GetNoise2D(x * 3.5f + 130f, y * 3.5f - 60f) * 0.5f + 0.5f;
				float mix = Mathf.Clamp(vertical * 0.62f + n * 0.38f, 0f, 1f);
				Color c = _skyTop.Lerp(_skyBottom, Mathf.SmoothStep(0f, 1f, mix));
				float ribbon = Mathf.Exp(-Mathf.Pow((y - 160f + x * 0.17f + (n - 0.5f) * 65f) / 58f, 2f));
				c = c.Lerp(new Color("#57406f"), ribbon * n * 0.24f);
				c = c.Lerp(new Color("#315e72"), fine * n * 0.09f);
				float vignette = new Vector2((x - 192f) / 240f, (y - 128f) / 165f).LengthSquared();
				c = c.Darkened(Mathf.Clamp(vignette * 0.14f, 0, 0.3f));
				img.SetPixel(x, y, c);
			}
		}
		_bgTexture = ImageTexture.CreateFromImage(img);
		return _bgTexture;
	}

	private static ImageTexture GetGlowTexture()
	{
		if (_glowTexture != null)
		{
			return _glowTexture;
		}
		using var image = Image.CreateEmpty(96, 96, false, Image.Format.Rgba8);
		for (int y = 0; y < 96; y++)
		{
			for (int x = 0; x < 96; x++)
			{
				float radius = new Vector2((x - 47.5f) / 47.5f, (y - 47.5f) / 47.5f).Length();
				image.SetPixel(x, y, new Color(1, 1, 1, Mathf.Pow(Mathf.Max(0, 1f - radius), 2.5f)));
			}
		}
		_glowTexture = ImageTexture.CreateFromImage(image);
		return _glowTexture;
	}
}
