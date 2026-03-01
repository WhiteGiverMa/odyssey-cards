using OdysseyCards.Character;
using OdysseyCards.Core;

namespace OdysseyCards.Character;

public static class EnemyFactory
{
    public static Enemy CreateFromDeck(EnemyDeckData deckData)
    {
        Enemy enemy = new Enemy();
        enemy.Initialize(deckData);
        return enemy;
    }
}
