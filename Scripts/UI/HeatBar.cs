using Godot;
using OdysseyCards.Heat;

namespace OdysseyCards.UI;

/// <summary>
/// 热力值 UI 条——显示在战斗界面全局位置。
/// 显示当前热力值百分比和进度条。
/// </summary>
public partial class HeatBar : HBoxContainer
{
	private HeatSystem? _heat;

	private Label? _label;
	private ProgressBar? _bar;

	public HeatBar()
	{
		MouseFilter = MouseFilterEnum.Ignore;
	}

	public override void _Ready()
	{
		EnsureControls();
	}

	/// <summary>
	/// 绑定热力值系统并刷新显示。
	/// </summary>
	public void Bind(HeatSystem heat)
	{
		_heat = heat;
		EnsureControls();
		Refresh();
	}

	/// <summary>
	/// 刷新显示。
	/// </summary>
	public void Refresh()
	{
		if (_heat == null || _label == null || _bar == null) return;

		float pct = _heat.CurrentHeat * 100f;
		_label.Text = $"热力 {pct:F0}%";
		_bar.Value = pct;

		// 根据热力值级别变色
		if (_heat.CurrentHeat < 0.4f)
			_bar.Modulate = new Color(0.3f, 0.8f, 1.0f); // 蓝（低温）
		else if (_heat.CurrentHeat < 1.2f)
			_bar.Modulate = new Color(1.0f, 0.7f, 0.2f); // 橙（正常）
		else
			_bar.Modulate = new Color(1.0f, 0.2f, 0.2f); // 红（过热）
	}

	private void EnsureControls()
	{
		if (_label != null) return;

		_label = new Label();
		_label.AddThemeFontSizeOverride("font_size", 12);
		AddChild(_label);

		_bar = new ProgressBar();
		_bar.CustomMinimumSize = new Vector2(100, 12);
		_bar.MinValue = 0;
		_bar.MaxValue = 300;
		_bar.ShowPercentage = false;
		AddChild(_bar);
	}
}
