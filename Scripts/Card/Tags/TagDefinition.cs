namespace OdysseyCards.Card.Tags;

public enum TagTriggerType
{
    OnDeploy,
    OnDeath,
    OnAttack,
    OnMove,
    OnTurnStart,
    OnTurnEnd,
    OnCardPlayed,
    Passive
}

public abstract class TagDefinition
{
    public abstract string Name { get; }
    public abstract string Description { get; }
    public abstract TagTriggerType TriggerType { get; }

    public virtual void OnDeploy(TagContext context) { }
    public virtual void OnDeath(TagContext context) { }
    public virtual void OnAttack(TagContext context) { }
    public virtual void OnMove(TagContext context) { }
    public virtual void OnTurnStart(TagContext context) { }
    public virtual void OnTurnEnd(TagContext context) { }
    public virtual void OnCardPlayed(TagContext context) { }
    public virtual void ApplyPassiveEffect(TagContext context) { }
}
