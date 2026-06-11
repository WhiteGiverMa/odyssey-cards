using Godot;
using System;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace OdysseyCards.Core;

/// <summary>
/// JSON 持久化管理器。
/// 负责将 GameSaveData 序列化到 user://save.json 并反序列化读取。
/// 支持 JSONC（带注释的 JSON）：读取时自动去除 // 和 /* */ 注释。
/// </summary>
public class SaveDataManager
{
	private const string SaveFileName = "user://save.json";

	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		WriteIndented = true,
		PropertyNameCaseInsensitive = true,
		AllowTrailingCommas = true,
	};

	/// <summary>
	/// 将存档数据序列化并写入磁盘。
	/// </summary>
	public bool Save(GameSaveData data)
	{
		try
		{
			string json = JsonSerializer.Serialize(data, JsonOptions);

			// Godot FileAccess 写入
			using var file = FileAccess.Open(SaveFileName, FileAccess.ModeFlags.Write);
			if (file == null)
			{
				GD.PushError($"[SaveDataManager] 无法打开 {SaveFileName} 进行写入");
				return false;
			}

			file.StoreString(json);
			file.Close();

			GD.Print($"[SaveDataManager] 存档已保存 — {GetSavePath()}");
			return true;
		}
		catch (Exception ex)
		{
			GD.PushError($"[SaveDataManager] 保存失败: {ex.Message}");
			return false;
		}
	}

	/// <summary>
	/// 从磁盘读取并反序列化存档数据。
	/// 如果文件不存在或解析失败，返回 null。
	/// </summary>
	public GameSaveData? Load()
	{
		try
		{
			if (!FileAccess.FileExists(SaveFileName))
			{
				GD.Print("[SaveDataManager] 存档文件不存在，返回 null");
				return null;
			}

			using var file = FileAccess.Open(SaveFileName, FileAccess.ModeFlags.Read);
			if (file == null)
			{
				GD.PushError($"[SaveDataManager] 无法打开 {SaveFileName} 进行读取");
				return null;
			}

			string rawJson = file.GetAsText();
			file.Close();

			// 去除 JSONC 注释
			string cleanJson = StripJsonComments(rawJson);

			var data = JsonSerializer.Deserialize<GameSaveData>(cleanJson, JsonOptions);
			if (data != null)
			{
				GD.Print($"[SaveDataManager] 存档已加载 — 牌组 {data.Decks.Count} 个，" +
						  $"已解锁 {data.OwnedCardIds.Count} 张卡牌");
			}
			return data;
		}
		catch (Exception ex)
		{
			GD.PushError($"[SaveDataManager] 加载失败: {ex.Message}");
			return null;
		}
	}

	/// <summary>
	/// 将指定牌组导出到文件对话框选择的路径。
	/// </summary>
	public bool ExportDeck(DeckSaveData deck, string exportPath)
	{
		try
		{
			string json = JsonSerializer.Serialize(deck, JsonOptions);
			using var file = FileAccess.Open(exportPath, FileAccess.ModeFlags.Write);
			if (file == null)
			{
				GD.PushError($"[SaveDataManager] 无法导出到 {exportPath}");
				return false;
			}

			file.StoreString(json);
			file.Close();

			GD.Print($"[SaveDataManager] 牌组「{deck.Name}」已导出到 {exportPath}");
			return true;
		}
		catch (Exception ex)
		{
			GD.PushError($"[SaveDataManager] 导出失败: {ex.Message}");
			return false;
		}
	}

	/// <summary>
	/// 从文件对话框选择的路径导入牌组。
	/// </summary>
	public DeckSaveData? ImportDeck(string importPath)
	{
		try
		{
			if (!FileAccess.FileExists(importPath))
			{
				GD.PushError($"[SaveDataManager] 导入文件不存在: {importPath}");
				return null;
			}

			using var file = FileAccess.Open(importPath, FileAccess.ModeFlags.Read);
			if (file == null)
			{
				GD.PushError($"[SaveDataManager] 无法打开导入文件: {importPath}");
				return null;
			}

			string rawJson = file.GetAsText();
			file.Close();

			string cleanJson = StripJsonComments(rawJson);
			var deck = JsonSerializer.Deserialize<DeckSaveData>(cleanJson, JsonOptions);

			if (deck != null)
			{
				GD.Print($"[SaveDataManager] 牌组「{deck.Name}」已从 {importPath} 导入");
			}
			return deck;
		}
		catch (Exception ex)
		{
			GD.PushError($"[SaveDataManager] 导入失败: {ex.Message}");
			return null;
		}
	}

	/// <summary>
	/// 获取 user://save.json 在文件系统中的绝对路径。
	/// </summary>
	public static string GetSavePath()
	{
		return ProjectSettings.GlobalizePath(SaveFileName);
	}

	/// <summary>
	/// 去除 JSON 中的单行注释（//）和多行注释（/* */）。
	/// 注意：不处理字符串内的注释标记。
	/// </summary>
	private static string StripJsonComments(string json)
	{
		// 移除单行注释 //
		json = Regex.Replace(json, @"//.*$", "", RegexOptions.Multiline);
		// 移除多行注释 /* */
		json = Regex.Replace(json, @"/\*.*?\*/", "", RegexOptions.Singleline);
		return json;
	}
}
