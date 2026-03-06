using System.Collections.Generic;

namespace OdysseyCards.Application.Ports
{
    public interface IRewardService
    {
        IReadOnlyList<CardRewardOption> GenerateRewards(CombatResult result);
        void GrantReward(int actorId, CardRewardOption option);
    }

    public sealed record CardRewardOption
    {
        public string CardId { get; init; }
        public string CardName { get; init; }
        public string Rarity { get; init; }
        public object CardResource { get; init; }

        public CardRewardOption() { }

        public CardRewardOption(string cardId, string cardName, string rarity, object cardResource)
        {
            CardId = cardId;
            CardName = cardName;
            Rarity = rarity;
            CardResource = cardResource;
        }
    }

    public sealed record CombatResult
    {
        public bool IsVictory { get; init; }
        public int WinnerActorId { get; init; }
        public int TurnCount { get; init; }
        public int EnemyCount { get; init; }
        public int RemainingPlayerHQHealth { get; init; }
        public int RemainingEnemyHQHealth { get; init; }

        public CombatResult() { }

        public CombatResult(bool isVictory, int winnerActorId, int turnCount, int enemyCount, int remainingPlayerHQHealth, int remainingEnemyHQHealth)
        {
            IsVictory = isVictory;
            WinnerActorId = winnerActorId;
            TurnCount = turnCount;
            EnemyCount = enemyCount;
            RemainingPlayerHQHealth = remainingPlayerHQHealth;
            RemainingEnemyHQHealth = remainingEnemyHQHealth;
        }
    }
}
