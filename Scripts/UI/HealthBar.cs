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
    }

    public void UpdateHealth(int current, int max)
    {
        MaxValue = max;
        Value = current;

        if (_healthLabel != null)
        {
            _healthLabel.Text = $"{current}/{max}";
        }
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
