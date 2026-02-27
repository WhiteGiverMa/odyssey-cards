using Godot;
using OdysseyCards.Character;

namespace OdysseyCards.UI;

public partial class HealthBar : Control
{
    [Export] public Character Target { get; set; }

    private ProgressBar _healthBar;
    private Label _healthLabel;
    private Label _blockLabel;

    public override void _Ready()
    {
        _healthBar = GetNode<ProgressBar>("HealthProgress");
        _healthLabel = GetNode<Label>("HealthLabel");
        _blockLabel = GetNode<Label>("BlockLabel");

        if (Target != null)
        {
            Target.OnHealthChanged += UpdateHealth;
            Target.OnBlockChanged += UpdateBlock;
            UpdateHealth(Target.CurrentHealth, Target.MaxHealth);
            UpdateBlock(Target.Block);
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

    public void SetTarget(Character target)
    {
        if (Target != null)
        {
            Target.OnHealthChanged -= UpdateHealth;
            Target.OnBlockChanged -= UpdateBlock;
        }

        Target = target;

        if (Target != null)
        {
            Target.OnHealthChanged += UpdateHealth;
            Target.OnBlockChanged += UpdateBlock;
            UpdateHealth(Target.CurrentHealth, Target.MaxHealth);
            UpdateBlock(Target.Block);
        }
    }
}
