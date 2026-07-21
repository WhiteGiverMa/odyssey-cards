using System.Collections.Generic;

namespace OdysseyCards.Roguelike;

/// <summary>
/// 房间类型枚举。
/// 定义地图节点上可以出现的房间类别，控制进入后的游戏模式。
/// </summary>
public enum RoomType
{
	/// <summary>普通怪物战斗。</summary>
	Monster,

	/// <summary>精英怪物战斗——更强的敌人，更好的奖励。</summary>
	Elite,

	/// <summary>位面 Boss——高血量高伤害的强敌。</summary>
	Boss,

	/// <summary>奖励房间——获得随机卡牌战利品。</summary>
	Treasure,

	/// <summary>商店——购买卡牌和遗物（占位符）。</summary>
	Shop,

	/// <summary>休息站点——回复生命值（占位符）。</summary>
	RestSite,

	/// <summary>随机事件——触发叙事/选择事件（占位符）。</summary>
	Event,
}

/// <summary>
/// 房间定义——描述地图上的单个节点。
/// 包含房间类型、显示名称、描述等信息。
/// </summary>
public class RoomDefinition
{
	/// <summary>房间类型。</summary>
	public RoomType Type { get; set; }

	/// <summary>显示名称（UI 按钮文字）。</summary>
	public string DisplayName { get; set; } = "";

	/// <summary>描述文本（UI 副标题/提示）。</summary>
	public string Description { get; set; } = "";

	/// <summary>该房间是否已被完成。</summary>
	public bool IsCompleted { get; set; }
}

/// <summary>
/// 平面层——地图上的一层，包含 1-2 个可选房间。
/// 玩家只能选择其中一个进入。
/// </summary>
public class PlaneLayer
{
	/// <summary>本层可选房间列表（1-2 个选择项）。</summary>
	public List<RoomDefinition> Choices { get; set; } = new();
}

/// <summary>
/// 位面定义——描述一次冒险中的一个完整位面（多层的房间序列）。
/// 负责生成特定位面的房间布局。
/// </summary>
public class PlaneDefinition
{
	/// <summary>位面名称（如"第一位面"）。</summary>
	public string PlaneName { get; set; } = "";

	/// <summary>位面唯一标识，用于存档恢复时工厂查找。</summary>
	public string Id { get; set; } = "";

	/// <summary>位面中的层序列（每层包含可选房间）。</summary>
	public List<PlaneLayer> Layers { get; set; } = new();

	/// <summary>
	/// 创建第一位面的预设布局（6 层，战斗→4个混合→Boss）。
	/// </summary>
	public static PlaneDefinition CreateFirstPlane()
	{
		return new PlaneDefinition
		{
			Id = "first_plane",
			PlaneName = Localization.Localization.T("plane.first_plane", "第一位面"),
			Layers = new List<PlaneLayer>
			{
				new()
				{
					Choices =
					{
						new RoomDefinition
						{
							Type = RoomType.Monster,
							DisplayName = Localization.Localization.T("room.display_name.monster", "战斗"),
							Description = Localization.Localization.T("room.description.first_battle", "击败前方出现的敌人，开启冒险之旅")
						}
					}
				},

				new()
				{
					Choices =
					{
						new RoomDefinition
						{
							Type = RoomType.Event,
							DisplayName = Localization.Localization.T("room.display_name.event", "事件"),
							Description = Localization.Localization.T("room.description.first_event", "触发随机事件，命运的齿轮开始转动")
						},
						new RoomDefinition
						{
							Type = RoomType.Shop,
							DisplayName = Localization.Localization.T("room.display_name.shop", "商店"),
							Description = Localization.Localization.T("room.description.first_shop", "购买卡牌和遗物，补充你的战力")
						}
					}
				},

				new()
				{
					Choices =
					{
						new RoomDefinition
						{
							Type = RoomType.Treasure,
							DisplayName = Localization.Localization.T("room.display_name.treasure", "奖励"),
							Description = Localization.Localization.T("room.description.first_treasure", "获得随机卡牌战利品，强化你的牌堆")
						},
						new RoomDefinition
						{
							Type = RoomType.Monster,
							DisplayName = Localization.Localization.T("room.display_name.monster", "战斗"),
							Description = Localization.Localization.T("room.description.first_battle2", "击败敌人，获取丰厚战利品")
						}
					}
				},

				new()
				{
					Choices =
					{
						new RoomDefinition
						{
							Type = RoomType.RestSite,
							DisplayName = Localization.Localization.T("room.display_name.rest", "休息"),
							Description = Localization.Localization.T("room.description.first_rest", "回复生命值，为后续战斗做好准备")
						},
						new RoomDefinition
						{
							Type = RoomType.Shop,
							DisplayName = Localization.Localization.T("room.display_name.shop", "商店"),
							Description = Localization.Localization.T("room.description.first_shop", "购买卡牌和遗物，补充你的战力")
						}
					}
				},

				new()
				{
					Choices =
					{
						new RoomDefinition
						{
							Type = RoomType.Monster,
							DisplayName = Localization.Localization.T("room.display_name.monster", "战斗"),
							Description = Localization.Localization.T("room.description.first_battle2", "击败敌人，获取丰厚战利品")
						},
						new RoomDefinition
						{
							Type = RoomType.Elite,
							DisplayName = Localization.Localization.T("room.display_name.elite", "精英"),
							Description = Localization.Localization.T("room.description.first_elite", "面对更强的敌人，但奖励更为丰厚")
						}
					}
				},

				new()
				{
					Choices =
					{
						new RoomDefinition
						{
							Type = RoomType.Boss,
							DisplayName = Localization.Localization.T("room.display_name.boss", "BOSS"),
							Description = Localization.Localization.T("room.description.first_boss", "位面首领——守护者，击败它完成第一位面！")
						}
					}
				},
			}
		};
	}

	/// <summary>
	/// 从 ID 字符串工厂创建位面定义（供存档恢复用）。
	/// </summary>
	public static PlaneDefinition FromId(string id)
	{
		return id switch
		{
			"first_plane" => CreateFirstPlane(),
			_ => CreateFirstPlane(), // 回退到第一位面
		};
	}
}
