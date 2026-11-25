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
	[InlineData((int)CombatSfxCue.UnitDeploySlam, 0f, 0.13, 0.15)]
	[InlineData((int)CombatSfxCue.UnitDeploySlam, 0.5f, 0.17, 0.21)]
	[InlineData((int)CombatSfxCue.UnitDeploySlam, 1f, 0.23, 0.26)]
	[InlineData((int)CombatSfxCue.CardDrawSlide, 0.5f, 0.08, 0.13)]
	[InlineData((int)CombatSfxCue.CardHoverTick, 0.5f, 0.02, 0.04)]
	[InlineData((int)CombatSfxCue.CardSelectClick, 0.5f, 0.04, 0.07)]
	[InlineData((int)CombatSfxCue.OrderCast, 0.5f, 0.10, 0.17)]
	[InlineData((int)CombatSfxCue.UnitMoveAdvance, 0.5f, 0.16, 0.26)]
	public void Synthesize_AllCues_StayFiniteSmoothAndAudible(int cueId, float intensity, double minSeconds, double maxSeconds)
	{
		var cue = (CombatSfxCue)cueId;
		float[] samples = ProceduralSfxMath.SynthesizeSamples(cue, 91742, intensity);
		float[] pcm = ReadPcm(cue, 91742, intensity);
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

	[Fact]
	public void Synthesize_HeavierDeploy_HasMoreLowEndAndLongerDecay()
	{
		float[] light = ReadPcm(CombatSfxCue.UnitDeploySlam, 364380, 0f);
		float[] standard = ReadPcm(CombatSfxCue.UnitDeploySlam, 364380);
		float[] heavy = ReadPcm(CombatSfxCue.UnitDeploySlam, 364380, 1f);
		Assert.True(light.Length < standard.Length && standard.Length < heavy.Length);
		Assert.True(Rms(light) < Rms(standard) && Rms(standard) < Rms(heavy));
		Assert.True(LowBandEnergy(heavy) > LowBandEnergy(light) * 3.0);
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

	private static float[] ReadPcm(CombatSfxCue cue, int seed, float intensity = ProceduralSfxMath.StandardImpact)
	{
		byte[] pcm = ProceduralSfxMath.Synthesize(cue, seed, intensity);
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

	private static double LowBandEnergy(float[] samples)
	{
		// 在相同物理时段测 45~90 Hz 的投影，不把较长的重拍尾音当成所有能量差异。
		int count = (int)(ProceduralSfxMath.SampleRate * 0.13f);
		double energy = 0;
		for (int hz = 45; hz <= 90; hz += 5)
		{
			double real = 0;
			double imaginary = 0;
			for (int i = 0; i < count; i++)
			{
				double phase = Math.Tau * hz * i / ProceduralSfxMath.SampleRate;
				real += samples[i] * Math.Cos(phase);
				imaginary += samples[i] * Math.Sin(phase);
			}
			energy += (real * real) + (imaginary * imaginary);
		}
		return energy;
	}
}
