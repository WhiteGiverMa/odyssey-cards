using System;
using Godot;

namespace OdysseyCards.UI;

public partial class HealthBar : Control
{
    private Character.Character _target;

    public Character.Character Target => _target;

    private ProgressBar _healthBar;
    private Label _healthLabel;
    private Label _blockLabel;

    public override void _Ready()
    {
        _healthBar = GetNode<ProgressBar>("HealthProgress");
        _healthLabel = GetNode<Label>("HealthLabel");
        _blockLabel = GetNode<Label>("BlockLabel");

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
        if (_healthBar != null)
        {
            _healthBar.MaxValue = max;
            _healthBar.Value = current;
        }

        if (_healthLabel != null)
        {
            _healthLabel.Text = $"{current}/{max}";
        }
    }

    private void UpdateBlock(int block)
    {
        if (_blockLabel != null)
        {
            _blockLabel.Text = block > 0 ? $"Block: {block}" : "";
            _blockLabel.Visible = block > 0;
        }
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
