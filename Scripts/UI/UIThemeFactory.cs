using Godot;

namespace OdysseyCards.UI;

/// <summary>
/// 星途主题工厂——全局共享 Theme 与色板的唯一真源。
/// 0 美术资产：按钮/面板样式全部由 StyleBoxFlat 程序化描述。
/// 色板调整只改这里，全场景生效。
/// </summary>
public static class UIThemeFactory
{
	// ===== 星途色板 =====

	/// <summary>深空底（场景背景）。</summary>
	public static readonly Color SpaceBg = new("#12101f");

	/// <summary>面板底（卡片/弹窗）。</summary>
	public static readonly Color PanelBg = new("#1c1930");

	/// <summary>按钮常态底。</summary>
	public static readonly Color ButtonBg = new("#252040");

	/// <summary>按钮悬停底。</summary>
	public static readonly Color ButtonHoverBg = new("#2e2852");

	/// <summary>按钮按压底。</summary>
	public static readonly Color ButtonPressedBg = new("#1a1730");

	/// <summary>按钮禁用底。</summary>
	public static readonly Color ButtonDisabledBg = new("#181528");

	/// <summary>常规描边。</summary>
	public static readonly Color BorderNormal = new("#3d3660");

	/// <summary>星粉（主强调色）。</summary>
	public static readonly Color StarPink = new("#ff9ed2");

	/// <summary>青金（次强调色/玩家阵营）。</summary>
	public static readonly Color CyanGold = new("#7fd8ff");

	/// <summary>暖金（警示/特殊）。</summary>
	public static readonly Color WarmGold = new("#ffd98e");

	/// <summary>绯红（敌方阵营/危险）。</summary>
	public static readonly Color Crimson = new("#ff6b7a");

	/// <summary>亮文字。</summary>
	public static readonly Color TextBright = new("#f0f0e8");

	/// <summary>暗文字。</summary>
	public static readonly Color TextDim = new("#9a95b8");

	private static Theme? _shared;

	/// <summary>
	/// 获取全局共享主题（懒构建，单例）。
	/// 覆盖 Button 五态、PanelContainer、Label 字色规范。
	/// </summary>
	public static Theme GetSharedTheme()
	{
		if (_shared != null)
			return _shared;

		var theme = new Theme();

		// ===== Button =====
		theme.SetStylebox("normal", "Button", CreateButtonStyle(ButtonBg, BorderNormal));
		theme.SetStylebox("hover", "Button", CreateButtonStyle(ButtonHoverBg, CyanGold));
		theme.SetStylebox("pressed", "Button", CreateButtonStyle(ButtonPressedBg, StarPink));
		theme.SetStylebox("disabled", "Button", CreateButtonStyle(ButtonDisabledBg, new Color(BorderNormal, 0.4f)));
		theme.SetStylebox("focus", "Button", CreateFocusStyle());

		theme.SetColor("font_color", "Button", TextBright);
		theme.SetColor("font_hover_color", "Button", Colors.White);
		theme.SetColor("font_pressed_color", "Button", StarPink);
		theme.SetColor("font_disabled_color", "Button", new Color(TextDim, 0.55f));
		theme.SetColor("font_focus_color", "Button", Colors.White);

		// ===== PanelContainer =====
		theme.SetStylebox("panel", "PanelContainer", CreatePanelStyle(PanelBg, BorderNormal));
		theme.SetStylebox("panel", "Panel", CreatePanelStyle(PanelBg, BorderNormal));

		// ===== Label =====
		theme.SetColor("font_color", "Label", TextBright);

		// ===== AcceptDialog / 弹窗 =====
		theme.SetStylebox("panel", "AcceptDialog", CreatePanelStyle(PanelBg, BorderNormal, 10, 2));

		_shared = theme;
		return _shared;
	}

	/// <summary>测试挂钩：清空共享主题缓存。</summary>
	public static void ResetSharedTheme() => _shared = null;

	/// <summary>
	/// 创建按钮样式——圆角 + 描边 + 内边距。
	/// </summary>
	public static StyleBoxFlat CreateButtonStyle(Color bg, Color border, int radius = 8, int borderWidth = 1)
	{
		return new StyleBoxFlat
		{
			BgColor = bg,
			BorderColor = border,
			BorderWidthLeft = borderWidth,
			BorderWidthRight = borderWidth,
			BorderWidthTop = borderWidth,
			BorderWidthBottom = borderWidth,
			CornerRadiusTopLeft = radius,
			CornerRadiusTopRight = radius,
			CornerRadiusBottomLeft = radius,
			CornerRadiusBottomRight = radius,
			ContentMarginLeft = 16,
			ContentMarginRight = 16,
			ContentMarginTop = 8,
			ContentMarginBottom = 8,
		};
	}

	/// <summary>
	/// 创建面板样式——圆角 + 细边 + 内容边距。
	/// </summary>
	public static StyleBoxFlat CreatePanelStyle(Color bg, Color border, int radius = 10, int borderWidth = 1)
	{
		return new StyleBoxFlat
		{
			BgColor = bg,
			BorderColor = border,
			BorderWidthLeft = borderWidth,
			BorderWidthRight = borderWidth,
			BorderWidthTop = borderWidth,
			BorderWidthBottom = borderWidth,
			CornerRadiusTopLeft = radius,
			CornerRadiusTopRight = radius,
			CornerRadiusBottomLeft = radius,
			CornerRadiusBottomRight = radius,
			ContentMarginLeft = 12,
			ContentMarginRight = 12,
			ContentMarginTop = 10,
			ContentMarginBottom = 10,
		};
	}

	/// <summary>焦点框——青金 2px 描边透明底。</summary>
	private static StyleBoxFlat CreateFocusStyle()
	{
		return new StyleBoxFlat
		{
			BgColor = new Color(0, 0, 0, 0),
			BorderColor = new Color(CyanGold, 0.7f),
			BorderWidthLeft = 2,
			BorderWidthRight = 2,
			BorderWidthTop = 2,
			BorderWidthBottom = 2,
			CornerRadiusTopLeft = 8,
			CornerRadiusTopRight = 8,
			CornerRadiusBottomLeft = 8,
			CornerRadiusBottomRight = 8,
			DrawCenter = false,
		};
	}
}
