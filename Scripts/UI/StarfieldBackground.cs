#nullable enable
using Godot;
using System;
using System.Collections.Generic;

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

	private sealed class Meteor
	{
		public Vector2 From, To;
		public float T, Duration;
	}

	private Star[] _stars = Array.Empty<Star>();
	private readonly List<Meteor> _meteors = new();
	private readonly Random _meteorRng = new();
	private float _time;
	private float _nextMeteorAt = 2.5f;

	private static readonly Color SkyTop = new("#1c1930");
	private static readonly Color SkyBottom = new("#12101f");
	private static ImageTexture? _bgTexture;

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

	public override void _Process(double delta)
	{
		// 编辑器预览保持静态星空，不启动动画与流星
		if (Engine.IsEditorHint())
			return;

		_time += (float)delta;

		if (EnableMeteors && _time >= _nextMeteorAt)
		{
			SpawnMeteor();
			_nextMeteorAt = _time + 4f + (float)_meteorRng.NextDouble() * 7f;
		}

		for (int i = _meteors.Count - 1; i >= 0; i--)
		{
			_meteors[i].T += (float)delta / _meteors[i].Duration;
			if (_meteors[i].T >= 1f)
				_meteors.RemoveAt(i);
		}

		QueueRedraw();
	}

	public override void _Draw()
	{
		var size = Size;
		if (size.X < 1f || size.Y < 1f)
			return;

		DrawTextureRect(GetBgTexture(), new Rect2(Vector2.Zero, size), false);

		if (CenterGlow)
			DrawCenterGlow(size);

		DrawStars(size);
		DrawMeteors();
	}

	// ===== 分层绘制 =====

	private void DrawStars(Vector2 size)
	{
		foreach (var star in _stars)
		{
			// 视差漂移：近层快、远层慢，水平向右循环
			float drift = _time * (1.5f + star.Layer * 2.5f);
			float x = Mathf.PosMod(star.X * size.X + drift, size.X);
			float y = star.Y * size.Y;
			float alpha = 0.3f + 0.5f * (0.5f + 0.5f * Mathf.Sin(_time * star.BlinkSpeed + star.Phase));

			// 85% 白星，15% 星粉点缀
			var c = star.Layer == 2 && star.Phase > Mathf.Pi
				? new Color(1f, 0.62f, 0.82f, alpha)
				: new Color(1f, 1f, 1f, alpha);
			DrawCircle(new Vector2(x, y), star.Size, c);
		}
	}

	private void DrawMeteors()
	{
		foreach (var meteor in _meteors)
		{
			var head = meteor.From.Lerp(meteor.To, meteor.T);
			var tail = meteor.From.Lerp(meteor.To, Mathf.Max(0f, meteor.T - 0.12f));
			float fade = 1f - meteor.T * meteor.T; // 尾段渐隐
			DrawLine(tail, head, new Color(1f, 1f, 1f, 0.85f * fade), 2f, true);
			DrawCircle(head, 2.2f, new Color(1f, 1f, 1f, 0.9f * fade));
		}
	}

	private void DrawCenterGlow(Vector2 size)
	{
		var center = new Vector2(size.X * 0.5f, size.Y * 0.52f);
		float maxR = Mathf.Min(size.X, size.Y) * 0.55f;
		// 同心圆递减 alpha 模拟径向渐变
		for (int i = 10; i >= 1; i--)
		{
			float t = i / 10f;
			DrawCircle(center, maxR * t, new Color(GlowColor, 0.016f * (1f - t) + 0.004f));
		}
	}

	private void SpawnMeteor()
	{
		var size = Size;
		if (size.X < 1f || size.Y < 1f)
			return;

		float startX = (float)_meteorRng.NextDouble() * size.X * 0.7f;
		float startY = (float)_meteorRng.NextDouble() * size.Y * 0.3f;
		float length = size.X * (0.25f + (float)_meteorRng.NextDouble() * 0.3f);
		// 斜向右下划过
		_meteors.Add(new Meteor
		{
			From = new Vector2(startX, startY),
			To = new Vector2(startX + length, startY + length * 0.45f),
			T = 0f,
			Duration = 0.9f + (float)_meteorRng.NextDouble() * 0.5f,
		});
	}

	private static ImageTexture GetBgTexture()
	{
		if (_bgTexture != null)
			return _bgTexture;

		// 星云底纹：低频单噪声调制垂直渐变——既根除 8bit 色阶断层（噪声天然
		// 打散量化边界），又让深空有星云状的明暗流动，而非死板渐变。
		var noise = new FastNoiseLite
		{
			NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth,
			Frequency = 0.012f,
			FractalOctaves = 3,
			Seed = 7,
		};
		var img = Image.CreateEmpty(256, 256, false, Image.Format.Rgba8);
		for (int y = 0; y < 256; y++)
		{
			float vertical = y / 255f;
			for (int x = 0; x < 256; x++)
			{
				float n = noise.GetNoise2D(x, y) * 0.5f + 0.5f; // 0..1
				float mix = Mathf.Clamp(vertical * 0.62f + n * 0.38f, 0f, 1f);
				var c = SkyTop.Lerp(SkyBottom, Mathf.SmoothStep(0f, 1f, mix));
				img.SetPixel(x, y, c);
			}
		}
		_bgTexture = ImageTexture.CreateFromImage(img);
		return _bgTexture;
	}
}
