using Godot;
using OdysseyCards.Combat;
using OdysseyCards.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using static OdysseyCards.Infrastructure.Commands.CombatUIHelper;
using Card = OdysseyCards.Card.Card;
using Minion = OdysseyCards.Card.Minion;
using CardType = OdysseyCards.Core.CardType;

namespace OdysseyCards.Infrastructure.Commands;

// ===== /token /play /summon_player =====

public class TokenCommand : DevConsoleCommand
{
    private static Dictionary<string, CardData>? _cardCache;

    public override string Name => "token";
    public override string[] Aliases => ["t"];
    public override string Signature => "/token <card_id> [count]";
    public override string Description => "将指定ID的卡牌加入手牌（可批量）。";

    public override CompletionCandidate[]? GetArgCandidates(string partialArg)
    {
        EnsureCardCache();
        return _cardCache!.Keys
            .Where(id => id.StartsWith(partialArg, StringComparison.OrdinalIgnoreCase))
            .OrderBy(id => id)
            .Take(8)
            .Select(id =>
            {
                var c = _cardCache[id];
                return new CompletionCandidate(id, id, $"{c.CardName}（{c.Cost}费）");
            })
            .ToArray();
    }

    public override CommandResult Execute(string[] args)
    {
        var cm = CombatManager.Instance;
        if (cm == null) return CommandResult.Fail("未在战斗中");
        if (args.Length < 1) return CommandResult.Fail("用法: /token <card_id> [count]");

        var tokenId = args[0];
        int count = args.Length >= 2 && int.TryParse(args[1], out var cnt) && cnt > 0 ? Math.Min(cnt, 99) : 1;

        EnsureCardCache();
        if (!_cardCache!.TryGetValue(tokenId, out var cardData))
        {
            var match = _cardCache.Keys.FirstOrDefault(k => string.Equals(k, tokenId, StringComparison.OrdinalIgnoreCase));
            if (match != null) _cardCache.TryGetValue(match, out cardData);
        }
        if (cardData == null) return CommandResult.Fail($"未找到卡牌: {tokenId}");

        for (int i = 0; i < count; i++)
        {
            var tokenCard = new OdysseyCards.Card.Card(cardData);
            cm.AddCardToHand(tokenCard);
        }

        return CommandResult.Ok(count > 1
            ? $"将 {count} 张「{cardData.CardName}」加入手牌（手牌 {cm.PlayerHero.Hand.Count} 张）"
            : $"将「{cardData.CardName}」加入手牌（手牌 {cm.PlayerHero.Hand.Count} 张）");
    }

    private static void EnsureCardCache()
    {
        if (_cardCache != null) return;
        _cardCache = [];
        var all = GameManager.Instance?.GetAllCards() ?? [];
        foreach (var cd in all)
            if (cd != null && !string.IsNullOrEmpty(cd.Id))
                _cardCache[cd.Id] = cd;
    }
}

public class PlayCommand : DevConsoleCommand
{
    public override string Name => "play";
    public override string[] Aliases => ["p"];
    public override string Signature => "/play <card_id>";
    public override string Description => "从手牌打出领域/无目标法术。";

    public override CompletionCandidate[]? GetArgCandidates(string partialArg)
    {
        // 复用 TokenCommand 的缓存
        var all = GameManager.Instance?.GetAllCards() ?? [];
        return all
            .Where(c => c != null && !string.IsNullOrEmpty(c.Id) && c.Id.StartsWith(partialArg, StringComparison.OrdinalIgnoreCase))
            .OrderBy(c => c.Id)
            .Take(8)
            .Select(c => new CompletionCandidate(c.Id, c.Id, $"{c.CardName}（{c.Cost}费）"))
            .ToArray();
    }

    public override CommandResult Execute(string[] args)
    {
        var cm = CombatManager.Instance;
        if (cm == null) return CommandResult.Fail("未在战斗中");
        if (args.Length < 1) return CommandResult.Fail("用法: /play <card_id>");

        var playId = args[0];
        var cardToPlay = cm.PlayerHero.Hand.FirstOrDefault(c =>
            string.Equals(c.Id, playId, StringComparison.OrdinalIgnoreCase));
        if (cardToPlay == null) return CommandResult.Fail($"手牌中没有卡牌: {playId}");

        bool played = cardToPlay.Type switch
        {
            CardType.Domain => cm.PlayDomain(cardToPlay),
            CardType.Spell when !cardToPlay.Data.RequiresTarget => cm.PlaySpell(cardToPlay, cm.PlayerHero),
            _ => false
        };

        return played
            ? CommandResult.Ok($"打出「{cardToPlay.GetLocalizedName()}」")
            : CommandResult.Fail($"无法通过 /play 打出「{cardToPlay.GetLocalizedName()}」");
    }
}

public class SummonPlayerCommand : DevConsoleCommand
{
    public override string Name => "summon_player";
    public override string[] Aliases => ["sp"];
    public override string Signature => "/summon_player <card_id> <slot>";
    public override string Description => "在己方槽位直接召唤随从（QA）。";

    public override CompletionCandidate[]? GetArgCandidates(string partialArg)
    {
        var all = GameManager.Instance?.GetAllCards() ?? [];
        return all
            .Where(c => c != null && !string.IsNullOrEmpty(c.Id) && c.Id.StartsWith(partialArg, StringComparison.OrdinalIgnoreCase) && c.IsMinion)
            .OrderBy(c => c.Id)
            .Take(8)
            .Select(c => new CompletionCandidate(c.Id, c.Id, $"{c.CardName}（{c.Cost}费）"))
            .ToArray();
    }

    public override CommandResult Execute(string[] args)
    {
        var cm = CombatManager.Instance;
        if (cm == null) return CommandResult.Fail("未在战斗中");
        if (args.Length < 2) return CommandResult.Fail("用法: /summon_player <card_id> <slot0-4>");

        var all = GameManager.Instance?.GetAllCards() ?? [];
        var cardData = all.FirstOrDefault(c => string.Equals(c.Id, args[0], StringComparison.OrdinalIgnoreCase));
        if (cardData == null) return CommandResult.Fail($"未找到卡牌: {args[0]}");
        if (!cardData.IsMinion) return CommandResult.Fail($"「{cardData.CardName}」不是随从牌");

        if (!int.TryParse(args[1], out var slot) || slot < 0 || slot >= Board.MaxSlotsPerSide)
            return CommandResult.Fail("槽位需为 0-4");

        var minion = new Minion(cardData, isPlayerSide: true);
        cm.Board.PlaceMinion(minion, slot);
        RefreshCombatUI(cm);
        return CommandResult.Ok($"已在己方槽位 {slot} 召唤「{minion.GetLocalizedName()}」");
    }
}
