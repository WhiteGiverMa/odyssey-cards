using OdysseyCards.Tools.CardTagEditor.Schema;

namespace OdysseyCards.Tools.CardTagEditor.Tres;

/// <summary>
/// CardData .tres 文件的类型化访问器。
/// 封装对 TresDocument 中拥有字段的读写操作。
/// </summary>
public class CardDataTres
{
	private readonly TresDocument _doc;

	public CardDataTres(TresDocument doc)
	{
		_doc = doc;
	}

	/// <summary>卡牌唯一标识。</summary>
	public string Id => _doc.GetField("Id")?.AsString() ?? "";

	/// <summary>卡牌展示名。</summary>
	public string CardName => _doc.GetField("CardName")?.AsString() ?? "";

	/// <summary>卡牌类型：0=Minion, 1=Spell, 2=Domain。</summary>
	public int Type => _doc.GetField("Type")?.AsInt() ?? 0;

	/// <summary>机制标签（位掩码）。</summary>
	public int MechanicTags
	{
		get => _doc.GetField("MechanicTags")?.AsInt() ?? 0;
		set
		{
			var f = _doc.GetField("MechanicTags");
			if (f != null)
				f.SetInt(value);
		}
	}

	/// <summary>旧种族标签（迁移前有值，迁移后行消失）。</summary>
	public int? Tags
	{
		get
		{
			var f = _doc.GetField("Tags");
			return f != null ? f.AsInt() : null;
		}
	}

	/// <summary>关键词列表（int 数组）。</summary>
	public int[] Keywords
	{
		get
		{
			var f = _doc.GetField("Keywords");
			return f?.AsIntArray() ?? Array.Empty<int>();
		}
		set
		{
			var f = _doc.GetField("Keywords");
			if (f != null)
				f.SetIntArray(value);
		}
	}

	/// <summary>获取 MechanicTags 的位名列表。</summary>
	public List<string> GetMechanicTagNames() =>
		CardMechanicTagValues.ParseBits(MechanicTags);

	/// <summary>获取 Keywords 的关键词名列表。</summary>
	public List<string> GetKeywordNames() =>
		KeywordValues.ParseValues(Keywords);

	/// <summary>是否有 Tags 行（迁移前）。</summary>
	public bool HasTags => _doc.GetField("Tags") != null;

	/// <summary>删除 Tags 行（迁移后）。</summary>
	public void DeleteTags()
	{
		_doc.GetField("Tags")?.Delete();
	}

	/// <summary>将旧 Tags 位 OR-merge 到 MechanicTags（迁移操作）。</summary>
	/// <returns>是否实际产生了变更。</returns>
	public bool MigrateTags()
	{
		var tags = Tags;
		if (tags == null)
			return false; // 已迁移或无 Tags

		int oldMechanics = MechanicTags;
		int newBits = LegacyCardTagValues.MigrateBits(tags.Value);
		int merged = oldMechanics | newBits;

		if (merged == oldMechanics)
		{
			// 位已存在，只删除 Tags 行
			DeleteTags();
			return true; // 仍有变更（Tags 行被删除）
		}

		MechanicTags = merged;
		DeleteTags();
		return true;
	}
}
