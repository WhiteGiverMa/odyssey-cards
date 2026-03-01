using Godot;
using System.Collections.Generic;

namespace OdysseyCards.Core;

public partial class EnemyDeckData : Resource
{
    [Export] public string EnemyName { get; set; } = "";
    [Export] public int StartingHealth { get; set; } = 8;
    [Export] public int StartingEnergy { get; set; } = 3;
    [Export] public int MaxEnergy { get; set; } = 3;
    [Export] public Godot.Collections.Array<UnitData> Units { get; set; } = new();
    [Export] public Godot.Collections.Array<OrderData> Orders { get; set; } = new();

    public List<Resource> GetAllCards()
    {
        var cards = new List<Resource>();
        if (Units != null)
        {
            cards.AddRange(Units);
        }
        if (Orders != null)
        {
            cards.AddRange(Orders);
        }
        return cards;
    }
}
