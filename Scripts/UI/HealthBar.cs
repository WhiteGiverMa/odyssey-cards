using System;
using Godot;
using OdysseyCards.Character;

namespace OdysseyCards.UI;

public partial class HealthBar : ProgressBar
{
    private ICommander _target;

    public ICommander Target => _target;

    private Label _healthLabel;

    public override void _Ready()
    {
        _healthLabel = GetNodeOrNull<Label>("HealthLabel");

        if (_target != null)
        {
            _target.HQ.OnHealthChanged += UpdateHealth;
            UpdateHealth(_target.HQ.CurrentHealth, _target.HQ.MaxHealth);
        }
    }

    private void UpdateHealth(int current, int max)
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
        if (_target != null && _target.HQ != null)
        {
            _target.HQ.OnHealthChanged -= UpdateHealth;
        }

        _target = target;

        if (_target != null && _target.HQ != null)
        {
            _target.HQ.OnHealthChanged += UpdateHealth;
            UpdateHealth(_target.HQ.CurrentHealth, _target.HQ.MaxHealth);
        }
    }
}
