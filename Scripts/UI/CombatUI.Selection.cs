#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using OdysseyCards.Card;
using OdysseyCards.Combat;
using OdysseyCards.Core;
using OdysseyCards.Infrastructure;
using Loc = OdysseyCards.Localization.Localization;

namespace OdysseyCards.UI;

/// <summary>
/// CombatUI 选择交互——8 种 SelectionMode 状态机 + 攻击拖拽 + 目标选择 + 手牌选择模式 + 发现 UI + 开发者伤害模式。
/// </summary>
public partial class CombatUI
{
	// ===== 选择状态 =====

	/// <summary>
	/// 当前交互模式。
	/// </summary>
	private enum SelectionMode
	{
		/// <summary>默认——无选中，等待玩家操作。</summary>
		Normal,
		/// <summary>随从放置模式——手牌中选了一张随从牌，等待选择玩家槽位。</summary>
		PlacingMinion,
		/// <summary>随从部署战吼目标模式——已选好支援线槽位，等待选择部署战吼目标。</summary>
		TargetingMinionBattlecry,
		/// <summary>法术目标模式——手牌中选了一张法术牌，等待选择目标。</summary>
		TargetingSpell,
		/// <summary>攻击目标模式——棋盘上选了一个己方随从，等待选择敌方目标。</summary>
		SelectingAttackTarget,
		/// <summary>武器攻击目标模式——玩家点击武器攻击后，等待选择敌方目标（随从或英雄）。</summary>
		SelectingWeaponTarget,
		/// <summary>武器主动技能目标模式——玩家点击主动技能后，等待选择敌方目标（随从或英雄）。无视嘲讽。</summary>
		SelectingActiveSkillTarget,
		/// <summary>无目标卡牌打出模式——拖拽到屏幕中央播放区域打出（类 STS2 风格）。</summary>
		PlayingNoTargetCard,
		/// <summary>开发者伤害模式——点击任意实体造成指定伤害。</summary>
		DevDamageTargeting,
	}

	/// <summary>
	/// 当前被选中为攻击/施法目标的敌人索引。用于点击敌方身份卡时分发正确的目标。
	/// -1 表示未选中任何敌人。
	/// </summary>
	private int _activeEnemyTargetIndex = -1;

	/// <summary>目标型部署中已选定的支援线槽位；-1 表示未进入该流程。</summary>
	private int _pendingMinionSlot = -1;

	/// <summary>选择流程统一指针采样：虚拟指针激活时优先，确保自动化拖拽与桌面路径一致。</summary>
	private Vector2 GetSelectionPointerPosition() => EvalGateway.GetEffectiveMousePosition(this);

	private bool IsSelectingHandCardForPlay() => _selectionMode is SelectionMode.PlacingMinion
		or SelectionMode.TargetingMinionBattlecry or SelectionMode.TargetingSpell or SelectionMode.PlayingNoTargetCard;

	// ===== 事件处理——棋盘点击 =====

	/// <summary>
	/// 棋盘槽位点击事件处理。
	/// 根据当前选择模式分发到不同的处理流程：
	/// <list type="bullet">
	/// <item>随从放置模式 → 在点击的玩家槽位召唤随从</item>
	/// <item>攻击目标模式 → 对点击的敌方槽位发动攻击</item>
	/// <item>普通模式 → 选中己方随从进入攻击目标模式</item>
	/// </list>
	/// </summary>
	/// <param name="position">三线绝对位置，前线阵营由当前驻扎单位确定。</param>
	private void OnBoardSlotClicked(BoardPosition position)
	{
		int slotIndex = position.SlotIndex;
		bool isPlayerSide = _boardUI.IsPlayerPosition(position);
		if (_combat.State.IsGameOver || _combat.IsEnemyTurnStepping || !_combat.State.IsPlayerTurn)
			return;
		if ((_attackDragFsm.HasDragged && IsPointerTargetSelectionMode()) || _dragCardUI?.IsPointerDragging == true)
		{
			GD.Print($"[CombatUI] 忽略槽位点击：目标拖拽尚未松手，mode={_selectionMode}, slot={slotIndex}, side={(isPlayerSide ? "P" : "E")}");
			return;
		}
		switch (_selectionMode)
		{
			case SelectionMode.PlacingMinion:
				HandleMinionPlacement(position);
				break;

			case SelectionMode.TargetingSpell:
				HandleSpellTarget(position);
				break;

			case SelectionMode.TargetingMinionBattlecry:
				HandleMinionBattlecryTarget(position);
				break;

			case SelectionMode.SelectingAttackTarget:
				HandleAttackTarget(position);
				break;

			case SelectionMode.SelectingWeaponTarget:
				HandleWeaponAttackTarget(position);
				break;

			case SelectionMode.SelectingActiveSkillTarget:
				HandleActiveSkillTarget(position);
				break;

			case SelectionMode.Normal:
				HandleNormalSlotClick(position);
				break;

			case SelectionMode.DevDamageTargeting:
				GD.Print($"[CombatUI] OnBoardSlotClicked → DevDamageTargeting, slot={slotIndex}, side={(isPlayerSide ? "P" : "E")}");
				HandleDevDamageSlot(position);
				break;
		}
	}

	/// <summary>
	/// 槽位右键点击回调——在目标选择模式下取消当前选择（等效 ESC）。
	/// 处理所有需要目标选择的模式：攻击、武器、法术、放置等。
	/// </summary>
	private void OnBoardSlotRightClicked(BoardPosition position)
	{
		if (_combat.State.IsGameOver)
			return;

		// 开发者伤害模式——退出
		if (_selectionMode == SelectionMode.DevDamageTargeting)
		{
			ExitDevDamageMode();
			return;
		}

		// 攻击/武器/法术目标选择模式——重置选择
		if (_selectionMode == SelectionMode.SelectingAttackTarget
			|| _selectionMode == SelectionMode.SelectingWeaponTarget
			|| _selectionMode == SelectionMode.SelectingActiveSkillTarget
			|| _selectionMode == SelectionMode.TargetingSpell
			|| _selectionMode == SelectionMode.TargetingMinionBattlecry
			|| _selectionMode == SelectionMode.PlacingMinion
			|| _selectionMode == SelectionMode.PlayingNoTargetCard)
		{
			GD.Print("[CombatUI] 槽位右键→取消目标选择");
			HandleCancel();
			return;
		}
	}

	// ----- 普通模式下的槽位点击 -----

	/// <summary>
	/// 普通模式下点击己方有随从的槽位 → 将该随从设为攻击方，进入攻击目标选择模式。
	/// </summary>
	private void HandleNormalSlotClick(BoardPosition position)
	{
		if (!_boardUI.IsPlayerPosition(position))
			return; // 普通模式下只响应己方槽位

		var minion = _combat.Board.GetMinionAt(position);
		if (minion == null || !_combat.CanMinionAct(minion))
			return;

		// 行动花费检查：法力不足时拒绝进入攻击模式
		if (minion.ActionCost > 0 && _combat.PlayerHero.CurrentMana < minion.ActionCost)
		{
			GD.Print($"[CombatUI] {minion.CardName} 行动花费 {minion.ActionCost}，当前法力不足（{_combat.PlayerHero.CurrentMana}），无法攻击");
			return;
		}

		// 设为攻击方
		_selectedAttacker = minion;
		_selectionMode = SelectionMode.SelectingAttackTarget;

		// 启动攻击拖拽追踪（委托给 InteractionFsm）
		_attackDragFsm.PickUpCard(GetSelectionPointerPosition(), isClickSelect: true, isMobile: MobileInputRouter.IsMobile);

		GD.Print($"[CombatUI] 选中己方随从 {minion.CardName} 准备攻击");

		// 高亮合法攻击目标
		HighlightValidAttackTargets();

		// 启用键盘目标选择（仅敌方槽位）
		_boardUI.EnableKeyboardTargeting(includePlayerSlots: true, includeEnemySlots: true);
	}

	// ----- 随从放置 -----

	/// <summary>
	/// 随从放置模式下点击玩家槽位 → 召唤随从。
	/// </summary>
	private void HandleMinionPlacement(BoardPosition position, DeployOriginKind origin = DeployOriginKind.PlayerClick)
	{
		int slotIndex = position.SlotIndex;
		if (position.Row != BoardRow.PlayerSupport)
		{
			GD.Print("[CombatUI] 随从只能放置在己方槽位");
			return;
		}

		if (_selectedCard == null)
		{
			GD.PrintErr("[CombatUI] 内部错误：放置模式但 _selectedCard 为 null");
			ResetSelection();
			return;
		}

		Card.Card card = _selectedCard;
		GD.Print($"[CombatUI] 尝试放置随从 {card.CardName}（{card.GetEffectiveCost()}费）到槽位 {slotIndex}");
		// 目标型部署：先选槽位，再选战吼目标；无合法目标时直接裸拍上场、战吼跳过。
		if (EarlyWarfareRules.NeedsBattlecryTargetSelection(
			card.Data.RequiresTarget, _combat.HasAnyValidBattlecryTarget(card)))
		{
			EnterMinionBattlecryTargetMode(card, slotIndex);
			return;
		}
		bool success = PlaySelectedMinion(card, position, origin);
		if (success)
		{
			GD.Print($"[CombatUI] [OK] 随从 {card.CardName} 已放置到槽位 {slotIndex}");
		}
		else
		{
			GD.Print($"[CombatUI] [FAIL] PlayMinion 失败 — 查看上方 [CombatManager] 错误日志");
		}

		RefreshAll();
	}

	// ----- 法术目标 -----

	/// <summary>
	/// 法术目标选择模式下点击槽位 → 对槽位上的随从施放法术。
	/// </summary>
	private void HandleSpellTarget(BoardPosition position)
	{
		if (_selectedCard == null)
		{
			ResetSelection();
			return;
		}

		var target = _combat.Board.GetMinionAt(position);
		if (target == null || target.IsDead)
		{
			GD.Print("[CombatUI] 法术目标无效（空槽位或已死亡）");
			return;
		}

		Card.Card card = _selectedCard;
		GD.Print($"[CombatUI] 对 {target.CardName} 施放 {card.CardName}");
		bool success = PlaySelectedSpellWithVfxOrigin(target);
		if (success)
		{
			GD.Print($"[CombatUI] [OK] 法术 {card.CardName} 已施放");
		}
		else
		{
			GD.Print($"[CombatUI] [FAIL] 法术施放失败");
		}
		RefreshAll();
	}

	// ----- 攻击目标 -----

	/// <summary>
	/// 攻击目标模式下点击敌方槽位 → 发动随从攻击。
	/// </summary>
	private void HandleAttackTarget(BoardPosition position)
	{
		if (_selectedAttacker == null)
		{
			ResetSelection();
			return;
		}

		if (position.Row == BoardRow.Frontline && _combat.Board.GetMinionAt(position) == null)
		{
			_combat.MoveMinionToFrontline(_selectedAttacker, position.SlotIndex);
			RefreshAll();
			return;
		}
		if (_boardUI.IsPlayerPosition(position))
		{
			GD.Print("[CombatUI] 不能攻击己方随从");
			return;
		}

		var defender = _combat.Board.GetMinionAt(position);
		if (defender == null || defender.IsDead)
		{
			GD.Print("[CombatUI] 攻击目标无效");
			return;
		}

		GD.Print($"[CombatUI] {_selectedAttacker.CardName} 攻击 {defender.CardName}");
		_pendingAssaultShotTarget = defender;
		try
		{
			_combat.MinionAttack(_selectedAttacker, defender);
		}
		finally
		{
			_pendingAssaultShotTarget = null;
		}
		RefreshAll();
	}

	/// <summary>
	/// 武器攻击目标模式下点击敌方槽位 → 发动武器攻击随从。
	/// </summary>
	private void HandleWeaponAttackTarget(BoardPosition position)
	{
		bool isPlayerSide = _boardUI.IsPlayerPosition(position);
		bool canTargetFriendly = _combat.PlayerHero.Weapon?.PassiveSkill is IFriendlyMinionWeaponAttackPassive;
		if (isPlayerSide && !canTargetFriendly)
		{
			GD.Print("[CombatUI] 武器不能攻击己方随从");
			return;
		}

		var target = _combat.Board.GetMinionAt(position);
		if (target == null || target.IsDead)
		{
			GD.Print("[CombatUI] 武器攻击目标无效");
			return;
		}

		GD.Print($"[CombatUI] 武器攻击 {target.CardName}");

		_enemyHeroCardAction = OnEnemyHeroAttackPressed;

		_combat.HeroWeaponAttackMinion(target);
		RefreshAll();
	}

	/// <summary>
	/// 武器主动技能目标模式下点击敌方槽位 → 对目标随从释放主动技能。
	/// 无视嘲讽限制。
	/// </summary>
	private void HandleActiveSkillTarget(BoardPosition position)
	{
		bool isPlayerSide = _boardUI.IsPlayerPosition(position);
		bool canTargetAnyUnit = _combat.PlayerHero.Weapon?.ActiveSkill is IAnyUnitWeaponActive;
		if (isPlayerSide && !canTargetAnyUnit)
		{
			GD.Print("[CombatUI] 主动技能不能对己方随从释放");
			return;
		}

		var target = _combat.Board.GetMinionAt(position);
		if (target == null || target.IsDead)
		{
			GD.Print("[CombatUI] 主动技能目标无效");
			return;
		}

		GD.Print($"[CombatUI] 主动技能目标：{target.CardName}");

		_enemyHeroCardAction = OnEnemyHeroAttackPressed;

		// 设置目标并执行技能
		_combat.ActiveSkillTarget = target;
		_combat.UseWeaponActiveSkill();
		ResetSelection();
		RefreshAll();
	}

	// ===== 事件处理——手牌选中 =====

	/// <summary>先归还上张牌，再借出当前真卡；投放模式和视觉驱动互不替代。</summary>
	private void OnCardSelectedFromHand(Card.Card card)
	{
		if (_combat.State.IsGameOver || _combat.IsEnemyTurnStepping || !_combat.State.IsPlayerTurn || _combat.IsDiscovering)
		{
			return;
		}
		CardUI? cardUI = _handUI.GetCardUIFor(card);
		if (cardUI == null || cardUI == _dragCardUI || !_combat.PlayerHero.Hand.Contains(card))
		{
			return;
		}

		ResetSelection();
		if (!_handUI.MoveCardOutOfHand(cardUI, _dragLayer, forPlay: true))
		{
			return;
		}
		_dragCardUI = cardUI;
		cardUI.ZIndex = 10;
		cardUI.OnCardDropped += OnCardDroppedHandler;
		cardUI.OnTargetingClickReleased += OnCardTargetingClickReleased;

		switch (card.Type)
		{
			case CardType.Minion:
				EnterMinionPlacementMode(card);
				break;
			case CardType.Spell when card.Data.RequiresTarget:
				EnterSpellTargetMode(card);
				break;
			case CardType.Spell or CardType.Domain or CardType.Status or CardType.Countermeasure:
				EnterNoTargetPlayMode(card);
				break;
			default:
				GD.Print($"[CombatUI] 未知卡牌类型：{card.Type}");
				ResetSelection();
				return;
		}
		PresentSelectedCardForPlay(cardUI);
	}

	private void PresentSelectedCardForPlay(CardUI view)
	{
		bool keyboard = _handUI.IsKeyboardSelection;
		bool requiresTarget = _selectionMode is SelectionMode.PlacingMinion or SelectionMode.TargetingSpell;
		if (requiresTarget && (keyboard || !Input.IsMouseButtonPressed(MouseButton.Left)))
		{
			PresentTargetingCard(view);
			return;
		}

		Vector2 anchor = keyboard ? GetSelectionPointerPosition() : view.LastClickGlobalPosition;
		if (!requiresTarget)
		{
			_playZoneDragStartY = keyboard ? view.ScreenCenter.Y : anchor.Y;
			_hasEnteredPlayZone = false;
			ShowPlayZonePanel();
		}
		view.BeginPointerFollowFrom(anchor, startAsClickFollow: keyboard || !MobileInputRouter.IsMobile,
			parkOnClickRelease: requiresTarget);
	}

	private void OnCardTargetingClickReleased(CardUI view)
	{
		if (_dragCardUI == view)
		{
			PresentTargetingCard(view);
		}
	}

	private void PresentTargetingCard(CardUI view)
	{
		Vector2 viewport = GetViewportRect().Size;
		float uiScale = UIScaler.Instance?.GetScaleFactor() ?? 1f;
		float scale = TargetingCardScale * uiScale / view.FaceRenderScale;
		Vector2 center = new(viewport.X * 0.5f, viewport.Y - view.Size.Y * scale * 0.5f - 8f * uiScale);
		view.PresentForTargeting(center, scale);
	}

	/// <summary>取消只归还当前视图，手牌其余姿态与进场继续播放。</summary>
	private void OnCardDragCancelled()
	{
		GD.Print("[CombatUI] 拖拽取消");
		ResetSelection();
	}

	/// <summary>
	/// 拖拽中左键松开的事件处理。
	/// 根据松手位置判断：落在有效槽位→打出，否则→取消（等效右键）。
	/// 实现「拖拽松手打出」和「点击选中→点击目标打出」的等效性。
	/// </summary>
	private void OnCardDroppedHandler(CardUI cardUI, Vector2 screenPos)
	{
		if (_combat.State.IsGameOver)
			return;
		if (_dragCardUI != cardUI)
			return;

		GD.Print($"[CombatUI] OnCardDropped — 模式 {_selectionMode}, 坐标 ({screenPos.X:F0}, {screenPos.Y:F0})");

		switch (_selectionMode)
		{
			case SelectionMode.PlacingMinion:
				HandleMinionDrop(screenPos);
				break;

			case SelectionMode.TargetingSpell:
				HandleSpellDrop(screenPos);
				break;

			case SelectionMode.TargetingMinionBattlecry:
				HandleMinionBattlecryDrop(screenPos);
				break;

			case SelectionMode.SelectingAttackTarget:
				HandleAttackDrop(screenPos);
				break;

			case SelectionMode.PlayingNoTargetCard:
				HandleNoTargetCardDrop(screenPos);
				break;

			default:
				// 未在有效选择模式 — 取消
				GD.Print("[CombatUI] 松手时未在选择模式，取消拖拽");
				OnCardDragCancelled();
				break;
		}
	}

	/// <summary>
	/// 随从放置模式下的松手处理：检查落点是否在玩家方槽位上。
	/// 命中己方槽位→放置随从；无效位置→取消（仅拖拽松手会触发此方法，点击选中已由 _hasMovedFromOrigin 过滤）。
	/// </summary>
	private void HandleMinionDrop(Vector2 screenPos)
	{
		GD.Print($"[CombatUI] 拖拽松手 — 坐标 ({screenPos.X:F0}, {screenPos.Y:F0})");
		var hit = _boardUI.GetSlotAtPosition(screenPos);
		if (hit is { Row: BoardRow.PlayerSupport } slot && _combat.Board.GetMinionAt(slot) == null)
		{
			GD.Print($"[CombatUI] 命中己方槽位 {hit.Value.SlotIndex}，执行放置");
			HandleMinionPlacement(slot, DeployOriginKind.PlayerDrag);
		}
		else
		{
			GD.Print(hit != null
				? $"[CombatUI] 命中非部署线 {hit.Value}，取消"
				: "[CombatUI] 未命中任何槽位 — 取消");
			OnCardDragCancelled();
		}
	}

	/// <summary>
	/// 法术目标模式下的松手处理：检查落点是否在有随从的槽位上。
	/// 命中有效目标→施放法术；无效位置→取消（仅拖拽松手会触发此方法，点击选中已由 _hasMovedFromOrigin 过滤）。
	/// </summary>
	private void HandleSpellDrop(Vector2 screenPos)
	{
		// 优先检查是否落在敌方英雄面板上
		int spellTargetIndex = GetEnemyCardIndexAt(screenPos, requireSpellButtonVisible: true);
		if (spellTargetIndex >= 0)
		{
			GD.Print($"[CombatUI] 法术松手位置：敌方英雄[{spellTargetIndex}]");
			OnEnemyHeroSpellTargetForIndex(spellTargetIndex);
			return;
		}

		// 检查是否落在己方英雄面板上
		if (_playerHeroSpellButton.Visible && _playerIdentityCard.GetGlobalRect().HasPoint(screenPos))
		{
			GD.Print("[CombatUI] 法术松手位置：己方英雄");
			OnPlayerHeroSpellTarget();
			return;
		}

		var hit = _boardUI.GetSlotAtPosition(screenPos);
		if (hit != null)
		{
			var target = _combat.Board.GetMinionAt(hit.Value);
			if (target != null && !target.IsDead && _selectedCard != null && _combat.ValidateTarget(_selectedCard, target))
			{
				HandleSpellTarget(hit.Value);
			}
			else
			{
				GD.Print("[CombatUI] 法术松手位置无有效随从目标 — 取消");
				OnCardDragCancelled();
			}
		}
		else
		{
			GD.Print("[CombatUI] 法术松手位置无效 — 取消");
			OnCardDragCancelled();
		}
	}

	/// <summary>
	/// 无目标卡牌播放模式下的松手处理：检查落点是否在播放区域内。
	/// 在区域内→打出卡牌；不在播放区域→取消（等效右键，卡牌回到手牌）。
	/// </summary>
	private void HandleNoTargetCardDrop(Vector2 screenPos)
	{
		if (_selectedCard == null)
		{
			GD.PrintErr("[CombatUI] 内部错误：播放模式但 _selectedCard 为 null");
			ResetSelection();
			return;
		}

		bool inZone = IsInPlayZone(screenPos);
		GD.Print($"[CombatUI] 无目标卡牌松手 — {_selectedCard.CardName}，" +
			$"{(inZone ? "播放区" : "无效区 → 取消")}");

		if (inZone)
		{
			PlaySelectedNoTargetCard();
		}
		else
		{
			// 不在播放区 → 取消，卡牌回到手牌
			OnCardDragCancelled();
		}
	}

	/// <summary>
	/// 攻击目标模式下的松手处理：检查落点是否在敌方槽位或敌方英雄面板上。
	/// </summary>
	private void HandleAttackDrop(Vector2 screenPos)
	{
		GD.Print($"[CombatUI] HandleAttackDrop mode={_selectionMode}, pos=({screenPos.X:F0},{screenPos.Y:F0})");
		var hit = _boardUI.GetSlotAtPosition(screenPos);
		if (hit != null)
		{
			if (_selectionMode == SelectionMode.SelectingWeaponTarget)
				HandleWeaponAttackTarget(hit.Value);
			else
				HandleAttackTarget(hit.Value);
		}
		else
		{
			int attackTargetIndex = GetEnemyCardIndexAt(screenPos, requireAttackHighlight: true);
			if (attackTargetIndex >= 0)
			{
				_activeEnemyTargetIndex = attackTargetIndex;
				if (_selectionMode == SelectionMode.SelectingWeaponTarget)
				{
					// 武器攻击——英雄直接攻击，无 _selectedAttacker
					_enemyHeroCardAction?.Invoke();
				}
				else
				{
					OnEnemyHeroAttackPressed();
				}
			}
			else
			{
				GD.Print("[CombatUI] 攻击松手位置无效，取消选择");
				ResetSelection();
			}
		}
	}

	/// <summary>
	/// 武器主动技能目标模式下的松手处理：检查落点是否在合法英雄或随从目标上。
	/// </summary>
	private void HandleActiveSkillDrop(Vector2 screenPos)
	{
		GD.Print($"[CombatUI] HandleActiveSkillDrop pos=({screenPos.X:F0},{screenPos.Y:F0})");

		int enemyTargetIndex = GetEnemyCardIndexAt(screenPos, requireAttackHighlight: true);
		if (enemyTargetIndex >= 0)
		{
			_activeEnemyTargetIndex = enemyTargetIndex;
			OnActiveSkillHeroPressed();
			return;
		}

		if (_playerHeroSpellButton.Visible && _playerIdentityCard.GetGlobalRect().HasPoint(screenPos))
		{
			OnPlayerHeroSpellTarget();
			return;
		}

		var hit = _boardUI.GetSlotAtPosition(screenPos);
		if (hit != null)
		{
			bool canTargetAnyUnit = _combat.PlayerHero.Weapon?.ActiveSkill is IAnyUnitWeaponActive;
			var target = _combat.Board.GetMinionAt(hit.Value);
			if (target != null && !target.IsDead && (!target.IsPlayerSide || canTargetAnyUnit))
			{
				HandleActiveSkillTarget(hit.Value);
				return;
			}
		}

		GD.Print("[CombatUI] 主动技能松手位置无效，取消选择");
		ResetSelection();
	}

	/// <summary>统一结束借牌。取消归还原节点；真实出牌已由亮牌/部署接管时才退役。</summary>
	private void ReleaseDragCard(bool played = false)
	{
		CardUI? view = _dragCardUI;
		_dragCardUI = null;
		if (!HandUI.IsLive(view))
		{
			return;
		}
		view!.OnCardDropped -= OnCardDroppedHandler;
		view.OnDragMove -= OnDragMoveForPlayZone;
		view.OnTargetingClickReleased -= OnCardTargetingClickReleased;
		bool retained = played ? _handUI.ParkPlayedCard(view) : _handUI.ReturnCardToHand(view);
		if (!retained)
		{
			HandUI.RetireDetachedCard(view);
			_handUI.RefreshHand();
		}
	}

	/// <summary>
	/// 获取弃牌堆按钮中心点的屏幕位置。
	/// </summary>
	private Vector2 GetDiscardPileCenter()
	{
		return _discardPileBtn.GlobalPosition + _discardPileBtn.Size / 2f;
	}

	/// <summary>
	/// 获取抽牌堆按钮中心点的屏幕位置，用于轮战卡牌飞行动画。
	/// </summary>
	private Vector2 GetDrawPileCenter()
	{
		return _drawPileBtn.GlobalPosition + _drawPileBtn.Size / 2f;
	}

	/// <summary>
	/// 获取玩家效果栏中心点的屏幕位置，用于领域卡牌飞行动画。
	/// 领域打出后附加到英雄，不进入任何牌堆。
	/// </summary>
	private Vector2 GetPlayerEffectBarCenter()
	{
		if (_playerIdentityCard != null)
			return _playerIdentityCard.EffectBar.GlobalPosition + _playerIdentityCard.EffectBar.Size / 2f;
		return Vector2.Zero;
	}

	/// <summary>
	/// 进入随从放置模式——高亮玩家方可用槽位（绿色）。
	/// </summary>
	private void EnterMinionPlacementMode(Card.Card card)
	{
		_selectionMode = SelectionMode.PlacingMinion;
		_selectedCard = card;

		// 收集玩家方空槽位
		var validSlots = new List<int>();
		for (int i = 0; i < Board.SupportCapacity; i++)
		{
			if (_combat.Board.GetMinionAt(i, isPlayerSide: true) == null)
			{
				validSlots.Add(i);
			}
		}

		if (validSlots.Count > 0)
		{
			_boardUI.HighlightSlots(validSlots, isPlayerSide: true, highlight: true);
			GD.Print($"[CombatUI] 随从放置模式——可放置槽位：{string.Join(", ", validSlots)}");
		}
		else
		{
			GD.Print("[CombatUI] 随从放置模式——无可用槽位（战场已满）");
		}

		// 启用键盘目标选择（仅玩家方空槽位）
		_boardUI.EnableKeyboardTargeting(includePlayerSlots: true, includeEnemySlots: false);
	}

	/// <summary>
	/// 进入法术目标选择模式——根据卡牌的 TargetFilter 过滤合法目标。
	/// 高亮通过子集匹配的敌方/己方随从，显示对应英雄施法按钮。
	/// </summary>
	private void EnterSpellTargetMode(Card.Card card)
	{
		_selectionMode = SelectionMode.TargetingSpell;
		_selectedCard = card;

		var require = card.Data.TargetFilter;
		var exclude = card.Data.ExcludeFilter;

		// 高亮敌方随从 —— 仅高亮通过 TargetFilter 的目标
		var enemyTargets = new List<int>();
		foreach (var m in _combat.Board.GetEnemyMinions())
		{
			if (_combat.ValidateTarget(card, m))
			{
				enemyTargets.Add(m.BoardSlotIndex);
				_boardUI.HighlightMinions(new[] { m });
			}
		}

		// 高亮己方随从 —— 仅高亮通过 TargetFilter 的目标
		var friendlyTargets = new List<int>();
		foreach (var m in _combat.Board.GetPlayerMinions())
		{
			if (_combat.ValidateTarget(card, m))
			{
				friendlyTargets.Add(m.BoardSlotIndex);
				_boardUI.HighlightMinions(new[] { m });
			}
		}

		// 高亮默认存活敌方英雄作为法术目标
		var enemyHero = GetDefaultEnemyHeroTarget();
		bool enemyHeroValid = enemyHero != null && _combat.ValidateTarget(card, enemyHero);
		SetEnemyHeroSpellTargetsVisible(enemyHeroValid, Loc.T("ui.combat.cast_on_enemy_hero", "对敌方英雄施放"));
		// 法术模式下覆盖层点击路由到法术目标选择
		if (enemyHeroValid)
			_enemyHeroCardAction = OnEnemyHeroSpellTargetViaOverlay;

		// 高亮己方英雄作为法术目标
		_playerHeroSpellButton.Visible = _combat.ValidateTarget(card, _combat.PlayerHero);

		GD.Print($"[CombatUI] 法术目标模式——{_selectedCard.CardName}（require={require} exclude={exclude}，" +
				  $"敌方随从 {enemyTargets.Count} + 己方随从 {friendlyTargets.Count} + " +
				  $"{(enemyTargets.Count > 0 || friendlyTargets.Count > 0 ? " + 英雄" : "")}）");

		// 启用键盘目标选择（根据高亮的阵营方）
		_boardUI.EnableKeyboardTargeting(
			includePlayerSlots: friendlyTargets.Count > 0,
			includeEnemySlots: enemyTargets.Count > 0);
	}

	/// <summary>
	/// 进入随从部署战吼的目标选择模式——与法术目标模式完全对称：
	/// 保留准备落地的支援线高亮，显示瞄准箭头，右键退回手牌。
	/// </summary>
	private void EnterMinionBattlecryTargetMode(Card.Card card, int slotIndex)
	{
		_selectionMode = SelectionMode.TargetingMinionBattlecry;
		_selectedCard = card;
		_pendingMinionSlot = slotIndex;

		// 已选槽位后把同一张牌固定到展示位，仍可随时取消归还。
		if (_dragCardUI is { } view)
		{
			PresentTargetingCard(view);
		}

		_boardUI.ClearHighlights();
		_boardUI.HighlightSlots(new List<int> { slotIndex }, isPlayerSide: true, highlight: true);

		bool anyPlayerTarget = false;
		foreach (var minion in _combat.Board.GetEnemyMinions())
		{
			if (_combat.ValidateBattlecryTarget(card, minion))
				_boardUI.HighlightMinions(new[] { minion });
		}
		foreach (var minion in _combat.Board.GetPlayerMinions())
		{
			if (_combat.ValidateBattlecryTarget(card, minion))
			{
				_boardUI.HighlightMinions(new[] { minion });
				anyPlayerTarget = true;
			}
		}

		var enemyHero = GetDefaultEnemyHeroTarget();
		bool enemyHeroValid = enemyHero != null && _combat.ValidateBattlecryTarget(card, enemyHero);
		SetEnemyHeroSpellTargetsVisible(enemyHeroValid, Loc.T("ui.combat.deploy_on_enemy_hero", "对敌方英雄部署"));
		if (enemyHeroValid)
			_enemyHeroCardAction = OnEnemyHeroSpellTargetViaOverlay;

		bool playerHeroValid = _combat.ValidateBattlecryTarget(card, _combat.PlayerHero);
		_playerHeroSpellButton.Visible = playerHeroValid;
		_playerHeroSpellButton.Disabled = false;

		_boardUI.EnableKeyboardTargeting(includePlayerSlots: anyPlayerTarget || playerHeroValid, includeEnemySlots: true);
		GD.Print($"[CombatUI] 部署战吼目标模式——{card.GetLocalizedName()} 将落在槽位 {slotIndex}，等待选择战吼目标（右键取消）");
	}

	/// <summary>部署战吼目标模式下点击随从槽位。</summary>
	private void HandleMinionBattlecryTarget(BoardPosition position)
	{
		if (_selectedCard == null)
		{
			ResetSelection();
			return;
		}
		var target = _combat.Board.GetMinionAt(position);
		if (target == null || target.IsDead || !_combat.ValidateBattlecryTarget(_selectedCard, target))
		{
			GD.Print("[CombatUI] 部署战吼目标无效");
			return;
		}
		PlayPendingMinionBattlecry(target);
	}

	/// <summary>部署战吼目标模式下松手：命中合法随从或英雄才结算。</summary>
	private void HandleMinionBattlecryDrop(Vector2 screenPos)
	{
		int enemyIndex = GetEnemyCardIndexAt(screenPos, requireSpellButtonVisible: true);
		if (enemyIndex >= 0)
		{
			_activeEnemyTargetIndex = enemyIndex;
			var hero = GetActiveEnemyHeroTarget();
			if (hero != null)
				PlayPendingMinionBattlecry(hero);
			return;
		}
		if (_playerHeroSpellButton.Visible && _playerIdentityCard.GetGlobalRect().HasPoint(screenPos))
		{
			PlayPendingMinionBattlecry(_combat.PlayerHero);
			return;
		}

		var hit = _boardUI.GetSlotAtPosition(screenPos);
		var target = hit is BoardPosition position ? _combat.Board.GetMinionAt(position) : null;
		if (target != null && !target.IsDead && _selectedCard != null && _combat.ValidateBattlecryTarget(_selectedCard, target))
		{
			PlayPendingMinionBattlecry(target);
			return;
		}

		GD.Print("[CombatUI] 部署战吼松手位置无合法目标 — 取消");
		OnCardDragCancelled();
	}

	/// <summary>用选定的战吼目标完成部署；失败时保持游戏状态不变。</summary>
	private void PlayPendingMinionBattlecry(object target)
	{
		if (_selectedCard == null || _pendingMinionSlot < 0)
		{
			OnCardDragCancelled();
			return;
		}

		int slotIndex = _pendingMinionSlot;
		var card = _selectedCard;
		var position = new BoardPosition(BoardRow.PlayerSupport, slotIndex);
		bool success = PlaySelectedMinion(card, position, DeployOriginKind.PlayerClick, target);
		if (success)
		{
			GD.Print($"[CombatUI] [OK] 随从 {card.CardName} 已放置到槽位 {slotIndex}（战吼目标：{target}）");
		}
		else
		{
			GD.Print("[CombatUI] [FAIL] 目标型部署失败 — 查看上方 [CombatManager] 错误日志");
		}
		RefreshAll();
	}

	/// <summary>规则结算前冻结真实卡面的姿态；只有本次手牌及槽位匹配才消费接棒记录。</summary>
	private bool PlaySelectedMinion(Card.Card card, BoardPosition position, DeployOriginKind origin, object? battlecryTarget = null)
	{
		CardUI? view = _dragCardUI;
		_playerDeployHandoff = (card, position, origin, CaptureDeploymentPose(view), view);
		try
		{
			bool played = _combat.PlayMinion(card, position.SlotIndex, battlecryTarget);
			if (played)
			{
				// 拦截击毁或布局无效时没有飞行接管，已打出的原卡仍须退役。
				ReleaseDragCard(played: true);
			}
			return played;
		}
		finally
		{
			_playerDeployHandoff = null;
		}
	}

	/// <summary>
	/// 进入无目标卡牌播放模式——显示播放区域指示器，等待玩家拖拽到播放区域松手打出。
	/// 适用于：领域（Domain）、无目标的法术（Spell.RequiresTarget == false）。
	/// </summary>
	private void EnterNoTargetPlayMode(Card.Card card)
	{
		GD.Print($"[CombatUI] EnterNoTargetPlayMode — 类型={card.Type}, 名称={card.CardName}, playZonePanel={_playZonePanel != null}, isInsideTree={IsInsideTree()}");

		_selectionMode = SelectionMode.PlayingNoTargetCard;
		_selectedCard = card;

		// 订阅拖拽卡牌的逐帧位置更新（用于播放区域判定和视觉反馈）
		if (_dragCardUI != null)
		{
			_dragCardUI.OnDragMove += OnDragMoveForPlayZone;
		}

		GD.Print($"[CombatUI] 无目标播放模式——{card.CardName}（拖到绿色区域松手打出，右键取消）");
	}

	/// <summary>
	/// 高亮合法攻击目标——统一应用速度与嘲讽拦截规则。
	/// </summary>
	private void HighlightValidAttackTargets()
	{
		_boardUI.ClearHighlights();
		_enemyHeroCardAction = OnEnemyHeroAttackPressed;
		if (_selectedAttacker == null || _selectedAttacker.IsDead)
		{
			SetEnemyHeroAttackTargetsVisible(false);
			return;
		}

		var legalTargets = GetLegalEnemyAttackTargets(_selectedAttacker);
		var legalMinions = legalTargets.OfType<Minion>().ToList();
		_boardUI.HighlightMinions(legalMinions);
		for (int slot = 0; slot < Board.FrontlineCapacity; slot++)
			if (_combat.CanMoveMinionToFrontline(_selectedAttacker, slot))
				_boardUI.HighlightPositions(new[] { new BoardPosition(BoardRow.Frontline, slot) });

		var legalHeroes = legalTargets.OfType<Hero>().ToList();
		SetEnemyHeroAttackTargetsVisible(legalHeroes.Count > 0, legalHeroes);

		GD.Print($"[CombatUI] 攻击目标模式——合法随从 {legalMinions.Count} 个，合法英雄 {legalHeroes.Count} 个");
	}

	private List<IDamageTarget> GetLegalEnemyAttackTargets(IDamageSource attacker)
	{
		var candidates = new List<IDamageTarget>();
		candidates.AddRange(_combat.Board.GetEnemyMinions().Where(m => !m.IsDead));
		foreach (var unit in _combat.EnemyUnits)
		{
			if (!unit.Body.IsDead)
				candidates.Add(unit.Body);
		}

		return attacker is Minion minion
			? candidates.Where(target => _combat.CanMinionAttackTarget(minion, target)).ToList()
			: AttackTargetRules.GetLegalAttackTargets(_combat.Board, attacker, candidates);
	}

	/// <summary>判断当前指针是否悬停在合法敌方随从/英雄上，用于箭头橙/灰反馈。</summary>
	private bool IsLegalAttackHover(BoardPosition? hit, Vector2 pointer)
	{
		if (_selectedAttacker == null)
			return false;
		if (hit is BoardPosition position)
		{
			if (_boardUI.IsPlayerPosition(position))
				return false;
			var minion = _combat.Board.GetMinionAt(position);
			return minion != null && !minion.IsDead && _combat.CanMinionAttackTarget(_selectedAttacker, minion);
		}

		foreach (var card in _enemyCards)
		{
			if (!GodotObject.IsInstanceValid(card) || !card.GetGlobalRect().HasPoint(pointer))
				continue;
			var hero = _combat.EnemyUnits[card.EnemyIndex].Body;
			return !hero.IsDead && _combat.CanMinionAttackTarget(_selectedAttacker, hero);
		}
		return false;
	}

	private void OnEnemyHeroCardActionPressed(int enemyIndex)
	{
		if (_combat.IsEnemyTurnStepping || !_combat.State.IsPlayerTurn)
			return;
		if ((_attackDragFsm.HasDragged && IsPointerTargetSelectionMode()) || _dragCardUI?.IsPointerDragging == true)
		{
			GD.Print($"[CombatUI] 忽略敌方英雄点击：目标拖拽尚未松手，mode={_selectionMode}, enemy={enemyIndex}");
			return;
		}
		_activeEnemyTargetIndex = enemyIndex;
		_enemyHeroCardAction?.Invoke();
	}

	private int GetDefaultEnemyTargetIndex()
	{
		for (int i = 0; i < _combat.EnemyUnits.Count; i++)
		{
			if (!_combat.EnemyUnits[i].Body.IsDead)
				return i;
		}

		return -1;
	}

	private Hero? GetDefaultEnemyHeroTarget()
	{
		int index = GetDefaultEnemyTargetIndex();
		return index >= 0 ? _combat.EnemyUnits[index].Body : null;
	}

	private void SetEnemyHeroAttackTargetsVisible(bool visible, IReadOnlyCollection<Hero>? legalTargets = null)
	{
		for (int i = 0; i < _enemyCards.Count; i++)
		{
			var hero = _combat.EnemyUnits[i].Body;
			bool isLegal = legalTargets == null || legalTargets.Contains(hero);
			_enemyCards[i].SetAttackTargetHighlight(visible && isLegal && !hero.IsDead);
		}
	}

	private void SetEnemyHeroSpellTargetsVisible(bool visible, string text)
	{
		for (int i = 0; i < _enemyCards.Count; i++)
		{
			bool isAlive = !_combat.EnemyUnits[i].Body.IsDead;
			var button = _enemyCards[i].SpellButton;
			button.Text = text;
			button.Visible = visible && isAlive;
			button.Disabled = !visible || !isAlive;
			// 法术/开发者伤害模式下也显示绿色高亮，与攻击/武器/主动技能模式一致
			_enemyCards[i].SetAttackTargetHighlight(visible && isAlive);
		}
	}

	private int GetEnemyCardIndexAt(Vector2 screenPos, bool requireAttackHighlight = false, bool requireSpellButtonVisible = false)
	{
		for (int i = 0; i < _enemyCards.Count; i++)
		{
			if (requireAttackHighlight && !_enemyCards[i].IsAttackTargetHighlighted)
				continue;
			if (requireSpellButtonVisible && !_enemyCards[i].SpellButton.Visible)
				continue;
			if (_enemyCards[i].GetGlobalRect().HasPoint(screenPos))
				return i;
		}

		return -1;
	}

	// ===== 事件处理——敌方英雄攻击 =====

	/// <summary>
	/// 攻击敌方英雄按钮点击——执行随从攻击英雄。
	/// </summary>
	private void OnEnemyHeroAttackPressed()
	{
		if (_combat.State.IsGameOver)
			return;
		if (_selectedAttacker == null)
		{
			GD.PrintErr("[CombatUI] 无攻击方随从");
			return;
		}

		var target = GetActiveEnemyHeroTarget();
		if (target == null)
			return;

		GD.Print($"[CombatUI] {_selectedAttacker.CardName} 攻击敌方英雄[{_activeEnemyTargetIndex}]");
		_pendingAssaultShotTarget = target;
		try
		{
			_combat.MinionAttackHero(_selectedAttacker, target);
		}
		finally
		{
			_pendingAssaultShotTarget = null;
		}
		RefreshAll();
	}

	/// <summary>
	/// 获取当前被选中为攻击/施法目标的敌方英雄。
	/// 优先使用 _activeEnemyTargetIndex；回退到首个存活敌人。
	/// </summary>
	private Hero? GetActiveEnemyHeroTarget()
	{
		if (_activeEnemyTargetIndex >= 0 && _activeEnemyTargetIndex < _combat.EnemyUnits.Count)
		{
			var unit = _combat.EnemyUnits[_activeEnemyTargetIndex];
			if (!unit.Body.IsDead)
				return unit.Body;
		}
		return GetDefaultEnemyHeroTarget();
	}

	/// <summary>
	/// 对敌方英雄施法按钮点击——执行法术对敌方英雄施放。
	/// </summary>
	/// <param name="enemyIndex">法术目标敌人索引（来自闭包捕获的 EnemyIdentityCard.EnemyIndex）</param>
	private void OnEnemyHeroSpellTargetForIndex(int enemyIndex)
	{
		if (_combat.State.IsGameOver || _dragCardUI?.IsPointerDragging == true)
			return;

		_activeEnemyTargetIndex = enemyIndex;

		// 开发者伤害模式：对敌方英雄造成伤害
		if (_selectionMode == SelectionMode.DevDamageTargeting)
		{
			var devTarget = GetActiveEnemyHeroTarget();
			if (devTarget == null)
				return;

			devTarget.ApplyDevDamage(_devDamageAmount);
			_combat.CheckVictoryOrDefeat();
			ExitDevDamageMode();
			return;
		}

		if (_selectedCard == null)
		{
			GD.PrintErr("[CombatUI] 无法术牌选中");
			return;
		}

		if (_selectionMode == SelectionMode.TargetingMinionBattlecry)
		{
			var deployTarget = GetActiveEnemyHeroTarget();
			if (deployTarget != null)
				PlayPendingMinionBattlecry(deployTarget);
			return;
		}

		var target = GetActiveEnemyHeroTarget();
		if (target == null)
			return;

		GD.Print($"[CombatUI] 对敌方英雄[{enemyIndex}]施放 {_selectedCard.CardName}");
		PlaySelectedSpellWithVfxOrigin(target);
		RefreshAll();
	}

	/// <summary>
	/// 法术模式下覆盖层点击路由——由 EnemyIdentityCard 的 OnAttackTargetClicked 触发，
	/// 经 OnEnemyHeroCardActionPressed 存储 _activeEnemyTargetIndex 后调用。
	/// </summary>
	private void OnEnemyHeroSpellTargetViaOverlay()
	{
		OnEnemyHeroSpellTargetForIndex(_activeEnemyTargetIndex);
	}

	/// <summary>
	/// 对己方英雄施法按钮点击——执行法术对己方英雄施放。
	/// </summary>
	private void OnPlayerHeroSpellTarget()
	{
		if (_combat.State.IsGameOver || _dragCardUI?.IsPointerDragging == true)
			return;

		if (_selectionMode == SelectionMode.SelectingActiveSkillTarget
			&& _combat.PlayerHero.Weapon?.ActiveSkill is IAnyUnitWeaponActive)
		{
			GD.Print("[CombatUI] 主动技能目标：己方英雄");
			_combat.ActiveSkillTarget = _combat.PlayerHero;
			_combat.UseWeaponActiveSkill();
			ResetSelection();
			RefreshAll();
			return;
		}

		if (_selectedCard == null)
		{
			GD.PrintErr("[CombatUI] 无法术牌选中");
			return;
		}

		if (_selectionMode == SelectionMode.TargetingMinionBattlecry)
		{
			PlayPendingMinionBattlecry(_combat.PlayerHero);
			return;
		}

		GD.Print($"[CombatUI] 对己方英雄施放 {_selectedCard.CardName}");
		PlaySelectedSpellWithVfxOrigin(_combat.PlayerHero);
		RefreshAll();
	}

	// ===== 事件处理——武器攻击 =====

	/// <summary>
	/// 武器攻击按钮点击——进入武器目标选择模式。
	/// 高亮敌方随从并显示攻击敌方英雄按钮。
	/// </summary>
	private void OnWeaponAttackPressed()
	{
		if (_ignoreNextWeaponAttackPressed)
		{
			_ignoreNextWeaponAttackPressed = false;
			GD.Print("[CombatUI] 忽略武器攻击按钮的同次鼠标 Pressed 事件，避免与按下拖拽入口冲突");
			return;
		}

		if (_combat.State.IsGameOver || _combat.IsEnemyTurnStepping || !_combat.State.IsPlayerTurn)
			return;
		if (_selectionMode == SelectionMode.SelectingWeaponTarget)
		{
			GD.Print("[CombatUI] 再次点击武器攻击按钮 → 取消武器攻击选择");
			ResetSelection();
			RefreshAll();
			return;
		}
		if (!_combat.PlayerHero.CanWeaponAttack())
			return;

		var weapon = _combat.PlayerHero.Weapon;
		if (weapon == null || weapon.IsDisabled)
			return;
		if (!_combat.PlayerHero.CanSpendMana(weapon.AttackCost))
			return;

		EnterWeaponAttackTargetMode(GetSelectionPointerPosition());
	}

	private void EnterWeaponAttackTargetMode(Vector2 startPos)
	{
		var weapon = _combat.PlayerHero.Weapon;
		if (weapon == null)
			return;

		GD.Print($"[CombatUI] 进入武器攻击目标选择模式 — {weapon.Name}，start=({startPos.X:F0},{startPos.Y:F0})");

		ResetSelection();
		_selectionMode = SelectionMode.SelectingWeaponTarget;
		_selectedAttacker = null;
		_selectedCard = null;
		_attackDragFsm.PickUpCard(startPos, isClickSelect: true, isMobile: MobileInputRouter.IsMobile);

		// 高亮合法攻击目标
		HighlightWeaponTargets();

		// 启用键盘目标选择；魔法棒等武器可选择己方随从。
		_boardUI.EnableKeyboardTargeting(
			includePlayerSlots: weapon.PassiveSkill is IFriendlyMinionWeaponAttackPassive,
			includeEnemySlots: true);
	}

	/// <summary>
	/// 武器主动技能按钮点击——进入主动技能目标选择模式。
	/// 所有敌方随从 + 敌方英雄都是合法目标（无视嘲讽——这是减益技能不是攻击）。
	/// </summary>
	private void OnWeaponActiveSkillPressed()
	{
		if (_ignoreNextWeaponActiveSkillPressed)
		{
			_ignoreNextWeaponActiveSkillPressed = false;
			GD.Print("[CombatUI] 忽略武器主动技能按钮的同次鼠标 Pressed 事件，避免与按下拖拽入口冲突");
			return;
		}

		if (_combat.State.IsGameOver || _combat.IsEnemyTurnStepping || !_combat.State.IsPlayerTurn)
			return;
		if (_selectionMode == SelectionMode.SelectingActiveSkillTarget)
		{
			GD.Print("[CombatUI] 再次点击武器主动技能按钮 → 取消目标选择");
			ResetSelection();
			RefreshAll();
			return;
		}

		var weapon = _combat.PlayerHero.Weapon;
		if (weapon?.ActiveSkill == null)
			return;
		if (!weapon.ActiveSkill.CanUse(_combat.PlayerHero))
			return;
		if (!weapon.ActiveSkill.RequiresTarget)
		{
			_combat.ActiveSkillTarget = null;
			_combat.UseWeaponActiveSkill();
			RefreshAll();
			return;
		}

		EnterWeaponActiveSkillTargetMode(GetSelectionPointerPosition());
	}

	private void EnterWeaponActiveSkillTargetMode(Vector2 startPos)
	{
		var weapon = _combat.PlayerHero.Weapon;
		if (weapon?.ActiveSkill == null || !weapon.ActiveSkill.RequiresTarget)
			return;

		GD.Print($"[CombatUI] 进入主动技能目标选择模式 — {weapon.ActiveSkill.Name}，start=({startPos.X:F0},{startPos.Y:F0})");

		ResetSelection();
		_selectionMode = SelectionMode.SelectingActiveSkillTarget;
		_selectedAttacker = null;
		_selectedCard = null;
		_attackDragFsm.PickUpCard(startPos, isClickSelect: true, isMobile: MobileInputRouter.IsMobile);

		HighlightActiveSkillTargets();

		// 启用键盘目标选择；任意单位主动技能可选择己方随从。
		_boardUI.EnableKeyboardTargeting(
			includePlayerSlots: weapon.ActiveSkill is IAnyUnitWeaponActive,
			includeEnemySlots: true);
	}

	/// <summary>
	/// 高亮武器主动技能合法目标——敌方所有随从 + 敌方英雄。
	/// 无视嘲讽限制（主动技能是减益效果，不是攻击）。
	/// </summary>
	private void HighlightActiveSkillTargets()
	{
		_boardUI.ClearHighlights();

		bool canTargetAnyUnit = _combat.PlayerHero.Weapon?.ActiveSkill is IAnyUnitWeaponActive;

		// 高亮所有敌方随从（无视嘲讽）
		_boardUI.HighlightMinions(_combat.Board.GetEnemyMinions());

		if (canTargetAnyUnit)
		{
			_boardUI.HighlightMinions(_combat.Board.GetPlayerMinions());

			_playerHeroSpellButton.Text = UiSymbolMode.Prefix(Loc.T("ui.combat.weapon_skill_on_player_hero", "对己方英雄释放"), "✦ ");
			_playerHeroSpellButton.Visible = true;
			_playerHeroSpellButton.Disabled = false;
		}

		// 显示敌方英雄按钮作为目标（复用，修改文本和事件）
		_enemyHeroCardAction = OnActiveSkillHeroPressed;
		foreach (var card in _enemyCards)
		{
			string activeName = _combat.PlayerHero.Weapon?.ActiveSkill?.Name
				?? UiSymbolMode.Prefix(Loc.T("ui.combat.weapon_skill", "技能"), "✦ ");
			card.AttackButton.Text = UiSymbolMode.Prefix(activeName, "✦ ");
		}
		SetEnemyHeroAttackTargetsVisible(true);

		GD.Print(canTargetAnyUnit
			? "[CombatUI] 主动技能目标模式——可对任意英雄或随从释放"
			: "[CombatUI] 主动技能目标模式——可对敌方英雄或任意敌方随从释放");
	}

	/// <summary>
	/// 武器主动技能对敌方英雄释放——在技能目标选择模式下点击敌方英雄按钮触发。
	/// 目标设为 null（IonPulse 默认行为：禁用敌方武器）。
	/// </summary>
	private void OnActiveSkillHeroPressed()
	{
		if (_combat.State.IsGameOver)
			return;

		GD.Print($"[CombatUI] 主动技能目标：敌方英雄[{_activeEnemyTargetIndex}]");

		_enemyHeroCardAction = OnEnemyHeroAttackPressed;

		// 目标设为具体敌人英雄（不再为 null，支持多敌人场景）
		_combat.ActiveSkillTarget = GetActiveEnemyHeroTarget();
		_combat.UseWeaponActiveSkill();
		ResetSelection();
		RefreshAll();
	}

	/// <summary>
	/// 高亮武器攻击合法目标——统一应用速度与嘲讽拦截规则。</summary>
	private void HighlightWeaponTargets()
	{
		_boardUI.ClearHighlights();
		bool canTargetFriendly = _combat.PlayerHero.Weapon?.PassiveSkill is IFriendlyMinionWeaponAttackPassive;
		if (canTargetFriendly)
		{
			_boardUI.HighlightMinions(_combat.Board.GetPlayerMinions());
		}

		var legalTargets = GetLegalEnemyAttackTargets(_combat.PlayerHero);
		var legalMinions = legalTargets.OfType<Minion>().ToList();
		_boardUI.HighlightMinions(legalMinions);

		_enemyHeroCardAction = OnWeaponAttackHeroPressed;
		foreach (var card in _enemyCards)
			card.AttackButton.Text = UiSymbolMode.Prefix(Loc.T("ui.combat.weapon_attack_cost", "武器攻击 ({cost}费)"), "⚔ ").Replace("{cost}", _combat.PlayerHero.Weapon!.AttackCost.ToString());
		var legalHeroes = legalTargets.OfType<Hero>().ToList();
		SetEnemyHeroAttackTargetsVisible(legalHeroes.Count > 0, legalHeroes);

		GD.Print($"[CombatUI] 武器攻击模式——合法随从 {legalMinions.Count} 个，合法英雄 {legalHeroes.Count} 个");
	}

	/// <summary>
	/// 武器攻击敌方英雄——在武器目标选择模式下点击敌方英雄按钮触发。
	/// </summary>
	private void OnWeaponAttackHeroPressed()
	{
		if (_combat.State.IsGameOver)
			return;
		var target = GetActiveEnemyHeroTarget();
		if (target == null)
			return;

		GD.Print($"[CombatUI] 武器攻击敌方英雄[{_activeEnemyTargetIndex}]");
		_combat.HeroWeaponAttackHero(target);
		_enemyHeroCardAction = OnEnemyHeroAttackPressed;
		ResetSelection();

		RefreshAll();
	}

	// ===== 开发者伤害模式 =====

	/// <summary>
	/// 进入开发者伤害目标选择模式（由 ChatScreen /damage -c N 触发）。
	/// 高亮所有合法目标，点击任意实体造成指定伤害，右键取消。
	/// </summary>
	public void EnterDevDamageMode(int damageAmount)
	{
		if (_combat.State.IsGameOver)
			return;

		ResetSelection();
		_devDamageAmount = damageAmount;
		_selectionMode = SelectionMode.DevDamageTargeting;
		_selectedCard = null;
		_selectedAttacker = null;

		// 高亮所有存活随从 + 显示敌方英雄按钮
		_boardUI.ClearHighlights();
		_boardUI.HighlightMinions(_combat.Board.GetAllMinions().Where(m => !m.IsDead));

		// 敌方英雄按钮
		SetEnemyHeroSpellTargetsVisible(true, UiSymbolMode.Prefix($"对敌方英雄造成 {damageAmount} 点伤害", "⚡ "));
		_enemyHeroCardAction = OnEnemyHeroSpellTargetViaOverlay;

		// 启用键盘目标选择（双方槽位）
		_boardUI.EnableKeyboardTargeting(includePlayerSlots: true, includeEnemySlots: true);

		GD.Print($"[CombatUI] 开发者伤害模式 — 点击目标造成 {damageAmount} 点伤害（右键取消）");
	}

	private void ExitDevDamageMode()
	{
		_boardUI.DisableKeyboardTargeting();

		_boardUI.ClearHighlights();
		SetEnemyHeroSpellTargetsVisible(false, "");
		_playerHeroSpellButton.Visible = false;
		_selectionMode = SelectionMode.Normal;
		RefreshAll();

		var h = OnDevDamageModeCompleted;
		OnDevDamageModeCompleted = null;
		h?.Invoke();
	}

	/// <summary>
	/// 开发者模式：对指定位置的随从造成伤害。
	/// </summary>
	private void HandleDevDamageSlot(BoardPosition position)
	{
		var target = _combat.Board.GetMinionAt(position);
		if (target == null || target.IsDead)
		{
			GD.Print($"[CombatUI] DevDamageSlot: no valid target");
			return;
		}

		GD.Print($"[CombatUI] DevDamage: {_devDamageAmount} dmg → {target.CardName}");
		target.ApplyDevDamage(_devDamageAmount);
		_combat.CheckDeaths();
		_combat.CheckVictoryOrDefeat();
		ExitDevDamageMode();
	}

	// ===== 箭头位置辅助方法 =====

	/// <summary>
	/// 获取随从所在槽位的屏幕中心坐标。
	/// </summary>
	/// <param name="minion">战场上的随从</param>
	/// <returns>槽位屏幕中心坐标；随从无有效槽位时返回 Vector2.Zero</returns>
	private Vector2 GetMinionScreenCenter(Minion minion)
	{
		if (minion.BoardRow == null)
			return Vector2.Zero;
		return _boardUI.GetSlotScreenCenter(new BoardPosition(minion.BoardRow.Value, minion.BoardSlotIndex));
	}

	/// <summary>
	/// 获取敌方身份卡（EnemyIdentityCard）的屏幕中心坐标。
	/// </summary>
	/// <param name="enemyIndex">敌人索引（对应 EnemyUnits 列表）</param>
	/// <returns>身份卡屏幕中心坐标；索引越界返回 Vector2.Zero</returns>
	private Vector2 GetEnemyIdentityCardCenter(int enemyIndex)
	{
		if (enemyIndex >= 0 && enemyIndex < _enemyCards.Count)
		{
			EnemyIdentityCard card = _enemyCards[enemyIndex];
			if (!SceneLifecycleGuard.ShouldSkip(card) && card.Size.X > 0f && card.Size.Y > 0f)
			{
				return card.GetGlobalTransformWithCanvas() * (card.Size * 0.5f);
			}
		}
		return Vector2.Zero;
	}

	/// <summary>
	/// 获取敌方身份卡朝向目标一侧的边缘锚点。
	/// 意图箭头应从攻击者卡片边缘伸出，而不是从卡片中心穿出来。
	/// </summary>
	private Vector2 GetEnemyIdentityCardAnchor(int enemyIndex, Vector2 targetPos)
	{
		if (enemyIndex < 0 || enemyIndex >= _enemyCards.Count)
			return Vector2.Zero;

		var rect = _enemyCards[enemyIndex].GetGlobalRect();
		var center = rect.Position + rect.Size / 2;
		var direction = targetPos - center;
		if (direction.LengthSquared() < 0.01f)
			return center;

		var half = rect.Size / 2;
		float tx = direction.X == 0 ? float.PositiveInfinity : half.X / MathF.Abs(direction.X);
		float ty = direction.Y == 0 ? float.PositiveInfinity : half.Y / MathF.Abs(direction.Y);
		float t = MathF.Min(tx, ty);
		return center + direction * t;
	}

	/// <summary>
	/// 获取玩家英雄生命值条的屏幕中心坐标。
	/// 用于敌方意图箭头指向玩家英雄的场景。
	/// </summary>
	/// <returns>生命值条屏幕中心坐标；未初始化返回 Vector2.Zero</returns>
	private Vector2 GetPlayerHeroScreenCenter()
	{
		if (_playerIdentityCard != null)
			return _playerIdentityCard.GetHealthBarScreenCenter();
		return Vector2.Zero;
	}

	/// <summary>
	/// 根据目标类型解析其屏幕中心坐标。
	/// 用于意图箭头从敌方随从指向其攻击目标的终点计算。
	/// </summary>
	/// <param name="target">伤害目标（Minion/Hero/null）</param>
	/// <returns>目标屏幕中心坐标；无法解析时返回 Vector2.Zero</returns>
	private Vector2 ResolveTargetScreenPos(IDamageTarget? target)
	{
		return target switch
		{
			Minion m => GetMinionScreenCenter(m),
			Hero h => h.IsPlayerSide ? GetPlayerHeroScreenCenter() :
					  GetEnemyHeroIndex(h) is var idx && idx >= 0 ? GetEnemyIdentityCardCenter(idx) : Vector2.Zero,
			_ => Vector2.Zero
		};
	}

	/// <summary>
	/// 根据 Hero 实例查找其在 EnemyUnits 中的索引，用于跳字/箭头等视觉定位。
	/// </summary>
	private int GetEnemyHeroIndex(Hero hero)
	{
		if (_combat == null)
			return -1;
		for (int i = 0; i < _combat.EnemyUnits.Count; i++)
			if (_combat.EnemyUnits[i].Body == hero)
				return i;
		return -1; // 未找到时返回 -1 而非 0，避免错误指向首个敌人
	}

	// ===== 选择状态管理 =====

	/// <summary>
	/// 重置所有选择状态——取消卡牌选中、攻击方选中、清除高亮、重置模式。
	/// </summary>
	private void ResetSelection()
	{
		ReleaseDragCard();
		_boardUI.DisableKeyboardTargeting();

		_arrowRenderer?.RemoveArrow("attack_select");
		_arrowRenderer?.RemoveArrow("move_select");
		_arrowRenderer?.RemoveArrow(CardTargetArrowKey);
		_selectionMode = SelectionMode.Normal;
		_selectedCard = null;
		_selectedAttacker = null;
		_pendingMinionSlot = -1;
		_boardUI.ClearHighlights();
		SetEnemyHeroAttackTargetsVisible(false);
		SetEnemyHeroSpellTargetsVisible(false, "");
		_playerHeroSpellButton.Visible = false;

		// 武器按钮可见性交由 UpdateWeaponDisplay() 根据 selectionMode + 武器状态统一管理
		UpdateWeaponDisplay();

		// 清除敌方英雄卡片绿色高亮
		foreach (var card in _enemyCards)
			card.SetAttackTargetHighlight(false);
		HidePlayZonePanel();

		// 清除攻击拖拽状态
		_attackDragFsm.ForceReset();

	}

	/// <summary>
	/// 移动端取消按钮按下回调。
	/// 根据当前状态执行不同的取消操作：手牌选择、开发者伤害、攻击选择等。
	/// </summary>
	private void OnMobileCancelPressed()
	{
		GD.Print("[CombatUI] 移动端取消按钮按下");

		// 手牌选择模式：取消手牌弃牌选择
		if (_isHandSelecting)
		{
			_combat?.CancelHandDiscardSelection();
			return;
		}

		// 开发者伤害模式：退出开发者伤害模式
		if (_selectionMode == SelectionMode.DevDamageTargeting)
		{
			ExitDevDamageMode();
			return;
		}

		// 其他选择模式（攻击目标、武器目标、法术目标、随从放置等）：重置选择
		ResetSelection();
	}

	// ===== 发现选牌 UI =====

	/// <summary>
	/// 战斗状态变化时的发现/手牌选择 UI 切换。
	/// 优先检查手牌选择模式，其次检查发现选牌。
	/// </summary>
	private void OnCombatStateChangedForDiscover()
	{
		if (_combat == null)
			return;

		if (_combat.IsDiscovering)
		{
			if (_combat.IsHandSelecting)
			{
				EnterHandSelectionMode();
			}
			else
			{
				ShowDiscoverUI();
			}
		}
		else if (_isHandSelecting)
		{
			ExitHandSelectionMode();
		}
		else if (_discoverUI != null && _discoverUI.Visible)
		{
			HideDiscoverUI();
			RefreshAll();
		}
	}

	/// <summary>
	/// 显示发现选牌覆盖层。
	/// </summary>
	private void ShowDiscoverUI()
	{
		if (_combat?.DiscoverOptions == null)
			return;

		_discoverUI ??= new DiscoverUI();
		if (_discoverUI.GetParent() == null)
			AddChild(_discoverUI);

		_discoverUI.CustomTitle = null;

		if (_combat.DiscoverRuntimeOptions != null && _combat.DiscoverPickCount > 1)
		{
			bool canSkip = true;
			var mode = _combat.CurrentSelectionMode;

			if (mode == Combat.CombatManager.PendingSelectionMode.ChooseDiscard)
			{
				_discoverUI.CustomTitle = Loc.T("ui.combat.discard_select_format", "选择 {count} 张手牌弃掉")
					.Replace("{count}", _combat.DiscoverPickCount.ToString());
				canSkip = false;
			}
			else if (mode == Combat.CombatManager.PendingSelectionMode.BladeCrisis)
			{
				_discoverUI.CustomTitle = Loc.T("ui.combat.discard_select_format_blade", "选择最多 {count} 张手牌弃掉")
					.Replace("{count}", _combat.DiscoverPickCount.ToString());
				canSkip = true;
			}

			_discoverUI.ShowCards(_combat.DiscoverRuntimeOptions, _combat.DiscoverPickCount, canSkip: canSkip, onChosen: chosen =>
			{
				_combat.ConfirmDiscoverCards(chosen);
			});
		}
		else
		{
			_discoverUI.ShowCards(_combat.DiscoverOptions, canSkip: true, onChosen: chosen =>
			{
				_combat.ConfirmDiscoverChoice(chosen);
			});
		}

		// 发现选牌期间禁用回合结束按钮（热键已由 HotkeyManager + _combat.IsDiscovering 守卫）
		_endTurnButton.Disabled = true;
		_smartAttackButton.Disabled = true;
		GD.Print("[CombatUI] 发现选牌 UI 已显示");
	}

	/// <summary>
	/// 隐藏发现选牌覆盖层。
	/// </summary>
	private void HideDiscoverUI()
	{
		if (_discoverUI != null)
		{
			_discoverUI.Hide();
		}
		_endTurnButton.Disabled = false;
		UpdateSmartAttackButton();
		GD.Print("[CombatUI] 发现选牌 UI 已隐藏");
	}

	// ===== 手牌选择模式（STS2 风格） =====

	/// <summary>
	/// 进入手牌选择模式（STS2 SimpleSelect 风格）。
	/// 半透明遮罩覆盖全屏，已选卡牌从手牌取出放到上方展示容器，
	/// 未选卡牌留在原处可继续点击选择。
	/// </summary>
	private void EnterHandSelectionMode()
	{
		if (_isHandSelecting)
			return;
		ResetSelection();
		_isHandSelecting = true;
		_selectedHandCards.Clear();
		_selectedHandCardUIs.Clear();

		_endTurnButton.Disabled = true;
		_smartAttackButton.Disabled = true;
		HidePlayZonePanel();

		float scale = UIScaler.Instance?.GetScaleFactor() ?? 1f;
		var viewportSize = GetViewport().GetVisibleRect().Size;

		// === 半透明遮罩 ===
		_handSelectMask = new ColorRect
		{
			Name = "HandSelectMask",
			Color = new Color(0, 0, 0, 0.6f),
			MouseFilter = MouseFilterEnum.Ignore,
		};
		_handSelectMask.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(_handSelectMask);

		// 遮罩淡入
		var maskTween = CreateTween();
		maskTween.TweenProperty(_handSelectMask, "color:a", 0.6f, 0.2);

		// === 已选卡牌展示容器（手牌上方） ===
		_selectedHandCardContainer = new Control
		{
			Name = "SelectedHandCardContainer",
			MouseFilter = MouseFilterEnum.Ignore,
			CustomMinimumSize = new Vector2(0, CardUI.DESIGN_HEIGHT * scale * 0.85f + 20f),
		};
		_selectedHandCardContainer.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(_selectedHandCardContainer);

		// === HandUI 选择模式 ===
		_handUI.SetHandSelectionMode(true);
		_handUI.OnCardSelectionToggled += OnHandCardSelectionToggled;

		// === 头部提示标签 ===
		string headerText;
		if (_combat.CurrentSelectionMode == Combat.CombatManager.PendingSelectionMode.CopyHandFill)
			headerText = Loc.T("ui.combat.copy_hand_fill_title", "选择 1 张手牌，用它的复制填满手牌");
		else if (_combat.CurrentSelectionMode == Combat.CombatManager.PendingSelectionMode.RestructureDiscard)
			headerText = Loc.T("ui.combat.restructure_discard_title", "重整：选择 1 张手牌弃掉");
		else if (_combat.CurrentSelectionMode == Combat.CombatManager.PendingSelectionMode.DevDiscard)
			headerText = Loc.T("ui.combat.dev_discard_title", "调试弃牌：选择任意张手牌弃掉");
		else if (_combat.HandSelectMin == _combat.HandSelectMax)
			headerText = Loc.T("ui.combat.discard_select_format", "选择 {count} 张手牌弃掉")
				.Replace("{count}", _combat.HandSelectMax.ToString());
		else
			headerText = Loc.T("ui.combat.discard_select_format_blade", "选择最多 {count} 张手牌弃掉")
				.Replace("{count}", _combat.HandSelectMax.ToString());

		_handSelectHeaderLabel = new Label
		{
			Name = "HandSelectHeader",
			Text = headerText,
			HorizontalAlignment = HorizontalAlignment.Center,
			CustomMinimumSize = new Vector2(400, 36),
			ZIndex = 10,
		};
		_handSelectHeaderLabel.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.3f));
		_handSelectHeaderLabel.AddThemeFontSizeOverride("font_size", 20);
		AddChild(_handSelectHeaderLabel);

		float headerY = viewportSize.Y - 210 * scale;
		_handSelectHeaderLabel.Position = new Vector2((viewportSize.X - 400) / 2, headerY);

		// === 确认按钮 ===
		_handSelectConfirmBtn = new Button
		{
			Name = "HandSelectConfirmBtn",
			Text = Loc.T("ui.hand_select.confirm", "确认"),
			CustomMinimumSize = new Vector2(120, 40),
			Visible = false,
			Disabled = true,
			ZIndex = 10,
		};
		_handSelectConfirmBtn.Pressed += OnHandSelectConfirmPressed;
		AddChild(_handSelectConfirmBtn);

		float btnY = headerY + 44 * scale;
		_handSelectConfirmBtn.Position = new Vector2((viewportSize.X - 120) / 2, btnY);

		GD.Print("[CombatUI] 进入手牌选择模式");
	}

	/// <summary>
	/// 退出手牌选择模式——清理 UI，恢复正常状态。
	/// </summary>
	private void ExitHandSelectionMode()
	{
		_isHandSelecting = false;
		_selectedHandCards.Clear();

		_handUI.SetHandSelectionMode(false);
		_handUI.OnCardSelectionToggled -= OnHandCardSelectionToggled;

		// 仍在模型手牌的真卡原样归还；已弃掉的牌才释放。
		foreach (var cardUI in _selectedHandCardUIs)
		{
			if (!_handUI.ReturnCardToHand(cardUI))
			{
				HandUI.RetireDetachedCard(cardUI);
			}
		}
		_selectedHandCardUIs.Clear();

		// 清理遮罩
		_handSelectMask?.QueueFree();
		_handSelectMask = null;

		// 清理展示容器
		_selectedHandCardContainer?.QueueFree();
		_selectedHandCardContainer = null;

		// 移除头部标签和确认按钮
		_handSelectHeaderLabel?.QueueFree();
		_handSelectHeaderLabel = null;
		if (_handSelectConfirmBtn != null)
		{
			_handSelectConfirmBtn.Pressed -= OnHandSelectConfirmPressed;
			_handSelectConfirmBtn.QueueFree();
			_handSelectConfirmBtn = null;
		}

		// 恢复回合结束按钮
		_endTurnButton.Disabled = false;
		UpdateSmartAttackButton();

		// 全量刷新恢复所有 UI
		RefreshAll();

		GD.Print("[CombatUI] 退出手牌选择模式");
	}

	/// <summary>
	/// 手牌选择模式：点击卡牌切换选中/取消。
	/// 选中时将 CardUI reparent 到上方展示容器，取消时放回手牌。
	/// </summary>
	private void OnHandCardSelectionToggled(Card.Card card)
	{
		if (_combat.HandSelectOptions?.Contains(card) != true)
		{
			return;
		}
		if (_selectedHandCards.Remove(card))
		{
			CardUI? selectedUI = _selectedHandCardUIs.Find(view => ReferenceEquals(view.Card, card));
			if (selectedUI != null)
			{
				_selectedHandCardUIs.Remove(selectedUI);
				if (!_handUI.ReturnCardToHand(selectedUI))
				{
					HandUI.RetireDetachedCard(selectedUI);
				}
			}
		}
		else if (_handUI.GetCardUIFor(card) is CardUI view && _selectedHandCardContainer != null
			&& _handUI.MoveCardOutOfHand(view, _selectedHandCardContainer, forPlay: false))
		{
			_selectedHandCards.Add(card);
			_selectedHandCardUIs.Add(view);
			view.SetHandSelectionHighlight(true);
			view.PreventDrag = true;
			view.ZIndex = 1;
		}

		// 重新排列展示容器内的卡片
		ArrangeSelectedHandCards();
		UpdateHandSelectConfirmButton();
	}

	/// <summary>
	/// 将展示容器内已选卡牌水平居中排列。
	/// </summary>
	private void ArrangeSelectedHandCards()
	{
		if (_selectedHandCardContainer == null || _selectedHandCardUIs.Count == 0)
			return;

		float s = UIScaler.Instance?.GetScaleFactor() ?? 1f;
		float cardW = CardUI.DESIGN_WIDTH * s * HandLayout.RestScale;
		float spacing = 20f * s;
		float totalW = cardW * _selectedHandCardUIs.Count + spacing * (_selectedHandCardUIs.Count - 1);
		float viewportW = GetViewportRect().Size.X;
		float startX = (viewportW - totalW) / 2f;
		float y = GetViewportRect().Size.Y * 0.5f;

		for (int i = 0; i < _selectedHandCardUIs.Count; i++)
		{
			var cardUI = _selectedHandCardUIs[i];
			if (HandUI.IsLive(cardUI))
			{
				cardUI.RotationDegrees = 0f;
				cardUI.Scale = Vector2.One * (HandLayout.RestScale * s / cardUI.FaceRenderScale);
				cardUI.SetScreenCenter(new Vector2(startX + (cardW * 0.5f) + i * (cardW + spacing), y));
			}
		}
	}

	/// <summary>
	/// 更新确认按钮的可见性和可用状态。
	/// </summary>
	private void UpdateHandSelectConfirmButton()
	{
		if (_handSelectConfirmBtn == null)
			return;

		int count = _selectedHandCards.Count;
		bool canConfirm = count >= _combat.HandSelectMin && count <= _combat.HandSelectMax;
		_handSelectConfirmBtn.Visible = canConfirm;
		_handSelectConfirmBtn.Disabled = !canConfirm;
	}

	/// <summary>
	/// 确认按钮点击——提交选中的卡牌给 CombatManager 结算。
	/// </summary>
	private void OnHandSelectConfirmPressed()
	{
		if (_combat == null || !_isHandSelecting)
			return;
		GD.Print($"[CombatUI] 手牌选择确认 — 选中 {_selectedHandCards.Count} 张");
		_combat.ConfirmHandDiscardSelection(_selectedHandCards);
	}

	// ===== 播放区域（类 STS2 风格） =====

	/// <summary>
	/// 判断屏幕坐标是否在播放区域内（STS2 动态阈值风格）。
	/// 阈值随拖拽起始位置自适应；点击提示、拖拽反馈和实际松手共用同一条判定线。
	/// </summary>
	private bool IsInPlayZone(Vector2 screenPos)
	{
		return screenPos.Y < GetPlayZoneThreshold();
	}

	private float GetPlayZoneThreshold()
	{
		float viewportH = GetViewport().GetVisibleRect().Size.Y;
		float baseThreshold = viewportH * PlayZoneBaseRatio;
		return _playZoneDragStartY > baseThreshold
			? Mathf.Max(baseThreshold, _playZoneDragStartY - 100f)
			: Mathf.Min(baseThreshold, _playZoneDragStartY - 50f);
	}

	/// <summary>
	/// 判断当前输入位置是否触发取消（STS2 CancelZone 风格）。
	/// 必须进入过出牌区，再回到底部 95% 才触发。
	/// </summary>
	private bool IsInCancelZone(Vector2 screenPos)
	{
		float viewportH = GetViewport().GetVisibleRect().Size.Y;
		float cancelThreshold = viewportH * CancelZoneScreenProportion;

		return _hasEnteredPlayZone && screenPos.Y > cancelThreshold;
	}


	/// <summary>
	/// 播放区域面板点击事件——click-select 模式下点击播放区域即打出卡牌。
	/// </summary>
	private void OnPlayZoneGuiInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
		{
			if (_selectionMode == SelectionMode.PlayingNoTargetCard && _selectedCard != null
				&& _dragCardUI?.IsClickFollowing == true && IsInPlayZone(mb.GlobalPosition))
			{
				GD.Print($"[CombatUI] 播放区域被点击 — 打出 {_selectedCard.CardName}");
				PlaySelectedNoTargetCard();
			}
		}
	}

	/// <summary>
	/// 执行无目标卡牌打出——拖拽松手或点击播放区域后的统一入口。
	/// </summary>
	private void PlaySelectedNoTargetCard()
	{
		if (_selectedCard == null || _combat.State.IsGameOver)
			return;

		// 效果结算期间（如进入发现/手牌选择）可能同步触发 ResetSelection() 清空 _selectedCard，先固定引用。
		Card.Card card = _selectedCard;
		bool success;
		switch (card.Type)
		{
			case CardType.Spell:
				success = PlaySelectedSpellWithVfxOrigin(_combat.PlayerHero);
				break;
			case CardType.Status:
				// 状态牌：自动以玩家英雄为目标
				success = PlaySelectedSpellWithVfxOrigin(_combat.PlayerHero);
				break;
			case CardType.Domain:
				// 领域亮牌直接从当前卡面中心弹入。
				success = _combat.PlayDomain(card);
				if (success)
				{
					ReleaseDragCard(played: true);
				}
				break;
			case CardType.Countermeasure:
				// 激活/取消不消耗反制牌；让同一张牌带着更新后的徽章弹回。
				bool toggled = _combat.PlayCountermeasure(card);
				RefreshAll();
				if (!toggled)
					GD.Print("[CombatUI] [FAIL] 反制牌激活/取消失败");
				return;
			default:
				GD.PrintErr($"[CombatUI] 不支持的类型：{card.Type}");
				OnCardDragCancelled();
				return;
		}

		if (success)
		{
			GD.Print($"[CombatUI] [OK] 无目标卡牌 {card.CardName} 已打出");
			RefreshAll();
		}
		else
		{
			GD.Print($"[CombatUI] [FAIL] 打出失败，取消");
			OnCardDragCancelled();
		}
	}

	/// <summary>
	/// 拖拽卡牌逐帧位置更新回调——检查是否进入/离开播放区域并更新视觉反馈。
	/// </summary>
	private void OnDragMoveForPlayZone(CardUI cardUI, Vector2 screenPos)
	{
		if (_selectionMode != SelectionMode.PlayingNoTargetCard)
			return;

		bool inZone = IsInPlayZone(screenPos);
		cardUI.SetPlayZoneHighlight(inZone);
		_hasEnteredPlayZone |= inZone;

		// STS2 CancelZone：拖回底部 95% 且曾离开取消区 → 自动取消
		if (cardUI.IsPointerDragging && IsInCancelZone(screenPos))
		{
			GD.Print("[CombatUI] 拖入取消区域 → 自动取消");
			OnCardDragCancelled();
		}
	}
}
