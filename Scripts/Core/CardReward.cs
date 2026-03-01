using Godot;
using System.Collections.Generic;

namespace OdysseyCards.Core;

public partial class CardReward : Resource
{
    [Export] public string RewardName { get; set; } = "";

    private Dictionary<CardRarity, CardRewardPool> _pools = new();

    public IReadOnlyDictionary<CardRarity, CardRewardPool> Pools => _pools;

    public void AddPool(CardRarity rarity, CardRewardPool pool)
    {
        _pools[rarity] = pool;
    }

    public void RemovePool(CardRarity rarity)
    {
        _pools.Remove(rarity);
    }

    public void ClearPools()
    {
        _pools.Clear();
    }

    public bool HasPool(CardRarity rarity)
    {
        return _pools.ContainsKey(rarity);
    }

    public CardRewardPool? GetPool(CardRarity rarity)
    {
        return _pools.TryGetValue(rarity, out var pool) ? pool : null;
    }

    public List<ICardData> GenerateRewards(CardRewardPool pool, int count)
    {
        List<ICardData> rewards = new();
        HashSet<string> selectedIds = new();

        if (pool == null || pool.Entries.Count == 0)
        {
            return rewards;
        }

        int maxAttempts = pool.Entries.Count * 3;
        int attempts = 0;

        while (rewards.Count < count && attempts < maxAttempts)
        {
            ICardData? card = pool.GetRandomCard();
            if (card != null && !selectedIds.Contains(card.Id))
            {
                rewards.Add(card);
                selectedIds.Add(card.Id);
            }
            attempts++;
        }

        return rewards;
    }

    public List<ICardData> GenerateRewardsFromMultiplePools(CardRarity[] rarities, int count)
    {
        List<ICardData> rewards = new();
        HashSet<string> selectedIds = new();

        List<CardRewardPool> availablePools = new();
        foreach (var rarity in rarities)
        {
            if (_pools.TryGetValue(rarity, out var pool) && pool.Entries.Count > 0)
            {
                availablePools.Add(pool);
            }
        }

        if (availablePools.Count == 0)
        {
            return rewards;
        }

        int maxAttempts = availablePools.Count * 10;
        int attempts = 0;

        while (rewards.Count < count && attempts < maxAttempts)
        {
            int poolIndex = GD.RandRange(0, availablePools.Count - 1);
            CardRewardPool selectedPool = availablePools[poolIndex];

            ICardData? card = selectedPool.GetRandomCard();
            if (card != null && !selectedIds.Contains(card.Id))
            {
                rewards.Add(card);
                selectedIds.Add(card.Id);
            }
            attempts++;
        }

        return rewards;
    }

    public List<ICardData> GenerateWeightedRewards(int count)
    {
        List<ICardData> rewards = new();
        HashSet<string> selectedIds = new();

        List<(CardRewardPool pool, int weight)> weightedPools = new();
        int totalWeight = 0;

        foreach (var kvp in _pools)
        {
            if (kvp.Value.Entries.Count > 0)
            {
                int weight = GetRarityWeight(kvp.Key);
                weightedPools.Add((kvp.Value, weight));
                totalWeight += weight;
            }
        }

        if (weightedPools.Count == 0 || totalWeight <= 0)
        {
            return rewards;
        }

        int maxAttempts = weightedPools.Count * 10;
        int attempts = 0;

        while (rewards.Count < count && attempts < maxAttempts)
        {
            CardRewardPool selectedPool = SelectPoolByWeight(weightedPools, totalWeight);

            ICardData? card = selectedPool.GetRandomCard();
            if (card != null && !selectedIds.Contains(card.Id))
            {
                rewards.Add(card);
                selectedIds.Add(card.Id);
            }
            attempts++;
        }

        return rewards;
    }

    private CardRewardPool SelectPoolByWeight(List<(CardRewardPool pool, int weight)> weightedPools, int totalWeight)
    {
        int randomValue = GD.RandRange(1, totalWeight);
        int currentWeight = 0;

        foreach (var (pool, weight) in weightedPools)
        {
            currentWeight += weight;
            if (randomValue <= currentWeight)
            {
                return pool;
            }
        }

        return weightedPools[0].pool;
    }

    private int GetRarityWeight(CardRarity rarity)
    {
        return rarity switch
        {
            CardRarity.Common => CardRarityWeights.Common,
            CardRarity.Uncommon => CardRarityWeights.Uncommon,
            CardRarity.Rare => CardRarityWeights.Rare,
            CardRarity.Legendary => CardRarityWeights.Legendary,
            _ => CardRarityWeights.Common
        };
    }
}
