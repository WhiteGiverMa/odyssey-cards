using OdysseyCards.Core;

namespace OdysseyCards.Infrastructure.Commands;

/// <summary>
/// /version 命令 — 显示当前游戏版本号。
/// 版本号唯一真源是仓库根目录 VERSION 文件，构建期烧入 AssemblyInformationalVersion。
/// </summary>
public class VersionCommand : ChatScreenCommand
{
	public override string Name => "version";
	public override string[] Aliases => ["ver", "v"];
	public override string Signature => "/version";
	public override string Description => "显示当前游戏版本号。";

	public override CommandResult Execute(string[] args)
	{
		return CommandResult.Ok($"OdysseyCards {VersionInfo.Display}");
	}
}
