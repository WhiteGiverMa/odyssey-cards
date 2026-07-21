using System;
using System.Collections.Generic;
using System.Linq;
using OdysseyCards.Core;

namespace OdysseyCards.Infrastructure.Commands;

// ===== /help /clear /unlock_all /tags =====

public class HelpCommand : ChatScreenCommand
{
	public override string Name => "help";
	public override string[] Aliases => ["?"];
	public override string Signature => "/help";
	public override string Description => "显示帮助。";

	/// <summary>由 ChatScreen.cs 设置，用于生成帮助时读取所有已注册命令。</summary>
	public static IReadOnlyList<ChatScreenCommand>? AllCommands { get; set; }

	public override CommandResult Execute(string[] args)
	{
		if (AllCommands == null || AllCommands.Count == 0)
			return CommandResult.Ok("暂无可用的开发者命令");

		var unique = new Dictionary<string, ChatScreenCommand>(StringComparer.OrdinalIgnoreCase);
		foreach (var cmd in AllCommands)
			if (!unique.ContainsKey(cmd.Name))
				unique[cmd.Name] = cmd;

		var lines = new List<string> { "=== 开发者命令 ===" };
		foreach (var cmd in unique.Values.OrderBy(c => c.Name))
		{
			var aliasStr = cmd.Aliases.Length > 0 ? $"（别名: {string.Join(", ", cmd.Aliases)}）" : "";
			var sig = cmd.Signature;
			// 对齐：命令名 + 签名占 32 字符宽度
			var padded = $"{sig}".PadRight(32);
			lines.Add($"  {padded} — {cmd.Description}{aliasStr}");
		}
		return CommandResult.Ok(string.Join("\n", lines));
	}
}

public class ClearCommand : ChatScreenCommand
{
	public override string Name => "clear";
	public override string[] Aliases => ["cls"];
	public override string Signature => "/clear";
	public override string Description => "清空控制台输出。";
	public override CommandResult Execute(string[] args) => CommandResult.Ok("__CLEAR__");
}

public class UnlockAllCommand : ChatScreenCommand
{
	public override string Name => "unlock_all";
	public override string Signature => "/unlock_all";
	public override string Description => "解锁全部卡牌（加入收藏）。";
	public override CommandResult Execute(string[] args)
	{
		GameManager.Instance?.UnlockAllCards();
		GameManager.Instance?.SaveToDisk();
		return CommandResult.Ok("已解锁全部卡牌并保存到磁盘");
	}
}

public class TagsCommand : ChatScreenCommand
{
	public override string Name => "tags";
	public override string Signature => "/tags";
	public override string Description => "显示所有卡牌标签分布（QA）。";

	public override CommandResult Execute(string[] args)
	{
		var allCards = GameManager.Instance?.GetAllCards() ?? [];
		var cardCache = new Dictionary<string, CardData>();
		foreach (var cd in allCards)
			if (cd != null && !string.IsNullOrEmpty(cd.Id))
				cardCache[cd.Id] = cd;

		var lines = new List<string> { "=== 标签分布 ===" };
		var allTags = Enum.GetValues<Core.CardMechanicTag>();
		foreach (var tag in allTags)
		{
			if (tag == Core.CardMechanicTag.None)
				continue;
			var cards = cardCache.Values.Where(c => c.MechanicTags.HasFlag(tag)).OrderBy(c => c.CardName).ToList();
			lines.Add($"  {tag} ({cards.Count} 张):");
			foreach (var c in cards)
				lines.Add($"    {c.Id} — {c.CardName}（{c.Cost}费 {c.Type}）");
		}
		var untagged = cardCache.Values.Where(c => c.MechanicTags == Core.CardMechanicTag.None).ToList();
		lines.Add($"  无标签 ({untagged.Count} 张)");
		return CommandResult.Ok(string.Join("\n", lines));
	}
}
