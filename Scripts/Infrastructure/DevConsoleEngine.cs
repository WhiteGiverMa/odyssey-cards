using System;
using System.Collections.Generic;
using System.Linq;

namespace OdysseyCards.Infrastructure;

/// <summary>
/// DevConsole 命令引擎 — 纯 C#，不依赖 Godot API。
/// 负责命令注册、执行调度、补全路由和历史管理。
/// </summary>
public class DevConsoleEngine
{
	private readonly Dictionary<string, DevConsoleCommand> _commands = new(StringComparer.OrdinalIgnoreCase);

	public IReadOnlyList<DevConsoleCommand> Commands => _commands.Values.ToList().AsReadOnly();

	public FixedSizedQueue<string> History { get; } = new(40);

	// ===== 注册 =====

	/// <summary>
	/// 注册命令。自动注册 Name 和所有 Aliases 到查找表。
	/// </summary>
	public void Register(DevConsoleCommand cmd)
	{
		_commands[cmd.Name] = cmd;
		foreach (var alias in cmd.Aliases)
			_commands[alias] = cmd;
	}

	// ===== 执行 =====

	/// <summary>
	/// 执行命令字符串。格式: "/&lt;action&gt; [参数]"。
	/// </summary>
	public CommandResult Execute(string input)
	{
		input = input.Trim();

		if (!input.StartsWith("/"))
			return CommandResult.Fail("命令需以 / 开头，输入 /help 查看帮助");

		var content = input[1..];
		var parts = content.Split(' ', StringSplitOptions.RemoveEmptyEntries);
		if (parts.Length == 0)
			return CommandResult.Fail("命令需以 / 开头，输入 /help 查看帮助");

		var action = parts[0];
		var args = parts.Skip(1).ToArray();

		// 历史记录
		History.Enqueue(input);

		if (!TryResolveCommand(action, out var cmd))
			return CommandResult.Fail($"未知命令: /{action}，输入 /help 查看帮助");

		try
		{
			return cmd.Execute(args);
		}
		catch (Exception e)
		{
			return CommandResult.Fail($"命令执行异常: {e.Message}");
		}
	}

	// ===== 补全 =====

	/// <summary>
	/// 根据当前输入生成补全候选列表。
	/// 分两阶段：命令名补全（空格前）、参数补全（空格后）。
	/// </summary>
	public List<CompletionCandidate> GetCompletions(string input)
	{
		var result = new List<CompletionCandidate>();

		if (string.IsNullOrEmpty(input) || !input.StartsWith("/"))
			return result;

		var content = input[1..];
		var spaceIdx = content.IndexOf(' ');
		var partialCmd = spaceIdx < 0 ? content.ToLowerInvariant() : content[..spaceIdx].ToLowerInvariant();
		var rawCommandToken = spaceIdx < 0 ? content : content[..spaceIdx];

		// 阶段 1：命令名补全（还没输入空格）
		if (spaceIdx < 0)
		{
			var uniqueByName = new Dictionary<string, DevConsoleCommand>(StringComparer.OrdinalIgnoreCase);
			foreach (var cmd in _commands.Values)
			{
				if (!uniqueByName.ContainsKey(cmd.Name))
					uniqueByName[cmd.Name] = cmd;
			}

			foreach (var cmd in uniqueByName.Values)
			{
				if (cmd.Name.StartsWith(partialCmd, StringComparison.OrdinalIgnoreCase) ||
					cmd.Aliases.Any(a => a.StartsWith(partialCmd, StringComparison.OrdinalIgnoreCase)))
				{
					var signatureTail = cmd.Signature.Contains(' ')
						? cmd.Signature[(cmd.Signature.IndexOf(' ') + 1)..]
						: "";
					var primaryText = $"/{cmd.Name} {signatureTail}".TrimEnd();
					var aliasStr = cmd.Aliases.Length > 0
						? $"（别名: {string.Join(", ", cmd.Aliases)}）"
						: "";
					result.Add(new CompletionCandidate(
						$"/{cmd.Name} ",
						primaryText,
						cmd.Description + aliasStr));
				}
			}

			return result.OrderBy(c => c.InsertText).Take(6).ToList();
		}

		// 阶段 2：参数补全（已输入空格）
		if (!TryResolveCommand(rawCommandToken, out var matchedCmd))
			return result;

		var argPart = content[(spaceIdx + 1)..].TrimStart();
		var argCandidates = matchedCmd.GetArgCandidates(argPart);

		if (argCandidates == null)
			return result;

		// 过滤并排序
		var filtered = argCandidates
			.Where(c => c.InsertText.Contains(argPart, StringComparison.OrdinalIgnoreCase))
			.OrderBy(c => c.InsertText)
			.Take(8)
			.ToList();

		foreach (var candidate in filtered)
		{
			// 确保 InsertText 带有完整的命令前缀
			if (!candidate.InsertText.StartsWith($"/{rawCommandToken} ", StringComparison.OrdinalIgnoreCase))
			{
				result.Add(new CompletionCandidate(
					$"/{rawCommandToken} {candidate.InsertText} ",
					candidate.PrimaryText,
					candidate.SecondaryText));
			}
			else
			{
				result.Add(candidate);
			}
		}

		return result;
	}

	// ===== 历史持久化 =====

	public void SaveHistory(string filePath)
	{
		History.Save(filePath);
	}

	public void LoadHistory(string filePath)
	{
		var loaded = FixedSizedQueue<string>.Load(filePath, 40);
		foreach (var item in loaded)
			History.Enqueue(item);
	}

	// ===== 内部 =====

	private bool TryResolveCommand(string token, out DevConsoleCommand command)
	{
		if (_commands.TryGetValue(token, out command!))
			return true;

		command = null!;
		return false;
	}
}
