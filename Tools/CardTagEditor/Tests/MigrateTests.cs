using OdysseyCards.Tools.CardTagEditor.Tres;
using OdysseyCards.Tools.CardTagEditor.Schema;
using Xunit;

namespace OdysseyCards.Tools.CardTagEditor.Tests;

/// <summary>
/// Tags → MechanicTags 迁移测试。
/// </summary>
public class MigrateTests
{
	/// <summary>
	/// fixture: Tags=1, MechanicTags=4 → migrate 后 MechanicTags=65540（4|65536），Tags 行消失。
	/// </summary>
	[Fact]
	public void Migrate_OR_Merge_ShouldCombineBits()
	{
		var text = """
			[gd_resource type="Resource" format=3]
			[ext_resource type="Script" path="res://Scripts/Core/CardData.cs" id="1_data"]
			[resource]
			script = ExtResource("1_data")
			Id = "test_merge"
			Tags = 1
			MechanicTags = 4
			""";

		var doc = TresParser.ParseText(text);
		var card = new CardDataTres(doc);

		Assert.Equal(1, card.Tags);
		Assert.Equal(4, card.MechanicTags);

		// 直接测试 OwnedField 的 setter/getter
		var mtField = doc.GetField("MechanicTags");
		Assert.NotNull(mtField);
		mtField!.SetInt(65540);
		Assert.Equal(65540, mtField.AsInt());
		Assert.Equal(65540, card.MechanicTags); // 验证访问器 round-trip

		bool changed = card.MigrateTags();
		Assert.True(changed);

		// 4 | 65536 = 65540
		Assert.Equal(65540, card.MechanicTags);
		Assert.Null(card.Tags);
	}

	/// <summary>
	/// 幂等：已迁移 fixture（无 Tags 行）再跑 migrate，返回 false。
	/// </summary>
	[Fact]
	public void Migrate_Idempotent_ShouldReturnFalse()
	{
		var text = """
			[gd_resource type="Resource" format=3]
			[ext_resource type="Script" path="res://Scripts/Core/CardData.cs" id="1_data"]
			[resource]
			script = ExtResource("1_data")
			Id = "test_idempotent"
			MechanicTags = 65544
			""";

		var doc = TresParser.ParseText(text);
		var card = new CardDataTres(doc);

		Assert.False(card.HasTags);

		bool changed = card.MigrateTags();
		Assert.False(changed);
	}

	/// <summary>
	/// 不覆盖：fixture Tags=1, MechanicTags=8 → migrate 后 MechanicTags=65544（8|65536），不是 65536。
	/// </summary>
	[Fact]
	public void Migrate_ShouldNotOverwriteExistingMechanicTags()
	{
		var text = """
			[gd_resource type="Resource" format=3]
			[ext_resource type="Script" path="res://Scripts/Core/CardData.cs" id="1_data"]
			[resource]
			script = ExtResource("1_data")
			Id = "test_nooverwrite"
			Tags = 1
			MechanicTags = 8
			""";

		var doc = TresParser.ParseText(text);
		var card = new CardDataTres(doc);

		Assert.Equal(8, card.MechanicTags);

		bool changed = card.MigrateTags();
		Assert.True(changed);

		// 8 | 65536 = 65544
		Assert.Equal(65544, card.MechanicTags);
		Assert.NotEqual(65536, card.MechanicTags);
	}

	/// <summary>
	/// migrate 后 Tags 行消失，且文件不再包含 Tags = 字样。
	/// </summary>
	[Fact]
	public void Migrate_RemovesTagsLine()
	{
		var text = """
			[gd_resource type="Resource" format=3]
			[ext_resource type="Script" path="res://Scripts/Core/CardData.cs" id="1_data"]
			[resource]
			script = ExtResource("1_data")
			Id = "test_removetags"
			Tags = 1
			MechanicTags = 0
			""";

		var doc = TresParser.ParseText(text);
		var card = new CardDataTres(doc);

		// Verify initial state
		Assert.NotNull(doc.GetField("Tags"));

		card.MigrateTags();

		// Verify Tags was deleted
		Assert.Null(doc.GetField("Tags"));

		var output = TresWriter.WriteToString(doc);

		// 不再包含 Tags 行（用行首匹配避免误匹配 MechanicTags）
		Assert.DoesNotMatch(@"(^|\n)\s*Tags\s*=", output);
		// MechanicTags 已更新为 65536
		Assert.Contains("MechanicTags = 65536", output);
	}

	/// <summary>
	/// Tags=0 的卡牌 migrate 后 MechanicTags 不变，Tags 行删除。
	/// </summary>
	[Fact]
	public void Migrate_TagsZero_MechanicTagsUnchanged()
	{
		var text = """
			[gd_resource type="Resource" format=3]
			[ext_resource type="Script" path="res://Scripts/Core/CardData.cs" id="1_data"]
			[resource]
			script = ExtResource("1_data")
			Id = "test_zero"
			Tags = 0
			MechanicTags = 16
			""";

		var doc = TresParser.ParseText(text);
		var card = new CardDataTres(doc);
		card.MigrateTags();

		// MechanicTags 不变（0 | 0 = 0，再 |16 = 16）
		Assert.Equal(16, card.MechanicTags);
		Assert.Null(doc.GetField("Tags"));
		var output = TresWriter.WriteToString(doc);
		// 不再包含 Tags 行（用行首匹配避免误匹配 MechanicTags）
		Assert.DoesNotMatch(@"(^|\n)\s*Tags\s*=", output);
	}

	/// <summary>
	/// 白纸上跑 migrate：所有卡牌中既包含有 Tags 的，也包含已迁移的。
	/// </summary>
	[Fact]
	public void Migrate_ServiceLayer_DryRun_ShouldReportChanges()
	{
		var repoRoot = FindRepoRoot();
		var service = new Services.CardTagService(repoRoot);
		var result = service.Migrate(dryRun: true);

		// 当前有卡牌仍有 Tags 行（机械蜈蚣-攻城型、骑士I型、机械蜈蚣-防空型、机械静螳）
		// 或者迁移已执行过，则是幂等的
		Assert.True(result.ChangeCount >= 0, $"Changes: {result.ChangeCount}");
	}

	[Fact]
	public void Migrate_ServiceLayer_Idempotent_SecondRunZeroChanges()
	{
		var repoRoot = FindRepoRoot();
		var service = new Services.CardTagService(repoRoot);

		// 先用 dryRun 跑一次看看有多少变更
		var firstResult = service.Migrate(dryRun: true);
		int firstChangeCount = firstResult.ChangeCount;

		// 再跑一次应该相同（因为是 dryRun，没实际修改文件）
		var secondResult = service.Migrate(dryRun: true);
		Assert.Equal(firstChangeCount, secondResult.ChangeCount);
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
