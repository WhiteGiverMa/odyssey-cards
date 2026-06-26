using System.Globalization;
using System.Text.Json;
using OdysseyCards.Tools.CardTagEditor.Services;
using OdysseyCards.Tools.CardTagEditor.Web;

namespace OdysseyCards.Tools.CardTagEditor;

/// <summary>
/// CardTagEditor CLI 入口——子命令分发。
/// 命令：list / dump / validate / migrate / serve / help。
/// 全局选项：--path &lt;dir&gt; 指定仓库根（默认自动检测）。
/// </summary>
public static class Program
{
	public static int Main(string[] args)
	{
		if (args.Length == 0)
		{
			PrintUsage();
			return 1;
		}

		// 解析全局 --path（可出现在任意位置）
		string? explicitPath = null;
		var cmdArgs = new List<string>();
		for (int i = 0; i < args.Length; i++)
		{
			if (args[i] == "--path" && i + 1 < args.Length)
			{
				explicitPath = args[i + 1];
				i++;
			}
			else
			{
				cmdArgs.Add(args[i]);
			}
		}

		if (cmdArgs.Count == 0)
		{
			PrintUsage();
			return 1;
		}

		var command = cmdArgs[0].ToLowerInvariant();
		var subArgs = cmdArgs.Skip(1).ToArray();

		try
		{
			return command switch
			{
				"list" => ListCommand(subArgs, explicitPath),
				"dump" => DumpCommand(subArgs, explicitPath),
				"validate" => ValidateCommand(subArgs, explicitPath),
				"migrate" => MigrateCommand(subArgs, explicitPath),
				"serve" => ServeCommand(subArgs, explicitPath),
				"help" or "--help" or "-h" => HelpCommand(),
				_ => UnknownCommand(command),
			};
		}
		catch (Exception ex)
		{
			Console.Error.WriteLine($"错误: {ex.Message}");
			return 1;
		}
	}

	// ===== list =====

	private static int ListCommand(string[] args, string? explicitPath)
	{
		var repo = CardTagService.DetectRepoRoot(explicitPath);
		var svc = new CardTagService(repo);

		Console.WriteLine("=== 卡牌列表 ===");
		var cards = svc.ListCards();
		foreach (var c in cards.OrderBy(c => c.Id, StringComparer.OrdinalIgnoreCase))
		{
			var typeName = c.Type switch
			{
				0 => "随从",
				1 => "法术",
				2 => "领域",
				_ => $"Type{c.Type}",
			};
			var tagNames = c.MechanicTagNames.Count > 0
				? string.Join(", ", c.MechanicTagNames)
				: "（无）";
			var kwNames = c.KeywordNames.Count > 0
				? string.Join(", ", c.KeywordNames)
				: "（无）";
			Console.WriteLine($"  {c.Id} — {c.CardName} [{typeName}]");
			Console.WriteLine($"    机制标签: {tagNames}");
			Console.WriteLine($"    关键词:   {kwNames}");
		}
		Console.WriteLine($"共 {cards.Count} 张卡牌。");

		Console.WriteLine();
		Console.WriteLine("=== 主题画像 ===");
		var themes = svc.ListThemes();
		foreach (var t in themes)
		{
			Console.WriteLine($"  {t.HeroId} — {t.ThemeName}");
			var tw = t.TagWeights;
			Console.WriteLine($"    TagWeights: {tw.Count} 项");
			foreach (var (k, v) in tw)
				Console.WriteLine($"      {k}: {v}");
			if (t.HasKeywordWeights)
			{
				var kw = t.KeywordWeights;
				Console.WriteLine($"    KeywordWeights: {kw.Count} 项");
				foreach (var (k, v) in kw)
					Console.WriteLine($"      {k}: {v}");
			}
			Console.WriteLine($"    CoreCardIds: {string.Join(", ", t.CoreCardIds)}");
		}
		Console.WriteLine($"共 {themes.Count} 个主题。");

		return 0;
	}

	// ===== dump =====

	private static int DumpCommand(string[] args, string? explicitPath)
	{
		if (args.Length == 0)
		{
			Console.Error.WriteLine("用法: dump <cardId|theme:heroId>");
			Console.Error.WriteLine("  dump minion_Mech_Lancer      — 转储单卡");
			Console.Error.WriteLine("  dump theme:ayame             — 转储单主题");
			return 1;
		}

		var repo = CardTagService.DetectRepoRoot(explicitPath);
		var svc = new CardTagService(repo);
		var target = args[0];

		var jsonOpts = new JsonSerializerOptions
		{
			WriteIndented = true,
			Encoder = System.Text.Encodings.Web.JavaScriptEncoder.Create(System.Text.Unicode.UnicodeRanges.All),
		};

		if (target.StartsWith("theme:", StringComparison.OrdinalIgnoreCase))
		{
			var heroId = target["theme:".Length..];
			var theme = svc.DumpTheme(heroId);
			if (theme == null)
			{
				Console.Error.WriteLine($"主题 '{heroId}' 不存在。");
				return 2;
			}
			var dto = new
			{
				HeroId = theme.HeroId,
				ThemeName = theme.ThemeName,
				TagWeights = theme.TagWeights,
				KeywordWeights = theme.HasKeywordWeights ? theme.KeywordWeights : null,
				CoreCardIds = theme.CoreCardIds,
				CardWeightOverrides = theme.CardWeightOverrides,
			};
			Console.WriteLine(JsonSerializer.Serialize(dto, jsonOpts));
			return 0;
		}
		else
		{
			var card = svc.DumpCard(target);
			if (card == null)
			{
				Console.Error.WriteLine($"卡牌 '{target}' 不存在。");
				return 2;
			}
			var dto = new
			{
				Id = card.Id,
				CardName = card.CardName,
				Type = card.Type,
				MechanicTags = card.MechanicTags,
				MechanicTagNames = card.GetMechanicTagNames(),
				Keywords = card.Keywords,
				KeywordNames = card.GetKeywordNames(),
				LegacyTags = card.Tags,
			};
			Console.WriteLine(JsonSerializer.Serialize(dto, jsonOpts));
			return 0;
		}
	}

	// ===== validate =====

	private static int ValidateCommand(string[] args, string? explicitPath)
	{
		var repo = CardTagService.DetectRepoRoot(explicitPath);
		var svc = new CardTagService(repo);
		var report = svc.Validate();

		foreach (var err in report.Errors)
			Console.Error.WriteLine($"[错误] {err}");
		foreach (var warn in report.Warnings)
			Console.WriteLine($"[警告] {warn}");

		if (report.HasErrors)
		{
			Console.Error.WriteLine($"校验失败：{report.Errors.Count} 错误, {report.Warnings.Count} 警告。");
			return 1;
		}
		Console.WriteLine($"校验通过：0 错误, {report.Warnings.Count} 警告。");
		return 0;
	}

	// ===== migrate =====

	private static int MigrateCommand(string[] args, string? explicitPath)
	{
		bool dryRun = args.Contains("--dry-run") || args.Contains("-n");
		var repo = CardTagService.DetectRepoRoot(explicitPath);
		var svc = new CardTagService(repo);

		Console.WriteLine(dryRun ? "（dry-run 模式，不写文件）" : "执行迁移...");
		var result = svc.Migrate(dryRun);

		if (result.ChangeCount == 0)
		{
			Console.WriteLine("没有需要迁移的卡牌（所有卡牌已迁移或无 Tags 字段）。");
			return 0;
		}

		foreach (var c in result.Changes)
		{
			Console.WriteLine($"  {c.FileName} ({c.CardId}): " +
				$"Tags={c.OldTags} + MechanicTags={c.OldMechanicTags} → MechanicTags={c.NewMechanicTags}");
		}
		Console.WriteLine($"共迁移 {result.ChangeCount} 张卡牌{(dryRun ? "（dry-run，未写盘）" : "，已写盘")}。");
		return 0;
	}

	// ===== serve =====

	private static int ServeCommand(string[] args, string? explicitPath)
	{
		int port = 8765;
		for (int i = 0; i < args.Length; i++)
		{
			if ((args[i] == "--port" || args[i] == "-p") && i + 1 < args.Length
				&& int.TryParse(args[i + 1], NumberStyles.None, CultureInfo.InvariantCulture, out var p))
			{
				port = p;
				i++;
			}
		}

		var repo = CardTagService.DetectRepoRoot(explicitPath);
		var svc = new CardTagService(repo);
		var prefix = $"http://localhost:{port}/";
		using var server = new WebServer(svc, prefix);

		// Ctrl+C 优雅关闭
		using var cts = new CancellationTokenSource();
		Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); server.Stop(); };

		Console.WriteLine($"CardTagEditor Web UI 启动中...");
		Console.WriteLine($"  仓库: {repo}");
		Console.WriteLine($"  地址: {prefix}");
		Console.WriteLine($"  按 Ctrl+C 停止。");
		Console.WriteLine();

		// 阻塞等待服务器关闭——StartAsync 内部循环直到取消
		server.StartAsync(cts.Token).GetAwaiter().GetResult();
		Console.WriteLine("服务器已停止。");
		return 0;
	}

	// ===== help =====

	private static int HelpCommand()
	{
		PrintUsage();
		return 0;
	}

	private static int UnknownCommand(string cmd)
	{
		Console.Error.WriteLine($"未知命令: {cmd}");
		PrintUsage();
		return 1;
	}

	private static void PrintUsage()
	{
		Console.WriteLine("CardTagEditor — OdysseyCards 卡牌标签编辑器");
		Console.WriteLine();
		Console.WriteLine("用法: CardTagEditor <command> [options]");
		Console.WriteLine();
		Console.WriteLine("命令:");
		Console.WriteLine("  list                    列出所有卡牌/主题摘要");
		Console.WriteLine("  dump <target>           转储单卡或单主题完整数据（JSON）");
		Console.WriteLine("                            target = cardId 或 theme:heroId");
		Console.WriteLine("  validate                校验所有卡牌数据（未知位/越界关键词/悬空引用）");
		Console.WriteLine("  migrate [--dry-run]     执行 Tags→MechanicTags 迁移（幂等）");
		Console.WriteLine("  serve [--port 8765]     启动 Web UI 服务器");
		Console.WriteLine("  help                    显示帮助");
		Console.WriteLine();
		Console.WriteLine("全局选项:");
		Console.WriteLine("  --path <dir>            指定仓库根目录（默认自动检测）");
	}
}
