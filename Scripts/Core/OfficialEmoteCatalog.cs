namespace OdysseyCards.Core;

/// <summary>
/// 内置的官方表情收藏集。
/// </summary>
public static class OfficialEmoteCatalog
{
	public const string PresetId = "official_collection";
	public const string DefaultPresetName = "星途卡牌官方表情收藏集";

	public static EmotePresetSaveData CreatePreset()
	{
		return new EmotePresetSaveData
		{
			Id = PresetId,
			Name = DefaultPresetName,
			Entries =
			[
				new EmotePresetEntrySaveData { Text = "阿姨快点啊阿姨", IsOfficialCollection = true },
				new EmotePresetEntrySaveData { Text = "给阿姨倒一杯卡布奇诺", IsOfficialCollection = true },
				new EmotePresetEntrySaveData { Text = "开始你的炸弹秀", IsOfficialCollection = true },
				new EmotePresetEntrySaveData { Text = "累啊累啊累", IsOfficialCollection = true },
				new EmotePresetEntrySaveData { Text = "我不停走在一条不回头的路", IsOfficialCollection = true },
				new EmotePresetEntrySaveData { Text = "请把我的歌带回你的家", IsOfficialCollection = true },
				new EmotePresetEntrySaveData { Text = "我们的关系进一步没资格", IsOfficialCollection = true },
				new EmotePresetEntrySaveData { Text = "真香", IsOfficialCollection = true },
				new EmotePresetEntrySaveData { Text = "还以为殉情只是古老的传言", IsOfficialCollection = true },
			],
		};
	}
}
