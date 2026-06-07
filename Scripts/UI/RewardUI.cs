using Godot;
using OdysseyCards.Core;
using System;
using System.Collections.Generic;
using Loc = OdysseyCards.Localization.Localization;

namespace OdysseyCards.UI;

/// <summary>
/// 战后卡牌奖励选择覆盖层 — 继承 CardSelectionScreen 基类。
/// 显示 3 个奖励卡牌包（每包 = 1 张卡 × N 张同名，含稀有度标签）。
/// 奖励通过 GameManager 发放：解锁卡牌 + 加入当前牌堆 + 保存。
/// </summary>
public partial class RewardUI : CardSelectionScreen
{
	// ===== 奖励包状态 =====

	private sealed class BundleState
	{
		public required CardData CardData;
		public int CopyCount;
		public CardUI CardUI = null!;
		public Label CountLabel = null!;
	}

	private readonly List<BundleState> _bundles = new();
	private readonly List<Label> _countLabels = new();

	/// <summary>奖励选取完成时触发（选取后自毁）。</summary>
	public event Action? OnRewardCompleted;

	// ===== 基类覆写 =====

	protected override string TitleText => Loc.T("ui.reward.title", "选择奖励卡牌");
	protected override string SkipButtonText => Loc.T("ui.reward.skip", "跳过");
	protected override string? ConfirmButtonText => null;
	protected override int DialogWidth => 600;
	protected override int OverlayZIndex => 300;
	protected override bool ShowSkipButton => true;

	protected override bool IsItemSelected(int index) => false;

	protected override void RefreshLocalizedTexts()
	{
		base.RefreshLocalizedTexts();
		// 刷新数量标签中的稀有度名称
		foreach (var bundle in _bundles)
		{
			if (bundle.CountLabel == null || !GodotObject.IsInstanceValid(bundle.CountLabel)) continue;
			string rarityName = GetLocalizedRarityName(bundle.CardData.Rarity);
			bundle.CountLabel.Text = BuildCopyLabel(bundle.CopyCount, rarityName);
		}
	}

	// ===== 公开 API =====

	/// <summary>
	/// 生成 3 组奖励并显示 UI。
	/// 如果没有符合条件的卡牌则直接触发 OnRewardCompleted 并跳过。
	/// </summary>
	public void ShowRewards()
	{
		// 获取可用奖励池
		var eligible = GameManager.Instance.GetRewardEligibleCards();
		if (eligible.Count == 0)
		{
			GD.Print("[RewardUI] 无可用奖励卡牌，跳过");
			OnRewardCompleted?.Invoke();
			QueueFree();
			return;
		}

		// Fisher-Yates 洗牌，取最多 3 张
		Shuffle(eligible);
		int count = Mathf.Min(eligible.Count, 3);

		for (int i = 0; i < count; i++)
		{
			var cardData = eligible[i];
			_bundles.Add(new BundleState
			{
				CardData = cardData,
				CopyCount = cardData.Rarity.GetMaxRewardCopies(),
			});
		}

		GD.Print($"[RewardUI] 生成 {_bundles.Count} 组奖励");

		_isShowing = true;
		_openedTicks = Time.GetTicksMsec();

		BuildOverlay();

		// 入场动画：卡牌 + 数量标签一起淡入
		PlayEntryAnimation(extraTargets: _countLabels);
	}

	// ===== 卡牌项构建 =====

	protected override void BuildCardItems()
	{
		float s = UIScaler.Instance?.GetScaleFactor() ?? 1f;
		_countLabels.Clear();

		foreach (var bundle in _bundles)
		{
			// 每包一个垂直容器：卡牌 + 数量/稀有度标签
			var bundleBox = new VBoxContainer
			{
				Alignment = BoxContainer.AlignmentMode.Center,
				MouseFilter = MouseFilterEnum.Ignore,
			};

			// 卡牌
			var card = new Card.Card(bundle.CardData);
			var cardUI = new CardUI
			{
				Name = $"RewardCard_{bundle.CardData.Id}",
				CustomMinimumSize = new Vector2(130 * s, 195 * s),
				Modulate = new Color(1, 1, 1, 0), // 入场前透明
			};
			cardUI.SetCard(card);

			bundle.CardUI = cardUI;
			bundleBox.AddChild(cardUI);

			// 数量 + 稀有度标签
			string rarityName = GetLocalizedRarityName(bundle.CardData.Rarity);
			string labelText = BuildCopyLabel(bundle.CopyCount, rarityName);

			var countLabel = new Label
			{
				Text = labelText,
				HorizontalAlignment = HorizontalAlignment.Center,
				MouseFilter = MouseFilterEnum.Ignore,
			};
			countLabel.AddThemeColorOverride("font_color", new Color(1, 1, 1, 0.7f));
			countLabel.AddThemeFontSizeOverride("font_size", Mathf.RoundToInt(14 * s));

			bundle.CountLabel = countLabel;
			bundleBox.AddChild(countLabel);

			_cardsContainer.AddChild(bundleBox);

			// 注册卡牌 UI 到基类（用于点击事件和键盘焦点）
			RegisterItem(cardUI);
			_countLabels.Add(countLabel);
		}
	}

	// ===== 选择处理 =====

	protected override void OnItemSelected(int index)
	{
		if (index < 0 || index >= _bundles.Count) return;

		var bundle = _bundles[index];
		GD.Print($"[RewardUI] 玩家选择了：{bundle.CardData.GetLocalizedName()} ×{bundle.CopyCount}");

		// 视觉反馈：高亮选中的卡牌
		bundle.CardUI.SelfModulate = SelectedColor;

		_isShowing = false;
		ApplyReward(bundle);
	}

	protected override void OnSkip()
	{
		GD.Print("[RewardUI] 玩家跳过了奖励");
		OnRewardCompleted?.Invoke();
		QueueFree();
	}

	// ===== 奖励发放 =====

	private void ApplyReward(BundleState bundle)
	{
		GameManager.Instance.UnlockCard(bundle.CardData.Id);

		for (int i = 0; i < bundle.CopyCount; i++)
			GameManager.Instance.AddCardToDeckInCombat(bundle.CardData);

		GameManager.Instance.SaveToDisk();

		GD.Print($"[RewardUI] 奖励已应用：{bundle.CardData.GetLocalizedName()} ×{bundle.CopyCount}");

		OnRewardCompleted?.Invoke();
		QueueFree();
	}

	// ===== 辅助 =====

	private static void Shuffle<T>(List<T> list)
	{
		var rng = new Random();
		for (int i = list.Count - 1; i > 0; i--)
		{
			int j = rng.Next(i + 1);
			(list[i], list[j]) = (list[j], list[i]);
		}
	}

	private static string GetLocalizedRarityName(CardRarity rarity)
	{
		return rarity switch
		{
			CardRarity.Common => Loc.T("ui.reward.rarity_common", "普通"),
			CardRarity.Good => Loc.T("ui.reward.rarity_good", "良好"),
			CardRarity.Excellent => Loc.T("ui.reward.rarity_excellent", "极佳"),
			CardRarity.Master => Loc.T("ui.reward.rarity_master", "大师级"),
			_ => Loc.T("ui.reward.rarity_unknown", "未知"),
		};
	}

	private static string BuildCopyLabel(int copyCount, string rarityName)
	{
		return Loc.T("ui.reward.copy_format", "×{count} — {rarity}")
			.Replace("{count}", copyCount.ToString())
			.Replace("{rarity}", rarityName);
	}
}
