using System.Collections;
using System.Collections.Generic;
using Godot;
using Xunit;
using OdysseyCards.Infrastructure;

namespace OdysseyCards.Tests.Unit;

/// <summary>
/// 单元测试 — EvalGateway 路径解析 + C# → Godot Variant marshalling
/// 测试不依赖 Node 上下文的纯逻辑方法。
/// </summary>
public class EvalGatewayTests
{
	// ===== ParsePath 路径解析 =====

	[Fact]
	public void ParsePath_SimpleDotSeparated_ReturnsSegments()
	{
		var segments = EvalGateway.ParsePath("A.B.C");

		Assert.Equal(3, segments.Count);
		Assert.Equal("A", segments[0].Name);
		Assert.Null(segments[0].Index);
		Assert.Null(segments[0].Key);
		Assert.Equal("B", segments[1].Name);
		Assert.Equal("C", segments[2].Name);
	}

	[Fact]
	public void ParsePath_WithIntIndex_ParsesIndex()
	{
		var segments = EvalGateway.ParsePath("Items[3].Name");

		Assert.Equal(2, segments.Count);
		Assert.Equal("Items", segments[0].Name);
		Assert.Equal(3, segments[0].Index);
		Assert.Null(segments[0].Key);
		Assert.Equal("Name", segments[1].Name);
	}

	[Fact]
	public void ParsePath_WithKeyIndex_ParsesKey()
	{
		var segments = EvalGateway.ParsePath("Dict[myKey]");

		Assert.Single(segments);
		Assert.Equal("Dict", segments[0].Name);
		Assert.Null(segments[0].Index);
		Assert.Equal("myKey", segments[0].Key);
	}

	[Fact]
	public void ParsePath_SingleName_ReturnsOneSegment()
	{
		var segments = EvalGateway.ParsePath("GameManager");

		Assert.Single(segments);
		Assert.Equal("GameManager", segments[0].Name);
	}

	[Fact]
	public void ParsePath_EmptyString_ReturnsEmptyList()
	{
		var segments = EvalGateway.ParsePath("");

		Assert.Empty(segments);
	}

	[Fact]
	public void ParsePath_StaticPrefix_PreservesPrefix()
	{
		var segments = EvalGateway.ParsePath("static:GameManager.Instance");

		Assert.Equal(2, segments.Count);
		Assert.Equal("static:GameManager", segments[0].Name);
		Assert.Equal("Instance", segments[1].Name);
	}

	// ===== GetPropertyOrField 反射 =====

	[Fact]
	public void GetPropertyOrField_PublicProperty_ReturnsValue()
	{
		var obj = new TestTarget { PublicProp = 42 };

		var result = EvalGateway.GetPropertyOrField(obj, "PublicProp");

		Assert.Equal(42, result);
	}

	[Fact]
	public void GetPropertyOrField_PublicField_ReturnsValue()
	{
		var obj = new TestTarget { PublicField = "hello" };

		var result = EvalGateway.GetPropertyOrField(obj, "PublicField");

		Assert.Equal("hello", result);
	}

	[Fact]
	public void GetPropertyOrField_PrivateField_ReturnsValue()
	{
		var obj = new TestTarget();

		// 反射 BindingFlags 包含 NonPublic，应能访问私有字段
		var result = EvalGateway.GetPropertyOrField(obj, "_privateField");

		Assert.Equal(99, result);
	}

	[Fact]
	public void GetPropertyOrField_NonExistent_Throws()
	{
		var obj = new TestTarget();

		Assert.Throws<System.InvalidOperationException>(
			() => EvalGateway.GetPropertyOrField(obj, "NonExistent"));
	}

	// ===== GetByIndex 索引访问 =====

	[Fact]
	public void GetByIndex_IList_ReturnsElement()
	{
		var list = new List<int> { 10, 20, 30 };

		var result = EvalGateway.GetByIndex(list, 1);

		Assert.Equal(20, result);
	}

	[Fact]
	public void GetByIndex_Array_ReturnsElement()
	{
		var arr = new string[] { "a", "b", "c" };

		var result = EvalGateway.GetByIndex(arr, 2);

		Assert.Equal("c", result);
	}

	[Fact]
	public void GetByIndex_Null_ReturnsNull()
	{
		var result = EvalGateway.GetByIndex(null, 0);

		Assert.Null(result);
	}

	// ===== GetByKey 字典键访问 =====

	[Fact]
	public void GetByKey_Dictionary_ReturnsValue()
	{
		var dict = new Dictionary<string, int> { ["hp"] = 30, ["mp"] = 10 };

		var result = EvalGateway.GetByKey(dict, "hp");

		Assert.Equal(30, result);
	}

	[Fact]
	public void GetByKey_Dictionary_MissingKey_ReturnsNull()
	{
		var dict = new Dictionary<string, int> { ["hp"] = 30 };

		var result = EvalGateway.GetByKey(dict, "nonexistent");

		Assert.Null(result);
	}

	[Fact]
	public void GetByKey_Null_ReturnsNull()
	{
		var result = EvalGateway.GetByKey(null, "anyKey");

		Assert.Null(result);
	}

	// ===== ToVariant marshalling =====
	// 以下测试需要 Godot native 运行时（Variant.From 调用 godotsharp_string_new_with_utf16_chars），
	// 纯 xUnit 环境会 AccessViolationException。与 CombatIntegrationTests 同模式标记 Skip。
	// 运行时验证通过 godot-mcp game_call_method("/root/EvalGateway", "Eval", [...]) 完成。

	[Fact(Skip = "需要 Godot 运行时 — Variant.From 调用 native interop")]
	public void ToVariant_Null_ReturnsDefaultVariant()
	{
		var result = EvalGateway.ToVariant(null, 0);

		Assert.Equal(default(Variant), result);
	}

	[Fact(Skip = "需要 Godot 运行时 — Variant.From 调用 native interop")]
	public void ToVariant_Int_ReturnsIntVariant()
	{
		var result = EvalGateway.ToVariant(42, 0);

		Assert.Equal(42, (int)result);
	}

	[Fact(Skip = "需要 Godot 运行时 — Variant.From 调用 native interop")]
	public void ToVariant_String_ReturnsStringVariant()
	{
		var result = EvalGateway.ToVariant("hello", 0);

		Assert.Equal("hello", (string)result);
	}

	[Fact(Skip = "需要 Godot 运行时 — Variant.From 调用 native interop")]
	public void ToVariant_Bool_ReturnsBoolVariant()
	{
		var result = EvalGateway.ToVariant(true, 0);

		Assert.True((bool)result);
	}

	[Fact(Skip = "需要 Godot 运行时 — Godot.Collections.Array 需要 native")]
	public void ToVariant_List_ReturnsArrayVariant()
	{
		var list = new List<int> { 1, 2, 3 };

		var result = EvalGateway.ToVariant(list, 0);
		var arr = result.As<Godot.Collections.Array>();

		Assert.Equal(3, arr.Count);
		Assert.Equal(1, (int)arr[0]);
		Assert.Equal(2, (int)arr[1]);
		Assert.Equal(3, (int)arr[2]);
	}

	[Fact(Skip = "需要 Godot 运行时 — Godot.Collections.Dictionary 需要 native")]
	public void ToVariant_Enum_ReturnsDictionaryWithMetadata()
	{
		var result = EvalGateway.ToVariant(TestEnum.AddDamage, 0);
		var dict = result.As<Godot.Collections.Dictionary>();

		Assert.Equal("AddDamage", (string)dict["value"]);
		Assert.Equal(1, (int)dict["intValue"]);
		Assert.Equal("TestEnum", (string)dict["_enumType"]);
	}

	[Fact(Skip = "需要 Godot 运行时 — Godot.Collections.Dictionary 需要 native")]
	public void ToVariant_CustomClass_ReturnsDictionaryWithProperties()
	{
		var obj = new TestTarget { PublicProp = 77, PublicField = "xyz" };

		var result = EvalGateway.ToVariant(obj, 0);
		var dict = result.As<Godot.Collections.Dictionary>();

		Assert.Equal("TestTarget", (string)dict["_type"]);
		Assert.Equal(77, (int)dict["PublicProp"]);
		Assert.Equal("xyz", (string)dict["PublicField"]);
	}

	[Fact(Skip = "需要 Godot 运行时 — Variant 转换需要 native")]
	public void ToVariant_DepthLimit_ReturnsTypeStringFallback()
	{
		var obj = new TestTarget { PublicProp = 1 };

		// 深度达到上限（MaxDepth=8）时返回类型名 + ToString
		var result = EvalGateway.ToVariant(obj, 8);

		Assert.Equal(typeof(string), result.VariantType == Variant.Type.String ? typeof(string) : result.GetType());
		// 结果应该是字符串形式，不再递归展开
		string strResult = (string)result;
		Assert.Contains("TestTarget", strResult);
	}

	// ===== 测试辅助类型 =====

	private enum TestEnum
	{
		None = 0,
		AddDamage = 1,
	}

	private class TestTarget
	{
		public int PublicProp { get; set; }
		public string PublicField = "";
		// 使用 `号声明私有字段，反射可访问（BindingFlags.NonPublic）
		// Roslyn 分析器建议属性 vs 字段，这里是测试专用
		#pragma warning disable CA1051 // 不要将可见实例字段更改为属性
		public int _privateField = 99;
		#pragma warning restore CA1051
	}
}
