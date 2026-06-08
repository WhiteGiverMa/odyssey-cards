using System;
using OdysseyCards.Core;
using OdysseyCards.Localization;
using OdysseyCards.Relic;

namespace OdysseyCards.Roguelike;

/// <summary>
/// 叙事事件选择项——描述、结果文本和执行效果。
/// Execute 可修改 ResultText 以反映实际执行结果（如金币不足）。
/// </summary>
public class EventChoice
{
	/// <summary>选项描述文本。</summary>
	public string Text { get; set; } = "";

	/// <summary>选择后的结果文本。Execute 可覆写此值。</summary>
	public string ResultText { get; set; } = "";

	/// <summary>执行选择效果。接收 GameManager 实例以操作金币/生命/卡牌/藏品。</summary>
	public Action<GameManager>? Execute { get; set; }
}

/// <summary>
/// 叙事事件数据——包含事件元信息、叙述文本和选择项。
/// </summary>
public class EventData
{
	/// <summary>事件唯一标识（对应本地化 key 前缀）。</summary>
	public string Id { get; set; } = "";

	/// <summary>事件标题。</summary>
	public string Title { get; set; } = "";

	/// <summary>叙述文本（3-5句）。</summary>
	public string Story { get; set; } = "";

	/// <summary>可选选择项列表。</summary>
	public EventChoice[] Choices { get; set; } = [];
}

/// <summary>
/// 事件池——5 个叙事事件，按 room_type=Event 随机抽取。
/// 所有用户可见文本通过 Localization.T 解析本地化 key。
/// </summary>
public static class EventPool
{
	/// <summary>本地化 key 辅助方法：拼接事件前缀。</summary>
	private static string L(string key, string fallback) =>
		Localization.Localization.T($"events.{key}", fallback);

	/// <summary>全部 5 个事件定义。</summary>
	public static readonly EventData[] All =
	[
		CreateMysteriousMerchant(),
		CreateAncientShrine(),
		CreateWanderingSmith(),
		CreateWheelOfFate(),
		CreateTravelerCampfire(),
	];

	// ──── 事件 1：神秘商人 ────

	private static EventData CreateMysteriousMerchant()
	{
		var choiceA = new EventChoice
		{
			Text = L("mysterious_merchant.choice_a", "买一张随机卡牌（50金币）"),
			ResultText = L("mysterious_merchant.result_a_success", "你获得了一张卡牌！"),
		};
		choiceA.Execute = gm =>
		{
			if (!gm.SpendGold(50))
			{
				choiceA.ResultText = L("mysterious_merchant.result_a_no_gold", "金币不足……商人摇了摇头。");
				return;
			}
			var eligible = gm.GetRewardEligibleCards();
			if (eligible.Count == 0) return;
			var rng = new Random();
			var card = eligible[rng.Next(eligible.Count)];
			gm.AddCardToDeckInCombat(card);
		};

		return new EventData
		{
			Id = "mysterious_merchant",
			Title = L("mysterious_merchant.title", "神秘商人"),
			Story = L("mysterious_merchant.story", "一个戴着面具的商人从阴影中走出，打开了他的箱子……"),
			Choices = new EventChoice[]
			{
				choiceA,
				new()
				{
					Text = L("mysterious_merchant.choice_b", "卖一张随机卡牌（获得40金币）"),
					ResultText = L("mysterious_merchant.result_b", "商人满意地离开了。"),
					Execute = gm =>
					{
						gm.AddGold(40);
					},
				},
				new()
				{
					Text = L("mysterious_merchant.choice_c", "不交易，离开"),
					ResultText = L("mysterious_merchant.result_c", "你摇了摇头，继续赶路。"),
					Execute = null,
				},
			},
		};
	}

	// ──── 事件 2：古老神龛 ────

	private static EventData CreateAncientShrine()
	{
		return new EventData
		{
			Id = "ancient_shrine",
			Title = L("ancient_shrine.title", "古老神龛"),
			Story = L("ancient_shrine.story", "一座散发着微光的神龛矗立在前方，上面刻着古老的符文……"),
			Choices = new EventChoice[]
			{
				new()
				{
					Text = L("ancient_shrine.choice_a", "祈祷（回复5点生命）"),
					ResultText = L("ancient_shrine.result_a", "你的伤势愈合了些许。"),
					Execute = gm =>
					{
						gm.PlayerHealth = Math.Min(gm.PlayerHealth + 5, gm.PlayerMaxHealth);
					},
				},
				new()
				{
					Text = L("ancient_shrine.choice_b", "献祭（失去3点生命，获得15金币）"),
					ResultText = L("ancient_shrine.result_b", "神龛的光芒闪烁了一下。"),
					Execute = gm =>
					{
						gm.PlayerHealth = Math.Max(gm.PlayerHealth - 3, 1);
						gm.AddGold(15);
					},
				},
				new()
				{
					Text = L("ancient_shrine.choice_c", "无视，继续前进"),
					ResultText = L("ancient_shrine.result_c", "你绕过神龛，没有回头。"),
					Execute = null,
				},
			},
		};
	}

	// ──── 事件 3：流浪铁匠 ────

	private static EventData CreateWanderingSmith()
	{
		return new EventData
		{
			Id = "wandering_smith",
			Title = L("wandering_smith.title", "流浪铁匠"),
			Story = L("wandering_smith.story", "一个背着巨大工具箱的老人拦住了你，他的眼睛闪烁着火光……"),
			Choices = new EventChoice[]
			{
				new()
				{
					Text = L("wandering_smith.choice_a", "强化护甲（获得5金币补偿）"),
					ResultText = L("wandering_smith.result_a", "铁匠拍了拍你的肩膀，你感觉更结实了。"),
					Execute = gm =>
					{
						gm.AddGold(5);
					},
				},
				new()
				{
					Text = L("wandering_smith.choice_b", "锻造武器（获得随机藏品）"),
					ResultText = L("wandering_smith.result_b", "你获得了一件新装备！"),
					Execute = gm =>
					{
						var rng = new Random();
						AbstractRelic relic = rng.Next(2) == 0
							? new SmallFanRelic()
							: new GoodDreamPillowRelic();
						gm.Relics.AddRelic(relic);
					},
				},
			},
		};
	}

	// ──── 事件 4：命运之轮 ────

	private static EventData CreateWheelOfFate()
	{
		var choiceA = new EventChoice
		{
			Text = L("wheel_of_fate.choice_a", "转动转盘（随机结果）"),
			ResultText = L("wheel_of_fate.result_a_nothing", "转盘缓缓停下……什么都没有发生。"),
		};
		// 捕获 choiceA 引用以在执行时动态覆写结果文本
		choiceA.Execute = gm =>
		{
			var rng = new Random();
			int roll = rng.Next(100);
			if (roll < 30)
			{
				gm.AddGold(20);
				choiceA.ResultText = L("wheel_of_fate.result_a_gold_gain", "获得 20 金币！");
			}
			else if (roll < 60)
			{
				gm.RunGold = Math.Max(gm.RunGold - 10, 0);
				choiceA.ResultText = L("wheel_of_fate.result_a_gold_loss", "损失了 10 金币……");
			}
			else if (roll < 80)
			{
				gm.PlayerHealth = Math.Min(gm.PlayerHealth + 5, gm.PlayerMaxHealth);
				choiceA.ResultText = L("wheel_of_fate.result_a_heal", "恢复了 5 点生命值！");
			}
			// else: nothing (keep default)
		};

		return new EventData
		{
			Id = "wheel_of_fate",
			Title = L("wheel_of_fate.title", "命运之轮"),
			Story = L("wheel_of_fate.story", "一个巨大的转盘出现在路中央，上面的图案不断地变换……"),
			Choices = new EventChoice[]
			{
				choiceA,
				new()
				{
					Text = L("wheel_of_fate.choice_b", "谨慎地绕过去"),
					ResultText = L("wheel_of_fate.result_b", "你选择了稳妥，绕过了转盘。"),
					Execute = null,
				},
			},
		};
	}

	// ──── 事件 5：篝火旁的旅人 ────

	private static EventData CreateTravelerCampfire()
	{
		var choiceA = new EventChoice
		{
			Text = L("traveler_campfire.choice_a", "分享食物（失去15金币，获得随机卡牌）"),
			ResultText = L("traveler_campfire.result_a_success", "旅人感激地送了你一张卡牌。"),
		};
		choiceA.Execute = gm =>
		{
			if (!gm.SpendGold(15))
			{
				choiceA.ResultText = L("traveler_campfire.result_a_no_gold", "你摸了摸口袋……好像不够。旅人尴尬地笑了笑。");
				return;
			}
			var eligible = gm.GetRewardEligibleCards();
			if (eligible.Count == 0) return;
			var rng = new Random();
			var card = eligible[rng.Next(eligible.Count)];
			gm.AddCardToDeckInCombat(card);
		};

		return new EventData
		{
			Id = "traveler_campfire",
			Title = L("traveler_campfire.title", "篝火旁的旅人"),
			Story = L("traveler_campfire.story", "一个疲惫的旅人坐在篝火旁，看到你后露出了微笑……"),
			Choices = new EventChoice[]
			{
				choiceA,
				new()
				{
					Text = L("traveler_campfire.choice_b", "聆听故事（无事发生）"),
					ResultText = L("traveler_campfire.result_b", "故事很精彩，但对你没什么实际帮助。"),
					Execute = null,
				},
				new()
				{
					Text = L("traveler_campfire.choice_c", "抢劫旅人（获得25金币，但……）"),
					ResultText = L("traveler_campfire.result_c", "你感到一阵内疚，但这笔钱确实有用。"),
					Execute = gm =>
					{
						gm.AddGold(25);
					},
				},
			},
		};
	}
}
