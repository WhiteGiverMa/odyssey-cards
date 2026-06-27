using OdysseyCards.Tools.CardTagEditor.Tres;
using Xunit;

namespace OdysseyCards.Tools.CardTagEditor.Tests;

/// <summary>
/// Keywords 双格式解析测试：bare 数组 [6] 和 typed 数组 Array[int]([8])]。
/// </summary>
public class KeywordsFormatTests
{
	// 重复使用的预期值（CA1861: 避免每次断言都分配新数组）
	private static readonly int[] s_expected_1_7 = { 1, 7 };
	private static readonly int[] s_expected_8 = { 8 };
	private static readonly int[] s_expected_6 = { 6 };
	private static readonly int[] s_expected_2_3 = { 2, 3 };

	[Fact]
	public void Parse_BareArray_ShouldReturnCorrectInts()
	{
		var text = """
			[gd_resource type="Resource" format=3]
			[ext_resource type="Script" path="res://Scripts/Core/CardData.cs" id="1_data"]
			[resource]
			script = ExtResource("1_data")
			Id = "test_bare"
			Keywords = [1, 7]
			""";

		var doc = TresParser.ParseText(text);
		var kwField = doc.GetField("Keywords");
		Assert.NotNull(kwField);

		var values = kwField.AsIntArray();
		Assert.Equal(s_expected_1_7, values);
	}

	[Fact]
	public void Parse_TypedArray_ShouldReturnCorrectInts()
	{
		var text = """
			[gd_resource type="Resource" format=3]
			[ext_resource type="Script" path="res://Scripts/Core/CardData.cs" id="1_data"]
			[resource]
			script = ExtResource("1_data")
			Id = "test_typed"
			Keywords = Array[int]([8])
			""";

		var doc = TresParser.ParseText(text);
		var kwField = doc.GetField("Keywords");
		Assert.NotNull(kwField);

		var values = kwField.AsIntArray();
		Assert.Equal(s_expected_8, values);
	}

	[Fact]
	public void Parse_EmptyArray_ShouldReturnEmpty()
	{
		var text = """
			[gd_resource type="Resource" format=3]
			[ext_resource type="Script" path="res://Scripts/Core/CardData.cs" id="1_data"]
			[resource]
			script = ExtResource("1_data")
			Id = "test_empty"
			Keywords = []
			""";

		var doc = TresParser.ParseText(text);
		var kwField = doc.GetField("Keywords");
		Assert.NotNull(kwField);

		var values = kwField.AsIntArray();
		Assert.Empty(values);
	}

	[Fact]
	public void Write_AlwaysUsesTypedFormat()
	{
		var text = """
			[gd_resource type="Resource" format=3]
			[ext_resource type="Script" path="res://Scripts/Core/CardData.cs" id="1_data"]
			[resource]
			script = ExtResource("1_data")
			Id = "test_bare"
			Keywords = [1, 7]
			""";

		var doc = TresParser.ParseText(text);
		var kwField = doc.GetField("Keywords");
		kwField!.SetIntArray(s_expected_1_7);

		var output = TresWriter.WriteToString(doc);

		// 回写应统一为 Array[int]([...])] 格式
		Assert.Contains("Keywords = Array[int]([1, 7])", output);
	}

	[Fact]
	public void RoundTrip_BareFormat_PreservesValue()
	{
		var text = """
			[gd_resource type="Resource" format=3]
			[ext_resource type="Script" path="res://Scripts/Core/CardData.cs" id="1_data"]
			[resource]
			script = ExtResource("1_data")
			Id = "test_bare"
			Keywords = [6]
			""";

		var doc = TresParser.ParseText(text);
		var kwField = doc.GetField("Keywords");
		var values = kwField!.AsIntArray();
		Assert.Equal(s_expected_6, values);
	}

	[Fact]
	public void RoundTrip_TypedFormat_PreservesValue()
	{
		var text = """
			[gd_resource type="Resource" format=3]
			[ext_resource type="Script" path="res://Scripts/Core/CardData.cs" id="1_data"]
			[resource]
			script = ExtResource("1_data")
			Id = "test_typed"
			Keywords = Array[int]([8])
			""";

		var doc = TresParser.ParseText(text);
		var kwField = doc.GetField("Keywords");
		var values = kwField!.AsIntArray();
		Assert.Equal(s_expected_8, values);
	}

	[Fact]
	public void Parse_MultiValueBareArray_ShouldReturnCorrectInts()
	{
		var text = """
			[gd_resource type="Resource" format=3]
			[ext_resource type="Script" path="res://Scripts/Core/CardData.cs" id="1_data"]
			[resource]
			script = ExtResource("1_data")
			Id = "test_multi"
			Keywords = [2, 3]
			""";

		var doc = TresParser.ParseText(text);
		var kwField = doc.GetField("Keywords");
		var values = kwField!.AsIntArray();
		Assert.Equal(s_expected_2_3, values);
	}
}
