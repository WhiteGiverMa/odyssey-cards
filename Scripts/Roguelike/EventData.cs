using System;
using System.Linq;
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

	/// <summary>
	/// 选择后是否停留在事件界面（不显示「继续」按钮，允许再次选择）。
	/// 默认 false：选择后显示结果→点继续→退出事件。
	/// 设为 true：选择后显示结果→按钮重新激活→可再次选择。
	/// 用于可重复选择的事件（如绮梦镜中自我事件）。
	/// </summary>
	public bool StaysInEvent { get; set; }
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
	/// 事件池——叙事事件，按 room_type=Event 随机抽取。
	/// 所有用户可见文本通过 Localization.T 解析本地化 key。
	/// 使用 <see cref="GetRandomEvent"/> 而非直接读 <see cref="All"/>，
	/// 该方法会根据英雄 ID 动态追加专属事件。
	/// </summary>
	public static class EventPool
	{
		/// <summary>本地化 key 辅助方法：拼接事件前缀。</summary>
		private static string L(string key, string fallback) =>
			Localization.Localization.T($"events.{key}", fallback);

		/// <summary>全部 5 个通用事件定义（不包含英雄专属事件）。</summary>
		public static readonly EventData[] All =
		[
			CreateMysteriousMerchant(),
			CreateAncientShrine(),
			CreateWanderingSmith(),
			CreateWheelOfFate(),
			CreateTravelerCampfire(),
		];

		/// <summary>
		/// 根据英雄 ID 获取可用事件池中的一个随机事件。
		/// 英雄专属事件（如绮梦的镜中自我）仅在对应英雄时加入池中。
		/// 每次调用都会重新创建专属事件（保证状态新鲜，不受跨次运行的闭包状态污染）。
		/// </summary>
		/// <param name="heroId">当前英雄 ID，传 null 表示无英雄（回退到通用池）。</param>
		/// <returns>从可用池中随机选取的事件。</returns>
		public static EventData GetRandomEvent(string? heroId)
		{
			// 构建当次可用事件列表（先加入通用事件，再按英雄追加专属事件）
			var eligible = new System.Collections.Generic.List<EventData>(All);

			if (heroId == "ayame" || string.Equals(heroId, "qimeng", StringComparison.OrdinalIgnoreCase))
			{
				eligible.Add(CreateAyameMirrorEvent());
			}

			var random = new Random();
			return eligible[random.Next(eligible.Count)];
		}

		/// <summary>
		/// 根据 ID 查找事件（通用池 + 英雄专属事件）。
		/// 用于 ChatScreen /event 命令按 ID 指定事件。
		/// </summary>
		/// <param name="eventId">事件 ID。</param>
		/// <param name="heroId">当前英雄 ID，用于判断是否可创建英雄专属事件。</param>
		/// <returns>匹配的事件，找不到返回 null。</returns>
		public static EventData? FindEvent(string eventId, string? heroId)
		{
			// 先查通用池
			var found = All.FirstOrDefault(e => e.Id == eventId);
			if (found != null)
				return found;

			// 英雄专属事件
			if (eventId == "ayame_mirror" &&
				(heroId == "ayame" || string.Equals(heroId, "qimeng", StringComparison.OrdinalIgnoreCase)))
			{
				return CreateAyameMirrorEvent();
			}

			return null;
		}

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
			if (eligible.Count == 0)
				return;
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
				if (eligible.Count == 0)
					return;
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

		// ──── 英雄专属事件：绮梦 — 镜中自我 ────

		/// <summary>
		/// 创建绮梦专属事件「镜中自我」——每次调用都创建全新实例，确保状态不被跨次运行复用。
		/// 参考 STS2 AbyssalBaths（热泉事件）：可重复的 HP 献祭换 MaxHP 提升 + 一次性回血。
		/// 选项 A（忍耐）：可重复选择，每次损失递增的生命值，换 +1 最大生命值。
		/// 选项 B（释放）：一次性回血 30% 进入时的生命值上限，并退出事件。
		/// </summary>
		private static EventData CreateAyameMirrorEvent()
		{
			// ── 每次调用创建全新的闭包状态（penalty 和 entryMaxHp 不会被跨次运行污染）──
			int penalty = 3;       // 当前耐力惩罚 HP 值（首次 -3，后续 -4、-5...）
			int entryMaxHp = 0;    // 进入事件时的生命值上限（首次执行时捕获）

			// ── 涩情文案开关：从 GameManager 读取（由 UIScaler 持久化并同步）──
			bool lewd = GameManager.Instance?.LewdTextEnabled ?? false;
			string prefix = lewd ? "ayame_mirror_lewd" : "ayame_mirror";

			string choiceATemplate = L($"{prefix}.choice_a", "忍耐……再坚持一会儿（-{0} 生命，+1 生命上限）");
			string resultATemplate = L($"{prefix}.result_a", "你咬紧牙关，克制住了冲动。虽然身体有些疲惫，但意志更加坚定了。\n（-{0} 生命，+1 生命上限）");

			var choiceA = new EventChoice
			{
				Text = string.Format(choiceATemplate, penalty),
				ResultText = "",
				StaysInEvent = true,
			};

			choiceA.Execute = gm =>
			{
				// 首次执行时捕获进入事件那一刻的生命值上限
				if (entryMaxHp == 0)
					entryMaxHp = gm.PlayerMaxHealth;

				int lostHp = penalty;
				gm.PlayerHealth = Math.Max(gm.PlayerHealth - lostHp, 1);
				gm.PlayerMaxHealth += 1;
				gm.SavePlayerHealth(gm.PlayerHealth, gm.PlayerMaxHealth);
				penalty++;
				choiceA.Text = string.Format(choiceATemplate, penalty);
				choiceA.ResultText = string.Format(resultATemplate, lostHp);
			};

			var choiceB = new EventChoice
			{
				Text = L($"{prefix}.choice_b", "已经到极限了……释放吧（恢复 30% 生命上限）"),
				ResultText = L($"{prefix}.result_b", "你不再压抑自己，尽情释放了积攒已久的情绪。身心都轻松了许多。"),
				StaysInEvent = false,
			};

			choiceB.Execute = gm =>
			{
				// 回血按进入事件时的生命值上限计算
				int baseMaxHp = entryMaxHp > 0 ? entryMaxHp : gm.PlayerMaxHealth;
				int healAmount = (int)(baseMaxHp * 0.3);
				if (healAmount < 1)
					healAmount = 1;
				int beforeHp = gm.PlayerHealth;
				gm.PlayerHealth = Math.Min(gm.PlayerHealth + healAmount, gm.PlayerMaxHealth);
				int actualHeal = gm.PlayerHealth - beforeHp;
				gm.SavePlayerHealth(gm.PlayerHealth, gm.PlayerMaxHealth);
				choiceB.ResultText = L($"{prefix}.result_b", "你不再压抑自己，尽情释放了积攒已久的情绪。身心都轻松了许多。")
					+ $"\n（恢复了 {actualHeal} 点生命）";
			};

			return new EventData
			{
				Id = "ayame_mirror",
				Title = L($"{prefix}.title", "镜中自我"),
				Story = L($"{prefix}.story",
					"夜深人静，皎洁的月光洒进房间。\n绮梦站在落地镜前，凝视着镜中那个熟悉的身影——\n少女的脸颊泛起红晕，猫耳轻轻颤动。\n她伸出手，指尖触碰到冰凉的镜面，心跳莫名加速。\n身体深处传来一阵难以言喻的躁动……"),
				Choices = new EventChoice[] { choiceA, choiceB },
			};
		}
	}
