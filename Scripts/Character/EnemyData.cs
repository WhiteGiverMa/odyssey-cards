using System;
using Godot;

namespace OdysseyCards.Character;

public partial class EnemyData : Resource
{
    [Export] public string CharacterName { get; set; } = "Unnamed";

    [Export] public int MaxHealth { get; set; } = 100;

    [Export] public int MaxEnergy { get; set; } = 3;

    [Export] public string IntentDisplayName { get; set; } = string.Empty;

    [Export] public Godot.Collections.Array<EnemyAction> Actions { get; set; } = new();
}
