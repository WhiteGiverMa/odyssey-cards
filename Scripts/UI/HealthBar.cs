using System;
using Godot;

namespace OdysseyCards.UI;

public partial class HealthBar : ProgressBar
{
    private Character.Character _target;

    public Character.Character Target => _target;

    private Label _healthLabel;

    public override void _Ready()
    {
        _healthLabel = GetNodeOrNull<Label>("HealthLabel");

        if (_target != null)
        {
            _target.OnHealthChanged += UpdateHealth;
            _target.OnBlockChanged += UpdateBlock;
            UpdateHealth(_target.CurrentHealth, _target.MaxHealth);
            UpdateBlock(_target.Block);
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

    private void UpdateBlock(int block)
    {
    }

    public void SetTarget(Character.Character target)
    {
        if (_target != null)
        {
            _target.OnHealthChanged -= UpdateHealth;
            _target.OnBlockChanged -= UpdateBlock;
        }

        _target = target;

        if (_target != null)
        {
            _target.OnHealthChanged += UpdateHealth;
            _target.OnBlockChanged += UpdateBlock;
            UpdateHealth(_target.CurrentHealth, _target.MaxHealth);
            UpdateBlock(_target.Block);
        }
    }
}
