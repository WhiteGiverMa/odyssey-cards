using Godot;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using OdysseyCards.AI;
using OdysseyCards.Card;
using OdysseyCards.Combat;

namespace OdysseyCards.Infrastructure;

/// <summary>
/// C# 运行时求值网关 — 为 godot-mcp game_call_method 提供 C# 反射求值能力。
/// </summary>
/// <remarks>
/// 解决 GDScript game_eval 无法访问 C# 静态成员、纯 C# 类（Hero/Minion/Board）、
/// event Action、List&lt;T&gt;、enum 的痛点。纯 C# 反射实现，不引入 Roslyn scripting。
///
/// AI 调用方式（godot-mcp）：
///   game_call_method(nodePath="/root/EvalGateway", method="Eval", args=["CombatManager.PlayerHero.CurrentHealth"])
///   game_call_method(nodePath="/root/EvalGateway", method="GetSnapshot", args=["combat"])
///
/// 路径语法：
///   RootName.Property.SubProperty           — 链式属性/字段访问
///   RootName.Collection[index]              — 索引访问（IList/Array）
///   RootName.Property[index].SubProperty    — 混合访问
///   static:TypeName.StaticMember            — 显式静态访问
///
/// 根解析顺序：
///   1. Autoload 名 → get_node("/root/Name")
///   2. 类型名 + static Instance 属性 → 反射
///   3. static: 前缀 → 直接反射静态成员
///
/// 深度上限 8，路径长度上限 256 字符。反射 BindingFlags: Public | NonPublic | Instance | Static。
/// 仅 DEBUG 构建执行反射逻辑；发布版方法返回错误字符串。
/// </remarks>
public partial class EvalGateway : Node
{
	private const int MaxDepth = 8;
	private const int MaxPathLength = 256;
	private const BindingFlags ReflectFlags =
		BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

	/// <summary>
	/// 路径表达式求值。链式访问 C# 字段/属性/索引器。
	/// </summary>
	/// <param name="pathExpr">点号分隔的路径表达式，如 "CombatManager.PlayerHero.CurrentHealth"</param>
	/// <returns>求值结果的 Godot Variant，或错误字符串</returns>
	public Variant Eval(string pathExpr)
	{
#if DEBUG
		try
		{
			if (string.IsNullOrWhiteSpace(pathExpr))
				return Variant.From("error: empty path expression");
			if (pathExpr.Length > MaxPathLength)
				return Variant.From($"error: path too long (max {MaxPathLength})");

			var segments = ParsePath(pathExpr);
			if (segments.Count == 0)
				return Variant.From("error: invalid path");

			object? current = ResolveRoot(segments[0]);
			for (int i = 1; i < segments.Count; i++)
			{
				if (current == null)
					return Variant.From($"error: null at segment '{segments[i - 1].Name}' (depth {i})");
				current = ResolveSegment(current, segments[i]);
			}
			return ToVariant(current, 0);
		}
		catch (Exception ex)
		{
			return Variant.From($"error: {ex.GetType().Name}: {ex.Message}");
		}
#else
		return Variant.From("error: EvalGateway disabled in release build");
#endif
	}

	/// <summary>
	/// 高频 QA 快照 — 一次性获取常用运行时状态，避免反复 Eval 链式访问。
	/// </summary>
	/// <param name="kind">快照种类：combat | player | enemy | board</param>
	/// <returns>结构化的 Godot Collections Dictionary/Variant</returns>
	public Variant GetSnapshot(string kind)
	{
#if DEBUG
		try
		{
			return kind switch
			{
				"combat" => GetCombatSnapshot(),
				"player" => GetPlayerSnapshot(),
				"enemy" => GetEnemySnapshot(),
				"board" => GetBoardSnapshot(),
				_ => Variant.From($"error: unknown snapshot kind '{kind}'. Available: combat, player, enemy, board")
			};
		}
		catch (Exception ex)
		{
			return Variant.From($"error: {ex.GetType().Name}: {ex.Message}");
		}
#else
		return Variant.From("error: EvalGateway disabled in release build");
#endif
	}

	// ===== 路径解析 =====

#if DEBUG
	internal record PathSegment(string Name, int? Index, string? Key);

	/// <summary>
	/// 解析路径表达式为段列表。支持点号分隔 + 方括号索引。
	/// </summary>
	internal static List<PathSegment> ParsePath(string path)
	{
		var segments = new List<PathSegment>();
		var parts = path.Split('.');

		foreach (var part in parts)
		{
			if (string.IsNullOrEmpty(part))
				continue;

			int bracketPos = part.IndexOf('[');
			if (bracketPos < 0)
			{
				segments.Add(new PathSegment(part, null, null));
				continue;
			}

			string name = part.Substring(0, bracketPos);
			string bracketContent = part.Substring(bracketPos + 1, part.Length - bracketPos - 2);
			if (int.TryParse(bracketContent, out int intIndex))
				segments.Add(new PathSegment(name, intIndex, null));
			else
				segments.Add(new PathSegment(name, null, bracketContent));
		}

		return segments;
	}

	/// <summary>
	/// 解析根段。先尝试 Autoload 节点，再尝试反射静态 Instance 属性，再尝试 static: 前缀。
	/// </summary>
	private object? ResolveRoot(PathSegment root)
	{
		// static:TypeName 静态访问前缀
		if (root.Name.StartsWith("static:"))
		{
			string typeName = root.Name.Substring("static:".Length);
			var type = FindTypeByName(typeName);
			if (type == null)
				throw new InvalidOperationException($"type not found: '{typeName}'");
			return type; // 返回 Type 本身，后续段通过反射静态成员访问
		}

		// 1. Autoload 节点
		var node = GetNodeOrNull("/root/" + root.Name);
		if (node != null)
			return node;

		// 2. 反射静态 Instance 属性
		var typeByName = FindTypeByName(root.Name);
		if (typeByName != null)
		{
			var instanceProp = typeByName.GetProperty("Instance", ReflectFlags);
			if (instanceProp != null && instanceProp.GetMethod != null)
				return instanceProp.GetValue(null);
		}

		throw new InvalidOperationException($"cannot resolve root: '{root.Name}'");
	}

	/// <summary>
	/// 解析单个路径段：先取属性/字段，再取索引。
	/// </summary>
	private static object? ResolveSegment(object current, PathSegment segment)
	{
		object? value = current;

		// 如果 current 是 Type（来自 static: 前缀），取静态成员
		bool isStaticContext = current is Type;
		Type? staticType = isStaticContext ? (Type)current : null;

		if (!string.IsNullOrEmpty(segment.Name))
		{
			if (isStaticContext && staticType != null)
			{
				value = GetStaticMember(staticType, segment.Name);
			}
			else
			{
				value = GetPropertyOrField(current, segment.Name);
			}
		}

		if (segment.Index.HasValue)
			value = GetByIndex(value, segment.Index.Value);
		else if (segment.Key != null)
			value = GetByKey(value, segment.Key);

		return value;
	}

	/// <summary>
	/// 获取属性或字段（public 或 nonpublic，instance 或 static）。
	/// </summary>
	internal static object? GetPropertyOrField(object obj, string memberName)
	{
		var type = obj.GetType();

		// 优先属性
		var prop = type.GetProperty(memberName, ReflectFlags);
		if (prop != null && prop.GetMethod != null)
			return prop.GetValue(obj);

		// 回退字段
		var field = type.GetField(memberName, ReflectFlags);
		if (field != null)
			return field.GetValue(obj);

		throw new InvalidOperationException($"member '{memberName}' not found on {type.Name}");
	}

	/// <summary>
	/// 获取静态成员。
	/// </summary>
	private static object? GetStaticMember(Type type, string memberName)
	{
		var prop = type.GetProperty(memberName, ReflectFlags);
		if (prop != null && prop.GetMethod != null)
			return prop.GetValue(null);

		var field = type.GetField(memberName, ReflectFlags);
		if (field != null)
			return field.GetValue(null);

		throw new InvalidOperationException($"static member '{memberName}' not found on {type.Name}");
	}

	/// <summary>
	/// 索引访问：支持 IList、Array、反射索引器。
	/// </summary>
	internal static object? GetByIndex(object? obj, int index)
	{
		if (obj == null)
			return null;

		if (obj is IList list)
			return list[index];

		if (obj is Array array)
			return array.GetValue(index);

		// 反射索引器（C# 索引器在反射中是名为 "Item" 的属性）
		var type = obj.GetType();
		var indexer = type.GetProperty("Item", ReflectFlags);
		if (indexer != null)
			return indexer.GetValue(obj, new object[] { index });

		throw new InvalidOperationException($"cannot index into {type.Name} with int");
	}

	/// <summary>
	/// 字典键访问：支持 IDictionary、反射索引器。
	/// </summary>
	internal static object? GetByKey(object? obj, string key)
	{
		if (obj == null)
			return null;

		if (obj is IDictionary dict)
			return dict.Contains(key) ? dict[key] : null;

		var type = obj.GetType();
		var indexer = type.GetProperty("Item", ReflectFlags);
		if (indexer != null)
		{
			var keyParam = indexer.GetIndexParameters().FirstOrDefault();
			if (keyParam != null)
			{
				object? convertedKey = Convert.ChangeType(key, keyParam.ParameterType);
				return indexer.GetValue(obj, new[] { convertedKey });
			}
		}

		throw new InvalidOperationException($"cannot index into {type.Name} with string key");
	}

	/// <summary>
	/// 在已加载程序集中按名称查找类型。
	/// </summary>
	private static Type? FindTypeByName(string typeName)
	{
		return AppDomain.CurrentDomain.GetAssemblies()
			.Select(asm => asm.GetType(typeName))
			.FirstOrDefault(t => t != null)
			?? AppDomain.CurrentDomain.GetAssemblies()
				.SelectMany(asm => asm.GetTypes())
				.FirstOrDefault(t => t.Name == typeName);
	}

	// ===== C# → Godot Variant marshalling =====

	/// <summary>
	/// 将 C# 对象转换为 Godot Variant。自定义 C# 类反射 public 属性一层。
	/// </summary>
	/// <param name="value">待转换的 C# 值</param>
	/// <param name="depth">当前递归深度（防止无限递归）</param>
	internal static Variant ToVariant(object? value, int depth)
	{
		if (value == null)
			return default(Variant);

		// 基本类型
		switch (value)
		{
			case bool b: return Variant.From(b);
			case int i: return Variant.From(i);
			case long l: return Variant.From(l);
			case float f: return Variant.From(f);
			case double d: return Variant.From(d);
			case string s: return Variant.From(s);
		}

		// Godot 原生类型（直接返回，Godot C# 绑定会处理）
		if (value is Variant v)
			return v;
		if (value is Godot.Collections.Dictionary gdDict)
			return gdDict;
		if (value is Godot.Collections.Array gdArr)
			return gdArr;

		// Godot 数值类型
		if (value is Vector2 vec2) return Variant.From(vec2);
		if (value is Vector3 vec3) return Variant.From(vec3);
		if (value is Color col) return Variant.From(col);

		// enum → int + 元信息
		if (value is Enum enumValue)
		{
			var dict = new Godot.Collections.Dictionary
			{
				["_enumType"] = enumValue.GetType().Name,
				["value"] = enumValue.ToString(),
				["intValue"] = Convert.ToInt32(enumValue)
			};
			return dict;
		}

		// 深度上限：超过则返回类型名 + ToString
		if (depth >= MaxDepth)
			return Variant.From($"<{value.GetType().Name}: {value}>");

		// 集合类型
		if (value is IList list)
		{
			var arr = new Godot.Collections.Array();
			foreach (var item in list)
				arr.Add(ToVariant(item, depth + 1));
			return arr;
		}

		if (value is IDictionary dict2)
		{
			var result = new Godot.Collections.Dictionary();
			foreach (DictionaryEntry entry in dict2)
				result[Variant.From(entry.Key?.ToString() ?? "null")] = ToVariant(entry.Value, depth + 1);
			return result;
		}

		if (value is IEnumerable enumerable && value is not string)
		{
			var arr = new Godot.Collections.Array();
			foreach (var item in enumerable)
				arr.Add(ToVariant(item, depth + 1));
			return arr;
		}

		// Godot Node → 简化引用
		if (value is Node node)
		{
			return new Godot.Collections.Dictionary
			{
				["_type"] = "Node",
				["class"] = node.GetClass(),
				["name"] = node.Name,
				["path"] = node.GetPath().ToString()
			};
		}

		// Godot Resource
		if (value is Resource res)
		{
			return new Godot.Collections.Dictionary
			{
				["_type"] = "Resource",
				["class"] = res.GetClass(),
				["path"] = res.ResourcePath
			};
		}

		// 自定义 C# 类 → 反射 public 属性一层
		return ReflectObjectToDictionary(value, depth);
	}

	/// <summary>
	/// 反射对象的 public 属性和字段，生成 Godot Dictionary。只递归一层到 depth 上限。
	/// </summary>
	private static Variant ReflectObjectToDictionary(object obj, int depth)
	{
		var type = obj.GetType();
		var result = new Godot.Collections.Dictionary
		{
			["_type"] = type.Name
		};

		// public 属性
		foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
		{
			if (prop.GetMethod == null || prop.GetIndexParameters().Length > 0)
				continue;
			try
			{
				object? propValue = prop.GetValue(obj);
				result[prop.Name] = ToVariant(propValue, depth + 1);
			}
			catch (Exception ex)
			{
				result[prop.Name] = Variant.From($"<error: {ex.Message}>");
			}
		}

		// public 字段（排除 backing field）
		foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
		{
			if (field.Name.StartsWith("_") || field.IsInitOnly && field.Name.Contains("k__BackingField"))
				continue;
			try
			{
				object? fieldValue = field.GetValue(obj);
				result[field.Name] = ToVariant(fieldValue, depth + 1);
			}
			catch (Exception ex)
			{
				result[field.Name] = Variant.From($"<error: {ex.Message}>");
			}
		}

		return result;
	}

	// ===== 预定义快照 =====

	/// <summary>
	/// 战斗全景快照。
	/// </summary>
	private static Variant GetCombatSnapshot()
	{
		var combat = CombatManager.Instance;
		if (combat == null)
			return Variant.From("error: CombatManager.Instance is null (not in combat scene)");

		var dict = new Godot.Collections.Dictionary
		{
			["phase"] = combat.State.Phase.ToString(),
			["turnCount"] = combat.State.TurnCount,
			["playerMana"] = combat.State.PlayerMana,
			["playerMaxMana"] = combat.State.PlayerMaxMana,
			["isPlayerTurn"] = combat.State.IsPlayerTurn,
			["isEnemyTurn"] = combat.State.IsEnemyTurn,
			["isGameOver"] = combat.State.IsGameOver,
			["isEnemyTurnAnimating"] = combat.IsEnemyTurnAnimating,
			["heroPowerUsedThisTurn"] = combat.HeroPowerUsedThisTurn,
			["player"] = GetHeroSnapshot(combat.PlayerHero),
			["enemies"] = GetEnemyUnitsSnapshot(combat),
			["playerMinions"] = GetMinionsSnapshot(combat.Board.GetPlayerMinions()),
			["enemyMinions"] = GetMinionsSnapshot(combat.Board.GetEnemyMinions())
		};
		return dict;
	}

	/// <summary>
	/// 玩家英雄快照。
	/// </summary>
	private static Variant GetPlayerSnapshot()
	{
		var combat = CombatManager.Instance;
		if (combat == null)
			return Variant.From("error: CombatManager.Instance is null (not in combat scene)");

		var dict = new Godot.Collections.Dictionary
		{
			["player"] = GetHeroSnapshot(combat.PlayerHero),
			["mana"] = combat.State.PlayerMana,
			["maxMana"] = combat.State.PlayerMaxMana,
			["heroPowerUsedThisTurn"] = combat.HeroPowerUsedThisTurn
		};
		return dict;
	}

	/// <summary>
	/// 敌方单位列表快照。
	/// </summary>
	private static Variant GetEnemySnapshot()
	{
		var combat = CombatManager.Instance;
		if (combat == null)
			return Variant.From("error: CombatManager.Instance is null (not in combat scene)");

		return GetEnemyUnitsSnapshot(combat);
	}

	/// <summary>
	/// 棋盘快照（双方随从槽位）。
	/// </summary>
	private static Variant GetBoardSnapshot()
	{
		var combat = CombatManager.Instance;
		if (combat == null)
			return Variant.From("error: CombatManager.Instance is null (not in combat scene)");

		var board = combat.Board;
		var playerSlots = new Godot.Collections.Array();
		for (int i = 0; i < Board.MaxSlotsPerSide; i++)
		{
			var minion = board.PlayerSlots[i];
			playerSlots.Add(minion == null ? default(Variant) : GetMinionSnapshot(minion));
		}

		var enemySlots = new Godot.Collections.Array();
		for (int i = 0; i < Board.MaxSlotsPerSide; i++)
		{
			var minion = board.EnemySlots[i];
			enemySlots.Add(minion == null ? default(Variant) : GetMinionSnapshot(minion));
		}

		return new Godot.Collections.Dictionary
		{
			["playerSlots"] = playerSlots,
			["enemySlots"] = enemySlots
		};
	}

	/// <summary>
	/// 单个英雄快照。
	/// </summary>
	private static Variant GetHeroSnapshot(Hero hero)
	{
		if (hero == null)
			return default(Variant);

		return new Godot.Collections.Dictionary
		{
			["_type"] = "Hero",
			["currentHealth"] = hero.CurrentHealth,
			["currentArmor"] = hero.CurrentArmor,
			["defense"] = hero.Defense,
			["isPlayerSide"] = hero.IsPlayerSide,
			["isDead"] = hero.IsDead
		};
	}

	/// <summary>
	/// 单个随从快照。
	/// </summary>
	private static Variant GetMinionSnapshot(Minion minion)
	{
		return new Godot.Collections.Dictionary
		{
			["_type"] = "Minion",
			["cardName"] = minion.CardName,
			["attack"] = minion.Attack,
			["currentHealth"] = minion.CurrentHealth,
			["maxHealth"] = minion.MaxHealth,
			["defense"] = minion.Defense,
			["isPlayerSide"] = minion.IsPlayerSide,
			["isDead"] = minion.IsDead,
			["hasTaunt"] = minion.HasTaunt
		};
	}

	/// <summary>
	/// 随从列表快照。
	/// </summary>
	private static Variant GetMinionsSnapshot(List<Minion> minions)
	{
		var arr = new Godot.Collections.Array();
		foreach (var minion in minions)
			arr.Add(GetMinionSnapshot(minion));
		return arr;
	}

	/// <summary>
	/// 敌方单位列表快照（含 Body 英雄状态）。
	/// </summary>
	private static Variant GetEnemyUnitsSnapshot(CombatManager combat)
	{
		var arr = new Godot.Collections.Array();
		foreach (var unit in combat.EnemyUnits)
		{
			arr.Add(new Godot.Collections.Dictionary
			{
				["_type"] = "EnemyUnit",
				["body"] = GetHeroSnapshot(unit.Body),
				["hasMoveStates"] = unit.HasMoveStates
			});
		}
		return arr;
	}
#endif
}
