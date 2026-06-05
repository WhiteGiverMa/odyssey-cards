namespace OdysseyCards.Infrastructure;

/// <summary>
/// 补全候选：InsertText 为补全后的完整输入文本，
/// PrimaryText 为候选在列表中显示的标题，SecondaryText 为辅助描述。
/// </summary>
public record CompletionCandidate(string InsertText, string PrimaryText, string SecondaryText);
