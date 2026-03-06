using System;
using System.IO;
using System.Text.Json;
using OdysseyCards.Application.Combat;
using OdysseyCards.Domain.Combat.Commands;

namespace OdysseyCards.Infrastructure.Replay
{
    public sealed class JsonlReplayWriter : IReplayWriter, IDisposable
    {
        private readonly string _filePath;
        private readonly StreamWriter _writer;
        private readonly JsonSerializerOptions _jsonOptions;
        private bool _disposed;

        public JsonlReplayWriter(string filePath)
        {
            _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = false,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            string directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            _writer = new StreamWriter(_filePath, append: false, System.Text.Encoding.UTF8);
        }

        public void WriteCommand(CombatCommand command)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            var wrapper = new CommandWrapper
            {
                CommandType = command.GetType().Name,
                Command = command
            };

            string json = JsonSerializer.Serialize(wrapper, _jsonOptions);
            _writer.WriteLine(json);
            _writer.Flush();
        }

        public void Flush()
        {
            _writer?.Flush();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _writer?.Flush();
            _writer?.Dispose();
            _disposed = true;
            GC.SuppressFinalize(this);
        }

        private sealed class CommandWrapper
        {
            public string CommandType { get; set; }
            public CombatCommand Command { get; set; }
        }
    }
}
