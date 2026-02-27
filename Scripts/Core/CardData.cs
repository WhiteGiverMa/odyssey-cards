using Godot;
using System;
using System.Collections.Generic;

namespace OdysseyCards.Core;

public enum CardType
{
    Attack,
    Skill,
    Power,
    Curse,
    Status
}

public enum CardRarity
{
    Common,
    Uncommon,
    Rare,
    Legendary
}

public enum CardTarget
{
    None,
    Self,
    SingleEnemy,
    AllEnemies,
    Everyone
}

public partial class CardData : Resource
{
    [Export] public string CardName { get; set; } = "Unnamed Card";
    [Export] public string Description { get; set; } = "";
    [Export] public CardType Type { get; set; } = CardType.Attack;
    [Export] public CardRarity Rarity { get; set; } = CardRarity.Common;
    [Export] public CardTarget Target { get; set; } = CardTarget.SingleEnemy;
    [Export] public int Cost { get; set; } = 1;
    [Export] public Texture2D Artwork { get; set; }
    [Export] public bool Upgraded { get; set; } = false;
    [Export] public bool Exhausts { get; set; } = false;
    [Export] public bool Ethereal { get; set; } = false;
    
    [Export] public Array<CardEffectData> Effects { get; set; }

    public CardData()
    {
        Effects = new Array<CardEffectData>();
    }
}
