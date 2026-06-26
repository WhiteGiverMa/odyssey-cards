using OdysseyCards.Tools.CardTagEditor.Schema;
using OdysseyCards.Tools.CardTagEditor.Tres;

namespace OdysseyCards.Tools.CardTagEditor.Services;

/// <summary>
/// 卡牌标签服务——用例层，CLI 和 Web 共用的业务逻辑。
/// </summary>
public class CardTagService
{
	private readonly string _repoRoot;

	public CardTagService(string repoRoot)
	{
		_repoRoot = repoRoot;
		if (!File.Exists(Path.Combine(_repoRoot, "project.godot")))
			throw new DirectoryNotFoundException($"未找到 project.godot：{_repoRoot}");
	}

	/// <summary>自动检测仓库根（从当前目录向上找 project.godot）。</summary>
	public static string DetectRepoRoot(string? explicitPath = null)
	{
		if (explicitPath != null && File.Exists(Path.Combine(explicitPath, "project.godot")))
			return Path.GetFullPath(explicitPath);

		var dir = explicitPath ?? Environment.CurrentDirectory;
		dir = Path.GetFullPath(dir);

		while (dir != null)
		{
			if (File.Exists(Path.Combine(dir, "project.godot")))
				return dir;
			var parent = Path.GetDirectoryName(dir);
			if (parent == dir) break;
			dir = parent!;
		}

		throw new DirectoryNotFoundException("未找到 project.godot。请从 OdysseyCards 仓库目录运行，或使用 --path 指定。");
	}

	private string CardsDir => Path.Combine(_repoRoot, "Resources", "Cards");
	private string ThemesDir => Path.Combine(_repoRoot, "Resources", "Themes");

	// ===== 列表 =====

	/// <summary>卡牌摘要记录。</summary>
	public record CardSummary(string Id, string CardName, int Type, int MechanicTags,
		List<string> MechanicTagNames, int[] Keywords, List<string> KeywordNames);

	/// <summary>列出所有卡牌摘要。</summary>
	public List<CardSummary> ListCards()
	{
		var result = new List<CardSummary>();
		foreach (var file in Directory.GetFiles(CardsDir, "*.tres"))
		{
			var doc = TresParser.Parse(file);
			var card = new CardDataTres(doc);
			result.Add(new CardSummary(
				card.Id, card.CardName, card.Type, card.MechanicTags,
				card.GetMechanicTagNames(), card.Keywords, card.GetKeywordNames()));
		}
		return result;
	}

	/// <summary>列出所有主题摘要。</summary>
	public List<ThemeProfileTres> ListThemes()
	{
		var result = new List<ThemeProfileTres>();
		foreach (var file in Directory.GetFiles(ThemesDir, "*.tres"))
		{
			var doc = TresParser.Parse(file);
			result.Add(new ThemeProfileTres(doc));
		}
		return result;
	}

	// ===== 详情转储 =====

	/// <summary>单卡完整数据。</summary>
	public CardDataTres? DumpCard(string id)
	{
		foreach (var file in Directory.GetFiles(CardsDir, "*.tres"))
		{
			var doc = TresParser.Parse(file);
			var card = new CardDataTres(doc);
			if (card.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
				return card;
		}
		return null;
	}

	/// <summary>单主题完整数据。</summary>
	public ThemeProfileTres? DumpTheme(string heroId)
	{
		foreach (var file in Directory.GetFiles(ThemesDir, "*.tres"))
		{
			var doc = TresParser.Parse(file);
			var theme = new ThemeProfileTres(doc);
			if (theme.HeroId.Equals(heroId, StringComparison.OrdinalIgnoreCase))
				return theme;
		}
		return null;
	}

	// ===== 校验 =====

	/// <summary>校验报告。</summary>
	public class ValidationReport
	{
		public List<string> Errors { get; } = new();
		public List<string> Warnings { get; } = new();
		public bool HasErrors => Errors.Count > 0;
	}

	/// <summary>校验所有卡牌数据。</summary>
	public ValidationReport Validate()
	{
		var report = new ValidationReport();
		var allCardIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		// 收集所有卡牌 ID
		foreach (var file in Directory.GetFiles(CardsDir, "*.tres"))
		{
			var doc = TresParser.Parse(file);
			var card = new CardDataTres(doc);
			if (string.IsNullOrEmpty(card.Id)) continue;
			allCardIds.Add(card.Id);
		}

		foreach (var file in Directory.GetFiles(CardsDir, "*.tres"))
		{
			var doc = TresParser.Parse(file);
			var card = new CardDataTres(doc);
			var fileName = Path.GetFileName(file);

			// 校验 MechanicTags
			if (!CardMechanicTagValues.IsValidMask(card.MechanicTags))
			{
				int unknown = card.MechanicTags & ~CardMechanicTagValues.AllValidBits;
				report.Errors.Add(
					$"{fileName}: MechanicTags 含未知位 0x{unknown:X}（完整值={card.MechanicTags}）");
			}

			// 校验 Keywords
			foreach (var kw in card.Keywords)
			{
				if (!KeywordValues.IsValid(kw))
					report.Errors.Add($"{fileName}: Keywords 含越界值 {kw}");
			}

			// 校验空 Id
			if (string.IsNullOrEmpty(card.Id))
				report.Warnings.Add($"{fileName}: Id 为空");
		}

		// 校验 ThemeProfile 引用
		foreach (var file in Directory.GetFiles(ThemesDir, "*.tres"))
		{
			var doc = TresParser.Parse(file);
			var theme = new ThemeProfileTres(doc);
			var fileName = Path.GetFileName(file);

			foreach (var cardId in theme.CoreCardIds)
			{
				if (!allCardIds.Contains(cardId))
					report.Warnings.Add($"{fileName}: CoreCardIds 引用了不存在的卡牌 '{cardId}'");
			}
		}

		return report;
	}

	// ===== 迁移 =====

	/// <summary>迁移变更记录。</summary>
	public record MigrateChange(string FileName, string CardId, int OldTags, int OldMechanicTags,
		int NewMechanicTags);

	/// <summary>迁移结果。</summary>
	public record MigrateResult(List<MigrateChange> Changes, bool DryRun)
	{
		public int ChangeCount => Changes.Count;
	}

	/// <summary>
	/// 执行 Tags → MechanicTags 迁移。
	/// 幂等：已迁移的再跑 = 0 changes。
	/// </summary>
	public MigrateResult Migrate(bool dryRun = false)
	{
		var changes = new List<MigrateChange>();

		foreach (var file in Directory.GetFiles(CardsDir, "*.tres"))
		{
			var fileName = Path.GetFileName(file);
			var doc = TresParser.Parse(file);
			var card = new CardDataTres(doc);

			if (!card.HasTags) continue; // 已迁移或无 Tags

			var oldTags = card.Tags ?? 0;
			var oldMechanicTags = card.MechanicTags;
			card.MigrateTags();
			var newMechanicTags = card.MechanicTags;

			changes.Add(new MigrateChange(fileName, card.Id, oldTags, oldMechanicTags, newMechanicTags));

			if (!dryRun)
				TresWriter.WriteToFile(doc, file);
		}

		return new MigrateResult(changes, dryRun);
	}

	// ===== 保存 =====

	/// <summary>保存单卡（只写 MechanicTags + Keywords）。</summary>
	public void SaveCard(string id, int mechanicTags, int[] keywords)
	{
		var filePath = FindCardFile(id);
		if (filePath == null)
			throw new FileNotFoundException($"卡牌 '{id}' 不存在。");

		var doc = TresParser.Parse(filePath);
		var card = new CardDataTres(doc);
		card.MechanicTags = mechanicTags;
		card.Keywords = keywords;
		TresWriter.WriteToFile(doc, filePath);
	}

	/// <summary>保存主题。</summary>
	public void SaveTheme(string heroId, Dictionary<int, int> tagWeights,
		Dictionary<int, int>? keywordWeights, string[] coreCardIds,
		Dictionary<string, int>? cardWeightOverrides)
	{
		var filePath = FindThemeFile(heroId);
		if (filePath == null)
			throw new FileNotFoundException($"主题 '{heroId}' 不存在。");

		var doc = TresParser.Parse(filePath);
		var theme = new ThemeProfileTres(doc);
		theme.TagWeights = tagWeights;
		if (keywordWeights != null && theme.HasKeywordWeights)
			theme.KeywordWeights = keywordWeights;
		theme.CoreCardIds = coreCardIds;
		// CardWeightOverrides 暂不修改
		TresWriter.WriteToFile(doc, filePath);
	}

	// ===== 内部辅助 =====

	private string? FindCardFile(string id)
	{
		foreach (var file in Directory.GetFiles(CardsDir, "*.tres"))
		{
			var doc = TresParser.Parse(file);
			var card = new CardDataTres(doc);
			if (card.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
				return file;
		}
		return null;
	}

	private string? FindThemeFile(string heroId)
	{
		foreach (var file in Directory.GetFiles(ThemesDir, "*.tres"))
		{
			var doc = TresParser.Parse(file);
			var theme = new ThemeProfileTres(doc);
			if (theme.HeroId.Equals(heroId, StringComparison.OrdinalIgnoreCase))
				return file;
		}
		return null;
	}
}
