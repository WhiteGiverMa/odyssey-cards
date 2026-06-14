#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using OdysseyCards.Card;
using OdysseyCards.Core;
using OdysseyCards.Combat;
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
	/// <param name="slotIndex">被点击的槽位索引（0-4）</param>
	/// <param name="isPlayerSide">点击的槽位是否属于玩家方</param>
	private void OnBoardSlotClicked(int slotIndex, bool isPlayerSide)
	{
		if (_combat.State.IsGameOver)
			return;
		if (_attackDragFsm.CurrentPhase != InteractionPhase.Idle && (_selectionMode == SelectionMode.SelectingAttackTarget || _selectionMode == SelectionMode.SelectingWeaponTarget))
		{
			GD.Print($"[CombatUI] 忽略槽位点击：攻击拖拽尚未松手，mode={_selectionMode}, slot={slotIndex}, side={(isPlayerSide ? "P" : "E")}");
			return;
		}
		switch (_selectionMode)
		{
			case SelectionMode.PlacingMinion:
				HandleMinionPlacement(slotIndex, isPlayerSide);
				break;

			case SelectionMode.TargetingSpell:
				HandleSpellTarget(slotIndex, isPlayerSide);
				break;

			case SelectionMode.SelectingAttackTarget:
				HandleAttackTarget(slotIndex, isPlayerSide);
				break;

			case SelectionMode.SelectingWeaponTarget:
				HandleWeaponAttackTarget(slotIndex, isPlayerSide);
				break;

			case SelectionMode.SelectingActiveSkillTarget:
				HandleActiveSkillTarget(slotIndex, isPlayerSide);
				break;

			case SelectionMode.Normal:
				HandleNormalSlotClick(slotIndex, isPlayerSide);
				break;

			case SelectionMode.DevDamageTargeting:
				GD.Print($"[CombatUI] OnBoardSlotClicked → DevDamageTargeting, slot={slotIndex}, side={(isPlayerSide ? "P" : "E")}");
				HandleDevDamageSlot(slotIndex, isPlayerSide);
				break;
		}
	}

	/// <summary>
	/// 槽位右键点击回调——在目标选择模式下取消当前选择（等效 ESC）。
	/// 处理所有需要目标选择的模式：攻击、武器、法术、放置等。
	/// </summary>
	private void OnBoardSlotRightClicked(int slotIndex, bool isPlayerSide)
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
			|| _selectionMode == SelectionMode.PlacingMinion
			|| _selectionMode == SelectionMode.PlayingNoTargetCard)
		{
			GD.Print("[CombatUI] 槽位右键→取消目标选择");
			ResetSelection();
			_handUI.RefreshHand();
			return;
		}
	}

	// ----- 普通模式下的槽位点击 -----

	/// <summary>
	/// 普通模式下点击己方有随从的槽位 → 将该随从设为攻击方，进入攻击目标选择模式。
	/// </summary>
	private void HandleNormalSlotClick(int slotIndex, bool isPlayerSide)
	{
		if (!isPlayerSide)
			return; // 普通模式下只响应己方槽位

		var minion = _combat.Board.GetMinionAt(slotIndex, isPlayerSide: true);
		if (minion == null || minion.IsDead)
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
		_attackDragFsm.PickUpCard(GetInputPosition(), isClickSelect: true, isMobile: MobileInputRouter.IsMobile);

		GD.Print($"[CombatUI] 选中己方随从 {minion.CardName} 准备攻击");

		// 高亮合法攻击目标
		HighlightValidAttackTargets();

		// 启用键盘目标选择（仅敌方槽位）
		_boardUI.EnableKeyboardTargeting(includePlayerSlots: false, includeEnemySlots: true);
	}

	// ----- 随从放置 -----

	/// <summary>
	/// 随从放置模式下点击玩家槽位 → 召唤随从。
	/// </summary>
	private void HandleMinionPlacement(int slotIndex, bool isPlayerSide)
	{
		if (!isPlayerSide)
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

		GD.Print($"[CombatUI] 尝试放置随从 {_selectedCard.CardName}（{_selectedCard.GetEffectiveCost()}费）到槽位 {slotIndex}");
		bool success = _combat.PlayMinion(_selectedCard, slotIndex);
		if (success)
		{
			GD.Print($"[CombatUI] ✓ 随从 {_selectedCard.CardName} 已放置到槽位 {slotIndex}");
			// 随从上场不走飞行动画——随从死亡时才飞向牌堆
		}
		else
		{
			GD.Print($"[CombatUI] ✗ PlayMinion 失败 — 查看上方 [CombatManager] 错误日志");
		}

		RefreshAll();
	}

	// ----- 法术目标 -----

	/// <summary>
	/// 法术目标选择模式下点击槽位 → 对槽位上的随从施放法术。
	/// </summary>
	private void HandleSpellTarget(int slotIndex, bool isPlayerSide)
	{
		if (_selectedCard == null)
		{
			ResetSelection();
			return;
		}

		var target = _combat.Board.GetMinionAt(slotIndex, isPlayerSide);
		if (target == null || target.IsDead)
		{
			GD.Print("[CombatUI] 法术目标无效（空槽位或已死亡）");
			return;
		}

		GD.Print($"[CombatUI] 对 {target.CardName} 施放 {_selectedCard.CardName}");
		bool success = PlaySelectedSpellWithVfxOrigin(target);
		if (success)
		{
			GD.Print($"[CombatUI] ✓ 法术 {_selectedCard.CardName} 已施放");
			AnimateCardToDiscardPile();
		}
		else
		{
			GD.Print($"[CombatUI] ✗ 法术施放失败");
		}
		RefreshAll();
	}

	// ----- 攻击目标 -----

	/// <summary>
	/// 攻击目标模式下点击敌方槽位 → 发动随从攻击。
	/// </summary>
	private void HandleAttackTarget(int slotIndex, bool isPlayerSide)
	{
		if (_selectedAttacker == null)
		{
			ResetSelection();
			return;
		}

		if (isPlayerSide)
		{
			GD.Print("[CombatUI] 不能攻击己方随从");
			return;
		}

		var defender = _combat.Board.GetMinionAt(slotIndex, isPlayerSide: false);
		if (defender == null || defender.IsDead)
		{
			GD.Print("[CombatUI] 攻击目标无效");
			return;
		}

		GD.Print($"[CombatUI] {_selectedAttacker.CardName} 攻击 {defender.CardName}");
		_combat.MinionAttack(_selectedAttacker, defender);
		RefreshAll();
	}

	/// <summary>
	/// 武器攻击目标模式下点击敌方槽位 → 发动武器攻击随从。
	/// </summary>
	private void HandleWeaponAttackTarget(int slotIndex, bool isPlayerSide)
	{
		if (isPlayerSide)
		{
			GD.Print("[CombatUI] 武器不能攻击己方随从");
			return;
		}

		var target = _combat.Board.GetMinionAt(slotIndex, isPlayerSide: false);
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
	private void HandleActiveSkillTarget(int slotIndex, bool isPlayerSide)
	{
		if (isPlayerSide)
		{
			GD.Print("[CombatUI] 主动技能不能对己方随从释放");
			return;
		}

		var target = _combat.Board.GetMinionAt(slotIndex, isPlayerSide: false);
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
		RefreshAll();
	}

	// ===== 事件处理——手牌选中 =====

	/// <summary>
	/// 手牌中卡牌被选中时的处理。
	/// 根据卡牌类型进入不同的选择模式，并将卡牌 UI 重 parent 到拖拽层使其可自由跟随鼠标。
	/// </summary>
	/// <param name="card">被选中的卡牌</param>
	private void OnCardSelectedFromHand(Card.Card card)
	{
		if (_combat.State.IsGameOver)
			return;
		if (card == null)
			return;

		// 取消之前的攻击选择
		_selectedAttacker = null;

		// 清理上一个拖拽中的卡牌（防止快速点击产生幽灵浮动卡）
		// 但先记住旧卡数据，以便后面归还手牌
		Card.Card? previousCardData = _dragCardUI?.Card;
		CleanupDragCard();

		// 将卡牌 UI 从 HandUI 移到 DragLayer，脱离 HBoxContainer 布局约束
		var cardUI = _handUI.GetCardUIFor(card);
		bool isKeyboardSel = _handUI.IsKeyboardSelection;
		Vector2 savedGlobalPos = Vector2.Zero;
		Vector2 savedSize = Vector2.Zero;
		Vector2 savedScale = Vector2.One;
		bool startedWithPointerDown = false;

		if (cardUI != null)
		{
			bool isMobileDrag = MobileInputRouter.IsMobile && cardUI.IsDragging;

			// 在 StopLayoutControl / Offset 清除之前保存全局位置——之后会变
			savedGlobalPos = cardUI.GlobalPosition;
			savedSize = cardUI.Size;
			savedScale = cardUI.Scale;

			_handUI.StopLayoutControl(cardUI);

			// 清除 HandUI 布局遗留的 OffsetTop（Select() 设的）。
			cardUI.OffsetTop = 0;
			cardUI.OffsetBottom = 0;
			cardUI.OffsetLeft = 0;
			cardUI.OffsetRight = 0;

			if (isMobileDrag)
			{
				// 移动端纯拖拽：卡牌已在跟随手指移动，用 Reparent 保持当前位置
				cardUI.Reparent(_dragLayer);
			}
			else
			{
				// 鼠标/键盘都先保持卡牌在手牌中的视觉位置，随后由统一表现方法决定跟随或居中。
				cardUI.GetParent()?.RemoveChild(cardUI);
				_dragLayer.AddChild(cardUI);
				cardUI.GlobalPosition = savedGlobalPos;
			}

			startedWithPointerDown = !isKeyboardSel && !MobileInputRouter.IsMobile && Input.IsMouseButtonPressed(MouseButton.Left);

			_dragCardUI = cardUI;

			// 从 HandUI 内部列表脱钩，防止 RefreshHand 误销毁拖拽中的卡片
			_handUI.DetachCardFromList(cardUI);

			// 订阅拖拽松手事件——用于拖拽→松手打出 / 松手取消
			cardUI.OnCardDropped += OnCardDroppedHandler;

			// 旧卡数据归还手牌——创建新的 CardUI 让它回到手牌可重新选中
			if (previousCardData != null)
			{
				_handUI.AddCardBack(previousCardData);
			}
		}

		switch (card.Type)
		{
			case CardType.Minion:
				EnterMinionPlacementMode(card);
				break;

			case CardType.Spell:
				if (card.Data.RequiresTarget)
				{
					EnterSpellTargetMode(card);
				}
				else
				{
					// 无目标法术（如「警戒」）：进入拖拽播放模式
					EnterNoTargetPlayMode(card);
				}
				break;

			case CardType.Domain:
				// 领域牌：进入拖拽播放模式（类 STS2 风格，拖到中央打出）
				EnterNoTargetPlayMode(card);
				break;

			case CardType.Status:
				// 状态牌：进入无目标播放模式（自动以玩家英雄为目标）
				EnterNoTargetPlayMode(card);
				break;

			default:
				GD.Print($"[CombatUI] 未知卡牌类型：{card.Type}");
				break;
		}

		PresentSelectedCardForPlay(card, isKeyboardSel, startedWithPointerDown, savedGlobalPos, savedSize, savedScale);
	}

	private static bool IsCardTargetSelectionCard(Card.Card card)
	{
		return card.Type == CardType.Minion || (card.Type == CardType.Spell && card.Data.RequiresTarget);
	}

	private void PresentSelectedCardForPlay(
		Card.Card card,
		bool startedByKeyboard,
		bool startedWithPointerDown,
		Vector2 originalGlobalPos,
		Vector2 originalSize,
		Vector2 originalScale)
	{
		if (_dragCardUI == null)
			return;

		if (IsCardTargetSelectionCard(card))
		{
			if (startedWithPointerDown)
			{
				// 鼠标按下路径：以 click-select 模式开始（卡牌跟随鼠标），
				// 让 CardUI._Process 根据实际拖拽距离决定升级为拖拽松手还是保持选中。
				// 快速点击→保持选中→点击目标打出；按住拖动>阈值→拖拽松手打出。
				_dragCardUI.BeginPointerFollowFrom(
					_dragCardUI.LastClickGlobalPosition,
					startAsClickFollow: true);
			}
			else
			{
				// 点击选中 / 键盘快捷键路径（ClickMouseToTarget）：
				// 卡牌居中展示，仅箭头跟随鼠标，第二击目标确认。
				Vector2 viewportSize = GetViewportRect().Size;
				Vector2 center = new(viewportSize.X * 0.5f, viewportSize.Y - _dragCardUI.Size.Y * TargetingCardScale * 0.5f);

				if (startedByKeyboard)
				{
					// 键盘选中：直接定位到展示位，仅对 Scale 做 Back.ease 弹入动画，
					// 跳过 Position Tween 以避免 reparent 后帧间位置漂移导致的跳变。
					_dragCardUI.CancelDragSilent();
					_dragCardUI.MouseFilter = MouseFilterEnum.Ignore;
					_dragCardUI.ZIndex = 10;

					Vector2 targetSize = _dragCardUI.Size * TargetingCardScale;
					_dragCardUI.GlobalPosition = center - targetSize * 0.5f;

					var scaleTween = _dragCardUI.CreateTween();
					scaleTween.TweenProperty(_dragCardUI, "scale", Vector2.One * TargetingCardScale, 0.18f)
						.SetTrans(Tween.TransitionType.Back)
						.SetEase(Tween.EaseType.Out);
				}
				else
				{
					_dragCardUI.PresentForTargeting(center, TargetingCardScale);
				}
			}

			return;
		}

		if (_selectionMode == SelectionMode.PlayingNoTargetCard)
		{
			Vector2 anchor = startedByKeyboard
				? originalGlobalPos + originalSize * originalScale * 0.5f
				: _dragCardUI.LastClickGlobalPosition;
			// 无目标卡牌始终以 click-select 模式开始，让用户看到播放区域后再决定打出/取消。
			// 按住拖动超过阈值时 CardUI._Process 会自动升级为拖拽松手模式。
			_dragCardUI.BeginPointerFollowFrom(anchor, startAsClickFollow: true);

			// 记录拖拽起始 Y（用于动态 PlayZone 阈值）
			_playZoneDragStartY = anchor.Y;
			_hasLeftCancelZone = false;
		}
	}

	/// <summary>
	/// 右键取消拖拽——卡牌回到手牌，退出所有选择模式。
	/// </summary>
	private void OnCardDragCancelled()
	{
		GD.Print("[CombatUI] 拖拽取消");
		CleanupDragCard();
		ResetSelection();

		// 重建手牌 UI（恢复卡牌到正确位置）
		_handUI.RefreshHand();
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
		if (hit != null && hit.Value.isPlayerSide)
		{
			GD.Print($"[CombatUI] 命中己方槽位 {hit.Value.slotIndex}，执行放置");
			HandleMinionPlacement(hit.Value.slotIndex, hit.Value.isPlayerSide);
		}
		else
		{
			GD.Print(hit != null
				? $"[CombatUI] 命中敌方槽位 {hit.Value.slotIndex}，但随从只能放在己方 — 取消"
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
		if (_playerHeroSpellButton.Visible && _playerHeroPanel.GetGlobalRect().HasPoint(screenPos))
		{
			GD.Print("[CombatUI] 法术松手位置：己方英雄");
			OnPlayerHeroSpellTarget();
			return;
		}

		var hit = _boardUI.GetSlotAtPosition(screenPos);
		if (hit != null)
		{
			var target = _combat.Board.GetMinionAt(hit.Value.slotIndex, hit.Value.isPlayerSide);
			if (target != null && !target.IsDead)
			{
				HandleSpellTarget(hit.Value.slotIndex, hit.Value.isPlayerSide);
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
			_handUI.RefreshHand();
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
		if (hit != null && !hit.Value.isPlayerSide)
		{
			HandleAttackTarget(hit.Value.slotIndex, hit.Value.isPlayerSide);
		}
		else
		{
			int attackTargetIndex = GetEnemyCardIndexAt(screenPos, requireAttackHighlight: true);
			if (attackTargetIndex >= 0)
			{
				_activeEnemyTargetIndex = attackTargetIndex;
				OnEnemyHeroAttackPressed();
			}
			else
			{
				GD.Print("[CombatUI] 攻击松手位置无效，取消选择");
				ResetSelection();
				_handUI.RefreshHand();
				UpdateWeaponDisplay();
			}
		}
	}

	/// <summary>
	/// 清理当前拖拽卡牌 UI 引用并取消订阅。
	/// 先隐藏再 QueueFree，避免与 RefreshHand 新建的卡牌产生视觉重叠。
	/// </summary>
	private void CleanupDragCard()
	{
		if (_dragCardUI != null)
		{
			_dragCardUI.OnCardDropped -= OnCardDroppedHandler;
			_dragCardUI.OnDragMove -= OnDragMoveForPlayZone;
			_dragCardUI.CancelDragSilent(); // 退出拖拽状态，防止 _Process 残留
			_dragCardUI.Visible = false;     // 立即隐藏，防止与 RefreshHand 新建卡牌重叠
			_dragCardUI.QueueFree();
			_dragCardUI = null;
		}
	}

	/// <summary>
	/// 将当前拖拽卡牌从 _dragLayer 提取出来并启动飞向牌堆的动画。
	/// 目标位置根据卡牌关键词自动选择：轮战卡牌 → 抽牌堆，普通卡牌 → 弃牌堆。
	/// 调用后 _dragCardUI 设为 null，CleanupDragCard 不再处理这张卡。
	/// 仅在卡牌成功打出后调用。
	/// </summary>
	private void AnimateCardToDiscardPile()
	{
		if (_dragCardUI == null)
			return;

		var cardUI = _dragCardUI;
		_dragCardUI = null; // 提取所有权，防止 CleanupDragCard 二次处理

		cardUI.OnCardDropped -= OnCardDroppedHandler;
		cardUI.OnDragMove -= OnDragMoveForPlayZone;
		cardUI.CancelDragSilent();

		// 领域 → 玩家效果栏，轮战 → 抽牌堆，普通 → 弃牌堆
		Vector2 targetPos;
		if (_selectedCard?.Type == CardType.Domain)
			targetPos = GetPlayerEffectBarCenter();
		else if (_selectedCard?.HasRecycle ?? false)
			targetPos = GetDrawPileCenter();
		else
			targetPos = GetDiscardPileCenter();
		CardFlyVfx.PlayToDiscard(cardUI, targetPos, _cardFlyLayer);
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
		return _playerEffectBar.GlobalPosition + _playerEffectBar.Size / 2f;
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
		for (int i = 0; i < Board.MaxSlotsPerSide; i++)
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
		for (int i = 0; i < Board.MaxSlotsPerSide; i++)
		{
			var m = _combat.Board.GetMinionAt(i, isPlayerSide: false);
			if (m != null && !m.IsDead && TargetTagsHelper.IsValidTarget(m.GetTargetTags(), require, exclude))
			{
				enemyTargets.Add(i);
			}
		}
		_boardUI.HighlightSlots(enemyTargets, isPlayerSide: false, highlight: true);

		// 高亮己方随从 —— 仅高亮通过 TargetFilter 的目标
		var friendlyTargets = new List<int>();
		for (int i = 0; i < Board.MaxSlotsPerSide; i++)
		{
			var m = _combat.Board.GetMinionAt(i, isPlayerSide: true);
			if (m != null && !m.IsDead && TargetTagsHelper.IsValidTarget(m.GetTargetTags(), require, exclude))
			{
				friendlyTargets.Add(i);
			}
		}
		if (friendlyTargets.Count > 0)
		{
			_boardUI.HighlightSlots(friendlyTargets, isPlayerSide: true, highlight: true);
		}

		// 高亮默认存活敌方英雄作为法术目标
		var enemyHero = GetDefaultEnemyHeroTarget();
		bool enemyHeroValid = enemyHero != null && TargetTagsHelper.IsValidTarget(enemyHero.GetTargetTags(), require, exclude);
		SetEnemyHeroSpellTargetsVisible(enemyHeroValid, Loc.T("ui.combat.cast_on_enemy_hero", "对敌方英雄施放"));
		// 法术模式下覆盖层点击路由到法术目标选择
		if (enemyHeroValid)
			_enemyHeroCardAction = OnEnemyHeroSpellTargetViaOverlay;

		// 高亮己方英雄作为法术目标
		_playerHeroSpellButton.Visible = TargetTagsHelper.IsValidTarget(
			_combat.PlayerHero.GetTargetTags(), require, exclude);

		GD.Print($"[CombatUI] 法术目标模式——{_selectedCard.CardName}（require={require} exclude={exclude}，" +
				  $"敌方随从 {enemyTargets.Count} + 己方随从 {friendlyTargets.Count} + " +
				  $"{(enemyTargets.Count > 0 || friendlyTargets.Count > 0 ? " + 英雄" : "")}）");

		// 启用键盘目标选择（根据高亮的阵营方）
		_boardUI.EnableKeyboardTargeting(
			includePlayerSlots: friendlyTargets.Count > 0,
			includeEnemySlots: enemyTargets.Count > 0);
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

		ShowPlayZonePanel();

		// 订阅拖拽卡牌的逐帧位置更新（用于播放区域判定和视觉反馈）
		if (_dragCardUI != null)
		{
			_dragCardUI.OnDragMove += OnDragMoveForPlayZone;
		}

		GD.Print($"[CombatUI] 无目标播放模式——{card.CardName}（拖到绿色区域松手打出，右键取消）");
	}

	/// <summary>
	/// 高亮合法攻击目标——敌方有嘲讽随从时仅高亮嘲讽目标，
	/// 无嘲讽时高亮所有敌方随从并显示攻击英雄按钮。
	/// </summary>
	private void HighlightValidAttackTargets()
	{
		_boardUI.ClearHighlights();
		_enemyHeroCardAction = OnEnemyHeroAttackPressed;

		var enemyTaunts = _combat.Board.GetTaunts(ofEnemy: true);
		if (enemyTaunts.Count > 0)
		{
			// 有嘲讽——仅高亮嘲讽随从
			var tauntIndices = enemyTaunts
				.Where(m => m.BoardSlotIndex >= 0)
				.Select(m => m.BoardSlotIndex)
				.ToList();

			_boardUI.HighlightSlots(tauntIndices, isPlayerSide: false, highlight: true);
			SetEnemyHeroAttackTargetsVisible(false);

			GD.Print($"[CombatUI] 攻击目标模式——敌方有 {enemyTaunts.Count} 个嘲讽随从阻挡");
		}
		else
		{
			// 无嘲讽——高亮所有敌方随从
			var allEnemyIndices = new List<int>();
			for (int i = 0; i < Board.MaxSlotsPerSide; i++)
			{
				var m = _combat.Board.GetMinionAt(i, isPlayerSide: false);
				if (m != null && !m.IsDead)
				{
					allEnemyIndices.Add(i);
				}
			}

			if (allEnemyIndices.Count > 0)
			{
				_boardUI.HighlightSlots(allEnemyIndices, isPlayerSide: false, highlight: true);
			}

			SetEnemyHeroAttackTargetsVisible(true);

			GD.Print("[CombatUI] 攻击目标模式——可攻击敌方英雄");
		}
	}

	private void OnEnemyHeroCardActionPressed(int enemyIndex)
	{
		if (_attackDragFsm.CurrentPhase != InteractionPhase.Idle && (_selectionMode == SelectionMode.SelectingAttackTarget || _selectionMode == SelectionMode.SelectingWeaponTarget))
		{
			GD.Print($"[CombatUI] 忽略敌方英雄点击：攻击拖拽尚未松手，mode={_selectionMode}, enemy={enemyIndex}");
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

	private void SetEnemyHeroAttackTargetsVisible(bool visible)
	{
		for (int i = 0; i < _enemyCards.Count; i++)
			_enemyCards[i].SetAttackTargetHighlight(visible && !_combat.EnemyUnits[i].Body.IsDead);
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
		_combat.MinionAttackHero(_selectedAttacker, target);
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
		if (_combat.State.IsGameOver)
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
		if (_combat.State.IsGameOver)
			return;

		if (_selectedCard == null)
		{
			GD.PrintErr("[CombatUI] 无法术牌选中");
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

		if (_combat.State.IsGameOver)
			return;
		if (_selectionMode == SelectionMode.SelectingWeaponTarget)
		{
			GD.Print("[CombatUI] 再次点击武器攻击按钮 → 取消武器攻击选择");
			ResetSelection();
			_handUI.RefreshHand();
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

		EnterWeaponAttackTargetMode(GetInputPosition());
	}

	private void EnterWeaponAttackTargetMode(Vector2 startPos)
	{
		var weapon = _combat.PlayerHero.Weapon;
		if (weapon == null)
			return;

		GD.Print($"[CombatUI] 进入武器攻击目标选择模式 — {weapon.Name}，start=({startPos.X:F0},{startPos.Y:F0})");

		_selectionMode = SelectionMode.SelectingWeaponTarget;
		_selectedAttacker = null;
		_selectedCard = null;
		_attackDragFsm.PickUpCard(startPos, isClickSelect: true, isMobile: MobileInputRouter.IsMobile);

		// 高亮合法攻击目标
		HighlightWeaponTargets();

		// 启用键盘目标选择（仅敌方槽位）
		_boardUI.EnableKeyboardTargeting(includePlayerSlots: false, includeEnemySlots: true);
	}

	/// <summary>
	/// 武器主动技能按钮点击——进入主动技能目标选择模式。
	/// 所有敌方随从 + 敌方英雄都是合法目标（无视嘲讽——这是减益技能不是攻击）。
	/// </summary>
	private void OnWeaponActiveSkillPressed()
	{
		if (_combat.State.IsGameOver)
			return;

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

		GD.Print($"[CombatUI] 进入主动技能目标选择模式 — {weapon.ActiveSkill.Name}");

		_selectionMode = SelectionMode.SelectingActiveSkillTarget;
		_selectedAttacker = null;
		_selectedCard = null;

		HighlightActiveSkillTargets();

		// 启用键盘目标选择（仅敌方槽位）
		_boardUI.EnableKeyboardTargeting(includePlayerSlots: false, includeEnemySlots: true);
	}

	/// <summary>
	/// 高亮武器主动技能合法目标——敌方所有随从 + 敌方英雄。
	/// 无视嘲讽限制（主动技能是减益效果，不是攻击）。
	/// </summary>
	private void HighlightActiveSkillTargets()
	{
		_boardUI.ClearHighlights();

		// 高亮所有敌方随从（无视嘲讽）
		var allEnemyIndices = new List<int>();
		for (int i = 0; i < Board.MaxSlotsPerSide; i++)
		{
			var m = _combat.Board.GetMinionAt(i, isPlayerSide: false);
			if (m != null && !m.IsDead)
			{
				allEnemyIndices.Add(i);
			}
		}

		if (allEnemyIndices.Count > 0)
		{
			_boardUI.HighlightSlots(allEnemyIndices, isPlayerSide: false, highlight: true);
		}

		// 显示敌方英雄按钮作为目标（复用，修改文本和事件）
		_enemyHeroCardAction = OnActiveSkillHeroPressed;
		foreach (var card in _enemyCards)
		{
			string activeName = _combat.PlayerHero.Weapon?.ActiveSkill?.Name
				?? Loc.T("ui.combat.weapon_skill", "✦ 技能");
			card.AttackButton.Text = $"✦ {activeName}";
		}
		SetEnemyHeroAttackTargetsVisible(true);

		GD.Print("[CombatUI] 主动技能目标模式——可对敌方英雄或任意随从释放");
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
		RefreshAll();
	}

	/// <summary>
	/// 高亮武器攻击合法目标——敌方随从（受嘲讽限制）+ 敌方英雄。</summary>
	private void HighlightWeaponTargets()
	{
		_boardUI.ClearHighlights();

		var enemyTaunts = _combat.Board.GetTaunts(ofEnemy: true);
		if (enemyTaunts.Count > 0)
		{
			// 有嘲讽——仅高亮嘲讽随从
			var tauntIndices = enemyTaunts
				.Where(m => m.BoardSlotIndex >= 0)
				.Select(m => m.BoardSlotIndex)
				.ToList();

			_boardUI.HighlightSlots(tauntIndices, isPlayerSide: false, highlight: true);
			SetEnemyHeroAttackTargetsVisible(false);

			GD.Print($"[CombatUI] 武器攻击模式——敌方有 {enemyTaunts.Count} 个嘲讽随从阻挡");
		}
		else
		{
			// 无嘲讽——高亮所有敌方随从
			var allEnemyIndices = new List<int>();
			for (int i = 0; i < Board.MaxSlotsPerSide; i++)
			{
				var m = _combat.Board.GetMinionAt(i, isPlayerSide: false);
				if (m != null && !m.IsDead)
				{
					allEnemyIndices.Add(i);
				}
			}

			if (allEnemyIndices.Count > 0)
			{
				_boardUI.HighlightSlots(allEnemyIndices, isPlayerSide: false, highlight: true);
			}

			// 显示攻击英雄按钮 + 绿色高亮
			_enemyHeroCardAction = OnWeaponAttackHeroPressed;
			foreach (var card in _enemyCards)
				card.AttackButton.Text = Loc.T("ui.combat.weapon_attack_cost", "⚔ 武器攻击 ({cost}费)").Replace("{cost}", _combat.PlayerHero.Weapon!.AttackCost.ToString());
			SetEnemyHeroAttackTargetsVisible(true);

			GD.Print("[CombatUI] 武器攻击模式——可攻击敌方英雄或随从");
		}
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
	/// 进入开发者伤害目标选择模式（由 DevConsole /damage -c N 触发）。
	/// 高亮所有合法目标，点击任意实体造成指定伤害，右键取消。
	/// </summary>
	public void EnterDevDamageMode(int damageAmount)
	{
		if (_combat.State.IsGameOver)
			return;

		_devDamageAmount = damageAmount;
		_selectionMode = SelectionMode.DevDamageTargeting;
		_selectedCard = null;
		_selectedAttacker = null;

		// 高亮所有存活随从 + 显示敌方英雄按钮
		_boardUI.ClearHighlights();
		var playerSlots = new List<int>();
		var enemySlots = new List<int>();
		for (int i = 0; i < Board.MaxSlotsPerSide; i++)
		{
			var pm = _combat.Board.GetMinionAt(i, isPlayerSide: true);
			if (pm != null && !pm.IsDead)
				playerSlots.Add(i);
			var em = _combat.Board.GetMinionAt(i, isPlayerSide: false);
			if (em != null && !em.IsDead)
				enemySlots.Add(i);
		}
		_boardUI.HighlightSlots(playerSlots, isPlayerSide: true, highlight: true);
		_boardUI.HighlightSlots(enemySlots, isPlayerSide: false, highlight: true);

		// 敌方英雄按钮
		SetEnemyHeroSpellTargetsVisible(true, $"⚡ 对敌方英雄造成 {damageAmount} 点伤害");
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
	private void HandleDevDamageSlot(int slotIndex, bool isPlayerSide)
	{
		GD.Print($"[CombatUI] DevDamageSlot: slot={slotIndex}, side={(isPlayerSide ? "player" : "enemy")}");
		var target = _combat.Board.GetMinionAt(slotIndex, isPlayerSide);
		if (target == null || target.IsDead)
		{
			GD.Print($"[CombatUI] DevDamageSlot: no valid target");
			return;
		}

		GD.Print($"[CombatUI] DevDamage: {_devDamageAmount} dmg → {(isPlayerSide ? "己方" : "敌方")} {target.CardName}");
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
		int slotIndex = minion.BoardSlotIndex;
		if (slotIndex < 0 || slotIndex >= Board.MaxSlotsPerSide)
			return Vector2.Zero;
		return _boardUI.GetSlotScreenCenter(slotIndex, minion.IsPlayerSide);
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
			var rect = _enemyCards[enemyIndex].GetGlobalRect();
			return rect.Position + rect.Size / 2;
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
		if (_playerHealthBar != null)
		{
			var rect = _playerHealthBar.GetGlobalRect();
			return new Vector2(rect.Position.X + rect.Size.X / 2, rect.Position.Y + rect.Size.Y / 2);
		}
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
			Minion m => m.BoardSlotIndex >= 0
				? _boardUI.GetSlotScreenCenter(m.BoardSlotIndex, m.IsPlayerSide)
				: Vector2.Zero,
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
		if (_combat == null) return -1;
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
		_boardUI.DisableKeyboardTargeting();

		_arrowRenderer?.RemoveArrow("attack_select");
		_arrowRenderer?.RemoveArrow(CardTargetArrowKey);
		_selectionMode = SelectionMode.Normal;
		_selectedCard = null;
		_selectedAttacker = null;
		_boardUI.ClearHighlights();
		SetEnemyHeroAttackTargetsVisible(false);
		SetEnemyHeroSpellTargetsVisible(false, "");
		_playerHeroSpellButton.Visible = false;
		_weaponAttackButton.Visible = false;
		_weaponActiveSkillButton.Visible = false;

		// 清除敌方英雄卡片绿色高亮
		foreach (var card in _enemyCards)
			card.SetAttackTargetHighlight(false);
		_handUI.DeselectCard();
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
		_handUI.RefreshHand();
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
		_isHandSelecting = true;
		_selectedHandCards.Clear();
		_selectedHandCardUIs.Clear();

		_endTurnButton.Disabled = true;
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
		if (_combat.HandSelectMin == _combat.HandSelectMax)
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

		// 归还展示容器内的卡牌回手牌
		foreach (var cardUI in _selectedHandCardUIs)
		{
			if (cardUI.Card != null && GodotObject.IsInstanceValid(cardUI))
				cardUI.QueueFree();
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

		// 恢复 HandUI 正常模式
		_handUI.HandSelectMode = false;
		_handUI.OnCardSelectionToggled -= OnHandCardSelectionToggled;

		// 恢复回合结束按钮
		_endTurnButton.Disabled = false;

		// 全量刷新恢复所有 UI
		RefreshAll();

		GD.Print("[CombatUI] 退出手牌选择模式");
	}

	/// <summary>
	/// 手牌选择模式：点击卡牌切换选中/取消。
	/// 选中时将 CardUI reparent 到上方展示容器，取消时放回手牌。
	/// </summary>
	private void OnHandCardSelectionToggled(Card.Card card, bool toggled)
	{
		if (_selectedHandCards.Contains(card))
		{
			// 取消选中
			_selectedHandCards.Remove(card);
			var cardUI = _handUI.GetCardUIFor(card);
			if (cardUI != null)
			{
				cardUI.SetHandSelectionHighlight(false);
			}
			// 尝试从展示容器移除
			var selectedUI = _selectedHandCardUIs.Find(c => c.Card == card);
			if (selectedUI != null)
			{
				_selectedHandCardUIs.Remove(selectedUI);
				_handUI.AddCardBack(card);
				// AddCardBack 创建了新 CardUI，销毁旧的
				selectedUI.QueueFree();
			}
		}
		else
		{
			// 选中：从手牌取出，放到上方展示容器
			_selectedHandCards.Add(card);
			var cardUI = _handUI.GetCardUIFor(card);
			if (cardUI != null && _selectedHandCardContainer != null)
			{
				_handUI.StopLayoutControl(cardUI);
				_handUI.DetachCardFromList(cardUI);
				cardUI.GetParent()?.RemoveChild(cardUI);
				cardUI.OffsetTop = 0;
				cardUI.OffsetBottom = 0;
				cardUI.OffsetLeft = 0;
				cardUI.OffsetRight = 0;
				cardUI.SetHandSelectionHighlight(true);
				cardUI.PreventDrag = true;
				_selectedHandCardContainer.AddChild(cardUI);
				_selectedHandCardUIs.Add(cardUI);
			}
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
		float cardW = CardUI.DESIGN_WIDTH * s * 0.85f;
		float spacing = 20f * s;
		float totalW = cardW * _selectedHandCardUIs.Count + spacing * (_selectedHandCardUIs.Count - 1);
		float viewportW = GetViewportRect().Size.X;
		float startX = (viewportW - totalW) / 2f;
		float y = _selectedHandCardContainer.Size.Y * 0.5f - CardUI.DESIGN_HEIGHT * s * 0.85f * 0.5f;

		for (int i = 0; i < _selectedHandCardUIs.Count; i++)
		{
			var cardUI = _selectedHandCardUIs[i];
			cardUI.Scale = new Vector2(0.85f, 0.85f);
			cardUI.Position = new Vector2(startX + i * (cardW + spacing), y);
		}
	}

	/// <summary>
	/// 刷新手牌选择模式下的卡牌高亮（用于 RefreshAll 中）。
	/// </summary>
	private void RefreshHandSelectionHighlights()
	{
		if (_combat?.PlayerHero?.Hand == null)
			return;

		foreach (var card in _combat.PlayerHero.Hand)
		{
			var cardUI = _handUI.GetCardUIFor(card);
			if (cardUI != null)
			{
				bool isSelected = _selectedHandCards.Contains(card);
				cardUI.SetHandSelectionHighlight(isSelected);
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
	/// 阈值随拖拽起始位置自适应：起始越高阈值越紧，起始越低阈值越宽。
	/// 键盘启动时额外放宽 100px（更难误触）。
	/// </summary>
	private bool IsInPlayZone(Vector2 screenPos)
	{
		float viewportH = GetViewport().GetVisibleRect().Size.Y;
		float baseThreshold = viewportH * PlayZoneBaseRatio;

		// STS2 自适应公式
		float threshold;
		if (_playZoneDragStartY > baseThreshold)
			threshold = Mathf.Max(baseThreshold, _playZoneDragStartY - 100f);  // 底部拖拽放宽
		else
			threshold = Mathf.Min(baseThreshold, _playZoneDragStartY - 50f);   // 顶部拖拽收紧

		return screenPos.Y < threshold;
	}

	/// <summary>
	/// 判断当前输入位置是否触发取消（STS2 CancelZone 风格）。
	/// 必须曾离开过取消区域（上滑进入播放区）再回到底部 95% 才触发。
	/// </summary>
	private bool IsInCancelZone(Vector2 screenPos)
	{
		float viewportH = GetViewport().GetVisibleRect().Size.Y;
		float cancelThreshold = viewportH * CancelZoneScreenProportion;

		// 离开取消区一次后永久标记
		if (screenPos.Y <= cancelThreshold)
			_hasLeftCancelZone = true;

		return _hasLeftCancelZone && screenPos.Y > cancelThreshold;
	}


	/// <summary>
	/// 播放区域面板点击事件——click-select 模式下点击播放区域即打出卡牌。
	/// </summary>
	private void OnPlayZoneGuiInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
		{
			if (_selectionMode == SelectionMode.PlayingNoTargetCard && _selectedCard != null)
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

		bool success;
		switch (_selectedCard.Type)
		{
			case CardType.Spell:
				success = PlaySelectedSpellWithVfxOrigin(_combat.PlayerHero);
				break;
			case CardType.Status:
				// 状态牌：自动以玩家英雄为目标
				success = PlaySelectedSpellWithVfxOrigin(_combat.PlayerHero);
				break;
			case CardType.Domain:
				success = _combat.PlayDomain(_selectedCard);
				break;
			default:
				GD.PrintErr($"[CombatUI] 不支持的类型：{_selectedCard.Type}");
				OnCardDragCancelled();
				return;
		}

		if (success)
		{
			GD.Print($"[CombatUI] ✓ 无目标卡牌 {_selectedCard.CardName} 已打出");
			AnimateCardToDiscardPile();
			RefreshAll();
		}
		else
		{
			GD.Print($"[CombatUI] ✗ 打出失败，取消");
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

		// STS2 CancelZone：拖回底部 95% 且曾离开取消区 → 自动取消
		if (IsInCancelZone(screenPos))
		{
			GD.Print("[CombatUI] 拖入取消区域 → 自动取消");
			OnCardDragCancelled();
		}
	}
}
