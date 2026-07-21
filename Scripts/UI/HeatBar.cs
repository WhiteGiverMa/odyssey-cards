using Godot;
using OdysseyCards.Heat;

namespace OdysseyCards.UI;

/// <summary>
/// 热力值 UI 条——显示在战斗界面法力区域。
/// 文本标签 + HeatBarFill 自绘火焰渐变条（低温青金 / 正常暖橙 / 过热绯红脉动）。
/// </summary>
public partial class HeatBar : HBoxContainer
{
	private HeatSystem? _heat;

	private Label? _label;
	private HeatBarFill? _fill;

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
		if (_heat == null || _label == null || _fill == null)
			return;

		float pct = _heat.CurrentHeat * 100f;
		_label.Text = $"热力 {pct:F0}%";
		_fill.SetHeat(_heat.CurrentHeat);
	}

	private void EnsureControls()
	{
		if (_label != null)
			return;

		_label = new Label();
		_label.AddThemeFontSizeOverride("font_size", 12);
		AddChild(_label);

		_fill = new HeatBarFill();
		AddChild(_fill);
	}
}
