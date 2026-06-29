using Godot;
using OdysseyCards.Core;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OdysseyCards.Card;

/// <summary>
/// 运行时随从类。
/// 代表战场上的一个随从单位，继承自 Card 基类，
/// 同时实现 IDamageSource（造成伤害）和 IDamageTarget（承受伤害）接口。
/// 遵循炉石传说风格的战斗模型，不继承 Godot Node。
/// </summary>
public class Minion : Card, IDamageSource, IDamageTarget
{
	/// <summary>
	/// 伤害修改器列表（防御力修改器等）。
	/// internal 访问允许 CombatManager 注入战斗触发的修饰器（如敌意伤害翻倍）。
	/// </summary>
	internal readonly List<IDamageModifier> _damageModifiers = new();

	/// <summary>
	/// 易伤修改器——被攻击时伤害×1.5。由 StatusEffect "vulnerable" 控制激活。
	/// </summary>
	internal readonly VulnerableDamageModifier _vulnerableModifier = new();

	/// <summary>
	/// 虚弱修改器——攻击时伤害×0.75。由 StatusEffect "weak" 控制激活。
	/// </summary>
	internal readonly WeakDamageModifier _weakModifier = new();

	/// <summary>
	/// 脆弱修改器——护甲获得量×0.75。由 StatusEffect "fragile" 控制激活。
	/// </summary>
	internal readonly FragileArmorModifier _fragileModifier = new();

	/// <summary>
	/// 聚焦——效果伤害每层 +1。
	/// 通过永久领域层数驱动，用于智能臭鸡蛋等敌方衍生物。
	/// </summary>
	internal readonly FocusSpellDamageModifier _focusModifier;

	/// <summary>
	/// 运行时替换后的亡语效果。null 表示使用 CardData 原始亡语。
	/// </summary>
	private List<CardEffectData>? _runtimeDeathrattleEffects;

	/// <summary>
	/// 亡语是否被显式替换过（vs 仅追加）。用于效果显示区分。
	/// </summary>
	private bool _deathrattleReplaced;

	// ===== 随从基础属性 =====

	/// <summary>
	/// 随从的攻击力。
	/// </summary>
	public int Attack { get; private set; }

	/// <summary>
	/// 修改攻击力（正数为增加，负数为减少，最小为 0）。
	/// </summary>
	/// <param name="delta">增减量</param>
	public void ModifyAttack(int delta)
	{
		Attack = Attack + delta;
	}

	/// <summary>
	/// 防御力。影响受到的伤害：最终伤害 = max(0, 基础伤害 - 防御力)。
	/// 通过 DefenseModifier 在 DamageResolver ADDITIVE 阶段自动生效。
	/// 可为负值表示脆弱（受到额外伤害）。
	/// </summary>
	public int Defense { get; private set; }

	// ===== 护甲系统 =====

	private int _currentArmor;
	private int _maxArmor;

	/// <summary>
	/// 当前护甲值。护甲在生命值之前吸收伤害。
	/// </summary>
	public int CurrentArmor => _currentArmor;

	/// <summary>
	/// 最大护甲值（记录通过 GainArmor 获得的最大护甲量）。
	/// </summary>
	public int MaxArmor => _maxArmor;

	/// <summary>
	/// 是否拥有护甲。
	/// </summary>
	public bool HasArmor => CurrentArmor > 0;

	/// <summary>
	/// 为随从增加护甲值。
	/// </summary>
	/// <param name="amount">护甲增加量</param>
	public int GainArmor(int amount)
	{
		// 脆弱：护甲获得量 ×0.75
		amount = _fragileModifier.ModifyArmorGain(amount);
		if (amount <= 0)
			return 0;

		_currentArmor += amount;
		if (_currentArmor > _maxArmor)
		{
			_maxArmor = _currentArmor;
		}
		OnArmorGained?.Invoke(amount);
		GD.Print($"[Minion:{CardName}] 获得 {amount} 点护甲，当前护甲：{CurrentArmor}");
		return amount;
	}

	/// <summary>
	/// 修改防御力（正数为增加，负数为减少）。
	/// 最大值不设上限，最小值不设下限（允许负防御表示脆弱状态）。
	/// </summary>
	/// <param name="delta">增减量</param>
	public void ModifyDefense(int delta)
	{
		Defense += delta;
	}

	/// <summary>
	/// 基础攻击力（等同于 Attack，用于接口兼容）。
	/// </summary>
	public int BaseAttack => Attack;

	/// <summary>
	/// 当前生命值。
	/// </summary>
	public int CurrentHealth { get; private set; }

	/// <summary>
	/// 最大生命值。
	/// </summary>
	public int MaxHealth { get; private set; }

	/// <summary>
	/// 在战场上的槽位索引。
	/// -1 表示尚未放置在战场上。
	/// </summary>
	public int BoardSlotIndex { get; set; } = -1;

	/// <summary>
	/// 是否属于玩家方。
	/// </summary>
	public bool IsPlayerSide { get; }

	/// <summary>
	/// 随从是否已死亡。
	/// </summary>
	public bool IsDead => CurrentHealth <= 0;

	// ===== 事件 =====

	/// <summary>
	/// 随从受到伤害时触发。传递伤害事件信息和伤害来源。
	/// 在 ApplyDamage 之后触发，此时生命值已减少。
	/// </summary>
	public event Action<DamageEventInfo, IDamageSource?>? OnDamageTaken;

	/// <summary>
	/// 随从恢复生命值时触发。参数为实际恢复量。
	/// </summary>
	public event Action<int>? OnHealed;
	public event Action<int> OnArmorGained;

	/// <summary>
	/// 计算此随从的目标标签掩码。
	/// 用于目标选择系统的合法性验证。
	/// </summary>
	public TargetTags GetTargetTags()
	{
		return (IsPlayerSide ? TargetTags.Friendly : TargetTags.Enemy) | TargetTags.Minion;
	}

	// ===== 关键词属性 =====

	/// <summary>
	/// 闪击：召唤的回合即可攻击。
	/// </summary>
	public bool HasCharge { get; }

	/// <summary>
	/// 嘲讽：敌方随从必须优先攻击此随从。
	/// </summary>
	public bool HasTaunt { get; internal set; }

	/// <summary>
	/// 战吼：从手牌中打出时触发效果。
	/// </summary>
	public bool HasBattlecry { get; }

	/// <summary>
	/// 亡语：随从死亡时触发效果。
	/// </summary>
	public bool HasDeathrattle { get; private set; }

	/// <summary>
	/// 风怒：每回合可以攻击两次。
	/// </summary>
	public bool HasWindfury { get; }

	/// <summary>
	/// 伏击：每回合第一次被攻击时，先于攻击者造成反击伤害。
	/// 若攻击者被伏击伤害消灭，则攻击被取消。
	/// </summary>
	public bool HasAmbush { get; internal set; }

	/// <summary>
	/// 冲击：攻击时抵消所有反击伤害（一次性消耗，类似圣盾）。
	/// 冲击随从攻击伏击随从时，伏击的先手伤害也被免疫。
	/// </summary>
	public bool HasImpact { get; internal set; }

	/// <summary>
	/// 诱饵战术：此随从受到攻击时，玩家的敌方英雄防御力-1。
	/// 注意触发目标是绝对阵营的敌方英雄，不随此随从阵营变化。
	/// </summary>
	public bool HasBaitTacticsOnAttacked { get; private set; }

	/// <summary>
	/// 获得「诱饵战术」授予的关键词与被攻击触发效果。
	/// </summary>
	public void GrantBaitTactics()
	{
		HasAmbush = true;
		HasImpact = true;
		HasBaitTacticsOnAttacked = true;
		GD.Print($"[Minion:{CardName}] 获得诱饵战术：伏击、冲击、被攻击时敌方英雄防御力-1");
	}

	/// <summary>
	/// 「偶像的黄昏」领域激活标记——由 <see cref="Combat.DomainTriggerManager"/> 在随从进场/领域展开时设置。
	/// 仅用于 EffectBar 显示；触发逻辑不读此标记，而是在被攻击时查询玩家英雄的 ActiveDomains。
	/// 领域被移除时由 <see cref="Combat.DomainTriggerManager.OnDomainRemoved"/> 清除。
	/// </summary>
	internal bool HasIdolTwilightBuff { get; set; }

	/// <summary>
	/// 本回合伏击是否已被消耗。回合开始时重置。
	/// </summary>
	internal bool AmbushUsedThisTurn { get; set; }

	/// <summary>
	/// 重置伏击状态（新回合开始时调用）。
	/// </summary>
	internal void ResetAmbush()
	{
		AmbushUsedThisTurn = false;
	}

	// ===== 效果访问器 =====

	/// <summary>
	/// 战吼效果列表（来源自卡牌数据）。
	/// </summary>
	public IReadOnlyList<CardEffectData> BattlecryEffects => Data.BattlecryEffects;

	/// <summary>
	/// 亡语效果列表（来源自卡牌数据）。
	/// </summary>
	public IReadOnlyList<CardEffectData> DeathrattleEffects => _runtimeDeathrattleEffects is { } effects
		? effects
		: Data.DeathrattleEffects;

	/// <summary>
	/// 向此随从追加一条亡语效果。保留原有亡语（包括 CardData 原始亡语和运行时替换的亡语），
	/// 在末尾追加新效果。与 <see cref="ReplaceDeathrattleEffects"/> 的「覆盖」语义不同。
	/// </summary>
	public void AddDeathrattleEffect(CardEffectData effect)
	{
		if (_runtimeDeathrattleEffects == null)
		{
			// 首次追加：先复制 CardData 原始亡语为运行时列表，再追加
			_runtimeDeathrattleEffects = Data.DeathrattleEffects.ToList();
		}
		_runtimeDeathrattleEffects.Add(effect);
		HasDeathrattle = true;
		GD.Print($"[Minion:{CardName}] 追加亡语效果：{effect.GetDescription()}（共{_runtimeDeathrattleEffects.Count}个）");
	}

	/// <summary>
	/// 替换此随从当前的亡语效果。
	/// </summary>
	public void ReplaceDeathrattleEffects(IEnumerable<CardEffectData> effects)
	{
		_runtimeDeathrattleEffects = effects.ToList();
		_deathrattleReplaced = true;
		HasDeathrattle = _runtimeDeathrattleEffects.Count > 0;
		GD.Print($"[Minion:{CardName}] 亡语已被替换（{_runtimeDeathrattleEffects.Count} 个效果）");
	}

	// ===== IDamageSource 实现 =====

	/// <summary>
	/// 伤害修改器列表。包含防御力修改器等，由 DamageResolver 在各阶段调用。
	/// </summary>
	public IReadOnlyList<IDamageModifier> DamageModifiers => _damageModifiers;

	/// <summary>
	/// 随从的意图大脑。非 null 时，EnemyMinionsAttack 优先用此执行意图，
	/// 而不是默认攻击逻辑。机械小蠊等有自定义意图的随从设置此字段。
	/// </summary>
	public AI.IIntentActor? IntentBrain { get; set; }

	// ===== 状态效果系统 =====

	private readonly Dictionary<string, StatusEffect> _statusEffects = new();
	private readonly Dictionary<string, ActiveDomain> _activeDomains = new();

	public IReadOnlyDictionary<string, StatusEffect> StatusEffects => _statusEffects;
	public IReadOnlyDictionary<string, ActiveDomain> ActiveDomains => _activeDomains;

	public void AddDomain(string domainId, CardEffectData? effectData)
	{
		if (_activeDomains.TryGetValue(domainId, out var existing))
		{
			existing.StackCount++;
			GD.Print($"[Minion:{CardName}] 领域「{domainId}」叠加至 {existing.StackCount} 层");
			return;
		}

		_activeDomains[domainId] = new ActiveDomain(domainId, effectData);
		GD.Print($"[Minion:{CardName}] 获得领域「{domainId}」");
	}

	public bool HasDomain(string domainId)
	{
		return _activeDomains.ContainsKey(domainId);
	}

	private int GetFocusStacks()
	{
		return _activeDomains.TryGetValue("focus", out var focus) ? focus.StackCount : 0;
	}

	public void AddStatusEffect(StatusEffect effect)
	{
		if (_statusEffects.TryGetValue(effect.Id, out var existing))
		{
			existing.Stacks += effect.Stacks;
			GD.Print($"[Minion:{CardName}] 状态「{effect.Id}」叠加到 {existing.Stacks} 层");
		}
		else
		{
			_statusEffects[effect.Id] = effect;
			GD.Print($"[Minion:{CardName}] 获得状态「{effect.Id}」{effect.Stacks} 层");
			ApplyStatusEffectImmediate(effect);
		}
	}

	public void RemoveStatusEffect(string id)
	{
		if (_statusEffects.Remove(id))
		{
			GD.Print($"[Minion:{CardName}] 状态「{id}」已移除");
			OnStatusEffectRemoved(id);
		}
	}

	public void TickStatusEffects(TickTiming timing)
	{
		var expiredIds = _statusEffects
			.Where(kv => kv.Value.TickOn == timing)
			.Select(kv => { kv.Value.Tick(); return kv; })
			.Where(kv => kv.Value.IsExpired)
			.Select(kv => kv.Key)
			.ToList();

		foreach (var id in expiredIds)
		{
			RemoveStatusEffect(id);
		}
	}

	/// <summary>
	/// 状态直接伤害：绕过伤害管线、护甲与状态修饰。
	/// </summary>
	public void ApplyStatusDamage(int amount, string statusId)
	{
		if (amount <= 0 || IsDead)
			return;

		CurrentHealth -= amount;
		GD.Print($"[Minion:{CardName}] 状态「{statusId}」造成 {amount} 点直接伤害，剩余生命值：{CurrentHealth}");
		OnDamageTaken?.Invoke(new DamageEventInfo(amount, 0, false), null);
	}

	public bool HasStatusEffect(string id)
	{
		return _statusEffects.ContainsKey(id) && !_statusEffects[id].IsExpired;
	}

	public bool IsIncapacitated => HasStatusEffect(StatusEffect.IncapacitatedId);

	private void ApplyStatusEffectImmediate(StatusEffect effect)
	{
		switch (effect.Id)
		{
			case "attack_zero":
				Attack = 0;
				GD.Print($"[Minion:{CardName}] 攻击力被设为 0");
				break;
			case "meltdown":
				// 熔毁：防御力-1，最多叠加2层
				int currentStacks = HasStatusEffect("meltdown") ? _statusEffects["meltdown"].Stacks : 0;
				if (currentStacks <= 2) // 允许第1层和第2层触发
				{
					ModifyDefense(-1);
					GD.Print($"[Minion:{CardName}] 熔毁！防御力 {Defense}（已叠{currentStacks}层）");
				}
				break;
			case "vulnerable":
				_vulnerableModifier.IsActive = true;
				GD.Print($"[Minion:{CardName}] 获得易伤（受到伤害×1.5）");
				break;
			case "weak":
				_weakModifier.IsActive = true;
				GD.Print($"[Minion:{CardName}] 获得虚弱（造成伤害×0.75）");
				break;
			case "fragile":
				_fragileModifier.IsActive = true;
				GD.Print($"[Minion:{CardName}] 获得脆弱（护甲获得×0.75）");
				break;
			case "total_observation":
				// 总观效应：翻倍易伤/虚弱/脆弱效果
				_vulnerableModifier.ExtraMultiplier = 0.5f;
				_weakModifier.ExtraMultiplier = -0.25f;
				_fragileModifier.ExtraMultiplier = -0.25f;
				GD.Print($"[Minion:{CardName}] 获得总观效应（效果翻倍）");
				break;
		}
	}

	private void OnStatusEffectRemoved(string id)
	{
		switch (id)
		{
			case "attack_zero":
				if (!HasStatusEffect("attack_zero"))
				{
					Attack = Data.Attack; // 恢复到原始攻击力
					GD.Print($"[Minion:{CardName}] 攻击力已恢复为 {Attack}");
				}
				break;
			case "vulnerable":
				if (!HasStatusEffect("vulnerable"))
					_vulnerableModifier.IsActive = false;
				break;
			case "weak":
				if (!HasStatusEffect("weak"))
					_weakModifier.IsActive = false;
				break;
			case "fragile":
				if (!HasStatusEffect("fragile"))
					_fragileModifier.IsActive = false;
				break;
			case "total_observation":
				if (!HasStatusEffect("total_observation"))
				{
					_vulnerableModifier.ExtraMultiplier = 0f;
					_weakModifier.ExtraMultiplier = 0f;
					_fragileModifier.ExtraMultiplier = 0f;
				}
				break;
		}
	}

	// ===== 构造函数 =====

	/// <summary>
	/// 创建随从运行时实例。
	/// </summary>
	/// <param name="data">卡牌数据资源（必须是随从类型）</param>
	/// <param name="isPlayerSide">是否属于玩家方</param>
	public Minion(CardData data, bool isPlayerSide)
		: base(data)
	{
		IsPlayerSide = isPlayerSide;
		_focusModifier = new FocusSpellDamageModifier(GetFocusStacks);

		// 从卡牌数据复制战斗属性
		Attack = data.Attack;
		MaxHealth = data.Health;
		CurrentHealth = MaxHealth;

		// 复制防御力
		Defense = data.Defense;

		// 注册防御力修改器到 DamageModifiers
		_damageModifiers.Add(new OdysseyCards.Core.DefenseModifier(() => Defense));

		// 注册易伤/虚弱修改器（始终在列表中，由 IsActive 控制激活）
		_damageModifiers.Add(_vulnerableModifier);
		_damageModifiers.Add(_weakModifier);
		_damageModifiers.Add(_focusModifier);

		// 注册护甲攻击加成修改器
		_damageModifiers.Add(new ArmorAttackBonusModifier(this));

		if (data.BonusDamageToDefendedTargets != 0)
		{
			_damageModifiers.Add(new DefendedTargetDamageBonusModifier(data.BonusDamageToDefendedTargets));
		}

		// 解析关键词
		HasCharge = data.HasKeyword(Keyword.Charge);
		HasTaunt = data.HasKeyword(Keyword.Taunt);
		HasBattlecry = data.HasKeyword(Keyword.Battlecry) || data.BattlecryEffects?.Count > 0;
		HasDeathrattle = data.HasKeyword(Keyword.Deathrattle) || data.DeathrattleEffects?.Count > 0;
		HasWindfury = data.HasKeyword(Keyword.Windfury);
		HasAmbush = data.HasKeyword(Keyword.Ambush);
		HasImpact = data.HasKeyword(Keyword.Impact);
		HasRecycle = data.HasKeyword(Keyword.Recycle);
	}

	/// <summary>
	/// 从运行时卡牌创建随从，并复制该卡牌上的临时修饰。
	/// </summary>
	public Minion(Card card, bool isPlayerSide)
		: this(card.Data, isPlayerSide)
	{
		CopyRuntimeModifiersFrom(card);
	}

	/// <summary>
	/// 轮战：随从被击败后返回抽牌堆底部，不进入弃牌堆。
	/// </summary>
	public new bool HasRecycle { get; }

	/// <summary>
	/// 复制此随从返回牌堆时应保留的运行时牌面修饰。
	/// </summary>
	public Card ToRuntimeCard()
	{
		var card = new Card(Data);
		card.CopyRuntimeModifiersFrom(this);
		return card;
	}

	// ===== 伤害与治疗 =====

	/// <summary>
	/// 受到来自某个伤害来源的基础伤害。
	/// 通过 DamageResolver 统一计算最终伤害值，然后应用。
	/// </summary>
	/// <param name="baseDamage">基础伤害值</param>
	/// <param name="source">伤害来源</param>
	public void TakeDamage(int baseDamage, IDamageSource? source)
	{
		TakeDamage(baseDamage, source, DamageKind.Attack);
	}

	/// <summary>
	/// 受到指定类型的基础伤害。
	/// </summary>
	/// <param name="baseDamage">基础伤害值</param>
	/// <param name="source">伤害来源</param>
	/// <param name="kind">伤害结算类型</param>
	public void TakeDamage(int baseDamage, IDamageSource? source, DamageKind kind)
	{
		// 死实体不接受任何伤害（防止反击链打到已死亡的单位产生跳字）
		if (IsDead)
			return;

		// Step 1: ALWAYS go through DamageResolver first (Defense modifier applies here)
		int resolvedDamage = DamageResolver.ResolveDamage(baseDamage, source, this, kind);
		int armorAbsorbed = 0;

		// Step 2: Armor absorbs remaining damage AFTER defense (与 Hero 统一)
		if (_currentArmor > 0)
		{
			armorAbsorbed = Math.Min(_currentArmor, resolvedDamage);
			_currentArmor -= armorAbsorbed;
			resolvedDamage -= armorAbsorbed;

			GD.Print($"[Minion:{CardName}] 护甲吸收了 {armorAbsorbed} 点伤害（防御力调整后），剩余护甲：{_currentArmor}");

			if (resolvedDamage <= 0)
			{
				OnDamageTaken?.Invoke(new DamageEventInfo(0, armorAbsorbed, true), source);
				return;
			}
		}

		ApplyDamage(resolvedDamage, source);

		// 伤害事件通知（ApplyDamage 之后触发，保证 HP 已减少）
		OnDamageTaken?.Invoke(new DamageEventInfo(resolvedDamage, armorAbsorbed, false), source);
	}

	/// <summary>
	/// 应用最终计算后的伤害值。
	/// 实现 IDamageTarget 接口方法，扣除实际生命值并输出日志。
	/// </summary>
	/// <param name="finalDamage">最终伤害值（已通过所有修改器）</param>
	/// <param name="source">伤害来源</param>
	public void ApplyDamage(int finalDamage, IDamageSource source)
	{
		CurrentHealth -= finalDamage;
		GD.Print($"{CardName} 受到 {finalDamage} 点伤害，剩余生命值：{CurrentHealth}");

		// 未来可在此处触发受伤事件（如苦痛侍僧抽牌等）
	}

	/// <summary>
	/// ChatScreen 专用直伤：绕过伤害管线、护甲与状态修饰，直接扣除生命值。
	/// </summary>
	internal void ApplyDevDamage(int amount)
	{
		if (amount <= 0)
			return;

		CurrentHealth -= amount;
		GD.Print($"[Minion:{CardName}] ChatScreen 直伤 {amount}，剩余生命值：{CurrentHealth}");
	}

	/// <summary>
	/// 为随从恢复生命值。随从生命没有上限；超过原最大生命时同步抬高 MaxHealth，等价于贴膜。
	/// </summary>
	/// <param name="amount">恢复量</param>
	public void Heal(int amount)
	{
		if (amount <= 0 || IsDead)
			return;

		int beforeHeal = CurrentHealth;
		CurrentHealth += amount;
		if (CurrentHealth > MaxHealth)
			MaxHealth = CurrentHealth;
		int healed = CurrentHealth - beforeHeal;

		if (healed > 0)
		{
			OnHealed?.Invoke(healed);
			GD.Print($"{CardName} 恢复了 {healed} 点生命值，当前生命值：{CurrentHealth}");
		}
	}

	/// <summary>
	/// 同时修改攻击力和最大生命值，并同步恢复等量生命。
	/// </summary>
	public void GainStats(int attackDelta, int healthDelta)
	{
		ModifyAttack(attackDelta);
		MaxHealth = Math.Max(1, MaxHealth + healthDelta);
		CurrentHealth = Math.Max(0, CurrentHealth + healthDelta);
	}

	// ===== 信息方法 =====


	// ===== 效果显示器数据聚合 =====

	/// <summary>
	/// 获取此随从当前所有应显示的效果图标数据。
	/// 聚合 StatusEffect + 数值变化 + 授予的关键词 + 运行时修饰。
	/// </summary>
	public List<DisplayableEffect> GetDisplayableEffects()
	{
		var effects = new List<DisplayableEffect>();

		// 1. StatusEffects
		foreach (var (id, se) in _statusEffects)
		{
			if (se.IsExpired)
				continue;
			var data = EffectIconTable.GetStatusEffect(id);
			if (data == null)
				continue;
			effects.Add(new DisplayableEffect
			{
				Icon = data.Value.Icon,
				Name = Localization.Localization.T(data.Value.NameKey, ""),
				Stacks = se.Stacks,
				Description = Localization.Localization.T(data.Value.DescKey, ""),
				IsBuff = !se.IsNegative,
				Category = EffectCategory.StatusEffect,
			});
		}

		// 2. ActiveDomains
		foreach (var (domainId, domain) in _activeDomains)
		{
			var data = EffectIconTable.GetDomain(domainId);
			if (data == null)
				continue;

			effects.Add(EffectIconTable.ToDisplayable(
				data.Value,
				EffectCategory.Domain,
				domain.StackCount));
		}

		// 3. Numerical stat changes (compared to CardData baseline)
		// HP changes are displayed on the health bar, not here.
		int attackDelta = Attack - Data.Attack;
		if (attackDelta != 0)
		{
			bool isBuff = attackDelta > 0;
			string icon = isBuff ? "⚔" : "🔽";
			string sign = isBuff ? "+" : "";
			effects.Add(new DisplayableEffect
			{
				Icon = icon,
				Name = isBuff
					? Localization.Localization.T("stat.attack_up", "攻击力+{value}").Replace("{value}", attackDelta.ToString())
					: Localization.Localization.T("stat.attack_down", "攻击力{value}").Replace("{value}", attackDelta.ToString()),
				Stacks = Math.Abs(attackDelta),
				Description = "",
				IsBuff = isBuff,
				Category = EffectCategory.StatBuff,
			});
		}

		int defenseDelta = Defense - Data.Defense;
		if (defenseDelta != 0 && !HasStatusEffect("meltdown"))
		{
			bool isBuff = defenseDelta > 0;
			string icon = isBuff ? "🛡" : "💔";
			string sign = isBuff ? "+" : "";
			effects.Add(new DisplayableEffect
			{
				Icon = icon,
				Name = isBuff
					? Localization.Localization.T("stat.defense_up", "防御力+{value}").Replace("{value}", defenseDelta.ToString())
					: Localization.Localization.T("stat.defense_down", "防御力{value}").Replace("{value}", defenseDelta.ToString()),
				Stacks = Math.Abs(defenseDelta),
				Description = "",
				IsBuff = isBuff,
				Category = EffectCategory.StatBuff,
			});
		}

		// 4. Granted keywords (not from CardData baseline)
		CollectGrantedKeywordEffects(effects);

	// 5. Runtime modifiers
	if (HasIdolTwilightBuff)
	{
		var data = EffectIconTable.GetDomain("idol_twilight");
		if (data != null)
		{
			effects.Add(EffectIconTable.ToDisplayable(
				data.Value, EffectCategory.Domain, 1));
		}
	}

		if (_deathrattleReplaced)
		{
			effects.Add(new DisplayableEffect
			{
				Icon = "💀",
				Name = Localization.Localization.T("modifier.deathrattle_replaced", "亡语替换"),
				Stacks = 0,
				Description = Localization.Localization.T("modifier.deathrattle_replaced_desc", "亡语效果已被替换。"),
				IsBuff = true,
				Category = EffectCategory.Modifier,
			});
		}

		// 敌意：受到来自玩家阵营的伤害翻倍
		if (_damageModifiers.Any(m => m is OdysseyCards.Core.AnimosityDamageModifier))
		{
			var data = EffectIconTable.GetModifier("animosity");
			if (data != null)
			{
				effects.Add(EffectIconTable.ToDisplayable(
					data.Value, EffectCategory.Modifier));
			}
		}

		return effects;
	}

	/// <summary>
	/// 收集运行时授予的关键词效果（非 CardData 自带的关键词），
	/// 按来源分组显示。
	/// </summary>
	private void CollectGrantedKeywordEffects(List<DisplayableEffect> effects)
	{
		// 诱饵战术：授予伏击 + 冲击 + 被攻击时敌方英雄防御-1
		if (HasBaitTacticsOnAttacked)
		{
			var data = EffectIconTable.GetKeywordSource("bait_tactics");
			if (data != null)
			{
				effects.Add(EffectIconTable.ToDisplayable(
					data.Value, EffectCategory.Keyword));
			}
		}

		// 单个授予的关键词（非 CardData 自带）
		if (HasTaunt && !Data.HasKeyword(Keyword.Taunt))
		{
			effects.Add(new DisplayableEffect
			{
				Icon = "🛡",
				Name = Localization.Localization.T("keyword.taunt", "嘲讽"),
				Stacks = 0,
				Description = Localization.Localization.T("keyword.taunt_desc", "敌方随从必须优先攻击此随从。"),
				IsBuff = true,
				Category = EffectCategory.Keyword,
				SourceId = "granted_taunt",
			});
		}

		if (HasAmbush && !Data.HasKeyword(Keyword.Ambush) && !HasBaitTacticsOnAttacked)
		{
			effects.Add(new DisplayableEffect
			{
				Icon = "🗡",
				Name = Localization.Localization.T("keyword.ambush", "伏击"),
				Stacks = 0,
				Description = Localization.Localization.T("keyword.ambush_desc", "每回合首次被攻击时，先于攻击者造成反击伤害。"),
				IsBuff = true,
				Category = EffectCategory.Keyword,
				SourceId = "granted_ambush",
			});
		}

		if (HasImpact && !Data.HasKeyword(Keyword.Impact) && !HasBaitTacticsOnAttacked)
		{
			effects.Add(new DisplayableEffect
			{
				Icon = "💥",
				Name = Localization.Localization.T("keyword.impact", "冲击"),
				Stacks = 0,
				Description = Localization.Localization.T("keyword.impact_desc", "攻击时抵消所有反击伤害（一次性消耗）。"),
				IsBuff = true,
				Category = EffectCategory.Keyword,
				SourceId = "granted_impact",
			});
		}
	}
}
