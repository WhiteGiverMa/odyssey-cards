using System;
using System.Linq;
using System.Text;
using Godot;
using OdysseyCards.Core;
using OdysseyCards.Roguelike;

namespace OdysseyCards.Infrastructure.Commands;

/// <summary>
/// /theme_preview — 主题卡组生成预览。
/// 为绮梦/理惠/溯光各生成一套主题随机卡组，打印对比统计。
/// 用法：/theme_preview [seed]  （seed 可选，默认随机）
/// </summary>
public class ThemePreviewCommand : ChatScreenCommand
{
	public override string Name => "theme_preview";
	public override string Signature => "/theme_preview [seed]";
	public override string Description => "预览三角色主题卡组生成结果对比（绮梦/理惠/溯光）。";

	public override CommandResult Execute(string[] args)
	{
		int seed = args.Length > 0 && int.TryParse(args[0], out var s) ? s : System.Environment.TickCount;

		var profiles = new[]
		{
			("绮梦", "ayame", "res://Resources/Themes/ThemeProfile_Ayame.tres"),
			("理惠", "rie", "res://Resources/Themes/ThemeProfile_Rie.tres"),
			("溯光", "sokou", "res://Resources/Themes/ThemeProfile_Sokou.tres"),
		};

		var cardPool = GameManager.Instance.GetAllCards();
		if (cardPool.Count == 0)
			return CommandResult.Fail("卡池为空，无法生成");

		var sb = new StringBuilder();
		sb.AppendLine($"[ThemePreview] 种子={seed}  卡池={cardPool.Count}张");
		sb.AppendLine(new string('=', 60));

		foreach (var (displayName, heroId, path) in profiles)
		{
			var profile = GD.Load<ThemeProfile>(path);
			if (profile == null)
			{
				sb.AppendLine($"[{displayName}] 加载失败：{path}");
				continue;
			}

			var rng = new Random(seed);
			var result = ThemedDeckGenerator.Generate(profile, cardPool, rng);

			if (result.CardIds.Count == 0)
			{
				sb.AppendLine($"[{displayName}] 生成失败（空结果）");
				continue;
			}

			sb.AppendLine($"[{displayName}] {profile.ThemeName}  （{result.Stats.TotalCards}张）");
			sb.AppendLine($"  核心={result.Stats.CoreCardsIncluded}  随从={result.Stats.MinionCount}  法术={result.Stats.SpellCount}  领域={result.Stats.DomainCount}");
			sb.AppendLine($"  曲线: 低费={result.Stats.ManaCurve[0]} 中费={result.Stats.ManaCurve[1]} 高费={result.Stats.ManaCurve[2]}");

			if (result.Stats.TagCounts.Count > 0)
			{
				var tags = result.Stats.TagCounts
					.OrderByDescending(kv => kv.Value)
					.Select(kv => $"{kv.Key}={kv.Value}");
				sb.AppendLine($"  标签: {string.Join(", ", tags)}");
			}

			// 列出卡牌 ID（简短）
			sb.AppendLine($"  卡牌: {string.Join(", ", result.CardIds)}");
			sb.AppendLine(new string('-', 60));
		}

		var resultText = sb.ToString();
		GD.Print(resultText);
		return CommandResult.Ok(resultText);
	}
}
