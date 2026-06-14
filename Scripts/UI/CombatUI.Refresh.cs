#nullable enable
using System.Linq;
using Godot;
using OdysseyCards.AI;
using AIIntents = OdysseyCards.AI.Intents;
using OdysseyCards.Card;
using OdysseyCards.Core;
using OdysseyCards.Combat;
using OdysseyCards.Infrastructure;
using Loc = OdysseyCards.Localization.Localization;

namespace OdysseyCards.UI;

/// <summary>
/// CombatUI 刷新管线——从游戏状态同步到 UI 显示的所有更新方法。
/// </summary>
public partial class CombatUI
{
	// ===== 刷新方法 =====

	/// <summary>
	/// 刷新所有子组件——棋盘、手牌、生命值、法力值和护甲。
	/// 在每次操作完成后调用以确保界面与游戏状态同步。
	/// </summary>
	public void RefreshAll()
	{
		// 发现选牌或手牌选择阶段：仅更新状态显示，跳过手牌和棋盘刷新
		if (_combat != null && _combat.IsDiscovering)
		{
			CleanupDragCard();
			UpdateHealthBars();
			UpdateManaDisplay();
			UpdateArmorDisplay();
			UpdateDefenseDisplay();
			UpdateDeckCounts();
			UpdateHeroPowerButton();
			UpdateWeaponDisplay();
			UpdateStatusEffectDisplay();
			UpdateHeatDisplay();
			UpdateRelicDisplay();

			// 手牌选择模式下，额外更新卡牌高亮状态
			if (_isHandSelecting)
			{
				RefreshHandSelectionHighlights();
			}
			return;
		}

		if (_combat == null)
		{
			return;
		}

		// 清理拖拽中的卡牌 UI（含取消事件订阅）
		CleanupDragCard();

		_boardUI.RefreshBoard();
		var playerHero = _combat.PlayerHero;
		if (playerHero != null)
		{
			_boardUI.UpdateActionCostDimming(playerHero.CurrentMana);
		}
		_handUI.RefreshHand();
		UpdateHealthBars();
		UpdateManaDisplay();
		UpdateArmorDisplay();
		UpdateDefenseDisplay();
		UpdateDeckCounts();
		UpdateHeroPowerButton();

		// 每次刷新时重置为正常模式（先重置再更新武器，避免显示被覆盖）
		ResetSelection();

		UpdateWeaponDisplay();
		UpdateStatusEffectDisplay();
		RefreshIntentDisplay();
		RefreshIntentArrows();
		UpdateHeatDisplay();
		UpdateRelicDisplay();

		// 游戏结束时禁用操作
		if (_combat.State.IsGameOver)
		{
			_endTurnButton.Disabled = true;
			_heroPowerButton.Disabled = true;
		}
	}

	/// <summary>
	/// 刷新敌方意图显示——根据当前战场状态重新计算攻击目标和伤害数值。
	/// 若敌方回合动画进行中则跳过（冻结机制，参考 STS2 的 NIntent._isFrozen）。
	/// </summary>
	private void RefreshIntentDisplay()
	{
		if (_combat == null)
			return;

		// 冻结检查：敌方回合执行动画期间不刷新，防止数值跳变
		if (_combat.IsEnemyTurnAnimating)
			return;

		// 通过卡片刷新所有敌人意图（包括首个和额外）
		foreach (var card in _enemyCards)
			card.Refresh(_combat);

		// 向后兼容：首个敌人意图标签
		if (_enemyIntentLabel != null && _enemyCards.Count > 0)
			_enemyIntentLabel.Text = _combat.GetCurrentEnemyIntent().GetDisplayDescription(_combat);
	}

	/// <summary>
	/// 刷新敌方意图箭头——根据当前战场状态绘制红色攻击箭头和蓝色增益箭头。
	/// 每次 OnCombatStateChanged 触发时调用。
	/// </summary>
	private void RefreshIntentArrows()
	{
		if (_combat == null || _arrowRenderer == null)
			return;

		// 冻结检查：敌方回合执行动画期间不刷新
		if (_combat.IsEnemyTurnAnimating)
			return;

		// 清除旧的意图箭头（前缀 "intent_"）
		_arrowRenderer.ClearArrows();

		// 清除敌方槽位的旧意图文字（由下面的循环重新设置）
		for (int i = 0; i < Board.MaxSlotsPerSide; i++)
			_boardUI.SetSlotIntentText(i, isPlayerSide: false, null);

		for (int i = 0; i < _combat.EnemyUnits.Count; i++)
		{
			var unit = _combat.EnemyUnits[i];

			// 死亡敌人不绘制意图箭头
			if (unit.Body.IsDead)
				continue;

			var intent = unit.GetCurrentIntent(_combat);
			switch (intent.Type)
			{
				case AIIntents.IntentType.Attack:
				{
					var target = intent.GetTarget(_combat);
					Vector2 targetPos;
					if (target is Minion minionTarget)
					{
						targetPos = GetMinionScreenCenter(minionTarget);
					}
					else if (target is Hero)
					{
						// 敌人攻击玩家英雄
						targetPos = GetPlayerHeroScreenCenter();
					}
					else
					{
						// 未能解析目标，默认指向玩家英雄
						targetPos = GetPlayerHeroScreenCenter();
					}

					if (targetPos != Vector2.Zero)
					{
						var source = GetEnemyIdentityCardAnchor(i, targetPos);
						if (source == Vector2.Zero)
							break;
						_arrowRenderer.AddArrow($"intent_attack_{i}", source, targetPos, ArrowRenderer.EnemyAttackColor);
					}
					break;
				}

				case AIIntents.IntentType.Buff:
				{
					// 增益意图：指向敌方战场上的首个友方随从
					Vector2? buffTarget = null;
					for (int slot = 0; slot < Board.MaxSlotsPerSide; slot++)
					{
						var friendly = _combat.Board.GetMinionAt(slot, isPlayerSide: false);
						if (friendly != null && !friendly.IsDead)
						{
							buffTarget = GetMinionScreenCenter(friendly);
							break;
						}
					}

					// 若无友方随从，指向敌人自身身份卡（自增益）
					buffTarget ??= GetEnemyIdentityCardCenter(i);

					var source = GetEnemyIdentityCardAnchor(i, buffTarget.Value);
					if (source != Vector2.Zero)
						_arrowRenderer.AddArrow($"intent_buff_{i}", source, buffTarget.Value, ArrowRenderer.BuffColor);
					break;
				}
			}
		}

		// === 敌方随从意图箭头 ===
		var enemyMinions = _combat.Board.GetEnemyMinions();
		foreach (var minion in enemyMinions)
		{
			if (minion.IsDead)
				continue;
			int slotIndex = minion.BoardSlotIndex;
			if (slotIndex < 0)
				continue;

			Vector2 sourcePos = _boardUI.GetSlotScreenCenter(slotIndex, isPlayerSide: false);

			// 获取意图：优先使用 MoveState（新系统），否则回退 EnemyIntent（旧系统）
			EnemyIntent intent;
			if (minion.IntentBrain != null && minion.IntentBrain.HasMoveStates)
			{
				// 新系统：从 MoveState.Intents 构建显示
				intent = BuildIntentFromMoveState(minion.IntentBrain, _combat, minion);
			}
			else if (minion.IntentBrain != null)
			{
				// 旧系统：直接用 EnemyIntent
				intent = minion.IntentBrain.GetCurrentIntent(_combat);
			}
			else
			{
				var playerTaunts = _combat.Board.GetTaunts(ofEnemy: false);
				IDamageTarget? target;
				if (playerTaunts.Count > 0)
					target = playerTaunts[0];
				else
					target = _combat.PlayerHero;

				int dmg = DamageResolver.ResolvePreviewDamage(minion.Attack, minion, target);
				intent = new EnemyIntent(AIIntents.IntentType.Attack, dmg, Loc.T("intent.attack_format", "对{target}造成 {damage} 点伤害").Replace("{target}", "英雄").Replace("{damage}", dmg.ToString()));
				intent.TargetSelector = _ => target;
			}

			string key = $"intent_minion_{slotIndex}";

			if (intent.Type == AIIntents.IntentType.Attack)
			{
				var target = intent.TargetSelector?.Invoke(_combat);
				Vector2 targetPos = ResolveTargetScreenPos(target);
				_arrowRenderer.AddArrow(key, sourcePos, targetPos, ArrowRenderer.EnemyAttackColor);
			}
			else if (intent.Type == AIIntents.IntentType.Buff)
			{
				// 增益意图：指向敌方战场首个友方随从
				var friendlies = _combat.Board.GetEnemyMinions()
					.Where(m => !m.IsDead && m != minion).ToList();
				Vector2 targetPos = friendlies.Count > 0
					? _boardUI.GetSlotScreenCenter(friendlies[0].BoardSlotIndex, isPlayerSide: false)
					: sourcePos;
				_arrowRenderer.AddArrow(key, sourcePos, targetPos, ArrowRenderer.BuffColor);
			}

			// 设置槽位意图文字
			string desc = intent.GetDisplayDescription(_combat);
			_boardUI.SetSlotIntentText(slotIndex, isPlayerSide: false, desc);
		}
	}

	/// <summary>
	/// 从 MoveState 的 AbstractIntent 列表构建用于显示/箭头的 EnemyIntent。
	/// 新系统统一入口：Boss 与随从的意图均通过此方法转为显示格式。
	/// </summary>
	private static EnemyIntent BuildIntentFromMoveState(IIntentActor brain, CombatManager combat, Card.Minion minion)
	{
		var move = brain.GetCurrentMove(combat);
		if (move == null || move.Intents.Count == 0)
			return new EnemyIntent(AIIntents.IntentType.Attack, 0, "—");

		// 取第一个意图（最常见），多意图叠加时取主要意图
		var primary = move.Intents[0];

		return primary switch
		{
			AIIntents.AttackIntent atk => new EnemyIntent(AIIntents.IntentType.Attack,
				atk.GetTotalDamage(combat),
				atk.GetIntentLabel(combat))
			{
				// 攻击意图：箭头指向目标（英雄或嘲讽随从）
				TargetSelector = _ =>
				{
					var taunts = combat.Board.GetTaunts(ofEnemy: false);
					return taunts.Count > 0 ? taunts[0] : combat.PlayerHero;
				}
			},
			AIIntents.BuffIntent => new EnemyIntent(AIIntents.IntentType.Buff, 0,
				primary.GetIntentLabel(combat)),
			AIIntents.DefendIntent => new EnemyIntent(AIIntents.IntentType.Defend, 0,
				primary.GetIntentLabel(combat)),
			_ => new EnemyIntent(AIIntents.IntentType.Attack, 0,
				primary.GetIntentLabel(combat)),
		};
	}

	/// <summary>
	/// 更新双方英雄生命值条。
	/// </summary>
	private void UpdateHealthBars()
	{
		if (_combat == null)
			return;

		_playerHealthBar.UpdateHealth(_combat.PlayerHero.CurrentHealth, _combat.PlayerHero.MaxHealth);

		// 敌方 HP 也在此处刷新——攻击/法术施放后 RefreshAll 不会触发
		// OnCombatStateChanged（只有放置/移除随从才触发），因此需要显式更新。
		foreach (var card in _enemyCards)
		{
			var unit = _combat.EnemyUnits[card.EnemyIndex];
			card.RefreshHealth(unit.Body.CurrentHealth, unit.Body.MaxHealth);
			card.RefreshArmor(unit.Body.CurrentArmor);
		}
	}

	/// <summary>
	/// 更新玩家法力值显示，格式「法力 Current/Max」。
	/// 敌人使用意图系统，不跟踪法力值。
	/// </summary>
	private void UpdateManaDisplay()
	{
		if (_combat == null)
			return;

		_playerManaLabel.Text = Localization.Localization.T("ui.combat.mana_format", "法力 {current}/{max}")
			.Replace("{current}", _combat.PlayerHero.CurrentMana.ToString())
			.Replace("{max}", _combat.PlayerHero.MaxMana.ToString());
	}

	/// <summary>
	/// 更新双方护甲值显示——护甲 > 0 时显示标签，否则隐藏。
	/// </summary>
	private void UpdateArmorDisplay()
	{
		if (_combat == null)
			return;

		// 玩家护甲
		int playerArmor = _combat.PlayerHero.CurrentArmor;
		_playerArmorLabel.Visible = playerArmor > 0;
		if (playerArmor > 0)
		{
			_playerArmorLabel.Text = Localization.Localization.T("ui.combat.armor_format", "护甲: {value}").Replace("{value}", playerArmor.ToString());
		}

		// 敌方护甲（已迁移到 EnemyIdentityCard，旧版 UI 跳过）
		if (_enemyArmorLabel != null)
		{
			int enemyArmor = _combat.GetDefaultEnemyTargetUnit()?.Body.CurrentArmor ?? 0;
			_enemyArmorLabel.Visible = enemyArmor > 0;
			if (enemyArmor > 0)
			{
				_enemyArmorLabel.Text = Localization.Localization.T("ui.combat.armor_format", "护甲: {value}").Replace("{value}", enemyArmor.ToString());
			}
		}
	}

	/// <summary>
	/// 更新双方防御力显示——防御 != 0 时显示标签，否则隐藏。
	/// 正防御显示为蓝色（增益），负防御显示为红色（减益/脆弱）。
	/// </summary>
	private void UpdateDefenseDisplay()
	{
		if (_combat == null)
			return;

		// 玩家防御
		int playerDef = _combat.PlayerHero.Defense;
		_playerDefenseLabel.Visible = playerDef != 0;
		if (playerDef != 0)
		{
			_playerDefenseLabel.Text = Localization.Localization.T("ui.combat.defense_format", "防御: {value}").Replace("{value}", playerDef >= 0 ? $"+{playerDef}" : $"{playerDef}");
			_playerDefenseLabel.AddThemeColorOverride("font_color",
				playerDef > 0 ? new Color(0.3f, 0.7f, 1f) : new Color(1f, 0.3f, 0.3f));
		}

		// 敌方防御（已迁移到 EnemyIdentityCard，旧版 UI 跳过）
		if (_enemyDefenseLabel != null)
		{
			int enemyDef = _combat.GetDefaultEnemyTargetUnit()?.Body.Defense ?? 0;
			_enemyDefenseLabel.Visible = enemyDef != 0;
			if (enemyDef != 0)
			{
				_enemyDefenseLabel.Text = Localization.Localization.T("ui.combat.defense_format", "防御: {value}").Replace("{value}", enemyDef >= 0 ? $"+{enemyDef}" : $"{enemyDef}");
				_enemyDefenseLabel.AddThemeColorOverride("font_color",
					enemyDef > 0 ? new Color(0.3f, 0.7f, 1f) : new Color(1f, 0.3f, 0.3f));
			}
		}
	}

	/// <summary>
	/// 更新武器信息显示——攻击力、费用、冷却信息。
	/// 普通模式下显示武器攻击按钮（如果可用），技能按钮显示冷却状态。
	/// </summary>
	private void UpdateWeaponDisplay()
	{
		if (_combat == null)
			return;

		// --- 玩家武器 ---
		var weapon = _combat.PlayerHero.Weapon;
		if (weapon != null)
		{
			// 武器信息标签
			string costSuffix = Localization.Localization.T("ui.combat.cost_suffix", "费");
			string costText = weapon.AttackCost > 0 ? $"{weapon.AttackCost}{costSuffix}" : Localization.Localization.T("ui.combat.free_cost", "免费");
			string disabledText = weapon.IsDisabled ? Localization.Localization.T("ui.combat.disabled_suffix", " [禁用]") : "";
			string localWeaponName = !string.IsNullOrEmpty(weapon.NameKey)
				? Localization.Localization.T(weapon.NameKey, weapon.Name)
				: weapon.Name;
			_weaponInfoLabel.Text = Localization.Localization.T("ui.combat.weapon_format", "{name} {attack}攻 {cost}")
				.Replace("{name}", localWeaponName)
				.Replace("{attack}", weapon.Attack.ToString())
				.Replace("{cost}", costText) + disabledText;

			if (weapon.PassiveSkill != null)
			{
				string localPassiveDesc = !string.IsNullOrEmpty(weapon.PassiveSkill.DescKey)
					? Localization.Localization.T(weapon.PassiveSkill.DescKey, weapon.PassiveSkill.Description)
					: weapon.PassiveSkill.Description;
				_weaponInfoLabel.TooltipText = Localization.Localization.T("ui.combat.passive_skill", "被动：{desc}")
					.Replace("{desc}", localPassiveDesc);
			}

			// 武器攻击按钮——普通模式下显示
			if (_selectionMode == SelectionMode.Normal && !_combat.State.IsGameOver)
			{
				bool canAttack = _combat.PlayerHero.CanWeaponAttack()
					&& _combat.PlayerHero.CanSpendMana(weapon.AttackCost);
				_weaponAttackButton.Visible = true;
				_weaponAttackButton.Disabled = !canAttack || weapon.IsDisabled;
				_weaponAttackButton.Text = weapon.IsDisabled
					? Localization.Localization.T("ui.combat.weapon_disabled", "⚔ 武器攻击 [禁用]")
					: Localization.Localization.T("ui.combat.weapon_attack_cost", "⚔ 武器攻击 ({cost}费)").Replace("{cost}", weapon.AttackCost.ToString());
			}
			else
			{
				_weaponAttackButton.Visible = false;
			}

			// 主动技能按钮
			if (weapon.ActiveSkill != null)
			{
				var active = weapon.ActiveSkill;
				bool skillVisible = (_selectionMode == SelectionMode.Normal || _selectionMode == SelectionMode.SelectingActiveSkillTarget) && !_combat.State.IsGameOver;
				_weaponActiveSkillButton.Visible = skillVisible;
				_weaponActiveSkillButton.Disabled = !active.CanUse(_combat.PlayerHero);

				if (active.CurrentCooldown > 0)
				{
					string localActiveName = !string.IsNullOrEmpty(active.NameKey)
						? Localization.Localization.T(active.NameKey, active.Name)
						: active.Name;
					_weaponActiveSkillButton.Text = Localization.Localization.T("ui.combat.skill_cooldown", "✦ {name} (冷却{cooldown})")
						.Replace("{name}", localActiveName).Replace("{cooldown}", active.CurrentCooldown.ToString());
				}
				else
				{
					string localActiveName = !string.IsNullOrEmpty(active.NameKey)
						? Localization.Localization.T(active.NameKey, active.Name)
						: active.Name;
					string text = Localization.Localization.T("ui.combat.skill_cost", "✦ {name} ({cost}费)")
						.Replace("{name}", localActiveName).Replace("{cost}", active.Cost.ToString());
					if (active is IChargeCooldownSkill chargeSkill)
					{
						text += Localization.Localization.T("ui.combat.skill_charges_suffix", " [{charges}/{max}]")
							.Replace("{charges}", chargeSkill.Charges.ToString())
							.Replace("{max}", chargeSkill.MaxCharges.ToString());
					}
					_weaponActiveSkillButton.Text = text;
				}
		}
			else
			{
				_weaponActiveSkillButton.Visible = false;
			}
		}
		else
		{
			_weaponInfoLabel.Text = Localization.Localization.T("ui.combat.weapon_none", "无武器");
			_weaponAttackButton.Visible = false;
			_weaponActiveSkillButton.Visible = false;
		}

		// --- 敌方武器（已迁移到 EnemyIdentityCard） ---
		if (_enemyWeaponLabel != null)
		{
			var enemyWeapon = _combat.GetDefaultEnemyTargetUnit()?.Body.Weapon;
			if (enemyWeapon != null)
			{
				string disabledText = enemyWeapon.IsDisabled ? Localization.Localization.T("ui.combat.disabled_suffix", " [禁用]") : "";
				string localEnemyWeaponName = !string.IsNullOrEmpty(enemyWeapon.NameKey)
					? Localization.Localization.T(enemyWeapon.NameKey, enemyWeapon.Name)
					: enemyWeapon.Name;
				_enemyWeaponLabel.Text = Localization.Localization.T("ui.combat.enemy_weapon_format", "武器: {name} {attack}攻{disabled}")
					.Replace("{name}", localEnemyWeaponName).Replace("{attack}", enemyWeapon.Attack.ToString()).Replace("{disabled}", disabledText);
			}
			else
			{
				_enemyWeaponLabel.Text = "";
			}
		}
	}

	/// <summary>
	/// 更新双方英雄的效果图标显示。
	/// 通过 Hero.GetDisplayableEffects() 聚合所有效果，传入 EffectBar。
	/// </summary>
	private void UpdateStatusEffectDisplay()
	{
		if (_combat == null)
			return;

		// 玩家英雄效果
		_playerEffectBar.Populate(_combat.PlayerHero.GetDisplayableEffects());

		// 敌方英雄效果（旧版单敌人兼容层——多敌人时使用 EnemyIdentityCard 内的 EffectBar）
		if (_enemyEffectBar != null)
		{
			_enemyEffectBar.Populate(_combat.GetDefaultEnemyTargetUnit()?.Body.GetDisplayableEffects() ?? []);
		}
	}

	private void UpdateHeatDisplay() => _heatBar?.Refresh();
	private void UpdateRelicDisplay() => _relicBar?.Refresh();

	/// <summary>
	/// 根据当前牌堆状态更新按钮文字。
	/// </summary>
	private void UpdateDeckCounts()
	{
		if (_combat == null)
			return;

		var deckState = _combat.PlayerHero.DeckState;
		_drawPileBtn.Text = Localization.Localization.T("ui.combat.draw_pile_format", "抽牌堆 ({count})").Replace("{count}", deckState.DrawPile.Count.ToString());
		_discardPileBtn.Text = Localization.Localization.T("ui.combat.discard_pile_format", "弃牌堆 ({count})").Replace("{count}", deckState.DiscardPile.Count.ToString());
	}

	/// <summary>
	/// 根据当前战斗状态刷新英雄技能按钮的启用/禁用和文本。
	/// </summary>
	private void UpdateHeroPowerButton()
	{
		if (_heroPowerButton == null || _combat == null)
			return;

		var heroPower = _combat.PlayerHero.HeroPower;
		if (heroPower == null)
		{
			_heroPowerButton.Visible = false;
			return;
		}

		_heroPowerButton.Visible = true;

		bool isPlayerTurn = _combat.State.IsPlayerTurn;
		bool alreadyUsed = _combat.HeroPowerUsedThisTurn;
		bool canAfford = _combat.PlayerHero.CurrentMana >= heroPower.Cost;
		bool isDiscovering = _combat.IsDiscovering;
		bool gameOver = _combat.State.IsGameOver;
		IChargeCooldownSkill? chargeSkill = heroPower as IChargeCooldownSkill;
		bool hasCharges = chargeSkill == null || chargeSkill.Charges > 0;

		bool canUse = isPlayerTurn && !alreadyUsed && canAfford && hasCharges && !isDiscovering && !gameOver;

		_heroPowerButton.Disabled = !canUse;

		// 更新按钮文本：显示技能名称 + 费用 + 状态
		string name = heroPower.Name;
		string costStr = heroPower.Cost.ToString();
		string text;
		if (chargeSkill != null && chargeSkill.Charges <= 0)
		{
			text = Localization.Localization.T("ui.combat.hero_power_cooldown", "{name} ({cost}费) [冷却{cooldown}]")
				.Replace("{name}", name)
				.Replace("{cost}", costStr)
				.Replace("{cooldown}", chargeSkill.CurrentCooldown.ToString());
		}
		else if (alreadyUsed)
		{
			text = $"{name} ({costStr}费) [已用]";
		}
		else if (!canAfford)
		{
			text = $"{name} ({costStr}费) [法力不足]";
		}
		else
		{
			text = $"{name} ({costStr}费)";
		}

		if (heroPower is IChargeCooldownSkill chargeDisplay)
		{
			text += Localization.Localization.T("ui.combat.skill_charges_suffix", " [{charges}/{max}]")
				.Replace("{charges}", chargeDisplay.Charges.ToString())
				.Replace("{max}", chargeDisplay.MaxCharges.ToString());
		}

		_heroPowerButton.Text = text;
	}

	/// <summary>
	/// 更新移动端取消按钮的可见性。
	/// 在非 Normal 选择模式或手牌选择模式下显示，帮助移动端用户取消当前操作（替代桌面端右键）。
	/// </summary>
	private void UpdateMobileCancelButton()
	{
		if (_mobileCancelButton == null)
			return;

		bool shouldShow = MobileInputRouter.IsMobile
			&& (_selectionMode != SelectionMode.Normal || _isHandSelecting);
		_mobileCancelButton.Visible = shouldShow;
	}
}
