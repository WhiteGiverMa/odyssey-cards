namespace OdysseyCards.AI;

public enum AIActionType
{
    None,
    DeployUnit,
    MoveUnit,
    AttackWithUnit,
    PlayOrder,
    EndTurn
}

public class AIAction
{
    public AIActionType Type { get; set; }
    public Card.Card Card { get; set; }
    public Card.Unit Unit { get; set; }
    public int TargetNodeId { get; set; } = -1;

    public AIAction()
    {
        Type = AIActionType.None;
    }

    public static AIAction None()
    {
        return new AIAction { Type = AIActionType.None };
    }

    public static AIAction DeployUnit(Card.Unit unit)
    {
        return new AIAction
        {
            Type = AIActionType.DeployUnit,
            Unit = unit,
            Card = unit
        };
    }

    public static AIAction MoveUnit(Card.Unit unit, int targetNodeId)
    {
        return new AIAction
        {
            Type = AIActionType.MoveUnit,
            Unit = unit,
            TargetNodeId = targetNodeId
        };
    }

    public static AIAction AttackWithUnit(Card.Unit unit, int targetNodeId)
    {
        return new AIAction
        {
            Type = AIActionType.AttackWithUnit,
            Unit = unit,
            TargetNodeId = targetNodeId
        };
    }

    public static AIAction PlayOrder(Card.Order order)
    {
        return new AIAction
        {
            Type = AIActionType.PlayOrder,
            Card = order
        };
    }

    public static AIAction EndTurn()
    {
        return new AIAction { Type = AIActionType.EndTurn };
    }

    public bool IsValid()
    {
        return Type != AIActionType.None;
    }
}
