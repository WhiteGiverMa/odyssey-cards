using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using OdysseyCards.Card;
using OdysseyCards.Combat;
using OdysseyCards.UI;
using Expr = System.Linq.Expressions.Expression;
using NewArrayExpression = System.Linq.Expressions.NewArrayExpression;
using ParameterExpression = System.Linq.Expressions.ParameterExpression;

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

	/// <summary>合作式虚拟指针坐标；仅 DEBUG 构建允许激活，供 godot-mcp 合成拖拽与 UI 轮询对齐。</summary>
	private static Vector2? _virtualMousePosition;
#if DEBUG
	// virtual_drag 在 process_frame 信号后立刻 Clear，此时节点 _Process 尚未运行；
	// 保留一帧“清除宽限坐标”，避免松手帧读到真实鼠标导致落点跳到屏幕角落。
	private static Vector2? _virtualMouseGracePosition;
	private static ulong _virtualMouseGraceFrame;
#endif

#if DEBUG
	private const int ProbeCapacity = 256;
	private readonly object _exceptionProbeLock = new();
	private readonly object _eventProbeLock = new();
	private readonly ProbeBuffer<ExceptionProbeRecord> _exceptionProbes = new(ProbeCapacity);
	private readonly ProbeBuffer<EventProbeRecord> _eventProbes = new(ProbeCapacity);
	private readonly Dictionary<string, EventSubscription> _eventSubscriptions = new(StringComparer.Ordinal);
	private long _exceptionSequence;
	private long _eventSequence;
	private bool _probeHandlersAttached;
#endif

	/// <summary>设置虚拟指针坐标（两个标量版本，便于 C# 测试与自动化脚本直接调用）。</summary>
	public void SetVirtualMousePosition(float x, float y) => SetVirtualMousePosition(new Vector2(x, y));

	/// <summary>设置虚拟指针坐标（godot-mcp 的 virtual_cursor / virtual_drag 实际传入 Vector2）。</summary>
	public void SetVirtualMousePosition(Vector2 position)
	{
#if DEBUG
		if (position.IsFinite())
		{
			_virtualMousePosition = position;
			_virtualMouseGracePosition = null;
		}
#endif
	}

	/// <summary>清除虚拟指针，恢复真实鼠标坐标；松手帧保留一次虚拟坐标宽限。</summary>
	public void ClearVirtualMousePosition()
	{
#if DEBUG
		if (_virtualMousePosition is Vector2 lastPosition)
		{
			_virtualMouseGracePosition = lastPosition;
			_virtualMouseGraceFrame = Engine.GetProcessFrames();
		}
		_virtualMousePosition = null;
#endif
	}

	/// <summary>获取当前虚拟指针；未激活时返回零向量。</summary>
	public Vector2 GetVirtualMousePosition()
	{
#if DEBUG
		return _virtualMousePosition ?? Vector2.Zero;
#else
		return Vector2.Zero;
#endif
	}

	/// <summary>是否已激活合作式虚拟指针。</summary>
	public bool HasVirtualMousePosition()
	{
#if DEBUG
		return _virtualMousePosition.HasValue;
#else
		return false;
#endif
	}

	/// <summary>
	/// 获取 UI 指针采样位置：虚拟指针激活时返回虚拟坐标，否则返回调用控件的真实全局鼠标位置。
	/// 该方法在 Release 中仍保留真实鼠标路径，不暴露 DEBUG 专用状态。
	/// </summary>
	public static Vector2 GetEffectiveMousePosition(CanvasItem? caller)
	{
#if DEBUG
		if (_virtualMousePosition is Vector2 virtualPosition)
			return virtualPosition;
		if (_virtualMouseGracePosition is Vector2 gracePosition
			&& Engine.GetProcessFrames() <= _virtualMouseGraceFrame + 1)
			return gracePosition;
		_virtualMouseGracePosition = null;
#endif
		return caller == null ? Vector2.Zero : caller.GetGlobalMousePosition();
	}

	/// <summary>
	/// 调用 C# 方法（godot-mcp game_call_csharp 协议）：参数为 JSON 数组字符串。
	/// 支持无参、单参与多参重载，实例与静态方法均可。
	/// </summary>
	public Variant Call(string pathExpr, string argsJson)
	{
#if DEBUG
		try
		{
			string normalized = NormalizeArgsJson(argsJson);
			Variant parsed = Godot.Json.ParseString(normalized);
			if (parsed.VariantType != Variant.Type.Array)
				return Variant.From("error: Call args must be a JSON array");
			return CallWithArguments(pathExpr, parsed.AsGodotArray());
		}
		catch (Exception ex)
		{
			return Variant.From($"error: {ex.GetType().Name}: {ex.Message}");
		}
#else
		return Variant.From("error: EvalGateway disabled in release build");
#endif
	}

	/// <summary>调用 C# 方法的 Godot Array 便捷入口；直接供 C# QA 与单元测试使用。</summary>
	internal Variant Call(string pathExpr, Godot.Collections.Array args)
	{
#if DEBUG
		try
		{
			return CallWithArguments(pathExpr, args);
		}
		catch (Exception ex)
		{
			return Variant.From($"error: {ex.GetType().Name}: {ex.Message}");
		}
#else
		return Variant.From("error: EvalGateway disabled in release build");
#endif
	}

	/// <summary>读取指定序号之后的 C# 未处理异常探针记录。</summary>
	public Variant GetExceptions(int sinceSequence = 0)
	{
#if DEBUG
		try
		{
			List<ExceptionProbeRecord> snapshot;
			lock (_exceptionProbeLock)
				snapshot = _exceptionProbes.Snapshot();
			var records = new Godot.Collections.Array();
			foreach (ExceptionProbeRecord record in snapshot)
			{
				if (record.Sequence <= sinceSequence)
					continue;
				records.Add(new Godot.Collections.Dictionary
				{
					["sequence"] = record.Sequence,
					["source"] = record.Source,
					["type"] = record.TypeName,
					["message"] = record.Message,
					["stackTrace"] = record.StackTrace,
					["timestampUtc"] = record.TimestampUtc,
				});
			}
			return new Godot.Collections.Dictionary
			{
				["latestSequence"] = Interlocked.Read(ref _exceptionSequence),
				["dropped"] = _exceptionProbes.Dropped,
				["exceptions"] = records,
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

	/// <summary>订阅任意 C# <c>event Action</c> / <c>Action&lt;...&gt;</c>，参数在触发时快照。</summary>
	public Variant SubscribeEvent(string pathExpr, string eventName)
	{
#if DEBUG
		if (string.IsNullOrWhiteSpace(pathExpr) || string.IsNullOrWhiteSpace(eventName))
			return Variant.From("error: pathExpr and eventName are required");
		try
		{
			object? owner = ResolvePathValue(pathExpr);
			if (owner == null)
				return Variant.From("error: event owner is null");
			Type ownerType = owner is Type type ? type : owner.GetType();
			EventInfo? eventInfo = ownerType.GetEvent(eventName, ReflectFlags);
			if (eventInfo == null)
				return Variant.From($"error: event '{eventName}' not found on {ownerType.Name}");
			Type handlerType = eventInfo.EventHandlerType
				?? throw new InvalidOperationException("event handler type is null");
			if (handlerType == typeof(Delegate) || !typeof(Delegate).IsAssignableFrom(handlerType))
				return Variant.From($"error: event '{eventName}' is not a delegate event");

			string key = $"{pathExpr}:{eventName}";
			lock (_eventProbeLock)
			{
				if (_eventSubscriptions.TryGetValue(key, out EventSubscription? previous))
				{
					RemoveEventSubscription(previous);
					_eventSubscriptions.Remove(key);
				}

				Action<object?[]> capture = values => CaptureEvent(key, pathExpr, eventName, values);
				Delegate handler = BuildEventHandler(handlerType, capture);
				object? target = eventInfo.AddMethod?.IsStatic == true ? null : owner as object;
				if (target is Type)
					target = null;
				eventInfo.AddEventHandler(target, handler);
				_eventSubscriptions[key] = new EventSubscription(key, target, eventInfo, handler);
			}
			return new Godot.Collections.Dictionary
			{
				["subscribed"] = true,
				["subscription"] = key,
				["path"] = pathExpr,
				["event"] = eventName,
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

	/// <summary>轮询已捕获的 C# 事件记录；clear=true 时读取后清空缓冲。</summary>
	public Variant PollEvents(bool clear = true)
	{
#if DEBUG
		try
		{
			List<EventProbeRecord> snapshot;
			lock (_eventProbeLock)
			{
				snapshot = _eventProbes.Snapshot();
				if (clear)
					_eventProbes.Clear();
			}
			var records = new Godot.Collections.Array();
			foreach (EventProbeRecord record in snapshot)
			{
				records.Add(new Godot.Collections.Dictionary
				{
					["sequence"] = record.Sequence,
					["subscription"] = record.Subscription,
					["path"] = record.Path,
					["event"] = record.EventName,
					["args"] = record.Arguments,
				});
			}
			return new Godot.Collections.Dictionary
			{
				["latestSequence"] = Interlocked.Read(ref _eventSequence),
				["dropped"] = _eventProbes.Dropped,
				["events"] = records,
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

	/// <summary>DEBUG 探针挂载；Release 完全无副作用。</summary>
	public override void _Ready()
	{
#if DEBUG
		if (_probeHandlersAttached)
			return;
		AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
		TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
		_probeHandlersAttached = true;
#endif
	}

	/// <summary>移除异常与事件探针，避免 Autoload 重载后重复订阅。</summary>
	public override void _ExitTree()
	{
#if DEBUG
		if (_probeHandlersAttached)
		{
			AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
			TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
			_probeHandlersAttached = false;
		}
		lock (_eventProbeLock)
		{
			foreach (EventSubscription subscription in _eventSubscriptions.Values)
				RemoveEventSubscription(subscription);
			_eventSubscriptions.Clear();
		}
		_virtualMousePosition = null;
		_virtualMouseGracePosition = null;
#endif
	}

	/// <summary>运行会改变当前测试战局的三线/情报集成 QA，仅 DEBUG 可用。</summary>
	public string RunFrontlineQa()
	{
#if DEBUG
		if (!IsInsideTree())
			return "error: gateway outside scene tree";
		var combat = CombatManager.Instance;
		return combat == null ? "error: not in combat" : FrontlineRuntimeQa.Run(combat);
#else
		return "error: QA disabled in release build";
#endif
	}

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
	/// <param name="kind">快照种类：combat | player | enemy | board | ui_targets</param>
	/// <returns>结构化的 Godot Collections Dictionary/Variant</returns>
	public Variant GetSnapshot(string kind)
	{
#if DEBUG
		if (!IsInsideTree())
			return Variant.From("error: gateway outside scene tree");
		try
		{
			return kind switch
			{
				"combat" => GetCombatSnapshot(),
				"player" => GetPlayerSnapshot(),
				"enemy" => GetEnemySnapshot(),
				"board" => GetBoardSnapshot(),
				"ui_targets" => GetUiTargetsSnapshot(),
				_ => Variant.From($"error: unknown snapshot kind '{kind}'. Available: combat, player, enemy, board, ui_targets")
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
		if (root.Name.StartsWith("static:", StringComparison.Ordinal))
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
			case bool b:
				return Variant.From(b);
			case int i:
				return Variant.From(i);
			case long l:
				return Variant.From(l);
			case float f:
				return Variant.From(f);
			case double d:
				return Variant.From(d);
			case string s:
				return Variant.From(s);
		}

		// Godot 原生类型（直接返回，Godot C# 绑定会处理）
		if (value is Variant v)
			return v;
		if (value is Godot.Collections.Dictionary gdDict)
			return gdDict;
		if (value is Godot.Collections.Array gdArr)
			return gdArr;

		// Godot 数值类型
		if (value is Vector2 vec2)
			return Variant.From(vec2);
		if (value is Vector2I vec2I)
			return Variant.From(vec2I);
		if (value is Vector3 vec3)
			return Variant.From(vec3);
		if (value is Rect2 rect)
			return Variant.From(rect);
		if (value is Color col)
			return Variant.From(col);

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
			if (field.Name.StartsWith('_') || field.IsInitOnly && field.Name.Contains("k__BackingField"))
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
		for (int i = 0; i < Board.SupportCapacity; i++)
		{
			var minion = board.PlayerSlots[i];
			playerSlots.Add(minion == null ? default(Variant) : GetMinionSnapshot(minion));
		}

		var enemySlots = new Godot.Collections.Array();
		for (int i = 0; i < Board.SupportCapacity; i++)
		{
			var minion = board.EnemySlots[i];
			enemySlots.Add(minion == null ? default(Variant) : GetMinionSnapshot(minion));
		}

		var frontline = new Godot.Collections.Array();
		foreach (var minion in board.FrontlineSlots)
			frontline.Add(minion == null ? default(Variant) : GetMinionSnapshot(minion));
		return new Godot.Collections.Dictionary
		{
			["playerSlots"] = playerSlots,
			["enemySlots"] = enemySlots,
			["frontlineSlots"] = frontline,
			["frontlineOwner"] = board.FrontlineOwner.ToString(),
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
			["hasTaunt"] = minion.HasTaunt,
			["hasGuard"] = minion.HasGuard,
			["unitClass"] = minion.UnitClass.ToString(),
			["boardRow"] = minion.BoardRow?.ToString() ?? "OffBoard",
			["slotIndex"] = minion.BoardSlotIndex,
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

	// ===== Call / 事件探针 / UI 坐标目录 =====

	/// <summary>将空白或 null 参数归一化为空 JSON 数组。</summary>
	internal static string NormalizeArgsJson(string? argsJson)
	{
		if (string.IsNullOrWhiteSpace(argsJson))
			return "[]";
		string trimmed = argsJson.Trim();
		return trimmed.Equals("null", StringComparison.OrdinalIgnoreCase) ? "[]" : trimmed;
	}

	/// <summary>解析完整路径并返回最终对象，用于事件订阅等无需拆出方法名的场景。</summary>
	private object? ResolvePathValue(string pathExpr)
	{
		if (string.IsNullOrWhiteSpace(pathExpr))
			throw new ArgumentException("empty path expression", nameof(pathExpr));
		if (pathExpr.Length > MaxPathLength)
			throw new InvalidOperationException($"path too long (max {MaxPathLength})");
		List<PathSegment> segments = ParsePath(pathExpr);
		if (segments.Count == 0)
			throw new InvalidOperationException("invalid path");
		object? current = ResolveRoot(segments[0]);
		for (int i = 1; i < segments.Count; i++)
		{
			if (current == null)
				throw new InvalidOperationException($"null at segment '{segments[i - 1].Name}'");
			current = ResolveSegment(current, segments[i]);
		}
		return current;
	}

	/// <summary>解析路径表达式的“所有者 + 最后成员名”，用于方法调用与事件订阅。</summary>
	private (object? Owner, string MemberName) ResolveOwnerAndMember(string pathExpr)
	{
		if (string.IsNullOrWhiteSpace(pathExpr))
			throw new ArgumentException("empty path expression", nameof(pathExpr));
		if (pathExpr.Length > MaxPathLength)
			throw new InvalidOperationException($"path too long (max {MaxPathLength})");
		List<PathSegment> segments = ParsePath(pathExpr);
		if (segments.Count < 2)
			throw new InvalidOperationException("path must contain an owner and a member");
		PathSegment last = segments[^1];
		if (last.Index.HasValue || last.Key != null)
			throw new InvalidOperationException("method/event path cannot end with an index");
		object? current = ResolveRoot(segments[0]);
		for (int i = 1; i < segments.Count - 1; i++)
		{
			if (current == null)
				throw new InvalidOperationException($"null at segment '{segments[i - 1].Name}'");
			current = ResolveSegment(current, segments[i]);
		}
		return (current, last.Name);
	}

	/// <summary>从候选方法中挑选与参数个数/类型最匹配的重载。</summary>
	internal static MethodInfo? FindBestMethod(Type ownerType, string methodName, IReadOnlyList<Variant> arguments)
	{
		ArgumentNullException.ThrowIfNull(ownerType);
		ArgumentNullException.ThrowIfNull(arguments);
		var candidates = ownerType.GetMethods(ReflectFlags)
			.Where(method => method.Name == methodName && !method.IsGenericMethodDefinition)
			.Where(method =>
			{
				ParameterInfo[] parameters = method.GetParameters();
				if (parameters.Length < arguments.Count)
					return false;
				for (int i = arguments.Count; i < parameters.Length; i++)
				{
					if (!parameters[i].IsOptional)
						return false;
				}
				return true;
			})
			.OrderBy(method => method.GetParameters().Length)
			.ThenByDescending(method => method.IsStatic)
			.ToList();
		if (candidates.Count == 0)
			return null;

		return candidates
			.Select(method => (Method: method, Score: ScoreMethod(method, arguments)))
			.OrderByDescending(item => item.Score)
			.ThenBy(item => item.Method.GetParameters().Length)
			.First().Method;
	}

	private static int ScoreMethod(MethodInfo method, IReadOnlyList<Variant> arguments)
	{
		ParameterInfo[] parameters = method.GetParameters();
		int score = 0;
		for (int i = 0; i < arguments.Count; i++)
			score += ScoreVariantConversion(arguments[i], parameters[i].ParameterType);
		return score;
	}

	private static int ScoreVariantConversion(Variant value, Type parameterType)
	{
		Type target = Nullable.GetUnderlyingType(parameterType) ?? parameterType;
		if (target == typeof(Variant))
			return 6;
		if (target == typeof(object))
			return 3;
		if (value.VariantType == Variant.Type.Nil)
			return target.IsValueType ? -100 : 4;
		if (target == typeof(string))
			return value.VariantType is Variant.Type.String or Variant.Type.StringName or Variant.Type.NodePath ? 6 : 1;
		if (target == typeof(bool))
			return value.VariantType == Variant.Type.Bool ? 6 : 2;
		if (target.IsEnum)
			return value.VariantType is Variant.Type.Int or Variant.Type.String ? 5 : 0;
		if (IsNumeric(target))
			return value.VariantType is Variant.Type.Int or Variant.Type.Float ? 5 : 0;
		if (target == typeof(Godot.Collections.Array))
			return value.VariantType == Variant.Type.Array ? 7 : 0;
		if (target == typeof(Godot.Collections.Dictionary))
			return value.VariantType == Variant.Type.Dictionary ? 7 : 0;
		if (target == typeof(Vector2))
			return value.VariantType == Variant.Type.Vector2 ? 7 : 0;
		if (target == typeof(Vector2I))
			return value.VariantType == Variant.Type.Vector2I ? 7 : 0;
		if (target == typeof(Vector3))
			return value.VariantType == Variant.Type.Vector3 ? 7 : 0;
		if (target == typeof(Rect2))
			return value.VariantType == Variant.Type.Rect2 ? 7 : 0;
		if (target == typeof(Color))
			return value.VariantType == Variant.Type.Color ? 7 : 0;
		if (typeof(GodotObject).IsAssignableFrom(target))
			return value.VariantType == Variant.Type.Object ? 7 : 0;
		return 0;
	}

	private static bool IsNumeric(Type type)
	{
		return type == typeof(byte) || type == typeof(sbyte) || type == typeof(short) || type == typeof(ushort)
			|| type == typeof(int) || type == typeof(uint) || type == typeof(long) || type == typeof(ulong)
			|| type == typeof(float) || type == typeof(double) || type == typeof(decimal);
	}

	private Variant CallWithArguments(string pathExpr, Godot.Collections.Array args)
	{
		if (string.IsNullOrWhiteSpace(pathExpr))
			return Variant.From("error: empty path expression");
		if (pathExpr.Length > MaxPathLength)
			return Variant.From($"error: path too long (max {MaxPathLength})");
		(object? owner, string methodName) = ResolveOwnerAndMember(pathExpr);
		if (owner == null)
			return Variant.From("error: method owner is null");
		Type ownerType = owner is Type type ? type : owner.GetType();
		MethodInfo? method = FindBestMethod(ownerType, methodName, args);
		if (method == null)
			return Variant.From($"error: method '{methodName}' not found on {ownerType.Name} with {args.Count} argument(s)");

		ParameterInfo[] parameters = method.GetParameters();
		var converted = new object?[parameters.Length];
		for (int i = 0; i < parameters.Length; i++)
		{
			converted[i] = i < args.Count
				? ConvertCallArgument(args[i], parameters[i].ParameterType)
				: Type.Missing;
		}
		if (!method.IsStatic && owner is Type)
			return Variant.From($"error: method '{methodName}' is an instance method on static type");
		try
		{
			object? result = method.Invoke(method.IsStatic ? null : owner, converted);
			return ToVariant(result, 0);
		}
		catch (TargetInvocationException ex)
		{
			Exception inner = ex.InnerException ?? ex;
			return Variant.From($"error: {inner.GetType().Name}: {inner.Message}");
		}
	}

	/// <summary>
	/// 转换调用参数；JSON 对象 <c>{"$path":"CombatManager.Board.PlayerSlots[0]"}</c>
	/// 可解析为纯 C# 对象引用，用于给 Minion/Hero 等非 GodotObject 参数传引用。
	/// </summary>
	private object? ConvertCallArgument(Variant value, Type targetType)
	{
		if (value.VariantType == Variant.Type.Dictionary)
		{
			Godot.Collections.Dictionary dictionary = value.AsGodotDictionary();
			if (dictionary.ContainsKey("$path"))
			{
				string referencePath = dictionary["$path"].AsString();
				object? referenced = ResolvePathValue(referencePath);
				if (referenced == null)
					throw new InvalidOperationException($"argument reference resolved to null: '{referencePath}'");
				if (!targetType.IsInstanceOfType(referenced))
					throw new InvalidOperationException(
						$"argument reference '{referencePath}' is {referenced.GetType().Name}, not {targetType.Name}");
				return referenced;
			}
		}
		return ConvertVariantArgument(value, targetType);
	}

	/// <summary>将 Godot Variant 参数转换为反射目标类型；支持常用基础/Godot 类型与集合。</summary>
	internal static object? ConvertVariantArgument(Variant value, Type targetType)
	{
		ArgumentNullException.ThrowIfNull(targetType);
		Type target = Nullable.GetUnderlyingType(targetType) ?? targetType;
		if (targetType == typeof(Variant))
			return value;
		if (value.VariantType == Variant.Type.Nil)
			return targetType.IsValueType && Nullable.GetUnderlyingType(targetType) == null
				? throw new InvalidOperationException($"cannot pass null to {targetType.Name}")
				: null;
		if (target == typeof(Godot.Collections.Array))
			return value.AsGodotArray();
		if (target == typeof(Godot.Collections.Dictionary))
			return value.AsGodotDictionary();
		if (target == typeof(string))
			return value.AsString();
		if (target == typeof(bool))
			return value.AsBool();
		if (target.IsEnum)
		{
			if (value.VariantType == Variant.Type.String)
				return Enum.Parse(target, value.AsString(), ignoreCase: true);
			return Enum.ToObject(target, Convert.ToInt64(GetNumericValue(value), CultureInfo.InvariantCulture));
		}
		if (IsNumeric(target))
			return Convert.ChangeType(GetNumericValue(value), target, CultureInfo.InvariantCulture);
		if (target == typeof(Vector2))
			return value.AsVector2();
		if (target == typeof(Vector2I))
			return value.AsVector2I();
		if (target == typeof(Vector3))
			return value.AsVector3();
		if (target == typeof(Rect2))
			return value.AsRect2();
		if (target == typeof(Color))
			return value.AsColor();
		if (typeof(GodotObject).IsAssignableFrom(target))
		{
			GodotObject? godotObject = value.AsGodotObject();
			if (godotObject != null && target.IsInstanceOfType(godotObject))
				return godotObject;
			throw new InvalidOperationException($"cannot convert Variant to {target.Name}");
		}
		object? clrValue = VariantToClr(value);
		if (clrValue == null || target.IsInstanceOfType(clrValue))
			return clrValue;
		if (target == typeof(object))
			return clrValue;
		return Convert.ChangeType(clrValue, target, CultureInfo.InvariantCulture);
	}

	private static double GetNumericValue(Variant value)
	{
		return value.VariantType == Variant.Type.Int ? value.AsInt64() : value.AsDouble();
	}

	private static object? VariantToClr(Variant value)
	{
		switch (value.VariantType)
		{
			case Variant.Type.Nil:
				return null;
			case Variant.Type.Bool:
				return value.AsBool();
			case Variant.Type.Int:
				return value.AsInt64();
			case Variant.Type.Float:
				return value.AsDouble();
			case Variant.Type.String:
			case Variant.Type.StringName:
			case Variant.Type.NodePath:
				return value.AsString();
			case Variant.Type.Array:
			{
				Godot.Collections.Array array = value.AsGodotArray();
				var result = new List<object?>(array.Count);
				for (int i = 0; i < array.Count; i++)
					result.Add(VariantToClr(array[i]));
				return result;
			}
			case Variant.Type.Dictionary:
			{
				Godot.Collections.Dictionary dictionary = value.AsGodotDictionary();
				var result = new Dictionary<string, object?>(StringComparer.Ordinal);
				foreach (Variant key in dictionary.Keys)
					result[key.ToString()] = VariantToClr(dictionary[key]);
				return result;
			}
			case Variant.Type.Object:
				return value.AsGodotObject();
			default:
				return value;
		}
	}

	/// <summary>用表达式树为任意 Action/Action&lt;...&gt; 事件构造捕获委托。</summary>
	internal static Delegate BuildEventHandler(Type handlerType, Action<object?[]> capture)
	{
		ArgumentNullException.ThrowIfNull(handlerType);
		ArgumentNullException.ThrowIfNull(capture);
		MethodInfo? invoke = handlerType.GetMethod("Invoke");
		if (invoke == null || invoke.ReturnType != typeof(void))
			throw new ArgumentException("event handler must be a void delegate", nameof(handlerType));
		ParameterInfo[] invokeParameters = invoke.GetParameters();
		ParameterExpression[] parameters = invokeParameters
			.Select(parameter => Expr.Parameter(parameter.ParameterType, parameter.Name))
			.ToArray();
		NewArrayExpression arguments = Expr.NewArrayInit(typeof(object),
			parameters.Select(parameter => Expr.Convert(parameter, typeof(object))));
		var body = Expr.Invoke(Expr.Constant(capture), arguments);
		return Expr.Lambda(handlerType, body, parameters).Compile();
	}

	private void CaptureEvent(string subscription, string pathExpr, string eventName, object?[] args)
	{
		var snapshot = new Godot.Collections.Array();
		foreach (object? argument in args)
		{
			try
			{
				snapshot.Add(ToVariant(argument, 0));
			}
			catch (Exception ex)
			{
				snapshot.Add($"<snapshot error: {ex.GetType().Name}: {ex.Message}>");
			}
		}
		long sequence = Interlocked.Increment(ref _eventSequence);
		lock (_eventProbeLock)
			_eventProbes.Add(new EventProbeRecord(sequence, subscription, pathExpr, eventName, snapshot));
	}

	private void OnUnhandledException(object? sender, UnhandledExceptionEventArgs args)
	{
		Exception exception = args.ExceptionObject as Exception
			?? new InvalidOperationException(args.ExceptionObject?.ToString() ?? "unknown unhandled exception");
		RecordException(exception, $"AppDomain.UnhandledException(terminating={args.IsTerminating})");
	}

	private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs args)
	{
		RecordException(args.Exception, "TaskScheduler.UnobservedTaskException");
		args.SetObserved();
	}

	private void RecordException(Exception exception, string source)
	{
		long sequence = Interlocked.Increment(ref _exceptionSequence);
		var record = new ExceptionProbeRecord(
			sequence,
			source,
			exception.GetType().Name,
			exception.Message,
			exception.StackTrace ?? string.Empty,
			DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
		lock (_exceptionProbeLock)
			_exceptionProbes.Add(record);
	}

	private void RemoveEventSubscription(EventSubscription subscription)
	{
		try
		{
			subscription.EventInfo.RemoveEventHandler(subscription.Target, subscription.Handler);
		}
		catch (Exception ex)
		{
			GD.Print($"[EvalGateway] 事件退订失败 {subscription.Key}: {ex.Message}");
		}
	}

	/// <summary>UI 坐标目录：手牌、13 个棋盘槽、英雄身份卡、结束回合按钮的屏幕绝对矩形。</summary>
	private Variant GetUiTargetsSnapshot()
	{
		var result = new Godot.Collections.Dictionary
		{
			["screen"] = GetViewport().GetVisibleRect(),
			["handCards"] = new Godot.Collections.Array(),
			["boardSlots"] = new Godot.Collections.Array(),
			["enemyIdentityCards"] = new Godot.Collections.Array(),
		};
		Node? root = GetTree()?.Root;
		if (root == null)
			return result;

		HandUI? hand = FindFirstNode<HandUI>(root);
		if (hand != null)
		{
			result["hand"] = MakeUiTarget(hand, "hand");
			var cards = new Godot.Collections.Array();
			IReadOnlyList<CardUI> views = hand.GetHandCardViews();
			for (int i = 0; i < views.Count; i++)
			{
				CardUI view = views[i];
				if (!GodotObject.IsInstanceValid(view) || !view.IsVisibleInTree())
					continue;
				// 中心轴旋转后，GetGlobalRect 的原点/宽高不再代表真实可点位置。
				Transform2D transform = view.GetGlobalTransformWithCanvas();
				Rect2 bounds = new(transform.Origin, Vector2.Zero);
				bounds = bounds.Expand(transform * new Vector2(view.Size.X, 0f));
				bounds = bounds.Expand(transform * new Vector2(0f, view.Size.Y));
				bounds = bounds.Expand(transform * view.Size).Intersection(GetViewport().GetVisibleRect());
				Godot.Collections.Dictionary target = MakeUiTarget(view, $"hand.{i}");
				target["globalRect"] = RectToDictionary(bounds);
				target["center"] = bounds.GetCenter();
				target["index"] = i;
				target["cardId"] = view.Card?.Id ?? string.Empty;
				target["cardName"] = view.Card?.GetLocalizedName() ?? string.Empty;
				target["revealed"] = view.Card?.IsRevealedByIntel ?? false;
				cards.Add(target);
			}
			result["handCards"] = cards;
		}

		BoardUI? board = FindFirstNode<BoardUI>(root);
		if (board != null)
		{
			result["board"] = MakeUiTarget(board, "board");
			var slots = new Godot.Collections.Array();
			foreach (BoardRow row in Enum.GetValues<BoardRow>())
			{
				for (int slot = 0; slot < Board.GetRowCapacity(row); slot++)
				{
					var position = new BoardPosition(row, slot);
					BoardUI.BoardSlot? view = board.GetSlotView(position);
					if (view == null || !GodotObject.IsInstanceValid(view))
						continue;
					Godot.Collections.Dictionary target = MakeUiTarget(view, $"board.{row}.{slot}");
					target["row"] = row.ToString();
					target["slot"] = slot;
					target["occupyingCard"] = view.OccupyingMinion?.CardName ?? string.Empty;
					slots.Add(target);
				}
			}
			result["boardSlots"] = slots;
		}

		var enemyCards = new Godot.Collections.Array();
		foreach (EnemyIdentityCard card in FindAllNodes<EnemyIdentityCard>(root))
		{
			Godot.Collections.Dictionary target = MakeUiTarget(card, $"enemy.{card.EnemyIndex}");
			target["enemyIndex"] = card.EnemyIndex;
			enemyCards.Add(target);
		}
		result["enemyIdentityCards"] = enemyCards;

		PlayerIdentityCard? playerCard = FindFirstNode<PlayerIdentityCard>(root);
		if (playerCard != null)
			result["playerIdentityCard"] = MakeUiTarget(playerCard, "player");

		Button? endTurnButton = FindAllNodes<Button>(root).FirstOrDefault(button => button.Name == "EndTurnButton");
		if (endTurnButton != null)
			result["endTurnButton"] = MakeUiTarget(endTurnButton, "endTurnButton");

		EnemyHandBar? enemyHandBar = FindFirstNode<EnemyHandBar>(root);
		if (enemyHandBar != null)
			result["enemyHandBar"] = MakeUiTarget(enemyHandBar, "enemyHandBar");

		result["virtualMouse"] = new Godot.Collections.Dictionary
		{
			["active"] = HasVirtualMousePosition(),
			["position"] = GetVirtualMousePosition(),
		};
		return result;
	}

	private static Godot.Collections.Dictionary MakeUiTarget(Control control, string id)
	{
		Rect2 rect = control.GetGlobalRect();
		return new Godot.Collections.Dictionary
		{
			["id"] = id,
			["path"] = control.GetPath().ToString(),
			["visible"] = control.IsVisibleInTree(),
			["globalRect"] = RectToDictionary(rect),
			["center"] = new Vector2(rect.Position.X + rect.Size.X * 0.5f, rect.Position.Y + rect.Size.Y * 0.5f),
		};
	}

	private static Godot.Collections.Dictionary RectToDictionary(Rect2 rect)
	{
		return new Godot.Collections.Dictionary
		{
			["x"] = rect.Position.X,
			["y"] = rect.Position.Y,
			["width"] = rect.Size.X,
			["height"] = rect.Size.Y,
			["position"] = rect.Position,
			["size"] = rect.Size,
		};
	}

	private static T? FindFirstNode<T>(Node root) where T : Node
	{
		if (root is T match)
			return match;
		foreach (Node child in root.GetChildren())
		{
			T? found = FindFirstNode<T>(child);
			if (found != null)
				return found;
		}
		return null;
	}

	private static IEnumerable<T> FindAllNodes<T>(Node root) where T : Node
	{
		if (root is T match)
			yield return match;
		foreach (Node child in root.GetChildren())
		{
			foreach (T descendant in FindAllNodes<T>(child))
				yield return descendant;
		}
	}

	private sealed record EventSubscription(string Key, object? Target, EventInfo EventInfo, Delegate Handler);
	private sealed record EventProbeRecord(long Sequence, string Subscription, string Path, string EventName,
		Godot.Collections.Array Arguments);
	private sealed record ExceptionProbeRecord(long Sequence, string Source, string TypeName, string Message,
		string StackTrace, string TimestampUtc);

	/// <summary>固定容量环形缓冲；记录被覆盖的条目数，便于 QA 判断是否遗漏。</summary>
	internal sealed class ProbeBuffer<T>
	{
		private readonly Queue<T> _items = new();

		public ProbeBuffer(int capacity)
		{
			if (capacity <= 0)
				throw new ArgumentOutOfRangeException(nameof(capacity));
			Capacity = capacity;
		}

		public int Capacity { get; }
		public int Count => _items.Count;
		public long Dropped { get; private set; }

		public void Add(T item)
		{
			while (_items.Count >= Capacity)
			{
				_items.Dequeue();
				Dropped++;
			}
			_items.Enqueue(item);
		}

		public List<T> Snapshot() => new(_items);

		public void Clear() => _items.Clear();
	}
#endif
}
