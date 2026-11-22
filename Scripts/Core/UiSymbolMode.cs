namespace OdysseyCards.Core;

/// <summary>
/// UI 符号对照模式。纯 C# 真源由 <see cref="UI.UIScaler"/> 同步，
/// 让运行时卡牌、效果元数据与无 Godot 单测共享同一套双模输出规则。
/// </summary>
public static class UiSymbolMode
{
	/// <summary>true=使用 Emoji 对照图标；false=使用纯色色块与简字/ASCII 文本。</summary>
	public static bool Enabled { get; set; }

	/// <summary>按当前模式选择纯文本或 Emoji 文本。</summary>
	public static string Select(string plainText, string emojiText) => Enabled ? emojiText : plainText;

	/// <summary>仅在对照模式下为干净文本添加 Emoji 前缀。</summary>
	public static string Prefix(string plainText, string emojiPrefix) => Enabled ? $"{emojiPrefix}{plainText}" : plainText;
}
