using System;
using System.Collections.Generic;
using OdysseyCards.Application.Ports;
using OdysseyCards.Core;

namespace OdysseyCards.Application.Reward
{
    public sealed class CardRewardService : IRewardService
    {
        private readonly CardReward _cardReward;
        private readonly Dictionary<CardRarity, CardRewardPool> _pools = new();
        private readonly ICardResourceLoader _resourceLoader;
        private readonly ILogger _logger;
        private readonly IDeckService _deckService;

        public CardRewardService(ICardResourceLoader resourceLoader, ILogger logger, IDeckService deckService)
        {
            _resourceLoader = resourceLoader;
            _logger = logger;
            _deckService = deckService;
            _cardReward = new CardReward();
            LoadRewardPools();
        }

        private void LoadRewardPools()
        {
            string[] poolPaths =
            [
                "res://Resources/CardRewardPools/CommonPool.tres",
                "res://Resources/CardRewardPools/UncommonPool.tres",
                "res://Resources/CardRewardPools/RarePool.tres"
            ];

            CardRarity[] rarities = [CardRarity.Common, CardRarity.Uncommon, CardRarity.Rare];

            for (int i = 0; i < poolPaths.Length; i++)
            {
                if (_resourceLoader.ResourceExists(poolPaths[i]))
                {
                    CardRewardPool pool = _resourceLoader.LoadCardPool(poolPaths[i]);
                    if (pool != null)
                    {
                        _pools[rarities[i]] = pool;
                        _cardReward.AddPool(rarities[i], pool);
                    }
                }
            }
        }

        public IReadOnlyList<CardRewardOption> GenerateRewards(CombatResult result)
        {
            if (result == null || !result.IsVictory)
            {
                return Array.Empty<CardRewardOption>();
            }

            CardRarity[] rarities = [CardRarity.Common, CardRarity.Uncommon, CardRarity.Rare];
            List<ICardData> cards = _cardReward.GenerateRewardsFromMultiplePools(rarities, 3);

            var options = new List<CardRewardOption>();
            foreach (var card in cards)
            {
                string rarity = card.Rarity.ToString();
                options.Add(new CardRewardOption(card.Id, card.CardName, rarity, card));
            }

            return options;
        }

        public void GrantReward(int actorId, CardRewardOption option)
        {
            if (option == null || option.CardResource == null)
            {
                return;
            }

            bool success = _deckService.AddCardToDeck(option.CardResource);
            if (success)
            {
                _logger.Log($"[CardRewardService] Granted card to deck: {option.CardName}");
            }
        }
    }
}
