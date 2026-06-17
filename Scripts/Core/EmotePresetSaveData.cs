using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace OdysseyCards.Core;

/// <summary>
/// 单条预设表情数据。
/// </summary>
public class EmotePresetEntrySaveData
{
	[JsonPropertyName("text")]
	public string Text { get; set; } = "";

	[JsonPropertyName("is_official_collection")]
	public bool IsOfficialCollection { get; set; }

	public EmotePresetEntrySaveData Clone()
	{
		return new EmotePresetEntrySaveData
		{
			Text = Text,
			IsOfficialCollection = IsOfficialCollection,
		};
	}
}

/// <summary>
/// 一组可切换的预设表情。
/// </summary>
public class EmotePresetSaveData
{
	[JsonPropertyName("id")]
	public string Id { get; set; } = Guid.NewGuid().ToString("N");

	[JsonPropertyName("name")]
	public string Name { get; set; } = "新表情组";

	[JsonPropertyName("entries")]
	public List<EmotePresetEntrySaveData> Entries { get; set; } = new();

	public EmotePresetSaveData Clone()
	{
		return new EmotePresetSaveData
		{
			Id = Id,
			Name = Name,
			Entries = Entries.Select(entry => entry.Clone()).ToList(),
		};
	}
}
