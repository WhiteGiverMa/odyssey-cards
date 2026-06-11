using System;
using System.Collections.Generic;
using System.IO;

namespace OdysseyCards.Infrastructure;

/// <summary>
/// 有界队列：新元素插入索引 0（最新在前），超出容量时从末尾移除最旧元素。
/// 用于 DevConsole 命令历史持久化。
/// </summary>
public class FixedSizedQueue<T> : List<T>
{
	private readonly int _limit;

	public new int Capacity => _limit;

	public FixedSizedQueue(int limit)
	{
		_limit = limit;
	}

	/// <summary>
	/// 入队：插入在索引 0，超出容量时移除末尾。
	/// </summary>
	public void Enqueue(T item)
	{
		if (Count >= _limit)
			RemoveAt(Count - 1);
		Insert(0, item);
	}

	/// <summary>
	/// 将历史持久化到文件，每行一条。调用方需确保路径有效。
	/// </summary>
	public void Save(string filePath)
	{
		using var writer = new StreamWriter(filePath);
		foreach (var item in this)
			writer.WriteLine(item?.ToString() ?? "");
	}

	/// <summary>
	/// 从文件加载历史。
	/// </summary>
	public static FixedSizedQueue<string> Load(string filePath, int limit = 40)
	{
		var queue = new FixedSizedQueue<string>(limit);
		if (!File.Exists(filePath))
			return queue;

		var lines = File.ReadAllLines(filePath);
		// 文件是从最旧到最新存储的，需要反转以保持 Enqueue 语义（最新在前）
		for (int i = lines.Length - 1; i >= 0; i--)
		{
			if (!string.IsNullOrWhiteSpace(lines[i]))
				queue.Enqueue(lines[i]);
		}
		return queue;
	}
}
