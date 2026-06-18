namespace OdysseyCards.Infrastructure;

/// <summary>
/// ChatScreen 命令抽象基类。每条命令自包含名称、别名、用法签名、描述、执行逻辑和参数补全。
/// </summary>
public abstract class ChatScreenCommand
{
	/// <summary>命令名（唯一标识，不含 / 前缀）。</summary>
	public abstract string Name { get; }

	/// <summary>命令别名。</summary>
	public virtual string[] Aliases => [];

	/// <summary>用法签名（如 "/damage [-c] N"）。</summary>
	public abstract string Signature { get; }

	/// <summary>描述文本。</summary>
	public abstract string Description { get; }

	/// <summary>
	/// 执行命令。args 不包含命令名本身，只包含空格分隔的参数。
	/// </summary>
	public abstract CommandResult Execute(string[] args);

	/// <summary>
	/// 获取参数补全候选。partialArg 为用户已输入的部分参数文本。
	/// 每个 CompletionCandidate 的 InsertText 应为纯参数值（不带命令前缀，引擎会自动补全）。
	/// 返回 null 表示该命令无参数补全（如 /help）。
	/// </summary>
	public virtual CompletionCandidate[]? GetArgCandidates(string partialArg) => null;
}
