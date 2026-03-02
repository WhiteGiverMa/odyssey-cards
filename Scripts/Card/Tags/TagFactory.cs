using OdysseyCards.Core;

namespace OdysseyCards.Card.Tags;

public static class TagFactory
{
    public static TagDefinition? CreateTag(CardTag tag)
    {
        return tag switch
        {
            CardTag.None => null,
            CardTag.Blitz => new BlitzTag(),
            CardTag.Maneuver => new ManeuverTag(),
            CardTag.Rotation => new RotationTag(),
            CardTag.Fury => new FuryTag(),
            CardTag.Guard => new GuardTag(),
            CardTag.LastWords => new LastWordsTag(),
            CardTag.Deploy => new DeployTag(),
            CardTag.Defense => new DefenseTag(),
            CardTag.Ambush => new AmbushTag(),
            CardTag.Impact => new ImpactTag(),
            CardTag.Immune => new ImmuneTag(),
            CardTag.Pin => new PinTag(),
            CardTag.Suppress => new SuppressTag(),
            CardTag.Massive => new MassiveTag(),
            CardTag.Infiltrate => new InfiltrateTag(),
            _ => null
        };
    }
}
