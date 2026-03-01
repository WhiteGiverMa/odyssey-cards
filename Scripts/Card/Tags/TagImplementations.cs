namespace OdysseyCards.Card.Tags;

public class BlitzTag : TagDefinition
{
    public override string Name => "闪击";
    public override string Description => "部署后可立即行动";
    public override TagTriggerType TriggerType => TagTriggerType.OnDeploy;

    public override void OnDeploy(TagContext context)
    {
        if (context.Unit != null)
        {
            context.Unit.CanActThisTurn = true;
        }
    }
}

public class ManeuverTag : TagDefinition
{
    public override string Name => "机动";
    public override string Description => "每层额外行动1次";
    public override TagTriggerType TriggerType => TagTriggerType.Passive;

    public override void ApplyPassiveEffect(TagContext context)
    {
        if (context.Unit != null)
        {
            context.Unit.AdditionalActions = context.TagCount;
        }
    }
}

public class RotationTag : TagDefinition
{
    public override string Name => "轮战";
    public override string Description => "返回抽牌堆随机位置";
    public override TagTriggerType TriggerType => TagTriggerType.OnDeath;

    public override void OnDeath(TagContext context)
    {
        if (context.Unit != null)
        {
            context.Unit.ShouldReturnToDeck = true;
        }
    }
}

public class FuryTag : TagDefinition
{
    public override string Name => "奋战";
    public override string Description => "每回合可攻击两次";
    public override TagTriggerType TriggerType => TagTriggerType.Passive;

    public override void ApplyPassiveEffect(TagContext context)
    {
        if (context.Unit != null)
        {
            context.Unit.MaxAttacksPerTurn = 2;
        }
    }
}

public class GuardTag : TagDefinition
{
    public override string Name => "守护";
    public override string Description => "保护距离内的友方单位";
    public override TagTriggerType TriggerType => TagTriggerType.Passive;

    public override void ApplyPassiveEffect(TagContext context)
    {
        if (context.Unit != null)
        {
            context.Unit.GuardRange = context.TagCount;
        }
    }
}

public class LastWordsTag : TagDefinition
{
    public override string Name => "亡语";
    public override string Description => "阵亡时触发效果";
    public override TagTriggerType TriggerType => TagTriggerType.OnDeath;
}

public class DeployTag : TagDefinition
{
    public override string Name => "部署";
    public override string Description => "部署时触发效果";
    public override TagTriggerType TriggerType => TagTriggerType.OnDeploy;
}

public class DefenseTag : TagDefinition
{
    public override string Name => "防御";
    public override string Description => "每层减免1点伤害";
    public override TagTriggerType TriggerType => TagTriggerType.Passive;

    public override void ApplyPassiveEffect(TagContext context)
    {
        if (context.Unit != null)
        {
            context.Unit.Defense = context.TagCount;
        }
    }
}

public class AmbushTag : TagDefinition
{
    public override string Name => "伏击";
    public override string Description => "首次被攻击时先造成反击伤害";
    public override TagTriggerType TriggerType => TagTriggerType.Passive;

    public override void ApplyPassiveEffect(TagContext context)
    {
        if (context.Unit != null)
        {
            context.Unit.HasAmbush = true;
        }
    }
}

public class ImpactTag : TagDefinition
{
    public override string Name => "冲击";
    public override string Description => "首次攻击不受到反击伤害";
    public override TagTriggerType TriggerType => TagTriggerType.Passive;

    public override void ApplyPassiveEffect(TagContext context)
    {
        if (context.Unit != null)
        {
            context.Unit.HasImpact = true;
        }
    }
}

public class ImmuneTag : TagDefinition
{
    public override string Name => "免疫";
    public override string Description => "不会受到伤害";
    public override TagTriggerType TriggerType => TagTriggerType.Passive;

    public override void ApplyPassiveEffect(TagContext context)
    {
        if (context.Unit != null)
        {
            context.Unit.IsImmune = true;
        }
    }
}

public class PinTag : TagDefinition
{
    public override string Name => "压制";
    public override string Description => "无法移动或攻击，下回合开始移除";
    public override TagTriggerType TriggerType => TagTriggerType.Passive;

    public override void ApplyPassiveEffect(TagContext context)
    {
        if (context.Unit != null)
        {
            context.Unit.IsPinned = true;
        }
    }
}

public class SuppressTag : TagDefinition
{
    public override string Name => "抑制";
    public override string Description => "失去所有关键词和效果";
    public override TagTriggerType TriggerType => TagTriggerType.Passive;

    public override void ApplyPassiveEffect(TagContext context)
    {
        if (context.Unit != null)
        {
            context.Unit.IsSuppressed = true;
        }
    }
}

public class MassiveTag : TagDefinition
{
    public override string Name => "断流";
    public override string Description => "无法与其他单位处于同一节点";
    public override TagTriggerType TriggerType => TagTriggerType.Passive;

    public override void ApplyPassiveEffect(TagContext context)
    {
        if (context.Unit != null)
        {
            context.Unit.IsMassive = true;
        }
    }
}

public class InfiltrateTag : TagDefinition
{
    public override string Name => "渗透";
    public override string Description => "可移动到敌方单位所在节点";
    public override TagTriggerType TriggerType => TagTriggerType.Passive;

    public override void ApplyPassiveEffect(TagContext context)
    {
        if (context.Unit != null)
        {
            context.Unit.CanInfiltrate = true;
        }
    }
}
