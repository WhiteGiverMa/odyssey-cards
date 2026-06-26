using System;
using System.Collections.Generic;
using Godot;
using Timer = Godot.Timer;

namespace OdysseyCards.Combat;

/// <summary>
/// 表情系统——管理敌人嘲讽表情的定时触发。
/// 从 CombatManager 拆出为独立 Node，在 _Ready 中作为子节点添加。
/// </summary>
public partial class EmoteSystem : Node
{
	public static float EmoteIdleBaseTime = 15f; // 基础空闲时间（秒）

	private Timer _emoteIdleTimer;
	private Random _emoteRng = new();
	private List<string> _tauntPool = new();

	public event Action<CombatEmoteMessage>? OnEmote;

	public override void _Ready()
	{
		_emoteIdleTimer = new Timer
		{
			Name = "EmoteIdleTimer",
			OneShot = true,
			Autostart = false,
		};
		AddChild(_emoteIdleTimer);
		_emoteIdleTimer.Timeout += OnEmoteIdleTimeout;

		// 硬编码嘲讽词库（未来可从 GameManager.EnemyTauntPool 配置读取）
		_tauntPool = new List<string>
		{
			"阿姨快点啊阿姨",
			"给阿姨倒一杯卡布奇诺",
			"开始你的炸弹秀",
		};
	}

	public void ResetIdleTimer()
	{
		_emoteIdleTimer.Stop();
		_emoteIdleTimer.WaitTime = EmoteIdleBaseTime + (float)_emoteRng.NextDouble() * 10f;
		_emoteIdleTimer.Start();
	}

	public void StartIdleTimer()
	{
		_emoteIdleTimer.WaitTime = EmoteIdleBaseTime + (float)_emoteRng.NextDouble() * 10f;
		_emoteIdleTimer.Start();
	}

	public void StopIdleTimer()
	{
		_emoteIdleTimer.Stop();
	}

	private void OnEmoteIdleTimeout()
	{
		if (_tauntPool.Count == 0)
			return;
		string emote = _tauntPool[_emoteRng.Next(_tauntPool.Count)];
		SendEnemyEmote(emote);
	}

	public void SendEnemyEmote(string text, int enemyIndex = 0, bool isOfficialCollection = false)
	{
		if (string.IsNullOrWhiteSpace(text))
			return;

		OnEmote?.Invoke(new CombatEmoteMessage(
			text.Trim(),
			CombatEmoteSpeaker.Enemy,
			enemyIndex,
			isOfficialCollection));
	}
}
