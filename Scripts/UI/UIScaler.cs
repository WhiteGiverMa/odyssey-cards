using System;
using System.Collections.Generic;
using Godot;
using OdysseyCards.Core;

namespace OdysseyCards.UI
{
	public partial class UIScaler : Node
	{
		public static UIScaler Instance { get; private set; }

		/// <summary>
		/// 是否为移动平台（Android/iOS）。用于守卫桌面端特有的 DisplayServer 窗口调用。
		/// </summary>
		public static bool IsMobile => OS.HasFeature("mobile");

		/// <summary>
		/// 设计基准分辨率 — TODO: 统一改为 1600×900
		/// </summary>
		private const float _designWidth = 1152f;
		private const float _designHeight = 648f;
		private const float _cardWidthRatio = 5f / 7f;
		private Vector2 _currentCardSize = new(175, 245);
		private const string ConfigPath = "user://settings.cfg";

		/// <summary>预设分辨率列表（从高到低）</summary>
		private static readonly (int Width, int Height)[] PresetResolutions = new[]
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
		/// <summary>随从部署动画速度倍率（实际时长 = 基础时长 / 速度，1.0=正常，2.0=两倍速）</summary>
		public float DeployAnimationSpeed { get; private set; } = 1.0f;

		public override void _Ready()
		{
			Instance = this;
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
			return Mathf.RoundToInt(baseSize * CurrentScale);
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

		public float GetNodeSize(float containerHeight)
		{
			return containerHeight / 8f;
		}

		#region 窗口管理

		/// <summary>
		/// 获取当前屏幕支持的预设分辨率列表，自动过滤超出屏幕尺寸的选项
		/// </summary>
		public List<(int Width, int Height, string Label)> GetSupportedResolutions()
		{
			Vector2I screenSize = DisplayServer.ScreenGetSize();
			var result = new List<(int, int, string)>();

			foreach (var (w, h) in PresetResolutions)
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
				return;

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
		public Vector2I GetCurrentWindowSize()
		{
			return IsMobile
				? DisplayServer.ScreenGetSize()
				: DisplayServer.WindowGetSize();
		}

		/// <summary>
		/// 获取匹配预设分辨率的索引，未找到则返回 -1
		/// </summary>
		public int FindResolutionIndex(int width, int height)
		{
			for (int i = 0; i < PresetResolutions.Length; i++)
			{
				if (PresetResolutions[i].Width == width && PresetResolutions[i].Height == height)
					return i;
			}
			return -1;
		}

		/// <summary>
		/// 获取匹配预设分辨率的实际索引（在过滤后列表中的位置），未找到则返回 0
		/// </summary>
		public int GetCurrentResolutionFilteredIndex()
		{
			Vector2I currentSize = GetCurrentWindowSize();
			var supported = GetSupportedResolutions();

			for (int i = 0; i < supported.Count; i++)
			{
				if (supported[i].Width == currentSize.X && supported[i].Height == currentSize.Y)
					return i;
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
				return;

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
			}

			SaveSettings();
			UpdateScale();
		}

		/// <summary>
		/// 获取当前窗口模式索引：0=窗口, 1=无边框, 2=全屏（移动端始终返回 1=全屏）。
		/// </summary>
		public int GetCurrentWindowModeIndex()
		{
			if (IsMobile)
				return 1; // 移动端始终全屏

			if (DisplayServer.WindowGetMode() == DisplayServer.WindowMode.Fullscreen
				|| DisplayServer.WindowGetMode() == DisplayServer.WindowMode.ExclusiveFullscreen)
				return 2;

			if (DisplayServer.WindowGetFlag(DisplayServer.WindowFlags.Borderless))
				return 1;

			return 0;
		}

		private void CenterWindow()
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
				return;
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
				GameManager.Instance.EcchiTextEnabled = enabled;
			OnEcchiTextChanged?.Invoke();
		}

		/// <summary>涩情文案切换事件——EventUI 等订阅用于实时刷新显示文本。</summary>
		public event Action? OnEcchiTextChanged;

		/// <summary>设置弹道特效缩放并持久化（范围 0.0~5.0）。</summary>
		public void SetProjectileScale(float scale)
		{
			scale = Mathf.Clamp(scale, 0.0f, 5.0f);
			if (Mathf.Abs(ProjectileScale - scale) < 0.001f)
				return;
			ProjectileScale = scale;
			SaveSettings();
			OnParticleEffectSettingsChanged?.Invoke();
		}

		/// <summary>设置卡牌飞行粒子缩放并持久化（范围 0.0~5.0）。</summary>
		public void SetCardFlyScale(float scale)
		{
			scale = Mathf.Clamp(scale, 0.0f, 5.0f);
			if (Mathf.Abs(CardFlyScale - scale) < 0.001f)
				return;
			CardFlyScale = scale;
			SaveSettings();
			OnParticleEffectSettingsChanged?.Invoke();
		}

		/// <summary>设置伤害数字缩放并持久化（范围 0.0~5.0）。</summary>
		public void SetDamageNumberScale(float scale)
		{
			scale = Mathf.Clamp(scale, 0.0f, 5.0f);
			if (Mathf.Abs(DamageNumberScale - scale) < 0.001f)
				return;
			DamageNumberScale = scale;
			SaveSettings();
			OnParticleEffectSettingsChanged?.Invoke();
		}

		/// <summary>设置表情文字缩放并持久化（范围 0.0~5.0）。</summary>
		public void SetEmoteScale(float scale)
		{
			scale = Mathf.Clamp(scale, 0.0f, 5.0f);
			if (Mathf.Abs(EmoteScale - scale) < 0.001f)
				return;
			EmoteScale = scale;
			SaveSettings();
			OnParticleEffectSettingsChanged?.Invoke();
		}

		/// <summary>设置随从部署动画速度倍率并持久化（范围 0.1~3.0，实际时长=基础时长/速度）。</summary>
		public void SetDeployAnimationSpeed(float speed)
		{
			speed = Mathf.Clamp(speed, 0.1f, 3.0f);
			if (Mathf.Abs(DeployAnimationSpeed - speed) < 0.001f)
				return;
			DeployAnimationSpeed = speed;
			SaveSettings();
			OnParticleEffectSettingsChanged?.Invoke();
		}

		/// <summary>设置稀有度颜色方案并持久化（0=经典, 1=新版）。</summary>
		public void SetRarityColorScheme(int schemeIndex)
		{
			schemeIndex = Mathf.Clamp(schemeIndex, 0, RarityColorScheme.SchemeCount - 1);
			if (RarityColorSchemeIndex == schemeIndex)
				return;
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
			config.SetValue("visual", "projectile_scale", ProjectileScale);
			config.SetValue("visual", "card_fly_scale", CardFlyScale);
			config.SetValue("visual", "damage_number_scale", DamageNumberScale);
			config.SetValue("visual", "emote_scale", EmoteScale);
			config.SetValue("visual", "deploy_animation_speed", DeployAnimationSpeed);
			config.SetValue("visual", "rarity_color_scheme", RarityColorSchemeIndex);

			Error err = config.Save(ConfigPath);
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
			Error err = config.Load(ConfigPath);
			if (err != Error.Ok)
			{
				// 首次启动：使用 project.godot 默认值，居中窗口
				GD.Print("[UIScaler] 未找到配置文件，使用默认设置");
				if (!IsMobile)
					CenterWindow();
				return;
			}

			IntentIconFloatingEnabled = config.GetValue("visual", "intent_icon_floating", true).AsBool();
			IntentValueFloatingEnabled = config.GetValue("visual", "intent_value_floating", true).AsBool();
			IntentTooltipShowAll = config.GetValue("visual", "intent_tooltip_show_all", false).AsBool();
			CardDescriptionCentered = config.GetValue("visual", "card_description_centered", false).AsBool();
			DevModeEnabled = config.GetValue("visual", "dev_mode", false).AsBool();
			EcchiTextEnabled = config.GetValue("visual", "ecchi_text", false).AsBool();

			// 持久化恢复后同步到 GameManager（Roguelike 层读取入口）
			if (GameManager.Instance != null)
				GameManager.Instance.EcchiTextEnabled = EcchiTextEnabled;
			ProjectileScale = (float)config.GetValue("visual", "projectile_scale", 1.0).AsDouble();
			CardFlyScale = (float)config.GetValue("visual", "card_fly_scale", 1.0).AsDouble();
			DamageNumberScale = (float)config.GetValue("visual", "damage_number_scale", 1.0).AsDouble();
			EmoteScale = (float)config.GetValue("visual", "emote_scale", 1.0).AsDouble();
			DeployAnimationSpeed = (float)config.GetValue("visual", "deploy_animation_speed", 1.0).AsDouble();
			RarityColorSchemeIndex = (int)config.GetValue("visual", "rarity_color_scheme", 0).AsInt32();

			if (IsMobile)
				return;

			int width = (int)config.GetValue("display", "window_width", 1600).AsInt32();
			int height = (int)config.GetValue("display", "window_height", 900).AsInt32();
			int windowMode = (int)config.GetValue("display", "window_mode", 0).AsInt32();

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
}
