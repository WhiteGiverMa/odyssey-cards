namespace OdysseyCards.Tools.CardTagEditor.Tres;

/// <summary>
/// TresDocument → .tres 文本回写器。
/// 遍历所有 TresLine 节点，对未修改行原样输出，对已修改行输出新值。
/// </summary>
public static class TresWriter
{
	/// <summary>将 TresDocument 写入文件。</summary>
	public static void WriteToFile(TresDocument doc, string filePath)
	{
		using var writer = new StreamWriter(filePath, false);
		doc.WriteTo(writer);
	}

	/// <summary>将 TresDocument 转为完整文本字符串。</summary>
	public static string WriteToString(TresDocument doc)
	{
		return doc.ToString();
	}

	/// <summary>
	/// 验证 round-trip：解析原始文本 → 不修改直接回写 → 与原始文本逐行比较。
	/// 返回差异行索引列表（空 = 完全一致）。
	/// </summary>
	public static List<int> VerifyRoundTrip(string originalText)
	{
		var doc = TresParser.ParseText(originalText);
		var rewritten = WriteToString(doc);

		var origLines = originalText.Replace("\r\n", "\n").Split('\n');
		var newLines = rewritten.Replace("\r\n", "\n").Split('\n');

		var diffs = new List<int>();
		int maxLen = Math.Max(origLines.Length, newLines.Length);
		for (int i = 0; i < maxLen; i++)
		{
			var orig = i < origLines.Length ? origLines[i] : "(missing)";
			var next = i < newLines.Length ? newLines[i] : "(missing)";
			if (orig != next)
				diffs.Add(i);
		}
		return diffs;
	}
}
