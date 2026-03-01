using Godot;
using System.Collections.Generic;

namespace OdysseyCards.Core;

public struct RewardCardEntry
{
    public ICardData CardData { get; set; }
    public int Weight { get; set; }

    public RewardCardEntry(ICardData cardData, int weight)
    {
        CardData = cardData;
        Weight = weight;
    }
}

public partial class CardRewardPool : Resource
{
    [Export] public string PoolName { get; set; } = "";
    [Export] public CardRarity Rarity { get; set; } = CardRarity.Common;

    private List<RewardCardEntry> _entries = new();

    public IReadOnlyList<RewardCardEntry> Entries => _entries;

    public void AddEntry(ICardData cardData, int weight)
    {
        _entries.Add(new RewardCardEntry(cardData, weight));
    }

    public void RemoveEntry(ICardData cardData)
    {
        _entries.RemoveAll(e => e.CardData == cardData);
    }

    public void ClearEntries()
    {
        _entries.Clear();
    }

    public int GetTotalWeight()
    {
        int total = 0;
        foreach (var entry in _entries)
        {
            total += entry.Weight;
        }
        return total;
    }

    public ICardData? GetRandomCard()
    {
        if (_entries.Count == 0)
            return null;

        int totalWeight = GetTotalWeight();
        if (totalWeight <= 0)
            return null;

        int randomValue = GD.RandRange(1, totalWeight);
        int currentWeight = 0;

        foreach (var entry in _entries)
        {
            currentWeight += entry.Weight;
            if (randomValue <= currentWeight)
            {
                return entry.CardData;
            }
        }

        return _entries[0].CardData;
    }
}

public static class CardRarityWeights
{
    public const int Common = 100;
    public const int Uncommon = 60;
    public const int Rare = 30;
    public const int Legendary = 10;
}
