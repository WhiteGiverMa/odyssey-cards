using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Godot;
using OdysseyCards.Core;

namespace OdysseyCards.UI;

/// <summary>统一 1600×900 逻辑设计基准及显示设置的自动加载缩放服务。</summary>
public partial class UIScaler : Node
{
	public static UIScaler Instance { get; private set; }

	/// <summary>
	/// 是否为移动平台（Android/iOS）。用于守卫桌面端特有的 DisplayServer 窗口调用。
	/// </summary>
	public static bool IsMobile => OS.HasFeature("mobile");

	/// <summary>
	/// 统一设计基准分辨率；1600×900 逻辑视口下缩放系数为 1。
	/// </summary>
	private const float _designWidth = 1600f;
	private const float _designHeight = 900f;
	private const float _cardWidthRatio = 5f / 7f;
	private Vector2 _currentCardSize = new(175, 245);
	private const string _configPath = "user://settings.cfg";

	/// <summary>
	/// 战斗动画速度默认倍率。低于 1.0 让亮牌/部署/敌方回合步进更从容可读。
	/// 持久化键已升级为 "combat_animation_speed"：旧配置里存过 1.0 的玩家也会拿到新的慢速默认值。
	/// </summary>
	public const float DefaultDeployAnimationSpeed = 0.75f;
	public const float MinDeployAnimationSpeed = 0.1f;
	public const float MaxDeployAnimationSpeed = 3.0f;

	/// <summary>界面字体缩放默认倍率——比旧版放大 20%，并清晰易读。</summary>
	public const float DefaultFontScale = 1.20f;
	public const float MinFontScale = 0.8f;
	public const float MaxFontScale = 1.8f;

	/// <summary>预设分辨率列表（从高到低）</summary>
	private static readonly (int Width, int Height)[] _presetResolutions = new[]
	{
		(3840, 2160),
		(2560, 1440),
		(1920, 1080),
		(1600, 900),
		(1280, 720),
		(1152, 648),
	};

	public event Action OnResolutionChanged;
	public event Action OnIntentVisualSettingsChanged;
	public event Action OnIntentTooltipModeChanged;
	public event Action OnCardDescriptionSettingsChanged;
	public event Action OnParticleEffectSettingsChanged;
	public event Action OnRarityColorSchemeChanged;
	public event Action? OnEmojiIconsChanged;
	/// <summary>界面字体缩放变化——各 UI 表面据此刷新字号与布局。</summary>
	public event Action? OnFontScaleChanged;

	public float CurrentScale { get; private set; } = 1f;
	public Vector2 CurrentCardSize => _currentCardSize;
	public bool IntentIconFloatingEnabled { get; private set; } = true;
	public bool IntentValueFloatingEnabled { get; private set; } = true;
	/// <summary>意图 tooltip 模式：false=每个意图单独显示（默认，参考 STS2），true=悬停任意图标显示全部意图。</summary>
	public bool IntentTooltipShowAll { get; private set; }
	public bool CardDescriptionCentered { get; private set; }
	public bool DevModeEnabled { get; private set; }
	/// <summary>涩情文案开关（仅存档需持久化，运行时由 GameManager 同步给 EventData 读取）。</summary>
	public bool EcchiTextEnabled { get; private set; }
	/// <summary>Emoji 图标对照模式；默认关闭，使用纯色色块与简字。</summary>
	public bool EmojiIconsEnabled { get; private set; }

	/// <summary>稀有度颜色方案索引：0=经典（金/银/铜），1=新版（紫/蓝/绿）。</summary>
	public int RarityColorSchemeIndex { get; private set; }

	/// <summary>弹道特效缩放（攻击/法术/战斗弹道的粒子半径、尾迹、弧高）</summary>
	public float ProjectileScale { get; private set; } = 1.0f;
	/// <summary>卡牌飞行粒子缩放（抽牌/弃牌/领域/死亡飞行的尾迹粒子大小）</summary>
	public float CardFlyScale { get; private set; } = 1.0f;
	/// <summary>伤害数字缩放（浮动伤害/治疗/护甲数字的字号和浮动距离）</summary>
	public float DamageNumberScale { get; private set; } = 1.0f;
	/// <summary>表情文字缩放（浮动表情弹幕的字号和浮动距离）</summary>
	public float EmoteScale { get; private set; } = 1.0f;
	/// <summary>随从部署/亮牌等战斗演出速度倍率（实际时长 = 基础时长 / 速度，0.75=默认从容，2.0=两倍速）</summary>
	public float DeployAnimationSpeed { get; private set; } = DefaultDeployAnimationSpeed;
	/// <summary>界面字体缩放倍率（仅界面文字；卡面内部排版仍按卡框自身尺寸自适应）</summary>
	public float FontScale { get; private set; } = DefaultFontScale;

	public override void _Ready()
	{
		Instance = this;
		UiSymbolMode.Enabled = EmojiIconsEnabled;
		GetTree().Root.SizeChanged += OnWindowSizeChanged;

		LoadSettings();

		// 移动端全屏运行，不需要窗口管理 / 分辨率设置持久化
		if (IsMobile)
		{
			GD.Print("[UIScaler] 移动平台 — 跳过窗口管理");
			UpdateScale();
		}
		else
		{
			UpdateScale();
		}
	}

	private void OnWindowSizeChanged()
	{
		UpdateScale();
	}

	private void UpdateScale()
	{
		Vector2 viewportSize = GetViewport().GetVisibleRect().Size;
		float widthRatio = viewportSize.X / _designWidth;
		float heightRatio = viewportSize.Y / _designHeight;
		CurrentScale = Mathf.Min(widthRatio, heightRatio);
		_currentCardSize = GetCardSize();
		OnResolutionChanged?.Invoke();
	}

	public float GetScaleFactor()
	{
		return CurrentScale;
	}

	public int GetScaledFontSize(int baseSize)
	{
		return Mathf.Max(8, Mathf.RoundToInt(baseSize * CurrentScale * FontScale));
	}

	/// <summary>按当前界面字体倍率缩放字号；供各 UI 控件一行调用。</summary>
	public int ScaleFont(int baseSize) => ScaleFontSize(baseSize, FontScale);

	/// <summary>
	/// 纯计算：把基础字号按倍率缩放并夹紧到可读下限与可调范围。
	/// 无 <see cref="Instance"/> 时（编辑器预览/单测）回退到 <see cref="DefaultFontScale"/>。
	/// </summary>
	public static int ApplyFontScale(int baseSize)
		=> ScaleFontSize(baseSize, Instance?.FontScale ?? DefaultFontScale);

	/// <summary>纯计算：字号缩放公式（倍率夹紧 0.8~1.8，结果不小于 8px）。</summary>
	public static int ScaleFontSize(int baseSize, float fontScale)
	{
		float clamped = Mathf.Clamp(fontScale, MinFontScale, MaxFontScale);
		return Mathf.Max(8, Mathf.RoundToInt(baseSize * clamped));
	}

	public Vector2 GetScaledSize(Vector2 baseSize)
	{
		return baseSize * CurrentScale;
	}

	public Vector2 GetCardSize()
	{
		Vector2 viewportSize = GetViewport().GetVisibleRect().Size;
		float cardHeight = viewportSize.Y * 0.35f;
		float cardWidth = cardHeight * _cardWidthRatio;
		return new Vector2(cardWidth, cardHeight);
	}

	[SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "保留自动加载实例的既有公开接口，调用方不在本次美术升级范围内。")]
	public float GetNodeSize(float containerHeight)
	{
		return containerHeight / 8f;
	}

	#region 窗口管理

	/// <summary>
	/// 获取当前屏幕支持的预设分辨率列表，自动过滤超出屏幕尺寸的选项
	/// </summary>
	[SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "保留自动加载实例的既有公开接口，调用方不在本次美术升级范围内。")]
	public List<(int Width, int Height, string Label)> GetSupportedResolutions()
	{
		Vector2I screenSize = DisplayServer.ScreenGetSize();
		var result = new List<(int, int, string)>();

		foreach ((int w, int h) in _presetResolutions)
		{
			if (w <= screenSize.X && h <= screenSize.Y)
			{
				result.Add((w, h, $"{w} × {h}"));
			}
		}

		return result;
	}

	/// <summary>
	/// 设置窗口分辨率（仅桌面窗口模式有效，移动端无操作）。
	/// </summary>
	public void SetWindowResolution(int width, int height)
	{
		if (IsMobile)
		{
			return;
		}

		DisplayServer.WindowSetSize(new Vector2I(width, height));

		// 窗口模式下居中
		Vector2I screenSize = DisplayServer.ScreenGetSize();
		Vector2I windowPos = (screenSize - new Vector2I(width, height)) / 2;
		DisplayServer.WindowSetPosition(windowPos);

		SaveSettings();
		UpdateScale();
	}

	/// <summary>
	/// 获取当前窗口尺寸（移动端返回屏幕尺寸）。
	/// </summary>
	[SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "保留自动加载实例的既有公开接口，调用方不在本次美术升级范围内。")]
	public Vector2I GetCurrentWindowSize()
	{
		return IsMobile
			? DisplayServer.ScreenGetSize()
			: DisplayServer.WindowGetSize();
	}

	/// <summary>
	/// 获取匹配预设分辨率的索引，未找到则返回 -1
	/// </summary>
	[SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "保留自动加载实例的既有公开接口，调用方不在本次美术升级范围内。")]
	public int FindResolutionIndex(int width, int height)
	{
		for (int i = 0; i < _presetResolutions.Length; i++)
		{
			if (_presetResolutions[i].Width == width && _presetResolutions[i].Height == height)
			{
				return i;
			}
		}
		return -1;
	}

	/// <summary>
	/// 获取匹配预设分辨率的实际索引（在过滤后列表中的位置），未找到则返回 0
	/// </summary>
	public int GetCurrentResolutionFilteredIndex()
	{
		Vector2I currentSize = GetCurrentWindowSize();
		List<(int Width, int Height, string Label)> supported = GetSupportedResolutions();

		for (int i = 0; i < supported.Count; i++)
		{
			if (supported[i].Width == currentSize.X && supported[i].Height == currentSize.Y)
			{
				return i;
			}
		}

		// 返回最接近的匹配
		return 0;
	}

	/// <summary>
	/// 设置窗口模式（仅桌面端有效，移动端无操作）。
	/// 0 = 窗口模式, 1 = 无边框全屏, 2 = 全屏
	/// </summary>
	public void SetWindowModeIndex(int modeIndex)
	{
		if (IsMobile)
		{
			return;
		}

		switch (modeIndex)
		{
			case 0: // 窗口
				DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.Borderless, false);
				DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
				CenterWindow();
				break;

			case 1: // 无边框全屏
				DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
				DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.Borderless, true);
				Vector2I screenSize = DisplayServer.ScreenGetSize();
				DisplayServer.WindowSetSize(screenSize);
				DisplayServer.WindowSetPosition(Vector2I.Zero);
				break;

			case 2: // 全屏
				DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.Borderless, false);
				DisplayServer.WindowSetMode(DisplayServer.WindowMode.Fullscreen);
				break;
			default:
				throw new ArgumentOutOfRangeException(nameof(modeIndex));
		}

		SaveSettings();
		UpdateScale();
	}

	/// <summary>
	/// 获取当前窗口模式索引：0=窗口, 1=无边框, 2=全屏（移动端始终返回 1=全屏）。
	/// </summary>
	[SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "保留自动加载实例的既有公开接口，调用方不在本次美术升级范围内。")]
	public int GetCurrentWindowModeIndex()
	{
		if (IsMobile)
		{
			return 1; // 移动端始终全屏
		}

		if (DisplayServer.WindowGetMode() == DisplayServer.WindowMode.Fullscreen
			|| DisplayServer.WindowGetMode() == DisplayServer.WindowMode.ExclusiveFullscreen)
		{
			return 2;
		}

		if (DisplayServer.WindowGetFlag(DisplayServer.WindowFlags.Borderless))
		{
			return 1;
		}

		return 0;
	}

	private static void CenterWindow()
	{
		Vector2I windowSize = DisplayServer.WindowGetSize();
		Vector2I screenSize = DisplayServer.ScreenGetSize();
		DisplayServer.WindowSetPosition((screenSize - windowSize) / 2);
	}

	#endregion

	#region 视觉风格设置

	/// <summary>
	/// 设置敌方意图图标的视觉浮动行为，并持久化到 user://settings.cfg。
	/// </summary>
	public void SetIntentVisualFloating(bool iconFloating, bool valueFloating)
	{
		IntentIconFloatingEnabled = iconFloating;
		IntentValueFloatingEnabled = valueFloating;
		SaveSettings();
		OnIntentVisualSettingsChanged?.Invoke();
	}

	/// <summary>
	/// 设置意图 tooltip 显示模式并持久化。
	/// false=每个意图单独显示（默认），true=悬停任意图标显示全部意图。
	/// </summary>
	public void SetIntentTooltipMode(bool showAll)
	{
		if (IntentTooltipShowAll == showAll)
		{
			return;
		}

		IntentTooltipShowAll = showAll;
		SaveSettings();
		OnIntentTooltipModeChanged?.Invoke();
	}

	/// <summary>
	/// 设置卡牌描述文本是否居中，并持久化到 user://settings.cfg。
	/// </summary>
	public void SetCardDescriptionCentered(bool centered)
	{
		if (CardDescriptionCentered == centered)
		{
			return;
		}

		CardDescriptionCentered = centered;
		SaveSettings();
		OnCardDescriptionSettingsChanged?.Invoke();
	}

	/// <summary>设置开发者模式并持久化到 user://settings.cfg。</summary>
	public void SetDevMode(bool enabled)
	{
		if (DevModeEnabled == enabled)
		{
			return;
		}

		DevModeEnabled = enabled;
		SaveSettings();
	}

	/// <summary>设置涩情文案模式并持久化到 user://settings.cfg。</summary>
	public void SetEcchiText(bool enabled)
	{
		if (EcchiTextEnabled == enabled)
		{
			return;
		}

		EcchiTextEnabled = enabled;
		SaveSettings();
		// 同步到 GameManager 供 Roguelike 层读取
		if (GameManager.Instance != null)
		{
			GameManager.Instance.EcchiTextEnabled = enabled;
		}

		OnEcchiTextChanged?.Invoke();
	}

	/// <summary>涩情文案切换事件——EventUI 等订阅用于实时刷新显示文本。</summary>
	public event Action? OnEcchiTextChanged;

	/// <summary>切换 Emoji 图标对照模式，并同步纯 C# UI 符号真源。</summary>
	public void SetEmojiIconsEnabled(bool enabled)
	{
		if (EmojiIconsEnabled == enabled)
		{
			UiSymbolMode.Enabled = enabled;
			return;
		}

		EmojiIconsEnabled = enabled;
		UiSymbolMode.Enabled = enabled;
		SaveSettings();
		OnEmojiIconsChanged?.Invoke();
	}

	/// <summary>设置弹道特效缩放并持久化（范围 0.0~5.0）。</summary>
	public void SetProjectileScale(float scale)
	{
		scale = Mathf.Clamp(scale, 0.0f, 5.0f);
		if (Mathf.Abs(ProjectileScale - scale) < 0.001f)
		{
			return;
		}

		ProjectileScale = scale;
		SaveSettings();
		OnParticleEffectSettingsChanged?.Invoke();
	}

	/// <summary>设置卡牌飞行粒子缩放并持久化（范围 0.0~5.0）。</summary>
	public void SetCardFlyScale(float scale)
	{
		scale = Mathf.Clamp(scale, 0.0f, 5.0f);
		if (Mathf.Abs(CardFlyScale - scale) < 0.001f)
		{
			return;
		}

		CardFlyScale = scale;
		SaveSettings();
		OnParticleEffectSettingsChanged?.Invoke();
	}

	/// <summary>设置伤害数字缩放并持久化（范围 0.0~5.0）。</summary>
	public void SetDamageNumberScale(float scale)
	{
		scale = Mathf.Clamp(scale, 0.0f, 5.0f);
		if (Mathf.Abs(DamageNumberScale - scale) < 0.001f)
		{
			return;
		}

		DamageNumberScale = scale;
		SaveSettings();
		OnParticleEffectSettingsChanged?.Invoke();
	}

	/// <summary>设置表情文字缩放并持久化（范围 0.0~5.0）。</summary>
	public void SetEmoteScale(float scale)
	{
		scale = Mathf.Clamp(scale, 0.0f, 5.0f);
		if (Mathf.Abs(EmoteScale - scale) < 0.001f)
		{
			return;
		}

		EmoteScale = scale;
		SaveSettings();
		OnParticleEffectSettingsChanged?.Invoke();
	}

	/// <summary>设置战斗演出速度倍率并持久化（实际时长=基础时长/速度）。</summary>
	public void SetDeployAnimationSpeed(float speed)
	{
		speed = Mathf.Clamp(speed, MinDeployAnimationSpeed, MaxDeployAnimationSpeed);
		if (Mathf.Abs(DeployAnimationSpeed - speed) < 0.001f)
		{
			return;
		}

		DeployAnimationSpeed = speed;
		SaveSettings();
		OnParticleEffectSettingsChanged?.Invoke();
	}

	/// <summary>设置界面字体缩放倍率并持久化（范围 0.8~1.8，默认 1.20）。</summary>
	public void SetFontScale(float scale)
	{
		scale = Mathf.Clamp(scale, MinFontScale, MaxFontScale);
		if (Mathf.Abs(FontScale - scale) < 0.001f)
		{
			return;
		}

		FontScale = scale;
		SaveSettings();
		// 共享 Theme 原地改字号：所有未写死 AddThemeFontSizeOverride 的控件全自动实时刷新。
		UIThemeFactory.RefreshFontSizes();
		OnFontScaleChanged?.Invoke();
	}

	/// <summary>设置稀有度颜色方案并持久化（0=经典, 1=新版）。</summary>
	public void SetRarityColorScheme(int schemeIndex)
	{
		schemeIndex = Mathf.Clamp(schemeIndex, 0, RarityColorScheme.SchemeCount - 1);
		if (RarityColorSchemeIndex == schemeIndex)
		{
			return;
		}

		RarityColorSchemeIndex = schemeIndex;
		SaveSettings();
		OnRarityColorSchemeChanged?.Invoke();
	}

	#endregion

	#region 持久化

	/// <summary>
	/// 保存当前显示设置到 user://settings.cfg（移动端跳过窗口尺寸保存）。
	/// </summary>
	public void SaveSettings()
	{
		using var config = new ConfigFile();

		if (!IsMobile)
		{
			Vector2I windowSize = DisplayServer.WindowGetSize();
			int windowMode = GetCurrentWindowModeIndex();
			config.SetValue("display", "window_width", windowSize.X);
			config.SetValue("display", "window_height", windowSize.Y);
			config.SetValue("display", "window_mode", windowMode);
		}

		config.SetValue("visual", "intent_icon_floating", IntentIconFloatingEnabled);
		config.SetValue("visual", "intent_value_floating", IntentValueFloatingEnabled);
		config.SetValue("visual", "intent_tooltip_show_all", IntentTooltipShowAll);
		config.SetValue("visual", "card_description_centered", CardDescriptionCentered);
		config.SetValue("visual", "dev_mode", DevModeEnabled);
		config.SetValue("visual", "ecchi_text", EcchiTextEnabled);
		config.SetValue("visual", "emoji_icons", EmojiIconsEnabled);
		config.SetValue("visual", "projectile_scale", ProjectileScale);
		config.SetValue("visual", "card_fly_scale", CardFlyScale);
		config.SetValue("visual", "damage_number_scale", DamageNumberScale);
		config.SetValue("visual", "emote_scale", EmoteScale);
		config.SetValue("visual", "combat_animation_speed", DeployAnimationSpeed);
		config.SetValue("visual", "font_scale", FontScale);
		config.SetValue("visual", "rarity_color_scheme", RarityColorSchemeIndex);

		Error err = config.Save(_configPath);
		if (err != Error.Ok)
		{
			GD.PushWarning($"[UIScaler] 保存设置失败: {err}");
		}
	}

	/// <summary>
	/// 从 user://settings.cfg 加载并应用显示设置（移动端跳过）。
	/// 首次启动时文件不存在，使用 project.godot 中的默认值。
	/// </summary>
	private void LoadSettings()
	{
		using var config = new ConfigFile();
		Error err = config.Load(_configPath);
		if (err != Error.Ok)
		{
			// 首次启动：使用 project.godot 默认值，居中窗口
			GD.Print("[UIScaler] 未找到配置文件，使用默认设置");
			if (!IsMobile)
			{
				CenterWindow();
			}

			return;
		}

		IntentIconFloatingEnabled = config.GetValue("visual", "intent_icon_floating", true).AsBool();
		IntentValueFloatingEnabled = config.GetValue("visual", "intent_value_floating", true).AsBool();
		IntentTooltipShowAll = config.GetValue("visual", "intent_tooltip_show_all", false).AsBool();
		CardDescriptionCentered = config.GetValue("visual", "card_description_centered", false).AsBool();
		DevModeEnabled = config.GetValue("visual", "dev_mode", false).AsBool();
		EcchiTextEnabled = config.GetValue("visual", "ecchi_text", false).AsBool();
		EmojiIconsEnabled = config.GetValue("visual", "emoji_icons", false).AsBool();
		UiSymbolMode.Enabled = EmojiIconsEnabled;

		// 持久化恢复后同步到 GameManager（Roguelike 层读取入口）
		if (GameManager.Instance != null)
		{
			GameManager.Instance.EcchiTextEnabled = EcchiTextEnabled;
		}

		ProjectileScale = (float)config.GetValue("visual", "projectile_scale", 1.0).AsDouble();
		CardFlyScale = (float)config.GetValue("visual", "card_fly_scale", 1.0).AsDouble();
		DamageNumberScale = (float)config.GetValue("visual", "damage_number_scale", 1.0).AsDouble();
		EmoteScale = (float)config.GetValue("visual", "emote_scale", 1.0).AsDouble();
		DeployAnimationSpeed = Mathf.Clamp(
			(float)config.GetValue("visual", "combat_animation_speed", DefaultDeployAnimationSpeed).AsDouble(),
			MinDeployAnimationSpeed, MaxDeployAnimationSpeed);
		FontScale = Mathf.Clamp(
			(float)config.GetValue("visual", "font_scale", DefaultFontScale).AsDouble(),
			MinFontScale, MaxFontScale);
		RarityColorSchemeIndex = config.GetValue("visual", "rarity_color_scheme", 0).AsInt32();

		if (IsMobile)
		{
			return;
		}

		int width = config.GetValue("display", "window_width", 1600).AsInt32();
		int height = config.GetValue("display", "window_height", 900).AsInt32();
		int windowMode = config.GetValue("display", "window_mode", 0).AsInt32();

		GD.Print($"[UIScaler] 加载设置: {width}x{height}, 模式={windowMode}");

		// 先设置模式
		if (windowMode == 1)
		{
			// 无边框全屏
			DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
			DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.Borderless, true);
			Vector2I screenSize = DisplayServer.ScreenGetSize();
			DisplayServer.WindowSetSize(screenSize);
			DisplayServer.WindowSetPosition(Vector2I.Zero);
		}
		else if (windowMode == 2)
		{
			// 全屏
			DisplayServer.WindowSetMode(DisplayServer.WindowMode.Fullscreen);
		}
		else
		{
			// 窗口模式
			DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
			DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.Borderless, false);
			DisplayServer.WindowSetSize(new Vector2I(width, height));
			CenterWindow();
		}
	}

	#endregion
}
