#nullable enable
using System;
using System.Collections.Generic;
using Godot;
using OdysseyCards.UI;

#pragma warning disable IDE0130 // 命名空间按模块组织，不包含 Scripts 物理目录。
namespace OdysseyCards.Audio;
#pragma warning restore IDE0130

/// <summary>主菜单和战斗共享的短音播放服务；波形计算、音量存储和表现时机均在外部。</summary>
public sealed partial class SfxManager : Node
{
	private const int _voiceCount = 10;
	private const int _variantCount = 4;
	private static readonly StringName _busName = new("CombatSfx");

	private readonly List<Voice> _voices = new(_voiceCount);
	private readonly Dictionary<CombatSfxCue, SoundBank> _banks = new();
	private readonly Dictionary<DeploySfxForm, SoundBank> _deployBanks = new();
	private readonly ulong[] _nextCueTime = new ulong[Enum.GetValues<CombatSfxCue>().Length];
	private UIScaler? _settings;
	private int _bus = -1;
	private float _volume;
	private ulong _nextPreviewTime;
	private bool _available;

	internal static SfxManager? Instance { get; private set; }

	public override void _Ready()
	{
		Instance = this;
		// 暂停菜单里的音量试听也要正常播放。
		ProcessMode = ProcessModeEnum.Always;
		_settings = UIScaler.Instance;
		if (_settings != null)
		{
			_settings.OnSfxVolumeChanged += ApplyVolume;
		}

		try
		{
			_available = AudioServer.GetDriverName() is not "Dummy";
			if (_available)
			{
				PreparePlayback();
			}
			ApplyVolume(_settings?.SfxVolume ?? UIScaler.DefaultSfxVolume);
		}
		catch (Exception error)
		{
			DisableAudio(error);
		}
	}

	public override void _ExitTree()
	{
		_available = false;
		if (_settings != null)
		{
			_settings.OnSfxVolumeChanged -= ApplyVolume;
			_settings = null;
		}
		if (Instance == this)
		{
			Instance = null;
		}
	}

	internal void PlayUnitDeploySlam(float intensity)
	{
		float weight = ProceduralSfxMath.ClampImpact(intensity);
		DeploySfxForm form = weight switch
		{
			< 0.25f => DeploySfxForm.Light,
			< 0.75f => DeploySfxForm.Medium,
			_ => ProceduralSfxMath.DefaultHeavyForm,
		};
		_ = Play(CombatSfxCue.UnitDeploySlam, form: form);
	}

	internal void PlayCardDrawSlide() => Play(CombatSfxCue.CardDrawSlide);
	internal void PlayCardHoverTick() => Play(CombatSfxCue.CardHoverTick);
	internal void PlayCardSelectClick() => Play(CombatSfxCue.CardSelectClick);
	internal void PlayOrderCast() => Play(CombatSfxCue.OrderCast);
	internal void PlayUnitMoveAdvance() => Play(CombatSfxCue.UnitMoveAdvance);
	internal void PreviewVolume() => Play(CombatSfxCue.CardSelectClick, preview: true);
	internal bool PreviewCue(CombatSfxCue cue, DeploySfxForm form = DeploySfxForm.Medium)
	{
		return Play(cue, preview: true, form: form);
	}

	private void ApplyVolume(float volume)
	{
		_volume = volume;
		if (!_available)
		{
			return;
		}

		try
		{
			if (_bus < 0 || !IsInstanceValid(this) || IsQueuedForDeletion())
			{
				return;
			}
			bool muted = volume <= ProceduralSfxMath.MuteThreshold;
			AudioServer.SetBusVolumeDb(_bus, ProceduralSfxMath.VolumeToDb(volume));
			AudioServer.SetBusMute(_bus, muted);
		}
		catch (Exception error)
		{
			DisableAudio(error);
		}
	}

	private void PreparePlayback()
	{
		_bus = AudioServer.GetBusIndex(_busName);
		if (_bus < 0)
		{
			AudioServer.AddBus();
			_bus = AudioServer.BusCount - 1;
			AudioServer.SetBusName(_bus, _busName);
			// 单个声音留有余量，限制器只兜底极端同帧叠加，不抬高平时的响度。
			using var limiter = new AudioEffectHardLimiter { CeilingDb = -1.2f, Release = 0.05f };
			AudioServer.AddBusEffect(_bus, limiter);
		}
		foreach (CombatSfxCue cue in Enum.GetValues<CombatSfxCue>())
		{
			if (cue != CombatSfxCue.UnitDeploySlam)
			{
				_banks.Add(cue, BakeBank(cue.ToString(), cue, seed => ProceduralSfxMath.Synthesize(cue, seed)));
			}
		}
		foreach (DeploySfxForm form in Enum.GetValues<DeploySfxForm>())
		{
			const CombatSfxCue cue = CombatSfxCue.UnitDeploySlam;
			_deployBanks.Add(form, BakeBank($"{cue}.{form}", cue, seed => ProceduralSfxMath.Synthesize(form, seed)));
		}
		for (int i = 0; i < _voiceCount; i++)
		{
			var player = new AudioStreamPlayer { Name = $"SfxVoice{i + 1}", Bus = _busName };
			AddChild(player);
			_voices.Add(new Voice(player));
		}
	}

	private bool Play(CombatSfxCue cue, bool preview = false, DeploySfxForm form = DeploySfxForm.Medium)
	{
		if (!_available || _bus < 0 || _volume <= ProceduralSfxMath.MuteThreshold)
		{
			return false;
		}
		try
		{
			if (!IsInstanceValid(this) || !IsInsideTree() || IsQueuedForDeletion())
			{
				return false;
			}
			ulong now = Time.GetTicksMsec();
			if (now < (preview ? _nextPreviewTime : _nextCueTime[(int)cue]))
			{
				return false;
			}
			(ulong cooldown, int priority) = cue switch
			{
				CombatSfxCue.CardHoverTick => (75ul, 0),
				CombatSfxCue.CardSelectClick => (40ul, 1),
				CombatSfxCue.CardDrawSlide => (0ul, 1),
				CombatSfxCue.UnitMoveAdvance => (35ul, 2),
				CombatSfxCue.OrderCast => (45ul, 3),
				CombatSfxCue.UnitDeploySlam => (0ul, 3),
				_ => throw new ArgumentOutOfRangeException(nameof(cue), cue, null),
			};
			Voice? voice = FindVoice(priority);
			if (voice == null)
			{
				return false;
			}

			AudioStreamPlayer player = voice.Player;
			SoundBank bank = cue == CombatSfxCue.UnitDeploySlam ? _deployBanks[form] : _banks[cue];
			player.Stream = bank.PickVariant();
			player.PitchScale = preview ? 1f : (Random.Shared.NextSingle() * 0.04f) + 0.98f;
			player.Play();
			voice.Priority = priority;
			voice.StartedAt = now;
			if (preview)
			{
				_nextPreviewTime = now + 200;
			}
			else
			{
				_nextCueTime[(int)cue] = now + cooldown;
			}
			return true;
		}
		catch (Exception error)
		{
			DisableAudio(error);
			return false;
		}
	}

	private Voice? FindVoice(int priority)
	{
		Voice? replacement = null;
		foreach (Voice voice in _voices)
		{
			if (!IsInstanceValid(voice.Player) || voice.Player.IsQueuedForDeletion())
			{
				continue;
			}
			if (!voice.Player.Playing)
			{
				return voice;
			}
			// 池满时不让悬停等小反馈抢掉拍桌，正式行动可以替换更轻、更早的声音。
			if (voice.Priority < priority && (replacement == null || voice.Priority < replacement.Priority
				|| (voice.Priority == replacement.Priority && voice.StartedAt < replacement.StartedAt)))
			{
				replacement = voice;
			}
		}
		return replacement;
	}

	private static SoundBank BakeBank(string name, CombatSfxCue cue, Func<int, byte[]> synthesize)
	{
		var streams = new AudioStreamWav[_variantCount];
		for (int i = 0; i < streams.Length; i++)
		{
			int seed = unchecked((int)(0x85EBCA6Bu ^ (0x9E3779B9u * (uint)((((int)cue) * _variantCount) + i + 1))));
			streams[i] = new AudioStreamWav
			{
				ResourceName = name,
				Format = AudioStreamWav.FormatEnum.Format16Bits,
				MixRate = ProceduralSfxMath.SampleRate,
				Stereo = false,
				Data = synthesize(seed),
			};
		}
		return new SoundBank(streams);
	}

	private void DisableAudio(Exception error)
	{
		_available = false;
		GD.PushWarning($"[SfxManager] 战斗音效已停用：{error.Message}");
	}

	private sealed class Voice(AudioStreamPlayer player)
	{
		public AudioStreamPlayer Player { get; } = player;
		public int Priority { get; set; }
		public ulong StartedAt { get; set; }
	}

	private sealed class SoundBank(AudioStreamWav[] variants)
	{
		private int _lastIndex = -1;

		public AudioStreamWav PickVariant()
		{
			// 不连续重复同一波形，除此之外保持等概率选择。
			int index = Random.Shared.Next(_lastIndex < 0 ? variants.Length : variants.Length - 1);
			if (_lastIndex >= 0 && index >= _lastIndex)
			{
				index++;
			}
			_lastIndex = index;
			return variants[index];
		}
	}
}
