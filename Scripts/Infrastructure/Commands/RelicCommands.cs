using OdysseyCards.Core;
using OdysseyCards.Relic;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OdysseyCards.Infrastructure.Commands;

// ===== /addrelic =====

public class AddRelicCommand : ChatScreenCommand
{
	private static readonly Dictionary<string, AbstractRelic> _relicDefs = [];

	public override string Name => "addrelic";
	public override string[] Aliases => ["ar"];
	public override string Signature => "/addrelic <relic_id>";
	public override string Description => "直接获得指定藏品。";

	public override CompletionCandidate[]? GetArgCandidates(string partialArg)
	{
		EnsureRelicCache();
		return _relicDefs.Keys
			.Where(id => id.StartsWith(partialArg, StringComparison.OrdinalIgnoreCase))
			.OrderBy(id => id)
			.Take(8)
			.Select(id =>
			{
				var r = _relicDefs[id];
				var tag = r.IsNegative ? "负面" : r.IsSubtle ? "微妙" : "正面";
				return new CompletionCandidate(id, id, $"{r.Name}（{tag}）");
			})
			.ToArray();
	}

	public override CommandResult Execute(string[] args)
	{
		if (args.Length < 1)
			return CommandResult.Fail($"用法: /addrelic <relic_id>  可用: {string.Join(", ", _relicDefs.Keys)}");

		var relicId = args[0].ToLowerInvariant();
		EnsureRelicCache();
		if (!_relicDefs.TryGetValue(relicId, out var relicDef))
			return CommandResult.Fail($"未知藏品: {relicId}，可用: {string.Join(", ", _relicDefs.Keys)}");

		AbstractRelic newRelic = relicDef switch
		{
			GoodDreamPillowRelic => new GoodDreamPillowRelic(),
			SmallFanRelic => new SmallFanRelic(),
			IceBagRelic => new IceBagRelic(),
			TacticalNukeRelic => new TacticalNukeRelic(),
			InternBadgeRelic => new InternBadgeRelic(),
			_ => relicDef
		};

		GameManager.Instance!.Relics.AddRelic(newRelic);
		return CommandResult.Ok($"已获得藏品「{newRelic.Name}」");
	}

	private static void EnsureRelicCache()
	{
		if (_relicDefs.Count > 0)
			return;
		var relics = new AbstractRelic[]
		{
			new GoodDreamPillowRelic(), new SmallFanRelic(), new IceBagRelic(),
			new TacticalNukeRelic(), new InternBadgeRelic(),
		};
		foreach (var r in relics)
			_relicDefs[r.Id] = r;
	}
}
