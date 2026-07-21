using Godot;
using OdysseyCards.Character;

namespace OdysseyCards.Core;

/// <summary>
/// 牌组合法性验证服务 —— 牌组上限规则的唯一真相源。
///
/// 审计发现的问题：
///   - GameManager.VerifyAndRepairCollection() 静默截断超限牌组（用户无感知）
///   - MainMenu.OnStartPressed() 自己检查超限（UI 层做域逻辑）
///   - CollectionUI.AddCardToActiveCollectionDeck() 直接调 deck.AddCard()（绕过上限检查）
///   - CreateStartingDeck() 使用 ActiveDeck 时不检查超限
///
/// 修复后规则：
///   1. 构筑牌组：添加时不得超过 20 张，超限操作直接拒绝并返回失败原因
///   2. 旧档/导入超限牌组：标记为 invalid，不静默截断，由 UI 展示不可用状态
///   3. Start 入口：调用 ValidateForStart() 统一验证；0 张放行（roguelite 主题选牌流程），1-9 张阻断（半构筑状态），>20 张阻断
///   4. 战斗奖励加入运行中牌堆：允许突破 20 张，但不得写回构筑牌组
///   5. 保存：invalid 牌组阻断保存
///   6. 诊断（DiagnoseDeck）：只诊断 >20 张的真正异常（报 PushWarning）；
///      <10 张是合法的「未构筑/roguelite 开局」状态，不报后端级警告，由 UI 层处理。
/// </summary>
public static class DeckValidityService
{
	/// <summary>
	/// 牌组验证结果。
	/// </summary>
	public readonly struct DeckValidationResult
	{
		public bool IsValid { get; init; }
		public string? ErrorKey { get; init; }   // 本地化 key
		public string? DefaultMessage { get; init; } // 回退默认文本
		public int CurrentCount { get; init; }

		public static DeckValidationResult Valid(int count) => new()
		{
			IsValid = true,
			CurrentCount = count,
		};

		public static DeckValidationResult Invalid(string errorKey, string defaultMessage, int count) => new()
		{
			IsValid = false,
			ErrorKey = errorKey,
			DefaultMessage = defaultMessage,
			CurrentCount = count,
		};
	}

	/// <summary>
	/// 检查构筑时能否添加卡牌（上限 20）。
	/// </summary>
	public static bool CanAddCard(Deck deck)
	{
		return deck.CardCount < Deck.MaxDeckSize;
	}

	/// <summary>
	/// 构筑时安全添加卡牌——超过 20 直接拒绝。
	/// 返回 true 表示添加成功，false 表示已达上限。
	/// </summary>
	public static bool TryAddCard(Deck deck, CardData card)
	{
		if (!CanAddCard(deck))
		{
			GD.Print($"[DeckValidityService] 牌组已达构筑上限 {Deck.MaxDeckSize}，拒绝添加 {card.Id}");
			return false;
		}

		deck.AddCard(card);
		return true;
	}

	/// <summary>
	/// 验证牌组是否可用于开始冒险。
	/// 规则：0 张放行（roguelite 主题选牌流程由 MainMenu 处理）；
	/// 1-9 张阻断（半构筑状态）；>20 张阻断。
	/// </summary>
	public static DeckValidationResult ValidateForStart(Deck? deck)
	{
		if (deck == null)
		{
			// null 视同 0 张，放行让 MainMenu 走主题选牌流程
			return DeckValidationResult.Valid(0);
		}

		// 0 张：合法的 roguelite 开局状态
		if (deck.CardCount == 0)
			return DeckValidationResult.Valid(0);

		if (deck.CardCount < Deck.MinCards)
		{
			return DeckValidationResult.Invalid(
				"ui.deck.validation.too_few",
				$"牌组至少需要 {Deck.MinCards} 张卡牌，当前只有 {deck.CardCount} 张",
				deck.CardCount);
		}

		if (deck.CardCount > Deck.MaxDeckSize)
		{
			return DeckValidationResult.Invalid(
				"ui.deck.validation.too_many",
				$"构筑牌组不能超过 {Deck.MaxDeckSize} 张，当前有 {deck.CardCount} 张",
				deck.CardCount);
		}

		return DeckValidationResult.Valid(deck.CardCount);
	}

	/// <summary>
	/// 诊断牌组状态（用于 GameManager 启动时的数据修复）。
	/// 只诊断 >20 张的真正异常（报 PushWarning）；
	/// <10 张是合法的「未构筑/roguelite 开局」状态，不报后端级警告。
	/// </summary>
	public static DeckValidationResult DiagnoseDeck(Deck deck)
	{
		if (deck.CardCount > Deck.MaxDeckSize)
		{
			return DeckValidationResult.Invalid(
				"ui.deck.validation.too_many",
				$"牌组「{deck.Name}」超过 20 张上限（{deck.CardCount} 张），请在收藏中调整",
				deck.CardCount);
		}

		// <10 张不报异常：0 张是 roguelite 开局，1-9 张是构筑中，均为合法状态
		return DeckValidationResult.Valid(deck.CardCount);
	}

	/// <summary>
	/// 战斗中加入卡牌的硬上限检查（999 张，基本无限制）。
	/// </summary>
	public static bool CanAddCardInCombat(Deck deck)
	{
		return deck.CardCount < Deck.CombatMaxCards;
	}
}
