using System.Diagnostics;

namespace OdysseyCards.Core;

/// <summary>模型层诊断输出，不依赖游戏引擎或场景树。</summary>
internal static class ModelLog
{
	[Conditional("DEBUG")]
	public static void Print(string message) => Trace.WriteLine(message);
}
