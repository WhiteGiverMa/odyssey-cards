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

/// <summary>预烘焙的拍桌音色；两种重拍共用视觉重量，不混成一个声音。</summary>
internal enum DeploySfxForm
{
	Light,
	Medium,
	HeavyBomb,
	HeavyPia,
}

/// <summary>在内存中合成确定性的 44.1 kHz 单声道 16 位小端 PCM，不依赖引擎或素材。</summary>
internal static class ProceduralSfxMath
{
	public const int SampleRate = 44100;
	public const float LightImpact = 0f;
	public const float MediumImpact = 0.5f;
	public const float HeavyImpact = 1f;
	public const DeploySfxForm DefaultHeavyForm = DeploySfxForm.HeavyBomb;
	public const float MinVolume = 0f;
	public const float MaxVolume = 1f;
	public const float MuteThreshold = 0.001f;

	/// <summary>无效音量按静音处理，有穷数值限制为线性增益范围。</summary>
	public static float ClampVolume(float volume) => float.IsNaN(volume) ? MinVolume : Math.Clamp(volume, MinVolume, MaxVolume);

	/// <summary>有效音量换算为分贝；静音另由播放端处理，不向引擎传入负无穷。</summary>
	public static float VolumeToDb(float volume) => 20f * MathF.Log10(MathF.Max(ClampVolume(volume), MuteThreshold));

	/// <summary>拍桌力度是连续的 0（轻）到 1（重）；未指定或无效时用标准力度。</summary>
	public static float ClampImpact(float intensity)
	{
		return float.IsNaN(intensity) ? MediumImpact : Math.Clamp(intensity, LightImpact, HeavyImpact);
	}

	public static byte[] Synthesize(CombatSfxCue cue, int seed, float intensity = MediumImpact)
	{
		return EncodePcm(SynthesizeSamples(cue, seed, intensity));
	}

	public static byte[] Synthesize(DeploySfxForm form, int seed)
	{
		return EncodePcm(SynthesizeSamples(form, seed));
	}

	private static byte[] EncodePcm(float[] samples)
	{
		byte[] pcm = new byte[samples.Length * sizeof(short)];
		for (int i = 0; i < samples.Length; i++)
		{
			short sample = (short)MathF.Round(samples[i] * short.MaxValue);
			BinaryPrimitives.WriteInt16LittleEndian(pcm.AsSpan(i * sizeof(short), sizeof(short)), sample);
		}
		return pcm;
	}

	internal static float[] SynthesizeSamples(DeploySfxForm form, int seed)
	{
		const CombatSfxCue cue = CombatSfxCue.UnitDeploySlam;
		return form switch
		{
			DeploySfxForm.Light => SynthesizeSamples(cue, seed, LightImpact, form),
			DeploySfxForm.Medium => SynthesizeSamples(cue, seed, MediumImpact, form),
			DeploySfxForm.HeavyBomb or DeploySfxForm.HeavyPia => SynthesizeSamples(cue, seed, HeavyImpact, form),
			_ => throw new ArgumentOutOfRangeException(nameof(form), form, null),
		};
	}

	/// <summary>去直流、压软峰值并保留各音效自己的响度差；起止用毫秒级淡变。</summary>
	internal static float[] SynthesizeSamples(CombatSfxCue cue, int seed, float intensity = MediumImpact)
	{
		return SynthesizeSamples(cue, seed, intensity, DefaultHeavyForm);
	}

	private static float[] SynthesizeSamples(CombatSfxCue cue, int seed, float intensity, DeploySfxForm form)
	{
		float weight = ClampImpact(intensity);
		bool sharpHeavy = form == DeploySfxForm.HeavyPia;
		(float duration, float peak, float lowHz, float highHz) = cue switch
		{
			CombatSfxCue.UnitDeploySlam => (ImpactCurve(weight, 0.146f, 0.211f, sharpHeavy ? 0.286f : 0.325f),
				ImpactCurve(weight, 0.40f, 0.57f, 0.79f), 0f, 0f),
			CombatSfxCue.CardDrawSlide => (0.108f, 0.17f, 720f, 3450f),
			CombatSfxCue.CardHoverTick => (0.030f, 0.075f, 900f, 3200f),
			CombatSfxCue.CardSelectClick => (0.052f, 0.14f, 680f, 3550f),
			CombatSfxCue.OrderCast => (0.122f, 0.34f, 710f, 3850f),
			CombatSfxCue.UnitMoveAdvance => (0.214f, 0.25f, 175f, 1850f),
			_ => throw new ArgumentOutOfRangeException(nameof(cue), cue, null),
		};
		float[] samples = new float[(int)MathF.Round(duration * SampleRate)];
		var noise = new SignedNoise(seed);
		float tuning = 1f + (0.015f * noise.Next());
		float detail = (noise.Next() + 1f) * 0.5f;
		if (cue == CombatSfxCue.UnitDeploySlam)
		{
			var impact = new WoodenTableImpact(weight, tuning, detail, sharpHeavy);
			for (int i = 0; i < samples.Length; i++)
			{
				samples[i] = impact.Step((float)i / SampleRate, noise.Next());
			}
		}
		else
		{
			FillFoley(samples, cue, lowHz, highHz, detail, ref noise);
		}
		FinishWaveform(samples, peak);
		return samples;
	}

	private static void FillFoley(float[] samples, CombatSfxCue cue, float lowHz, float highHz, float detail, ref SignedNoise noise)
	{
		var paper = new NoiseBand(lowHz, highHz);
		var contact = new NoiseBand(135f, 750f);
		for (int i = 0; i < samples.Length; i++)
		{
			float time = (float)i / SampleRate;
			float white = noise.Next();
			float grain = paper.Step(white);
			float knock = contact.Step(white);
			samples[i] = cue switch
			{
				CombatSfxCue.CardDrawSlide => DrawSlide(time, grain, knock, detail),
				CombatSfxCue.CardHoverTick => (grain + (0.42f * knock)) * ImpactPulse(time, 0.018f),
				CombatSfxCue.CardSelectClick => ((1.05f * grain) + (1.6f * knock)) * ImpactPulse(time, 0.024f),
				CombatSfxCue.OrderCast => CastOrder(time, grain, knock, detail),
				CombatSfxCue.UnitMoveAdvance => MoveAdvance(time, grain, knock, detail),
				CombatSfxCue.UnitDeploySlam => throw new InvalidOperationException("拍桌必须走木桌模态合成。"),
				_ => throw new ArgumentOutOfRangeException(nameof(cue), cue, null),
			};
		}
	}

	private static void FinishWaveform(float[] samples, float peak)
	{
		double sum = 0.0;
		double windowSum = 0.0;
		for (int i = 0; i < samples.Length; i++)
		{
			float window = EdgeWindow(i, samples.Length);
			float sample = MathF.Tanh(samples[i] * 0.86f) * window;
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
	}

	private static float DrawSlide(float time, float grain, float knock, float detail)
	{
		float rub = grain * Smooth(time / 0.017f) * Decay(time, 0.029f);
		float tail = time - Lerp(0.035f, 0.049f, detail);
		float fold = ((0.66f * grain) + (0.58f * knock)) * ImpactPulse(tail, 0.036f);
		return rub + fold;
	}

	private static float CastOrder(float time, float grain, float knock, float detail)
	{
		float stamp = (1.4f * grain * ImpactPulse(time, 0.014f)) + (2.3f * knock * ImpactPulse(time, 0.032f));
		float release = time - Lerp(0.012f, 0.019f, detail);
		return stamp + (((0.48f * grain) + (0.62f * knock)) * ImpactPulse(release, 0.024f));
	}

	private static float MoveAdvance(float time, float grain, float knock, float detail)
	{
		float stopTime = Lerp(0.119f, 0.141f, detail);
		float trail = Smooth(time / 0.021f) * Smooth((stopTime - time) / 0.030f) * Decay(time, 0.087f);
		float friction = trail * Lerp(1.5f * grain, 1.35f * knock, Smooth(time / stopTime));
		float stop = time - stopTime;
		return friction + (((0.65f * grain) + (1.7f * knock)) * ImpactPulse(stop, 0.034f));
	}

	private static float ImpactPulse(float time, float seconds)
	{
		if (time < 0f || time >= seconds)
		{
			return 0f;
		}
		float t = time / seconds;
		float tail = 1f - t;
		return Smooth(t * 8f) * tail * tail * tail;
	}

	private static float ImpactCurve(float weight, float light, float medium, float heavy)
	{
		return weight <= MediumImpact
			? Lerp(light, medium, weight * 2f)
			: Lerp(medium, heavy, (weight - MediumImpact) * 2f);
	}

	private static float EdgeWindow(int index, int count)
	{
		float t = Math.Clamp(MathF.Min(index / (SampleRate * 0.0035f), (count - 1 - index) / (SampleRate * 0.0045f)), 0f, 1f);
		return t * t * t * (10f + (t * ((6f * t) - 15f)));
	}

	private static float Smooth(float value)
	{
		float t = Math.Clamp(value, 0f, 1f);
		return t * t * (3f - (2f * t));
	}

	private static float Lerp(float min, float max, float t) => min + ((max - min) * t);
	private static float Decay(float time, float seconds) => MathF.Exp(-time / seconds);

	/// <summary>卡角与整面接触敲击同一块桌板；各模态的频率在整次撞击中固定。</summary>
	private sealed class WoodenTableImpact
	{
		private readonly DampedResonator[] _plate;
		private readonly float _forceLength;
		private readonly float _contactLength;
		private readonly float _cornerDelay;
		private readonly float _cornerLength;
		private readonly float _slapDelay;
		private readonly float _flattenLength;
		private readonly float _plateGain;
		private readonly float _cavityGain;
		private readonly float _woodGrainGain;
		private readonly float _cardGain;
		private readonly float _flattenGain;
		private NoiseBand _woodGrain = new(115f, 1250f);
		private NoiseBand _tableAir = new(30f, 170f);
		private NoiseBand _cardSlap;
		private DampedResonator _cavityLow;
		private DampedResonator _cavityHigh;

		public WoodenTableImpact(float weight, float tuning, float detail, bool sharpHeavy)
		{
			float fundamental = ImpactCurve(weight, 143f, 116f, sharpHeavy ? 95f : 88f) * tuning;
			float ring = ImpactCurve(weight, 0.015f, 0.044f, sharpHeavy ? 0.082f : 0.109f);
			// 实木板的弯曲模态非整数倍频；频率越高，材料阻尼越快。
			_plate =
			[
				new(fundamental, ring, 1.15f),
				new(fundamental * 1.47f, ring * 0.75f, 1.03f),
				new(fundamental * 2.13f, ring * 0.59f, 0.92f),
				new(fundamental * 2.86f, ring * 0.40f, 0.85f),
				new(fundamental * 3.74f, ring * 0.27f, 0.72f),
				new(fundamental * 5.12f, ring * 0.17f, 0.51f),
			];
			float cavityRing = ImpactCurve(weight, 0.014f, 0.031f, sharpHeavy ? 0.043f : 0.081f);
			_cavityLow = new DampedResonator(ImpactCurve(weight, 83f, 72f, 59f) * tuning, cavityRing, 1f);
			_cavityHigh = new DampedResonator(ImpactCurve(weight, 94f, 91f, 79f) * tuning, cavityRing * 0.74f, 0.76f);
			_plateGain = ImpactCurve(weight, 0.73f, 1.62f, sharpHeavy ? 3.95f : 3.5f);
			_cavityGain = ImpactCurve(weight, 0.13f, 1.19f, sharpHeavy ? 1.12f : 4.5f);
			_forceLength = ImpactCurve(weight, 0.0024f, 0.0035f, 0.0048f);
			_contactLength = ImpactCurve(weight, 0.0060f, 0.0085f, 0.0115f);
			_woodGrainGain = ImpactCurve(weight, 0.22f, 0.49f, sharpHeavy ? 0.98f : 0.74f);
			_cornerDelay = ImpactCurve(weight, 0f, 0.0004f, sharpHeavy ? 0.0017f : 0.0004f);
			_cornerLength = ImpactCurve(weight, 0.0038f, 0.0048f, sharpHeavy ? 0.0036f : 0.0058f);
			_slapDelay = _cornerDelay + Lerp(0.0038f, 0.0064f, detail);
			_flattenLength = ImpactCurve(weight, 0.0048f, 0.0068f, 0.0105f);
			// Bomb 只留闷实的接触；Pia 让卡角先着桌、整面晚几毫秒挤出第二段脆啪。
			_cardSlap = new NoiseBand(ImpactCurve(weight, 950f, 1450f, sharpHeavy ? 2200f : 1150f),
				ImpactCurve(weight, 3000f, 4100f, sharpHeavy ? 5500f : 2700f));
			_cardGain = ImpactCurve(weight, 0.026f, 0.18f, sharpHeavy ? 0.80f : 0.033f);
			_flattenGain = ImpactCurve(weight, 0.035f, 0.22f, sharpHeavy ? 1.70f : 0.049f);
		}

		public float Step(float time, float white)
		{
			float contact = ImpactPulse(time, _contactLength);
			float flatten = ImpactPulse(time - _slapDelay, _flattenLength);
			// 纸卡接触力是短而略带颗粒的正向脉冲；板鸣来自滤波器的自由衰减。
			float force = (3.6f * ImpactPulse(time, _forceLength) * (0.91f + (0.49f * white)))
				+ (0.34f * flatten * (0.57f + white));
			float plate = 0f;
			for (int i = 0; i < _plate.Length; i++)
			{
				plate += _plate[i].Step(force);
			}
			float wood = (_plateGain * plate) + (_woodGrainGain * _woodGrain.Step(white) * contact);
			float pressure = contact * (0.72f + (0.92f * white));
			float cavity = _cavityLow.Step(pressure) + _cavityHigh.Step(pressure) + (0.73f * _tableAir.Step(pressure));
			float thump = _cavityGain * MathF.Tanh(1.28f * cavity);
			float snap = MathF.Tanh(2.4f * _cardSlap.Step(white));
			float slap = snap * ((_cardGain * ImpactPulse(time - _cornerDelay, _cornerLength)) + (_flattenGain * flatten));
			return wood + thump + slap;
		}
	}

	/// <summary>共轭极点的二阶带通，零点在直流和奈奎斯特频率；衰减以 e 折秒数给出。</summary>
	private struct DampedResonator(float hz, float decaySeconds, float gain)
	{
		private readonly double _feedback = 2.0 * Math.Exp(-1.0 / (decaySeconds * SampleRate)) * Math.Cos(Math.Tau * hz / SampleRate);
		private readonly double _radiusSquared = Math.Exp(-2.0 / (decaySeconds * SampleRate));
		private readonly double _inputGain = gain * (1.0 - Math.Exp(-1.0 / (decaySeconds * SampleRate)));
		private double _input1;
		private double _input2;
		private double _output1;
		private double _output2;

		public float Step(float input)
		{
			double output = (_inputGain * (input - _input2)) + (_feedback * _output1) - (_radiusSquared * _output2);
			_input2 = _input1;
			_input1 = input;
			_output2 = _output1;
			_output1 = output;
			return (float)output;
		}
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
