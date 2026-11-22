using System.Collections.Generic;
using OdysseyCards.Core;
using Loc = OdysseyCards.Localization.Localization;

namespace OdysseyCards.Card;

/// <summary>
/// 运行时卡牌基类。
/// 随从（Minion）和法术（Spell）的公共抽象，
/// 不继承 Godot Node，纯 C# 类。
/// </summary>
public class Card
{
	/// <summary>
	/// 关联的卡牌数据资源。
	/// </summary>
	public CardData Data { get; protected set; }

	private readonly string _id = "";
	private readonly CardType _type;
	private readonly int _actionCost;
	private readonly int _cost;

	/// <summary>情报公开状态随实例回手、洗回与显式运行时复制保留。</summary>
	public bool IsRevealedByIntel { get; set; }

	/// <summary>
	/// 卡牌唯一标识。
	/// </summary>
	public string Id => Data?.Id ?? _id;

	/// <summary>
	/// 卡牌名称（原始数据，不作为 UI 渲染源）。
	/// 渲染时使用 <see cref="GetLocalizedName"/> 以支持多语言切换。
	/// </summary>
	public string CardName => Data?.CardName ?? _id;

	/// <summary>
	/// 法力值消耗（部署费用）。
	/// </summary>
	public int Cost => Data?.Cost ?? _cost;

	/// <summary>
	/// 运行时费用调整值（正数=增加费用，负数=减少费用）。
	/// 由 StatusEffect、ActiveDomain 等系统动态修改。
	/// 例：「宇宙冷漠」每层使「解释」费用 +1。
	/// </summary>
	public int CostModifier { get; set; }

	/// <summary>
	/// 回合结束回到抽牌堆的剩余次数；-1 表示此牌不使用该计数器。
	/// </summary>
	internal int EndTurnDrawPileReturnsRemaining { get; set; } = -1;

	/// <summary>
	/// 计算考虑所有运行时修改后的有效费用。
	/// 最低为 0，不封顶。
	/// </summary>
	/// <returns>有效费用</returns>
	public int GetEffectiveCost()
	{
		return System.Math.Max(0, Cost + CostModifier);
	}

	/// <summary>
	/// 行动花费——随从每次推进或普通攻击分别消耗的法力值。
	/// </summary>
	public int ActionCost => System.Math.Max(0, Data?.ActionCost ?? _actionCost);

	/// <summary>
	/// 卡牌类型（随从或法术）。
	/// </summary>
	public CardType Type => Data?.Type ?? _type;

	/// <summary>
	/// 轮战：法术打出后/随从被击败后返回抽牌堆底部，不进入弃牌堆。
	/// </summary>
	public bool HasRecycle => GrantedRecycle || (Data?.HasKeyword(Keyword.Recycle) ?? false);
	internal bool GrantedRecycle { get; set; }

	/// <summary>
	/// 奇巧：被弃掉时自动打出。
	/// 法术/领域直接打出（0费），随从召唤到最左空槽位。
	/// </summary>
	public bool HasQiqiao => Data?.HasKeyword(Keyword.Qiqiao) ?? false;

	/// <summary>
	/// 不可打出：无法从手牌中主动打出。
	/// </summary>
	public bool HasUnplayable => Data?.HasKeyword(Keyword.Unplayable) ?? false;

	/// <summary>
	/// 当前反制牌是否已在手中激活（待命）。敌方回合触发或失效后清零。
	/// </summary>
	public bool IsCountermeasureArmed { get; internal set; }

	/// <summary>
	/// 激活反制牌时实际支付的法力值，用于友方回合内取消激活时精确返还。
	/// </summary>
	public int ArmedManaCost { get; internal set; }

	/// <summary>
	/// 「能量币」的动态额外法力层数：打出时获得 <c>1 + BonusEnergyValue</c> 点法力。
	/// 仅对 <c>spell_energy_coin</c> 有意义，通过 <see cref="CopyRuntimeModifiersFrom"/> 同步复制。
	/// </summary>
	public int BonusEnergyValue { get; set; }

	/// <summary>战斗日志等历史卡面快照使用的攻击力；null 表示读取静态资源。</summary>
	public int? RuntimeAttackSnapshot { get; private set; }

	/// <summary>战斗日志等历史卡面快照使用的当前生命；null 表示读取静态资源。</summary>
	public int? RuntimeHealthSnapshot { get; private set; }

	/// <summary>记录随从在某一结算时刻的真实攻防快照，不改变静态卡牌资源。</summary>
	public void SetRuntimeStatSnapshot(int attack, int currentHealth)
	{
		RuntimeAttackSnapshot = attack;
		RuntimeHealthSnapshot = currentHealth;
	}

	/// <summary>
	/// 创建卡牌运行时实例。
	/// </summary>
	/// <param name="data">卡牌数据资源</param>
	public Card(CardData data)
	{
		Data = data;
	}

	/// <summary>不依赖资源加载的运行时入口，供规则模拟与纯 C# 测试使用。</summary>
	public Card(string id, CardType type = CardType.Spell, int actionCost = 0, int cost = 0)
	{
		_id = id;
		_type = type;
		_actionCost = actionCost;
		_cost = cost;
	}

	/// <summary>
	/// 从另一张运行时卡牌复制牌面修饰。
	/// </summary>
	public void CopyRuntimeModifiersFrom(Card other)
	{
		CostModifier = other.CostModifier;
		GrantedRecycle = other.GrantedRecycle;
		EndTurnDrawPileReturnsRemaining = other.EndTurnDrawPileReturnsRemaining;
		IsRevealedByIntel = other.IsRevealedByIntel;
		BonusEnergyValue = other.BonusEnergyValue;
	}

	/// <summary>创建保留运行时费用、关键词修饰与随从攻防的历史卡面快照。</summary>
	public Card CreateSnapshot()
	{
		var snapshot = Data != null
			? new Card(Data)
			: new Card(Id, Type, ActionCost, Cost);
		snapshot.CopyRuntimeModifiersFrom(this);
		snapshot.RuntimeAttackSnapshot = RuntimeAttackSnapshot;
		snapshot.RuntimeHealthSnapshot = RuntimeHealthSnapshot;
		return snapshot;
	}

	/// <summary>
	/// 获取本地化的卡牌名称。
	/// 优先读取 YAML 翻译，缺失时回退到 CardData.CardName 原始字段。
	/// </summary>
	public string GetLocalizedName()
	{
		if (Id == "spell_energy_coin" && BonusEnergyValue > 0)
		{
			return Loc.T("cards.spell_energy_coin.plus_name", "能量币+{bonus}")
				.Replace("{bonus}", BonusEnergyValue.ToString());
		}
		return Data?.GetLocalizedName() ?? CardName;
	}

	/// <summary>
	/// 获取本地化的卡牌描述。
	/// 优先读取 YAML 翻译，缺失时回退到 CardData.Description 原始字段。
	/// </summary>
	/// <param name="parameters">占位符替换参数（可选）</param>
	public string GetLocalizedDescription(Dictionary<string, object> parameters = null)
	{
		if (Id == "spell_energy_coin" && BonusEnergyValue > 0)
		{
			string amount = (1 + BonusEnergyValue).ToString();
			return Loc.T("cards.spell_energy_coin.description_with_bonus", "获得{amount}点法力。")
				.Replace("{amount}", amount);
		}
		return Data?.GetLocalizedDescription(parameters) ?? Data?.Description ?? string.Empty;
	}

	/// <summary>
	/// 检查是否有足够法力值打出此牌。
	/// </summary>
	/// <param name="availableMana">当前可用法力值</param>
	/// <returns>费用足够时返回 true</returns>
	public virtual bool CanPlay(int availableMana)
	{
		// 已激活的反制牌在友方回合始终可点击/拖出，以便取消激活并返还法力。
		return IsCountermeasureArmed || availableMana >= GetEffectiveCost();
	}

}
