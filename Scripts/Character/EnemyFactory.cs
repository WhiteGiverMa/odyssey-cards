using System.Collections.Generic;
using OdysseyCards.Character;

namespace OdysseyCards.Character;

public static class EnemyFactory
{
    public static Enemy FromData(EnemyData data)
    {
        Enemy enemy = new Enemy();

        enemy.CharacterName = data.CharacterName;
        enemy.MaxHealth = data.MaxHealth;
        enemy.MaxEnergy = data.MaxEnergy;
        enemy.EnemyType = data.CharacterName;

        List<EnemyAction> actions = new List<EnemyAction>();
        foreach (EnemyAction action in data.Actions)
        {
            actions.Add(action);
        }
        enemy.Initialize(actions);

        return enemy;
    }
}
