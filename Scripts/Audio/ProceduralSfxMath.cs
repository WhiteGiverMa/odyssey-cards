using System;
using System.Buffers.Binary;

#pragma warning disable IDE0130 // 命名空间按模块组织，不包含 Scripts 物理目录。
namespace OdysseyCards.Audio;
#pragma warning restore IDE0130

/// <summary>由实际表现时机触发的战斗音效，不表示规则状态。</summary>
internal enum CombatSfxCue
{
	UnitDeploySlam,
	CardDrawSlide,
	CardHoverTick,
	CardSelectClick,
	OrderCast,
	UnitMoveAdvance,
}

/// <summary>在内存中合成确定性的 44.1 kHz 单声道 16 位小端 PCM，不依赖引擎或素材。</summary>
internal static class ProceduralSfxMath
{
	public const int SampleRate = 44100;
	public const float StandardImpact = 0.5f;
	public const float MinVolume = 0f;
	public const float MaxVolume = 1f;
	public const float MuteThreshold = 0.001f;

	/// <summary>无效音量按静音处理，有穷数值限制为线性增益范围。</summary>
	public static float ClampVolume(float volume) => float.IsNaN(volume) ? MinVolume : Math.Clamp(volume, MinVolume, MaxVolume);

	/// <summary>有效音量换算为分贝；静音另由播放端处理，不向引擎传入负无穷。</summary>
	public static float VolumeToDb(float volume) => 20f * MathF.Log10(MathF.Max(ClampVolume(volume), MuteThreshold));

	/// <summary>拍桌力度是连续的 0（轻）到 1（重）；未指定或无效时用标准力度。</summary>
	public static float ClampImpact(float intensity) => float.IsNaN(intensity) ? StandardImpact : Math.Clamp(intensity, 0f, 1f);

	public static byte[] Synthesize(CombatSfxCue cue, int seed, float intensity = StandardImpact)
	{
		float[] samples = SynthesizeSamples(cue, seed, intensity);
		byte[] pcm = new byte[samples.Length * sizeof(short)];
		for (int i = 0; i < samples.Length; i++)
		{
			short sample = (short)MathF.Round(samples[i] * short.MaxValue);
			BinaryPrimitives.WriteInt16LittleEndian(pcm.AsSpan(i * sizeof(short), sizeof(short)), sample);
		}
		return pcm;
	}

	/// <summary>去直流、压软峰值并保留各音效自己的响度差；起止约 2.5/4.5 ms 淡变。</summary>
	internal static float[] SynthesizeSamples(CombatSfxCue cue, int seed, float intensity = StandardImpact)
	{
		float weight = ClampImpact(intensity);
		(float duration, float peak, float lowHz, float highHz) = cue switch
		{
			CombatSfxCue.UnitDeploySlam => (Lerp(0.137f, 0.244f, weight), Lerp(0.37f, 0.57f, weight), 680f, Lerp(3000f, 4900f, weight)),
			CombatSfxCue.CardDrawSlide => (0.108f, 0.17f, 720f, 3450f),
			CombatSfxCue.CardHoverTick => (0.030f, 0.075f, 900f, 3200f),
			CombatSfxCue.CardSelectClick => (0.052f, 0.14f, 770f, 3550f),
			CombatSfxCue.OrderCast => (0.136f, 0.34f, 830f, 3850f),
			CombatSfxCue.UnitMoveAdvance => (0.214f, 0.25f, 175f, 1850f),
			_ => throw new ArgumentOutOfRangeException(nameof(cue), cue, null),
		};
		float[] samples = new float[(int)MathF.Round(duration * SampleRate)];
		var noise = new SignedNoise(seed);
		var band = new NoiseBand(lowHz, highHz);
		float tuning = 1f + (0.035f * noise.Next());
		float detail = (noise.Next() + 1f) * 0.5f;
		double sum = 0.0;
		double windowSum = 0.0;
		for (int i = 0; i < samples.Length; i++)
		{
			float time = (float)i / SampleRate;
			float grain = band.Step(noise.Next());
			float raw = cue switch
			{
				CombatSfxCue.UnitDeploySlam => DeploySlam(time, grain, tuning, detail, weight),
				CombatSfxCue.CardDrawSlide => DrawSlide(time, grain, tuning, detail),
				CombatSfxCue.CardHoverTick => (1.6f * grain * Decay(time, 0.005f)) + (0.22f * Mode(time, 540f * tuning, 0.0055f)),
				CombatSfxCue.CardSelectClick => (1.5f * grain * Decay(time, 0.007f))
					+ (0.44f * Mode(time, 430f * tuning, 0.008f)) + (0.15f * Mode(time, 790f * tuning, 0.003f)),
				CombatSfxCue.OrderCast => CastOrder(time, grain, tuning, detail),
				CombatSfxCue.UnitMoveAdvance => MoveAdvance(time, grain, tuning, detail),
				_ => 0f,
			};
			float window = EdgeWindow(i, samples.Length);
			float sample = MathF.Tanh(raw * 0.86f) * window;
			samples[i] = sample;
			sum += sample;
			windowSum += window;
		}

		// 按同一窗形移除均值，避免去直流后把已经淡到零的首尾重新拉出偏移。
		float offset = (float)(sum / windowSum);
		float actualPeak = 0f;
		for (int i = 0; i < samples.Length; i++)
		{
			samples[i] -= offset * EdgeWindow(i, samples.Length);
			actualPeak = MathF.Max(actualPeak, MathF.Abs(samples[i]));
		}
		float gain = peak / MathF.Max(actualPeak, 0.00001f);
		for (int i = 0; i < samples.Length; i++)
		{
			samples[i] = Math.Clamp(samples[i] * gain, -1f, 1f);
		}
		return samples;
	}

	private static float DeploySlam(float time, float grain, float tuning, float detail, float weight)
	{
		// 卡角先碰桌、两处木面模态接住重量，桌体稍晚下潜，避免像一声持续的电子低音。
		float edge = (grain * Lerp(1.15f, 1.7f, weight) * Decay(time, Lerp(0.0065f, 0.010f, weight)))
			+ (0.27f * Mode(time, Lerp(710f, 835f, weight) * tuning, 0.0033f));
		float table = (0.82f * Mode(time, Lerp(158f, 112f, weight) * tuning, Lerp(0.027f, 0.057f, weight)))
			+ (0.28f * Mode(time, Lerp(212f, 185f, weight) * tuning, Lerp(0.018f, 0.038f, weight)));
		float sub = Lerp(0.16f, 0.92f, weight) * Smooth(time / 0.014f) * Decay(time, Lerp(0.030f, 0.076f, weight))
			* Sweep(time, Lerp(76f, 85f, weight) * tuning, Lerp(66f, 49f, weight) * tuning, 0.027f);
		float rebound = time - Lerp(0.010f, 0.016f, detail);
		float paper = rebound > 0f ? 0.33f * grain * Smooth(rebound / 0.002f) * Decay(rebound, 0.007f) : 0f;
		return edge + table + sub + paper;
	}

	private static float DrawSlide(float time, float grain, float tuning, float detail)
	{
		float rub = grain * Smooth(time / 0.017f) * Decay(time, 0.029f);
		float tail = time - Lerp(0.035f, 0.049f, detail);
		float fold = tail > 0f ? Smooth(tail / 0.003f) * ((0.52f * grain * Decay(tail, 0.012f))
			+ (0.09f * Mode(tail, 435f * tuning, 0.006f))) : 0f;
		return rub + fold;
	}

	private static float CastOrder(float time, float grain, float tuning, float detail)
	{
		float stamp = (1.65f * grain * Decay(time, 0.0072f)) + (0.78f * Mode(time, 246f * tuning, 0.024f))
			+ (0.30f * Mode(time, 365f * tuning, 0.017f));
		float release = time - Lerp(0.014f, 0.020f, detail);
		return stamp + (release > 0f ? 0.23f * grain * Smooth(release / 0.003f) * Decay(release, 0.012f) : 0f);
	}

	private static float MoveAdvance(float time, float grain, float tuning, float detail)
	{
		float stopTime = Lerp(0.119f, 0.141f, detail);
		float trail = Smooth(time / 0.021f) * Smooth((stopTime - time) / 0.030f) * Decay(time, 0.087f);
		float friction = trail * ((1.4f * grain) + (0.14f * Sweep(time, 470f * tuning, 265f * tuning, 0.041f)));
		float stop = time - stopTime;
		return friction + (stop > 0f ? Smooth(stop / 0.003f) * ((0.55f * grain * Decay(stop, 0.008f))
			+ (0.42f * Mode(stop, 181f * tuning, 0.017f)) + (0.18f * Mode(stop, 288f * tuning, 0.012f))) : 0f);
	}

	private static float EdgeWindow(int index, int count) => Smooth(MathF.Min(
		index / (SampleRate * 0.0025f), (count - 1 - index) / (SampleRate * 0.0045f)));

	private static float Smooth(float value)
	{
		float t = Math.Clamp(value, 0f, 1f);
		return t * t * (3f - (2f * t));
	}

	private static float Lerp(float min, float max, float t) => min + ((max - min) * t);
	private static float Decay(float time, float seconds) => MathF.Exp(-time / seconds);
	private static float Mode(float time, float hz, float seconds) => MathF.Sin(MathF.Tau * hz * time) * Decay(time, seconds);

	// 对指数降频积分成相位，保持滑音连续，不直接用随时间变化的频率乘 t。
	private static float Sweep(float time, float startHz, float endHz, float dropSeconds)
	{
		float cycles = (endHz * time) + ((startHz - endHz) * dropSeconds * (1f - Decay(time, dropSeconds)));
		return MathF.Sin(MathF.Tau * cycles);
	}

	private struct SignedNoise(int seed)
	{
		private uint _state = unchecked((uint)seed) == 0u ? 0xA511E9B3u : unchecked((uint)seed);

		public float Next()
		{
			uint value = _state;
			value ^= value << 13;
			value ^= value >> 17;
			value ^= value << 5;
			_state = value;
			return ((value >> 8) * (1f / 8388608f)) - 1f;
		}
	}

	private struct NoiseBand(float lowHz, float highHz)
	{
		private readonly float _upperMix = 1f - MathF.Exp(-MathF.Tau * highHz / SampleRate);
		private readonly float _lowerMix = 1f - MathF.Exp(-MathF.Tau * lowHz / SampleRate);
		private float _upper;
		private float _lower;

		public float Step(float sample)
		{
			_upper += (sample - _upper) * _upperMix;
			_lower += (_upper - _lower) * _lowerMix;
			return _upper - _lower;
		}
	}
}
