#nullable enable

using System;
using System.Linq;
using OdysseyCards.Combat;
using OdysseyCards.Core;

namespace OdysseyCards.Infrastructure.Commands;

/// <summary>
/// /emote 命令——强制敌人发送一段表情文本。
/// 用法：/emote &lt;text&gt;    从第一个敌人发送
///       /emote &lt;index&gt; &lt;text&gt;  从指定索引敌人发送
/// </summary>
public class EmoteCommand : ChatScreenCommand
{
	public override string Name => "emote";
	public override string[] Aliases => ["emo"];
	public override string Signature => "/emote [enemyIndex] <text>";
	public override string Description => "强制敌人发送表情文本。不指定索引时从第一个敌人发送。";

	public override CommandResult Execute(string[] args)
	{
		var cm = CombatManager.Instance;
		if (cm == null)
			return CommandResult.Fail("未在战斗中");

		if (args.Length == 0)
			return CommandResult.Fail("用法：/emote [enemyIndex] <text>");

		string text;
		int targetEnemyIndex = 0;
		if (args.Length >= 2 && int.TryParse(args[0], out int index))
		{
			// 指定了敌人索引
			if (index < 0 || index >= cm.EnemyUnits.Count)
				return CommandResult.Fail($"敌人索引 {index} 无效（共 {cm.EnemyUnits.Count} 个敌人）");
			targetEnemyIndex = index;
			text = string.Join(" ", args, 1, args.Length - 1);
		}
		else
		{
			// 第一个敌人发送
			text = string.Join(" ", args);
		}

		if (string.IsNullOrWhiteSpace(text))
			return CommandResult.Fail("表情文本不能为空");

		cm.SendEnemyEmote(text, targetEnemyIndex);
		return CommandResult.Ok($"已发送表情：「{text}」");
	}

	public override CompletionCandidate[]? GetArgCandidates(string partialArg)
	{
		var gm = GameManager.Instance;
		if (gm == null)
			return null;

		return gm.GetActiveEmoteEntries()
			.Select(entry => entry.Text.Trim())
			.Where(text => !string.IsNullOrWhiteSpace(text))
			.Where(text => text.Contains(partialArg, StringComparison.OrdinalIgnoreCase))
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.Select(text => new CompletionCandidate(text, text, "预设表情"))
			.ToArray();
	}
}
