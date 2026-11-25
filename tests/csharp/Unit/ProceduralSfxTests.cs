using System;
using System.Buffers.Binary;
using System.Linq;
using OdysseyCards.Audio;
using Xunit;

namespace OdysseyCards.Tests.Unit;

/// <summary>对程序化音效的采样、听感层次与音量契约做纯 C# 回归。</summary>
public sealed class ProceduralSfxTests
{
	[Fact]
	public void Synthesize_StandardDeploy_ProducesShortMonoPcm16()
	{
		byte[] pcm = ProceduralSfxMath.Synthesize(CombatSfxCue.UnitDeploySlam, seed: 42);
		Assert.Equal(0, pcm.Length % sizeof(short));
		Assert.InRange(pcm.Length / sizeof(short) / (double)ProceduralSfxMath.SampleRate, 0.15, 0.24);
		Assert.Contains(pcm, value => value != 0);
	}

	[Theory]
	[InlineData((int)CombatSfxCue.CardDrawSlide, 0.08, 0.13)]
	[InlineData((int)CombatSfxCue.CardHoverTick, 0.02, 0.04)]
	[InlineData((int)CombatSfxCue.CardSelectClick, 0.04, 0.07)]
	[InlineData((int)CombatSfxCue.OrderCast, 0.10, 0.17)]
	[InlineData((int)CombatSfxCue.UnitMoveAdvance, 0.16, 0.26)]
	public void Synthesize_AllOtherCues_StayFiniteSmoothAndAudible(int cueId, double minSeconds, double maxSeconds)
	{
		var cue = (CombatSfxCue)cueId;
		AssertWaveform(ProceduralSfxMath.SynthesizeSamples(cue, 91742), ReadPcm(cue, 91742), minSeconds, maxSeconds);
	}

	[Theory]
	[InlineData((int)DeploySfxForm.Light, 0.13, 0.16)]
	[InlineData((int)DeploySfxForm.Medium, 0.19, 0.23)]
	[InlineData((int)DeploySfxForm.HeavyBomb, 0.28, 0.35)]
	[InlineData((int)DeploySfxForm.HeavyPia, 0.28, 0.35)]
	public void Synthesize_AllDeployForms_StayFiniteSmoothAndAudible(int formId, double minSeconds, double maxSeconds)
	{
		var form = (DeploySfxForm)formId;
		const int seed = 91742;
		byte[] pcm = ProceduralSfxMath.Synthesize(form, seed);
		Assert.Equal(pcm, ProceduralSfxMath.Synthesize(form, seed));
		AssertWaveform(ProceduralSfxMath.SynthesizeSamples(form, seed), ReadPcm(pcm), minSeconds, maxSeconds);
	}

	private static void AssertWaveform(float[] samples, float[] pcm, double minSeconds, double maxSeconds)
	{
		Assert.Equal(samples.Length, pcm.Length);
		Assert.InRange(pcm.Length / (double)ProceduralSfxMath.SampleRate, minSeconds, maxSeconds);
		for (int i = 0; i < samples.Length; i++)
		{
			Assert.True(float.IsFinite(samples[i]));
			Assert.InRange(MathF.Abs(samples[i]), 0f, 0.8f);
			Assert.InRange(MathF.Abs(samples[i] - pcm[i]), 0f, 1f / short.MaxValue);
		}
		Assert.Equal(0f, pcm[0]);
		Assert.Equal(0f, pcm[^1]);
		int halfMillisecond = ProceduralSfxMath.SampleRate / 2000;
		Assert.InRange(Peak(pcm.AsSpan(0, halfMillisecond)), 0f, 0.035f);
		Assert.InRange(Peak(pcm.AsSpan(pcm.Length - halfMillisecond)), 0f, 0.008f);
		Assert.InRange(Math.Abs(pcm.Average()), 0f, 0.0001f);
		Assert.InRange(Rms(pcm), 0.005, 0.35);
	}

	[Theory]
	[InlineData((int)CombatSfxCue.UnitDeploySlam)]
	[InlineData((int)CombatSfxCue.CardDrawSlide)]
	[InlineData((int)CombatSfxCue.CardHoverTick)]
	[InlineData((int)CombatSfxCue.CardSelectClick)]
	[InlineData((int)CombatSfxCue.OrderCast)]
	[InlineData((int)CombatSfxCue.UnitMoveAdvance)]
	public void Synthesize_ChangingSeed_VariesSoundButKeepsItsLevel(int cueId)
	{
		var cue = (CombatSfxCue)cueId;
		Assert.Equal(ProceduralSfxMath.Synthesize(cue, seed: 0), ProceduralSfxMath.Synthesize(cue, seed: 0));
		float[] first = ReadPcm(cue, 0);
		float[] second = ReadPcm(cue, 84361);
		Assert.Equal(first.Length, second.Length);
		Assert.InRange(Rms(first.Zip(second, (a, b) => a - b).ToArray()), 0.002, 0.25);
		Assert.InRange(Rms(first) / Rms(second), 0.65, 1.55);
	}

	[Theory]
	[InlineData(42)]
	[InlineData(91742)]
	[InlineData(467448786)]
	[InlineData(-1182451431)]
	[InlineData(1598924608)]
	[InlineData(-46781297)]
	public void Synthesize_BooPomBomb_AddsTableBodyAndWeight(int seed)
	{
		float[] light = ReadPcm(ProceduralSfxMath.Synthesize(DeploySfxForm.Light, seed));
		float[] medium = ReadPcm(ProceduralSfxMath.Synthesize(DeploySfxForm.Medium, seed));
		float[] heavy = ReadPcm(ProceduralSfxMath.Synthesize(DeploySfxForm.HeavyBomb, seed));
		Assert.True(light.Length < medium.Length && medium.Length < heavy.Length);
		Assert.True(Peak(light) < Peak(medium) && Peak(medium) < Peak(heavy));
		Assert.True(Rms(medium) > Rms(light) * 1.15 && Rms(heavy) > Rms(medium));

		int bodyFrames = ProceduralSfxMath.SampleRate * 130 / 1000;
		double lightBass = MeasureBand(light.AsSpan(0, bodyFrames), 50, 150).Power;
		double mediumBass = MeasureBand(medium.AsSpan(0, bodyFrames), 50, 150).Power;
		double heavyBass = MeasureBand(heavy.AsSpan(0, bodyFrames), 50, 150).Power;
		Assert.True(mediumBass > lightBass * 1.5 && heavyBass > mediumBass * 1.6);

		// 旧扫频把中重档能量挤在低频；木面应同时有可辨识的两段非低音板鸣。
		foreach (float[] samples in new[] { light, medium, heavy })
		{
			ReadOnlySpan<float> body = samples.AsSpan(0, bodyFrames);
			(double total, double strongest) = MeasureBand(body, 50, 550);
			Assert.InRange(strongest / total, 0.0, 0.24);
			Assert.InRange(MeasureBand(body, 170, 255).Power / total, 0.065, 1.0);
			Assert.InRange(MeasureBand(body, 255, 550).Power / total, 0.055, 1.0);
		}
	}

	[Theory]
	[InlineData(42)]
	[InlineData(91742)]
	[InlineData(467448786)]
	[InlineData(-1182451431)]
	[InlineData(1598924608)]
	[InlineData(-46781297)]
	public void Synthesize_HeavyForms_DistinguishSubstantialBombFromBroadbandPia(int seed)
	{
		float[] medium = ReadPcm(ProceduralSfxMath.Synthesize(DeploySfxForm.Medium, seed));
		float[] bomb = ReadPcm(ProceduralSfxMath.Synthesize(DeploySfxForm.HeavyBomb, seed));
		float[] pia = ReadPcm(ProceduralSfxMath.Synthesize(DeploySfxForm.HeavyPia, seed));
		int bodyFrames = ProceduralSfxMath.SampleRate * 130 / 1000;
		ReadOnlySpan<float> bombBody = bomb.AsSpan(0, bodyFrames);
		ReadOnlySpan<float> piaBody = pia.AsSpan(0, bodyFrames);
		double bombBass = MeasureBand(bombBody, 50, 150).Power;
		double piaBass = MeasureBand(piaBody, 50, 150).Power;
		Assert.True(bombBass > piaBass * 1.18);
		Assert.True(bombBass / MeasureBand(bombBody, 40, 12000).Power > (piaBass / MeasureBand(piaBody, 40, 12000).Power) + 0.08);
		Assert.True(piaBass > MeasureBand(medium.AsSpan(0, bodyFrames), 50, 150).Power);

		int attackFrames = ProceduralSfxMath.SampleRate * 25 / 1000;
		ReadOnlySpan<float> bombAttack = bomb.AsSpan(0, attackFrames);
		ReadOnlySpan<float> piaAttack = pia.AsSpan(0, attackFrames);
		double bombSlap = MeasureBand(bombAttack, 2000, 5500).Power;
		double piaSlap = MeasureBand(piaAttack, 2000, 5500).Power;
		double mediumSlap = MeasureBand(medium.AsSpan(0, attackFrames), 2000, 5500).Power;
		Assert.True(bombSlap < mediumSlap && piaSlap > mediumSlap * 2.0);
		Assert.True(piaSlap > bombSlap * 8.0);
		Assert.True(piaSlap / MeasureBand(piaAttack, 40, 12000).Power > 5.0 * bombSlap / MeasureBand(bombAttack, 40, 12000).Power);
	}

	[Fact]
	public void Synthesize_ImpactAnchors_MatchTheExplicitForms()
	{
		const CombatSfxCue cue = CombatSfxCue.UnitDeploySlam;
		Assert.Equal(ProceduralSfxMath.Synthesize(DeploySfxForm.Light, 42), ProceduralSfxMath.Synthesize(cue, 42, 0f));
		Assert.Equal(ProceduralSfxMath.Synthesize(DeploySfxForm.Medium, 42), ProceduralSfxMath.Synthesize(cue, 42, 0.5f));
		Assert.Equal(ProceduralSfxMath.Synthesize(ProceduralSfxMath.DefaultHeavyForm, 42), ProceduralSfxMath.Synthesize(cue, 42, 1f));
	}

	[Fact]
	public void Synthesize_BetweenImpactAnchors_InterpolatesWithoutQuantizing()
	{
		const CombatSfxCue cue = CombatSfxCue.UnitDeploySlam;
		float[] previous = ReadPcm(cue, 42, 0f);
		foreach (float weight in new[] { 0.25f, 0.5f, 0.75f, 1f })
		{
			float[] current = ReadPcm(cue, 42, weight);
			Assert.True(previous.Length < current.Length && Peak(previous) < Peak(current));
			previous = current;
		}
	}

	[Theory]
	[InlineData((int)CombatSfxCue.CardSelectClick)]
	[InlineData((int)CombatSfxCue.OrderCast)]
	public void Synthesize_MechanicalFeedback_IsNotDominatedByASingleTone(int cueId)
	{
		(double power, double strongest) = MeasureBand(ReadPcm((CombatSfxCue)cueId, 91742), 50, 8000);
		Assert.InRange(strongest / power, 0, 0.15);
	}

	[Fact]
	public void Synthesize_HandFeedback_IsQuieterThanTableImpact()
	{
		float slamPeak = Peak(ReadPcm(CombatSfxCue.UnitDeploySlam, 7112));
		float hoverPeak = Peak(ReadPcm(CombatSfxCue.CardHoverTick, 7112));
		float selectPeak = Peak(ReadPcm(CombatSfxCue.CardSelectClick, 7112));
		float drawPeak = Peak(ReadPcm(CombatSfxCue.CardDrawSlide, 7112));
		Assert.True(hoverPeak < selectPeak && selectPeak < slamPeak * 0.4f);
		Assert.True(drawPeak < slamPeak * 0.45f);
	}

	[Theory]
	[InlineData(-0.3f, 0f, -60f)]
	[InlineData(0f, 0f, -60f)]
	[InlineData(0.001f, 0.001f, -60f)]
	[InlineData(0.25f, 0.25f, -12.0412f)]
	[InlineData(0.8f, 0.8f, -1.9382f)]
	[InlineData(1f, 1f, 0f)]
	[InlineData(1.3f, 1f, 0f)]
	[InlineData(float.NaN, 0f, -60f)]
	[InlineData(float.PositiveInfinity, 1f, 0f)]
	[InlineData(float.NegativeInfinity, 0f, -60f)]
	public void VolumeToDb_ClampsLinearGainAndStaysFinite(float requested, float expectedGain, float expectedDb)
	{
		Assert.Equal(expectedGain, ProceduralSfxMath.ClampVolume(requested));
		Assert.InRange(MathF.Abs(ProceduralSfxMath.VolumeToDb(requested) - expectedDb), 0f, 0.0002f);
	}

	[Fact]
	public void Synthesize_OutOfRangeImpact_UsesSamePcmAsClampedImpact()
	{
		const CombatSfxCue cue = CombatSfxCue.UnitDeploySlam;
		Assert.Equal(ProceduralSfxMath.Synthesize(cue, 1, 0f), ProceduralSfxMath.Synthesize(cue, 1, -1f));
		Assert.Equal(ProceduralSfxMath.Synthesize(cue, 1, 1f), ProceduralSfxMath.Synthesize(cue, 1, 10f));
		Assert.Equal(ProceduralSfxMath.Synthesize(cue, 1), ProceduralSfxMath.Synthesize(cue, 1, float.NaN));
	}

	private static float[] ReadPcm(CombatSfxCue cue, int seed, float intensity = ProceduralSfxMath.MediumImpact)
	{
		return ReadPcm(ProceduralSfxMath.Synthesize(cue, seed, intensity));
	}

	private static float[] ReadPcm(byte[] pcm)
	{
		Assert.Equal(0, pcm.Length % sizeof(short));
		float[] samples = new float[pcm.Length / sizeof(short)];
		for (int i = 0; i < samples.Length; i++)
		{
			samples[i] = BinaryPrimitives.ReadInt16LittleEndian(pcm.AsSpan(i * sizeof(short), sizeof(short))) / (float)short.MaxValue;
		}
		return samples;
	}

	private static float Peak(ReadOnlySpan<float> samples)
	{
		float peak = 0f;
		foreach (float sample in samples)
		{
			peak = MathF.Max(peak, MathF.Abs(sample));
		}
		return peak;
	}

	private static double Rms(float[] samples) => Math.Sqrt(samples.Average(sample => (double)sample * sample));

	private static (double Power, double Strongest) MeasureBand(ReadOnlySpan<float> samples, int minHz, int maxHz)
	{
		// Goertzel 在实信号的等距正频率 bin 上取功率，不把更长的尾音计入同窗能量差。
		int count = samples.Length;
		int firstBin = (int)Math.Ceiling((double)minHz * count / ProceduralSfxMath.SampleRate);
		int lastBin = maxHz * count / ProceduralSfxMath.SampleRate;
		double total = 0;
		double strongest = 0;
		for (int bin = firstBin; bin <= lastBin; bin++)
		{
			double coefficient = 2 * Math.Cos(Math.Tau * bin / count);
			double last = 0;
			double earlier = 0;
			foreach (float sample in samples)
			{
				double current = sample + (coefficient * last) - earlier;
				earlier = last;
				last = current;
			}
			double power = (last * last) + (earlier * earlier) - (coefficient * last * earlier);
			total += power;
			strongest = Math.Max(strongest, power);
		}
		return (total, strongest);
	}
}
