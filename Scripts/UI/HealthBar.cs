using Godot;
using OdysseyCards.Character;

namespace OdysseyCards.UI;

/// <summary>
/// 生命值条组件。
/// 显示指挥官的当前生命值和最大生命值。
/// </summary>
public partial class HealthBar : ProgressBar
{
	private ICommander _target;
	public ICommander Target => _target;

	private Label _healthLabel;

	public override void _Ready()
	{
		_healthLabel = GetNodeOrNull<Label>("HealthLabel");

		// 如果场景中没有预设 HealthLabel 子节点，程序化创建一个
		if (_healthLabel == null)
		{
			_healthLabel = new Label
			{
				Name = "HealthLabel",
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center,
				AnchorRight = 1.0f,
				AnchorBottom = 1.0f,
				OffsetLeft = 0,
				OffsetTop = 0,
				OffsetRight = 0,
				OffsetBottom = 0,
				MouseFilter = MouseFilterEnum.Ignore,
			};
			_healthLabel.AddThemeColorOverride("font_color", new Color(1f, 1f, 1f));
			_healthLabel.AddThemeFontSizeOverride("font_size", 11);
			AddChild(_healthLabel);
		}
	}

	public void UpdateHealth(int current, int max)
	{
		MaxValue = max;
		Value = current;

		// 程序化创建时可能还没有进入场景树，_Ready 尚未执行，兜底创建标签
		if (_healthLabel == null)
		{
			_healthLabel = new Label
			{
				Name = "HealthLabel",
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center,
				AnchorRight = 1.0f,
				AnchorBottom = 1.0f,
				OffsetLeft = 0,
				OffsetTop = 0,
				OffsetRight = 0,
				OffsetBottom = 0,
				MouseFilter = MouseFilterEnum.Ignore,
			};
			_healthLabel.AddThemeColorOverride("font_color", new Color(1f, 1f, 1f));
			_healthLabel.AddThemeFontSizeOverride("font_size", 11);
			AddChild(_healthLabel);
		}

		_healthLabel.Text = $"{current}/{max}";
	}

	public void SetTarget(ICommander target)
	{
		_target = target;
		if (_target != null)
		{
			UpdateHealth(_target.CurrentHealth, _target.MaxHealth);
		}
	}
}
