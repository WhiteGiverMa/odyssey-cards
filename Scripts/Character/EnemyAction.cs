using Godot;

namespace OdysseyCards.Character;

public enum ActionType
{
    Attack,
    Defend,
    Buff,
    Debuff,
    Special
}

public partial class EnemyAction : Resource
{
    [Export] public ActionType Type { get; set; }
    [Export] public int Value { get; set; }
    [Export] public string Description { get; set; }
    [Export] public int Hits { get; set; } = 1;
}
