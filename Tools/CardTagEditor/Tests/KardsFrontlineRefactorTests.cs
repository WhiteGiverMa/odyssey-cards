using OdysseyCards.Tools.CardTagEditor.Schema;
using OdysseyCards.Tools.CardTagEditor.Services;
using OdysseyCards.Tools.CardTagEditor.Tres;
using Xunit;

namespace OdysseyCards.Tools.CardTagEditor.Tests;

/// <summary>
/// KARDS 三线重构：四兵种、守护、情报与旧 Speed 兼容性测试。
/// </summary>
public class KardsFrontlineRefactorTests
{
	[Fact]
	public void UnitClass_ValuesAndChineseNames_MatchPrd()
	{
		Assert.Equal(0, UnitClassValues.Vanguard);
		Assert.Equal(1, UnitClassValues.Assault);
		Assert.Equal(2, UnitClassValues.Support);
		Assert.Equal(3, UnitClassValues.Skirmisher);
		Assert.Equal(new[] { 0, 1, 2, 3 }, UnitClassValues.AllValues);
		Assert.Equal("先锋", UnitClassValues.ValueToDisplayName[UnitClassValues.Vanguard]);
		Assert.Equal("突击", UnitClassValues.ValueToDisplayName[UnitClassValues.Assault]);
		Assert.Equal("支援", UnitClassValues.ValueToDisplayName[UnitClassValues.Support]);
		Assert.Equal("游击", UnitClassValues.ValueToDisplayName[UnitClassValues.Skirmisher]);
		Assert.Equal("Skirmisher", UnitClassValues.ValueToName[UnitClassValues.Skirmisher]);
		Assert.Equal("Unknown(99)", UnitClassValues.GetName(99));
		Assert.Equal("Unknown(99)", UnitClassValues.GetDisplayName(99));
	}

	[Fact]
	public void UnitClass_MissingOnMinion_DefaultsVanguard_AndRoundTripUnchanged()
	{
		const string text = """
			[gd_resource type="Resource" format=3]
			[resource]
			Id = "test_default_vanguard"
			Type = 0
			Attack = 2
			Health = 3
			Speed = 3
			Keywords = Array[int]([2])
			""";

		var doc = TresParser.ParseText(text);
		var card = new CardDataTres(doc);

		Assert.Equal(UnitClassValues.Vanguard, card.UnitClass);
		Assert.Equal(UnitClassValues.Vanguard, card.MinionUnitClass);
		Assert.False(card.HasExplicitUnitClass);
		Assert.Equal(3, card.LegacySpeed);
		Assert.Empty(TresWriter.VerifyRoundTrip(text));
		Assert.Equal(NormalizeNewlines(text), TresWriter.WriteToString(doc));
	}

	[Fact]
	public void UnitClass_SetNonDefault_InsertsAfterType_AndPreservesOtherLines()
	{
		const string text = """
			[gd_resource type="Resource" format=3]
			[resource]
			Id = "test_insert_unit_class"
			Type = 0
			Attack = 4
			Health = 5
			Speed = 3
			Keywords = [2, 8]
			CustomProperty = "keep"
			""";

		var doc = TresParser.ParseText(text);
		var card = new CardDataTres(doc);
		card.UnitClass = UnitClassValues.Assault;
		var output = TresWriter.WriteToString(doc);

		Assert.True(card.HasExplicitUnitClass);
		Assert.Equal(UnitClassValues.Assault, card.UnitClass);
		Assert.Contains("Type = 0\nUnitClass = 1\nAttack = 4", output);
		Assert.Contains("Health = 5\nSpeed = 3\nKeywords = [2, 8]\nCustomProperty = \"keep\"", output);
		Assert.Single(doc.Lines.OfType<OwnedField>(), f => f.Key == "UnitClass");
	}

	[Fact]
	public void UnitClass_SetExisting_UpdatesSingleLine_AndCanReturnToVanguard()
	{
		const string text = """
			[gd_resource type="Resource" format=3]
			[resource]
			Id = "test_update_unit_class"
			Type = 0
			UnitClass = 2
			Attack = 7
			CustomProperty = "unchanged"
			""";

		var doc = TresParser.ParseText(text);
		var card = new CardDataTres(doc);
		Assert.Equal(UnitClassValues.Support, card.UnitClass);

		card.UnitClass = UnitClassValues.Skirmisher;
		Assert.Contains("UnitClass = 3", TresWriter.WriteToString(doc));
		Assert.Single(doc.Lines.OfType<OwnedField>(), f => f.Key == "UnitClass");

		card.UnitClass = UnitClassValues.Vanguard;
		var output = TresWriter.WriteToString(doc);
		Assert.Contains("UnitClass = 0", output);
		Assert.DoesNotContain("UnitClass = 2", output);
		Assert.DoesNotContain("UnitClass = 3", output);
		Assert.Contains("Attack = 7\nCustomProperty = \"unchanged\"", output);
	}

	[Fact]
	public void UnitClass_SetVanguardWhenMissing_IsNoOpAndIdempotent()
	{
		const string text = """
			[gd_resource type="Resource" format=3]
			[resource]
			Id = "test_implicit_vanguard"
			Type = 0
			Attack = 1
			""";

		var doc = TresParser.ParseText(text);
		var card = new CardDataTres(doc);
		card.UnitClass = UnitClassValues.Vanguard;
		var firstOutput = TresWriter.WriteToString(doc);
		card.UnitClass = UnitClassValues.Vanguard;
		var secondOutput = TresWriter.WriteToString(doc);

		Assert.Equal(NormalizeNewlines(text), firstOutput);
		Assert.Equal(firstOutput, secondOutput);
		Assert.False(card.HasExplicitUnitClass);
	}

	[Fact]
	public void UnitClass_SetOnNonMinion_DoesNotWriteField()
	{
		const string text = """
			[gd_resource type="Resource" format=3]
			[resource]
			Id = "test_spell_unit_class"
			Type = 1
			Description = "保持原样"
			""";

		var doc = TresParser.ParseText(text);
		var card = new CardDataTres(doc);
		card.UnitClass = UnitClassValues.Support;

		Assert.Null(card.MinionUnitClass);
		Assert.False(card.HasExplicitUnitClass);
		Assert.Equal(NormalizeNewlines(text), TresWriter.WriteToString(doc));
	}

	[Fact]
	public void UnitClass_InvalidValue_Throws()
	{
		var doc = TresParser.ParseText("[resource]\nType = 0");
		var card = new CardDataTres(doc);
		Assert.Throws<ArgumentOutOfRangeException>(() => card.UnitClass = 4);
	}

	[Fact]
	public void LegacySpeed_CanBeRemovedWithoutTouchingNeighbors_AndRemovalIsIdempotent()
	{
		const string text = """
			[gd_resource type="Resource" format=3]
			[resource]
			Id = "test_legacy_speed"
			Type = 0
			Health = 4
			Speed = 3
			Keywords = Array[int]([2, 8])
			CustomProperty = "keep"
			""";

		var doc = TresParser.ParseText(text);
		var card = new CardDataTres(doc);
		Assert.True(card.HasLegacySpeed);
		Assert.Equal(3, card.LegacySpeed);

		card.DeleteLegacySpeed();
		var output = TresWriter.WriteToString(doc);
		Assert.False(card.HasLegacySpeed);
		Assert.Null(card.LegacySpeed);
		Assert.DoesNotMatch(@"(^|\n)\s*Speed\s*=", output);
		Assert.Contains("Health = 4\nKeywords = Array[int]([2, 8])\nCustomProperty = \"keep\"", output);

		card.DeleteLegacySpeed();
		Assert.Equal(output, TresWriter.WriteToString(doc));
	}

	[Fact]
	public void LegacySpeed_AlreadyMissing_RemainsValidAndUnchanged()
	{
		const string text = """
			[gd_resource type="Resource" format=3]
			[resource]
			Id = "test_without_speed"
			Type = 0
			UnitClass = 3
			Health = 2
			""";

		var doc = TresParser.ParseText(text);
		var card = new CardDataTres(doc);
		Assert.False(card.HasLegacySpeed);
		Assert.Null(card.LegacySpeed);
		Assert.Equal(UnitClassValues.Skirmisher, card.UnitClass);
		Assert.Empty(TresWriter.VerifyRoundTrip(text));
	}

	[Fact]
	public void Guard_Keyword_ReadAddRemoveAndDisplayName_Work()
	{
		Assert.Equal(12, KeywordValues.Guard);
		Assert.Equal("Guard", KeywordValues.ValueToName[KeywordValues.Guard]);
		Assert.Equal("Guard · 守护", KeywordValues.GetDisplayName(KeywordValues.Guard));
		Assert.True(KeywordValues.IsValid(KeywordValues.Guard));

		var doc = TresParser.ParseText("""
			[resource]
			Id = "test_guard"
			Type = 0
			Keywords = Array[int]([2])
			""");
		var card = new CardDataTres(doc);
		card.Keywords = card.Keywords.Append(KeywordValues.Guard).ToArray();

		Assert.Equal(new[] { KeywordValues.Taunt, KeywordValues.Guard }, card.Keywords);
		Assert.Contains("Guard", card.GetKeywordNames());
		Assert.Contains("Keywords = Array[int]([2, 12])", TresWriter.WriteToString(doc));

		card.Keywords = card.Keywords.Where(k => k != KeywordValues.Guard).ToArray();
		Assert.DoesNotContain("Guard", card.GetKeywordNames());
		Assert.Equal(new[] { KeywordValues.Taunt }, card.Keywords);
	}

	[Fact]
	public void Guard_ThemeKeywordWeight_CanBeUpdatedAndInsertedWhenMissing()
	{
		var doc = ParseTheme("""
			[resource]
			HeroId = "test"
			TagWeights = {}
			CoreCardIds = PackedStringArray("test_guard")
			""");
		var theme = new ThemeProfileTres(doc);
		Assert.False(theme.HasKeywordWeights);

		theme.KeywordWeights = new Dictionary<int, int> { [KeywordValues.Guard] = 4 };
		var output = TresWriter.WriteToString(doc);

		Assert.True(theme.HasKeywordWeights);
		Assert.Equal(4, theme.KeywordWeights[KeywordValues.Guard]);
		Assert.Contains("TagWeights = {}\nKeywordWeights = {\n\t12: 4,\n}\nCoreCardIds", output);

		var weights = theme.KeywordWeights;
		weights[KeywordValues.Guard] = 2;
		theme.KeywordWeights = weights;
		Assert.Contains("\t12: 2,", TresWriter.WriteToString(doc));
	}

	[Fact]
	public void Intel_MechanicTag_ReadToggleAndDisplayName_Work()
	{
		Assert.Equal(262144, CardMechanicTagValues.Intel);
		Assert.Equal("Intel", CardMechanicTagValues.BitToName[CardMechanicTagValues.Intel]);
		Assert.Equal("Intel · 情报", CardMechanicTagValues.GetDisplayName(CardMechanicTagValues.Intel));

		var doc = TresParser.ParseText("""
			[resource]
			Id = "test_intel"
			Type = 1
			MechanicTags = 131072
			""");
		var card = new CardDataTres(doc);
		card.MechanicTags |= CardMechanicTagValues.Intel;

		Assert.Equal(CardMechanicTagValues.SuperBody | CardMechanicTagValues.Intel, card.MechanicTags);
		Assert.Contains("SuperBody", card.GetMechanicTagNames());
		Assert.Contains("Intel", card.GetMechanicTagNames());
		Assert.True(CardMechanicTagValues.IsValidMask(card.MechanicTags));
		Assert.Contains("MechanicTags = 393216", TresWriter.WriteToString(doc));

		card.MechanicTags &= ~CardMechanicTagValues.Intel;
		Assert.Equal(CardMechanicTagValues.SuperBody, card.MechanicTags);
		Assert.DoesNotContain("Intel", card.GetMechanicTagNames());
	}

	[Fact]
	public void Intel_ThemeTagWeight_CanBeConfigured()
	{
		var doc = ParseTheme("""
			[resource]
			HeroId = "sokou"
			TagWeights = {
			32: 2,
			}
			""");
		var theme = new ThemeProfileTres(doc);
		var weights = theme.TagWeights;
		weights[CardMechanicTagValues.Intel] = 5;
		theme.TagWeights = weights;
		var output = TresWriter.WriteToString(doc);

		Assert.Equal(5, theme.TagWeights[CardMechanicTagValues.Intel]);
		Assert.Contains("\t262144: 5,", output);
		Assert.Contains("\t32: 2,", output);
	}

	[Fact]
	public void Service_SaveCard_WritesNewFields_CleansSpeed_AndSecondSaveIsIdentical()
	{
		var repoRoot = Path.Combine(AppContext.BaseDirectory, "TestRepos", Guid.NewGuid().ToString("N"));
		var cardsDir = Path.Combine(repoRoot, "Resources", "Cards");
		var themesDir = Path.Combine(repoRoot, "Resources", "Themes");
		Directory.CreateDirectory(cardsDir);
		Directory.CreateDirectory(themesDir);
		try
		{
			File.WriteAllText(Path.Combine(repoRoot, "project.godot"), "; test project\n");
			var cardPath = Path.Combine(cardsDir, "Minion_ServiceTest.tres");
			File.WriteAllText(cardPath, """
				[gd_resource type="Resource" format=3]
				[resource]
				Id = "test_service"
				CardName = "服务层测试"
				Description = "其他属性必须保留"
				MechanicTags = 0
				Type = 0
				Attack = 3
				Health = 4
				Speed = 3
				Keywords = Array[int]([2])
				CustomProperty = "keep"
				""");

			var service = new CardTagService(repoRoot);
			service.SaveCard("test_service", CardMechanicTagValues.Intel,
				new[] { KeywordValues.Guard }, UnitClassValues.Support);
			var firstOutput = File.ReadAllText(cardPath);

			Assert.Contains("UnitClass = 2", firstOutput);
			Assert.Contains("MechanicTags = 262144", firstOutput);
			Assert.Contains("Keywords = Array[int]([12])", firstOutput);
			Assert.DoesNotMatch(@"(^|\n)\s*Speed\s*=", firstOutput);
			Assert.Contains("Description = \"其他属性必须保留\"", firstOutput);
			Assert.Contains("CustomProperty = \"keep\"", firstOutput);

			var summary = Assert.Single(service.ListCards());
			Assert.Equal(UnitClassValues.Support, summary.UnitClass);
			Assert.Contains("Intel", summary.MechanicTagNames);
			Assert.Contains("Guard", summary.KeywordNames);
			Assert.False(service.Validate().HasErrors);

			service.SaveCard("test_service", CardMechanicTagValues.Intel,
				new[] { KeywordValues.Guard }, UnitClassValues.Support);
			Assert.Equal(firstOutput, File.ReadAllText(cardPath));
		}
		finally
		{
			if (Directory.Exists(repoRoot))
				Directory.Delete(repoRoot, recursive: true);
		}
	}

	private static TresDocument ParseTheme(string text)
	{
		var lines = NormalizeNewlines(text).Split('\n');
		return TresParser.ParseLines(lines, "ThemeProfile_Test.tres");
	}

	private static string NormalizeNewlines(string text) => text.Replace("\r\n", "\n");
}
