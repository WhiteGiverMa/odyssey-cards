#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

namespace OdysseyCards.Core;

public partial class GameManager
{
	public event Action? OnEmotePresetsChanged;

	public List<EmotePresetSaveData> EmotePresets { get; private set; } = new();

	public string ActiveEmotePresetId { get; private set; } = OfficialEmoteCatalog.PresetId;

	public void EnsureEmotePresetsInitialized()
	{
		if (EmotePresets.Count == 0)
		{
			EmotePresets.Add(OfficialEmoteCatalog.CreatePreset());
			ActiveEmotePresetId = OfficialEmoteCatalog.PresetId;
			return;
		}

		if (EmotePresets.All(preset => preset.Id != OfficialEmoteCatalog.PresetId))
			EmotePresets.Insert(0, OfficialEmoteCatalog.CreatePreset());

		if (string.IsNullOrWhiteSpace(ActiveEmotePresetId) || EmotePresets.All(preset => preset.Id != ActiveEmotePresetId))
			ActiveEmotePresetId = EmotePresets[0].Id;
	}

	public EmotePresetSaveData? GetActiveEmotePreset()
	{
		EnsureEmotePresetsInitialized();
		return EmotePresets.FirstOrDefault(preset => preset.Id == ActiveEmotePresetId);
	}

	public IReadOnlyList<EmotePresetEntrySaveData> GetActiveEmoteEntries()
	{
		return GetActiveEmotePreset()?.Entries ?? [];
	}

	public IReadOnlyList<string> GetActiveEmoteTexts()
	{
		return GetActiveEmoteEntries()
			.Select(entry => entry.Text.Trim())
			.Where(text => !string.IsNullOrWhiteSpace(text))
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToList();
	}

	public bool IsOfficialCollectionEmote(string text)
	{
		if (string.IsNullOrWhiteSpace(text))
			return false;

		string normalized = text.Trim();
		return EmotePresets.Any(preset => preset.Entries.Any(entry =>
			entry.IsOfficialCollection &&
			string.Equals(entry.Text.Trim(), normalized, StringComparison.OrdinalIgnoreCase)));
	}

	public void SetActiveEmotePreset(string presetId)
	{
		if (string.IsNullOrWhiteSpace(presetId))
			return;

		if (EmotePresets.All(preset => preset.Id != presetId))
			return;

		if (ActiveEmotePresetId == presetId)
			return;

		ActiveEmotePresetId = presetId;
		SaveToDisk();
		OnEmotePresetsChanged?.Invoke();
	}

	public void RenameActiveEmotePreset(string name)
	{
		var preset = GetActiveEmotePreset();
		if (preset == null)
			return;

		string trimmed = string.IsNullOrWhiteSpace(name) ? "新表情组" : name.Trim();
		if (preset.Name == trimmed)
			return;

		preset.Name = trimmed;
		SaveToDisk();
		OnEmotePresetsChanged?.Invoke();
	}

	public void CreateEmotePreset(string name = "")
	{
		string baseName = string.IsNullOrWhiteSpace(name) ? "新表情组" : name.Trim();
		string finalName = baseName;
		int suffix = 2;
		while (EmotePresets.Any(preset => string.Equals(preset.Name, finalName, StringComparison.OrdinalIgnoreCase)))
		{
			finalName = $"{baseName} {suffix}";
			suffix++;
		}

		var preset = new EmotePresetSaveData
		{
			Name = finalName,
			Entries =
			[
				new EmotePresetEntrySaveData { Text = "", IsOfficialCollection = false },
			],
		};

		EmotePresets.Add(preset);
		ActiveEmotePresetId = preset.Id;
		SaveToDisk();
		OnEmotePresetsChanged?.Invoke();
	}

	public bool DeleteActiveEmotePreset()
	{
		EnsureEmotePresetsInitialized();
		if (EmotePresets.Count <= 1)
			return false;

		var preset = GetActiveEmotePreset();
		if (preset == null)
			return false;
		if (preset.Id == OfficialEmoteCatalog.PresetId)
			return false;

		EmotePresets.Remove(preset);
		ActiveEmotePresetId = EmotePresets[0].Id;
		SaveToDisk();
		OnEmotePresetsChanged?.Invoke();
		return true;
	}

	public void AddActiveEmoteEntry()
	{
		var preset = GetActiveEmotePreset();
		if (preset == null)
			return;

		preset.Entries.Add(new EmotePresetEntrySaveData());
		SaveToDisk();
		OnEmotePresetsChanged?.Invoke();
	}

	public void RemoveActiveEmoteEntry(int entryIndex)
	{
		var preset = GetActiveEmotePreset();
		if (preset == null)
			return;
		if (entryIndex < 0 || entryIndex >= preset.Entries.Count)
			return;

		preset.Entries.RemoveAt(entryIndex);
		if (preset.Entries.Count == 0)
			preset.Entries.Add(new EmotePresetEntrySaveData());

		SaveToDisk();
		OnEmotePresetsChanged?.Invoke();
	}

	public void UpdateActiveEmoteEntryText(int entryIndex, string text)
	{
		var preset = GetActiveEmotePreset();
		if (preset == null)
			return;
		if (entryIndex < 0 || entryIndex >= preset.Entries.Count)
			return;

		string trimmed = text.Trim();
		if (preset.Entries[entryIndex].Text == trimmed)
			return;

		preset.Entries[entryIndex].Text = trimmed;
		SaveToDisk();
		OnEmotePresetsChanged?.Invoke();
	}
}
