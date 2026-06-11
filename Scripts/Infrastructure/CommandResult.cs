using System.Threading.Tasks;

namespace OdysseyCards.Infrastructure;

/// <summary>
/// 命令执行结果。统一返回模型，区分成功/失败，支持异步任务。
/// </summary>
public readonly struct CommandResult
{
	public bool Success { get; }
	public string Message { get; }
	public Task? Task { get; }

	public CommandResult(bool success, string message, Task? task = null)
	{
		Success = success;
		Message = message;
		Task = task;
	}

	public static CommandResult Ok(string message) => new(true, message);
	public static CommandResult Fail(string message) => new(false, message);
	public static CommandResult OkWithTask(Task task, string message) => new(true, message, task);
}
