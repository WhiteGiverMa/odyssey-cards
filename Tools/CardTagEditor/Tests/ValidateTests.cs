using OdysseyCards.Tools.CardTagEditor.Tres;
using OdysseyCards.Tools.CardTagEditor.Schema;
using OdysseyCards.Tools.CardTagEditor.Services;
using Xunit;

namespace OdysseyCards.Tools.CardTagEditor.Tests;

/// <summary>
/// 校验测试：未知 MechanicTag 位、越界 Keyword 等。
/// </summary>
public class ValidateTests
{
	[Fact]
	public void Validate_UnknownMechanicTag_ShouldReportError()
	{
		// 999999 含未知位（999999 - 65535 = 934464，远超已知位范围）
		Assert.False(CardMechanicTagValues.IsValidMask(999999));
	}

	[Fact]
	public void Validate_KnownMechanicTag_ShouldBeValid()
	{
		Assert.True(CardMechanicTagValues.IsValidMask(1));
		Assert.True(CardMechanicTagValues.IsValidMask(65536));
		Assert.True(CardMechanicTagValues.IsValidMask(1 | 4 | 65536)); // DirectDamage + Heal + Mechanics
	}

	[Fact]
	public void Validate_OutOfRangeKeyword_ShouldDetect()
	{
		Assert.False(KeywordValues.IsValid(99));
		Assert.False(KeywordValues.IsValid(-1));
		Assert.False(KeywordValues.IsValid(12)); // Qiqiao=11 是最后一个
	}

	[Fact]
	public void Validate_ValidKeyword_ShouldPass()
	{
		Assert.True(KeywordValues.IsValid(1));  // Charge
		Assert.True(KeywordValues.IsValid(4));  // Deathrattle
		Assert.True(KeywordValues.IsValid(11)); // Qiqiao
	}

	[Fact]
	public void Validate_ServiceLayer_CurrentResourcesShouldBeValid()
	{
		var repoRoot = FindRepoRoot();
		var service = new CardTagService(repoRoot);
		var report = service.Validate();

		// 当前数据应该没有错误（Tags 迁移还没有在 Resources/ 中执行，但 Tags 不是 MechanicTags）
		// Errors 应该为空（MechanicTags 都是已知位、Keywords 都在范围内）
		Assert.False(report.HasErrors,
			$"预期无错误，但发现: {string.Join("; ", report.Errors)}");
	}

	[Fact]
	public void CardMechanicTagValues_AllBits_MatchesGetAllValid()
	{
		int all = CardMechanicTagValues.AllBits.Aggregate(0, (a, b) => a | b);
		Assert.Equal(CardMechanicTagValues.AllValidBits, all);
		Assert.Equal(17, CardMechanicTagValues.AllBits.Length); // 17 个有效位
	}

	[Fact]
	public void KeywordValues_AllValues_CountIsCorrect()
	{
		Assert.Equal(11, KeywordValues.AllValues.Length);
		Assert.Equal(11, KeywordValues.ValueToName.Count);
	}

	[Fact]
	public void Validate_ParseBits_ReturnsCorrectNames()
	{
		var names = CardMechanicTagValues.ParseBits(1 | 4 | 65536);
		Assert.Contains("DirectDamage", names);
		Assert.Contains("Heal", names);
		Assert.Contains("Mechanics", names);
		Assert.Equal(3, names.Count);
	}

	[Fact]
	public void Validate_EncodeBits_RoundTrip()
	{
		var names = new[] { "DirectDamage", "Heal", "Mechanics" };
		int encoded = CardMechanicTagValues.EncodeBits(names);
		Assert.Equal(1 | 4 | 65536, encoded);

		var decoded = CardMechanicTagValues.ParseBits(encoded);
		Assert.Equal(names.OrderBy(x => x), decoded.OrderBy(x => x));
	}

	[Fact]
	public void Validate_LegacyTagMigration_MapsCorrectly()
	{
		Assert.Equal(65536, LegacyCardTagValues.MigrateBits(1));
		Assert.Equal(0, LegacyCardTagValues.MigrateBits(0));
	}

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
}
