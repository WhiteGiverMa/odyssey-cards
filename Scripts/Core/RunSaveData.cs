using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace OdysseyCards.Core;

/// 运行存档数据——序列化跑状态的最小快照。
/// 与 GameSaveData 分离：RunSaveData 是单次冒险的快照，GameSaveData 是永久收藏/配置。
public class RunSaveData
{
	[JsonPropertyName("plane_id")]
	public string PlaneId { get; set; } = "first_plane";

	[JsonPropertyName("current_layer_index")]
	public int CurrentLayerIndex { get; set; }

	// 选中的房间位置（layer 内的 choice 索引）。null = 在选关界面。
	[JsonPropertyName("selected_room_layer")]
	public int? SelectedRoomLayerIndex { get; set; }

	[JsonPropertyName("selected_room_choice")]
	public int? SelectedRoomChoiceIndex { get; set; }

	// 已完成的房间：(layerIndex, choiceIndex) 对列表
	[JsonPropertyName("completed_rooms")]
	public List<CompletedRoomEntry> CompletedRooms { get; set; } = new();

	[JsonPropertyName("is_run_complete")]
	public bool IsRunComplete { get; set; }

	[JsonPropertyName("is_run_failed")]
	public bool IsRunFailed { get; set; }

	// 玩家状态
	[JsonPropertyName("hero_id")]
	public string HeroId { get; set; } = "qimeng";

	[JsonPropertyName("player_health")]
	public int PlayerHealth { get; set; } = 30;

	[JsonPropertyName("player_max_health")]
	public int PlayerMaxHealth { get; set; } = 30;

	[JsonPropertyName("run_gold")]
	public int RunGold { get; set; }

	// 藏品 ID 列表
	[JsonPropertyName("relic_ids")]
	public List<string> RelicIds { get; set; } = new();
}

public class CompletedRoomEntry
{
	[JsonPropertyName("layer")]
	public int LayerIndex { get; set; }

	[JsonPropertyName("choice")]
	public int ChoiceIndex { get; set; }
}
