using System.Reflection;

namespace OdysseyCards.Core;

/// <summary>
/// 版本号统一入口。从 AssemblyInformationalVersion 读取，
/// 后者在构建期由 csproj 的 &lt;Version&gt; 表达式从仓库根 VERSION 文件派生。
/// </summary>
public static class VersionInfo
{
	private static readonly string _full = ReadFull();
	private static readonly string _display = ExtractDisplay(_full);

	/// <summary>显示版本号（无 v 前缀、无 source revision，如 "0.2.0-alpha"）。用于玩家可见 UI。</summary>
	public static string Display => _display;

	/// <summary>带 v 前缀的版本号（如 "v0.2.0-alpha"），用于日志/标签展示。</summary>
	public static string Tagged => $"v{_display}";

	/// <summary>完整版本字符串（含 MSBuild source revision，如 "0.2.0-alpha+abc123"）。用于调试。</summary>
	public static string FullVersion => _full;

	private static string ReadFull()
	{
		var attr = Assembly.GetExecutingAssembly()
			.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
		return string.IsNullOrWhiteSpace(attr?.InformationalVersion)
			? "0.0.0-unknown"
			: attr.InformationalVersion.Trim();
	}

	/// <summary>去掉 MSBuild 自动附加的 +source revision 后缀，只保留 SemVer 部分。</summary>
	private static string ExtractDisplay(string full)
	{
		var plusIdx = full.IndexOf('+');
		return plusIdx > 0 ? full[..plusIdx] : full;
	}
}
