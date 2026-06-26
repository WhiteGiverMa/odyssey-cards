using OdysseyCards.Tools.CardTagEditor.Tres;
using Xunit;

namespace OdysseyCards.Tools.CardTagEditor.Tests;

/// <summary>
/// Round-trip 测试：解析 .tres → 回写 → 与原文件逐行比对。
/// </summary>
public class TresParserRoundTripTests
{
	private static string RepoRoot => FindRepoRoot();

	private static string FindRepoRoot()
	{
		var dir = AppContext.BaseDirectory;
		while (dir != null)
		{
			if (File.Exists(Path.Combine(dir, "project.godot")))
				return dir;
			var parent = Path.GetDirectoryName(dir);
			if (parent == dir) break;
			dir = parent!;
		}
		throw new DirectoryNotFoundException("未找到 project.godot");
	}

	/// <summary>
	/// 对所有 41 个卡牌 .tres 做 round-trip，未拥有行必须字节相同。
	/// </summary>
	[Theory]
	[MemberData(nameof(AllCardTresFiles))]
	public void CardData_RoundTrip_ShouldBeIdentical(string filePath)
	{
		var originalText = File.ReadAllText(filePath);
		var diffs = TresWriter.VerifyRoundTrip(originalText);
		Assert.Empty(diffs);
	}

	/// <summary>
	/// 对所有 3 个 ThemeProfile .tres 做 round-trip。
	/// </summary>
	[Theory]
	[MemberData(nameof(AllThemeTresFiles))]
	public void ThemeProfile_RoundTrip_ShouldBeIdentical(string filePath)
	{
		var originalText = File.ReadAllText(filePath);
		var diffs = TresWriter.VerifyRoundTrip(originalText);
		Assert.Empty(diffs);
	}

	public static IEnumerable<object[]> AllCardTresFiles()
	{
		var dir = Path.Combine(FindRepoRoot(), "Resources", "Cards");
		foreach (var file in Directory.GetFiles(dir, "*.tres"))
			yield return new object[] { file };
	}

	public static IEnumerable<object[]> AllThemeTresFiles()
	{
		var dir = Path.Combine(FindRepoRoot(), "Resources", "Themes");
		foreach (var file in Directory.GetFiles(dir, "*.tres"))
			yield return new object[] { file };
	}
}
