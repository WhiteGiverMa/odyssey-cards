using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using Godot;
using OdysseyCards.Character;

namespace OdysseyCards.Core;

/// <summary>
/// 顶级存档数据结构。序列化为 JSON 持久化到 user://save.json。
/// </summary>
public class GameSaveData
{
	[JsonPropertyName("version")]
	public int Version { get; set; } = 1;

	[JsonPropertyName("language")]
	public string Language { get; set; } = "zh";

	[JsonPropertyName("owned_card_ids")]
	public List<string> OwnedCardIds { get; set; } = new();

	[JsonPropertyName("decks")]
	public List<DeckSaveData> Decks { get; set; } = new();

	[JsonPropertyName("active_deck_index")]
	public int ActiveDeckIndex { get; set; } = -1;

	/// <summary>表情空闲计时器时长（秒）。玩家不出牌超过此时间后敌人发送嘲讽表情。</summary>
	[JsonPropertyName("emote_idle_time_seconds")]
	public float EmoteIdleTimeSeconds { get; set; } = 5.0f;

	/// <summary>空闲计时器随机浮动的最小倍率。下次触发时间 = 基础值 × random(min, max)。</summary>
	[JsonPropertyName("emote_idle_variation_min")]
	public float EmoteIdleVariationMin { get; set; } = 0.7f;

	/// <summary>空闲计时器随机浮动的最大倍率。</summary>
	[JsonPropertyName("emote_idle_variation_max")]
	public float EmoteIdleVariationMax { get; set; } = 1.3f;

	/// <summary>当前冒险中的金币数量（每局重置）。用于商店消费。</summary>
	[JsonPropertyName("run_gold")]
	public int RunGold { get; set; }

	/// <summary>上次选择的英雄。</summary>
	[JsonPropertyName("selected_hero_id")]
	public string SelectedHeroId { get; set; } = "ayame";

	/// <summary>当前冒险的存档快照。null = 没有进行中的冒险。</summary>
	[JsonPropertyName("active_run")]
	public RunSaveData? ActiveRun { get; set; }

	/// <summary>所有可切换的预设表情组。</summary>
	[JsonPropertyName("emote_presets")]
	public List<EmotePresetSaveData> EmotePresets { get; set; } = new();

	/// <summary>当前激活的预设表情组 ID。</summary>
	[JsonPropertyName("active_emote_preset_id")]
	public string ActiveEmotePresetId { get; set; } = OfficialEmoteCatalog.PresetId;
}

/// <summary>
/// 单个牌组的存档数据。
/// </summary>
public class DeckSaveData
{
	[JsonPropertyName("name")]
	public string Name { get; set; } = "默认牌组";

	[JsonPropertyName("cards")]
	public List<CardCountData> Cards { get; set; } = new();

	/// <summary>
	/// 从 Deck 运行时对象创建存档数据（按卡牌 Id 合并数量）。
	/// </summary>
	public static DeckSaveData FromDeck(Deck deck)
	{
		var counts = new Dictionary<string, int>();
		foreach (var card in deck.Cards)
		{
			if (counts.ContainsKey(card.Id))
				counts[card.Id]++;
			else
				counts[card.Id] = 1;
		}

		return new DeckSaveData
		{
			Name = deck.Name,
			Cards = counts.Select(kv => new CardCountData
			{
				Id = kv.Key,
				Count = kv.Value,
			}).ToList(),
		};
	}

	/// <summary>
	/// 从存档数据还原 Deck 对象。
	/// </summary>
	public Deck? ToDeck(GameManager gm)
	{
		var deck = new Deck { Name = Name };
		var cards = new List<CardData>();

		foreach (var item in Cards)
		{
			var cardData = gm.GetCardById(item.Id);
			if (cardData == null)
			{
				GD.PushWarning($"[DeckSaveData] 牌组「{Name}」中的卡牌 {item.Id} 在注册表中未找到，已跳过");
				continue;
			}

			for (int i = 0; i < item.Count; i++)
			{
				cards.Add(cardData);
			}
		}

		deck.Initialize(cards);
		return deck;
	}
}

/// <summary>
/// 卡牌 ID 与数量的配对。
/// </summary>
public class CardCountData
{
	[JsonPropertyName("id")]
	public string Id { get; set; } = "";

	[JsonPropertyName("count")]
	public int Count { get; set; } = 0;
}
