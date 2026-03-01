using OdysseyCards.Character;

namespace OdysseyCards.Card.Tags;

public class TagContext
{
    public Unit Unit { get; set; }
    public Character.Character Target { get; set; }
    public int TagCount { get; set; } = 1;
    public int Value { get; set; } = 0;

    public TagContext(Unit unit, Character.Character target = null, int tagCount = 1, int value = 0)
    {
        Unit = unit;
        Target = target;
        TagCount = tagCount;
        Value = value;
    }
}
