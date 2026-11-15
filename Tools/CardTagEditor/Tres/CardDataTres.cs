using OdysseyCards.Tools.CardTagEditor.Schema;

namespace OdysseyCards.Tools.CardTagEditor.Tres;

/// <summary>
/// CardData .tres 文件的类型化访问器。
/// 封装对 TresDocument 中拥有字段的读写操作。
/// </summary>
public class CardDataTres
{
	private const int MinionType = 0;

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

	/// <summary>
	/// 随从兵种。旧随从资源未显式写出时默认 Vanguard（先锋，0）。
	/// 非零兵种会写回或更新 UnitClass 行；缺省先锋不新增冗余行。
	/// </summary>
	public int UnitClass
	{
		get => _doc.GetField("UnitClass")?.AsInt() ?? UnitClassValues.Vanguard;
		set
		{
			if (Type != MinionType)
				return;
			if (!UnitClassValues.IsValid(value))
				throw new ArgumentOutOfRangeException(nameof(value), value, "未知随从兵种。");

			var field = _doc.GetField("UnitClass");
			if (value == UnitClassValues.Vanguard && field == null)
				return;

			TresWriter.SetIntField(_doc, "UnitClass", value, insertAfterKey: "Type");
		}
	}

	/// <summary>随从返回兵种，非随从返回 null。</summary>
	public int? MinionUnitClass => Type == MinionType ? UnitClass : null;

	/// <summary>资源是否显式包含 UnitClass 行。</summary>
	public bool HasExplicitUnitClass => _doc.GetField("UnitClass") != null;

	/// <summary>旧 Speed 值；已清理或从未存在时返回 null。</summary>
	public int? LegacySpeed
	{
		get
		{
			var field = _doc.GetField("Speed");
			return field != null ? field.AsInt() : null;
		}
	}

	/// <summary>资源是否仍含旧 Speed 行。</summary>
	public bool HasLegacySpeed => _doc.GetField("Speed") != null;

	/// <summary>清理旧 Speed 行，不影响相邻属性。</summary>
	public void DeleteLegacySpeed()
	{
		_doc.GetField("Speed")?.Delete();
	}

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
