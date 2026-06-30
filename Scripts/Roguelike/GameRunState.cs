using Godot;
using OdysseyCards.AI;
using OdysseyCards.Core;
using System;
using System.Collections.Generic;

namespace OdysseyCards.Roguelike;

/// <summary>
/// 单次游戏运行状态管理器。
/// 管理一次冒险的完整生命周期：位面进度、当前房间、层推进和遭遇路由。
/// 纯 C# 类，不继承 Godot Node——由 GameManager 持有引用。
/// </summary>
public class GameRunState
{
	// ===== 位面进度 =====

	/// <summary>
	/// 当前正在进行的位面定义。
	/// </summary>
	public PlaneDefinition CurrentPlane { get; private set; }

	/// <summary>
	/// 当前所在层索引（0-based，指向 CurrentPlane.Layers 中的位置）。
	/// </summary>
	public int CurrentLayerIndex { get; private set; }

	/// <summary>
	/// 本层中玩家选择的房间（null 表示尚未选择）。
	/// </summary>
	public RoomDefinition? SelectedRoom { get; private set; }

	/// <summary>
	/// 是否已完成所有层（到达位面终点）。
	/// </summary>
	public bool IsPlaneComplete => CurrentLayerIndex >= CurrentPlane?.Layers.Count;

	/// <summary>
	/// 运行是否已结束（Boss 击败或玩家死亡）。
	/// </summary>
	public bool IsRunComplete { get; private set; }

	/// <summary>
	/// 运行是否失败（玩家在战斗中死亡）。
	/// </summary>
	public bool IsRunFailed { get; private set; }

	/// <summary>
	/// 失败原因文本（由触发失败的系统设置，MapUI ShowRunFailed 展示）。
	/// null 表示战斗死亡，非 null 表示特殊失败（如镜中自我虚脱）。
	/// </summary>
	public string? FailureReason { get; set; }

	// ===== 运行事件 =====

	/// <summary>
	/// 层推进事件——当玩家完成一个房间并进入下一层时触发。
	/// </summary>
	public event Action<int, int>? OnLayerAdvanced; // (oldLayerIndex, newLayerIndex)

	/// <summary>
	/// 运行完成事件——Boss 被击败时触发。
	/// </summary>
	public event Action? OnRunCompleted;

	/// <summary>
	/// 运行失败事件——玩家死亡时触发。
	/// </summary>
	public event Action? OnRunFailed;

	// ===== 初始化 =====

	/// <summary>
	/// 开始一次新的冒险运行。
	/// 创建第一位面并将进度重置到起点。
	/// </summary>
	public void StartNewRun()
	{
		GD.Print("[GameRunState] 开始新冒险！");

		CurrentPlane = PlaneDefinition.CreateFirstPlane();
		CurrentLayerIndex = 0;
		SelectedRoom = null;
		IsRunComplete = false;
		IsRunFailed = false;

		GD.Print($"[GameRunState] 第一位面已生成 — {CurrentPlane.PlaneName}，" +
				  $"{CurrentPlane.Layers.Count} 层，" +
				  $"首层：{CurrentPlane.Layers[0].Choices[0].DisplayName}");
	}

	/// <summary>
	/// 获取当前层可选的房间列表。
	/// 返回当前层中尚未被完成的房间选择项。
	/// </summary>
	/// <returns>可选房间列表（1-2 个）。</returns>
	public System.Collections.Generic.IReadOnlyList<RoomDefinition> GetCurrentLayerChoices()
	{
		if (IsPlaneComplete)
			return Array.Empty<RoomDefinition>();

		var layer = CurrentPlane.Layers[CurrentLayerIndex];
		// 过滤掉已完成的房间（降级场景：层内只剩 1 个选项）
		return layer.Choices.FindAll(r => !r.IsCompleted)
			.AsReadOnly();
	}

	/// <summary>
	/// 获取总层数。
	/// </summary>
	public int TotalLayers => CurrentPlane?.Layers.Count ?? 0;

	/// <summary>
	/// 当前层可选房间数量。
	/// </summary>
	public int CurrentChoiceCount => GetCurrentLayerChoices().Count;

	// ===== 房间选择与推进 =====

	/// <summary>
	/// 玩家选择一个房间进入。
	/// 将选中的房间标记为已选择，尚未标记为完成。
	/// </summary>
	/// <param name="room">玩家选择的房间。</param>
	public void SelectRoom(RoomDefinition room)
	{
		if (IsPlaneComplete)
		{
			GD.PrintErr("[GameRunState] SelectRoom 失败 — 位面已完成");
			return;
		}

		SelectedRoom = room;
		GD.Print($"[GameRunState] 选择了房间：{room.DisplayName} ({room.Type})，层 {CurrentLayerIndex + 1}/{TotalLayers}");
	}

	/// <summary>
	/// 标记当前房间为已完成，并推进到下一层。
	/// 如果是 Boss 房间，同时标记运行完成。
	/// </summary>
	public void CompleteRoom()
	{
		if (SelectedRoom == null)
		{
			GD.PrintErr("[GameRunState] CompleteRoom 失败 — 没有选中的房间");
			return;
		}

		SelectedRoom.IsCompleted = true;
		int oldLayer = CurrentLayerIndex;

		GD.Print($"[GameRunState] 房间完成：{SelectedRoom.DisplayName}");

		// Boss 被击败 → 运行完成
		if (SelectedRoom.Type == RoomType.Boss)
		{
			IsRunComplete = true;
			GD.Print("[GameRunState] ★★★ Boss 被击败 — 冒险胜利！★★★");
			OnRunCompleted?.Invoke();
			return;
		}

		// 推进到下一层
		CurrentLayerIndex++;
		SelectedRoom = null;

		OnLayerAdvanced?.Invoke(oldLayer, CurrentLayerIndex);

		if (IsPlaneComplete)
		{
			GD.Print($"[GameRunState] 位面完成（第 {CurrentLayerIndex} 层已超出 {TotalLayers} 层范围）");
		}
		else
		{
			var nextLayer = CurrentPlane.Layers[CurrentLayerIndex];
			GD.Print($"[GameRunState] 进入第 {CurrentLayerIndex + 1}/{TotalLayers} 层，" +
					  $"{nextLayer.Choices.Count} 个可选房间");
		}
	}

	/// <summary>
	/// 标记运行失败（玩家在战斗中阵亡）。
	/// </summary>
	public void FailRun()
	{
		IsRunFailed = true;
		IsRunComplete = true;
		GD.Print("[GameRunState] ☠ 冒险失败 — 玩家阵亡");
		OnRunFailed?.Invoke();
	}

	/// <summary>
	/// 重置运行状态——清空所有进度。
	/// </summary>
	public void Reset()
	{
		CurrentPlane = null!;
		CurrentLayerIndex = 0;
		SelectedRoom = null;
		IsRunComplete = false;
		IsRunFailed = false;
		GD.Print("[GameRunState] 运行状态已重置");
	}

	// ===== 遭遇路由 =====

	/// <summary>
	/// 根据当前选中的房间类型，确定应该使用哪些敌人 AI。
	/// 返回列表以支持多敌人战斗（精英房间可返回 2 个敌人）。
	/// </summary>
	/// <returns>敌人遭遇实例列表。</returns>
	/// <exception cref="InvalidOperationException">当房间类型不是战斗房间时抛出。</exception>
	public IReadOnlyList<EnemyEncounter> CreateEncounters()
	{
		if (SelectedRoom == null)
			throw new InvalidOperationException("没有选中的房间，无法确定敌人");

		return SelectedRoom.Type switch
		{
			RoomType.Monster => new EnemyEncounter[] { CreateMonsterEncounter() },
			RoomType.Elite => CreateEliteEncounters(),
			RoomType.Boss => new EnemyEncounter[] { CreateBossEncounter() },
			_ => throw new InvalidOperationException(
				$"房间类型 {SelectedRoom.Type} 不是战斗房间，无法创建敌人")
		};
	}

	/// <summary>
	/// 创建单个敌人遭遇（向后兼容）。
	/// </summary>
	public EnemyEncounter CreateEncounter() => CreateEncounters()[0];

	/// <summary>
	/// 创建普通怪物遭遇。
	/// </summary>
	private static EnemyEncounter CreateMonsterEncounter()
	{
		// 简单随机选择一个普通敌人
		var enemies = new EnemyEncounter[]
		{
			new Cultist(),
			new WolfRider(),
			new SlimeBoss(),
			new Goutansha(),
		};
		return enemies[new Random().Next(enemies.Length)];
	}

	/// <summary>
	/// 创建精英怪物遭遇列表——张郎 & 珊胡双敌人战。
	/// </summary>
	private static IReadOnlyList<EnemyEncounter> CreateEliteEncounters()
	{
		return new EnemyEncounter[] { new ZhangLang(), new ShanHu() };
	}

	/// <summary>
	/// 创建 Boss 遭遇。
	/// </summary>
	private static EnemyEncounter CreateBossEncounter()
	{
		return new GuardianBoss();
	}

	// ===== 持久化 =====

	/// <summary>
	/// 序列化当前跑状态。
	/// </summary>
	public RunSaveData Save()
	{
		var data = new RunSaveData
		{
			PlaneId = CurrentPlane?.Id ?? "first_plane",
			CurrentLayerIndex = CurrentLayerIndex,
			IsRunComplete = IsRunComplete,
			IsRunFailed = IsRunFailed,
		};
		// 记录选中的房间位置
		if (SelectedRoom != null && CurrentPlane != null && CurrentLayerIndex < CurrentPlane.Layers.Count)
		{
			var layer = CurrentPlane.Layers[CurrentLayerIndex];
			int ci = layer.Choices.IndexOf(SelectedRoom);
			data.SelectedRoomLayerIndex = CurrentLayerIndex;
			data.SelectedRoomChoiceIndex = ci >= 0 ? ci : 0;
		}
		// 记录已完成的房间
		for (int li = 0; li < (CurrentPlane?.Layers.Count ?? 0); li++)
		{
			var layer = CurrentPlane!.Layers[li];
			for (int ci = 0; ci < layer.Choices.Count; ci++)
				if (layer.Choices[ci].IsCompleted)
					data.CompletedRooms.Add(new CompletedRoomEntry { LayerIndex = li, ChoiceIndex = ci });
		}
		// 玩家状态
		var gm = GameManager.Instance;
		if (gm != null)
		{
			data.HeroId = gm.SelectedHeroId;
			data.PlayerHealth = gm.PlayerHealth;
			data.PlayerMaxHealth = gm.PlayerMaxHealth;
			data.RunGold = gm.RunGold;
			data.RelicIds = gm.Relics.GetRelicIds();
		}
		return data;
	}

	/// <summary>
	/// 从存档恢复跑状态。
	/// </summary>
	public void Restore(RunSaveData data)
	{
		var plane = PlaneDefinition.FromId(data.PlaneId);
		CurrentPlane = plane;
		CurrentLayerIndex = data.CurrentLayerIndex;
		IsRunComplete = data.IsRunComplete;
		IsRunFailed = data.IsRunFailed;
		GameManager.Instance.SelectedHeroId = HeroProfile.Get(data.HeroId).Id;
		// 恢复已完成房间
		foreach (var entry in data.CompletedRooms)
		{
			if (entry.LayerIndex >= 0 && entry.LayerIndex < plane.Layers.Count)
			{
				var layer = plane.Layers[entry.LayerIndex];
				if (entry.ChoiceIndex >= 0 && entry.ChoiceIndex < layer.Choices.Count)
					layer.Choices[entry.ChoiceIndex].IsCompleted = true;
			}
		}
		// 恢复选中的房间
		if (data.SelectedRoomLayerIndex.HasValue)
		{
			int li = data.SelectedRoomLayerIndex.Value;
			int ci = data.SelectedRoomChoiceIndex ?? 0;
			if (li >= 0 && li < plane.Layers.Count)
			{
				var layer = plane.Layers[li];
				if (ci >= 0 && ci < layer.Choices.Count)
					SelectedRoom = layer.Choices[ci];
			}
		}
		GD.Print($"[GameRunState] 从存档恢复 — {plane.PlaneName}，层 {CurrentLayerIndex + 1}/{TotalLayers}");
	}
}
