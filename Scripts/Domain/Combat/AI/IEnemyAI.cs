using System.Collections.Generic;
using OdysseyCards.Domain.Combat.Commands;

namespace OdysseyCards.Domain.Combat.AI
{
    public interface IEnemyAI
    {
        IReadOnlyList<CombatCommand> GenerateCommands(AIContext context);
    }
}
