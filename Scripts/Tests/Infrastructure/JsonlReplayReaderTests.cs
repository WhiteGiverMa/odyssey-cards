using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using OdysseyCards.Domain.Combat.Commands;
using OdysseyCards.Infrastructure.Replay;
using Xunit;

namespace OdysseyCards.Tests.Infrastructure
{
    public class JsonlReplayReaderTests : IDisposable
    {
        private readonly string _testFilePath;

        public JsonlReplayReaderTests()
        {
            _testFilePath = Path.Combine(Path.GetTempPath(), $"replay_test_{Guid.NewGuid()}.jsonl");
        }

        public void Dispose()
        {
            if (File.Exists(_testFilePath))
            {
                File.Delete(_testFilePath);
            }
        }

        [Fact]
        public void ReadCommands_WithEmptyFile_ReturnsEmptyList()
        {
            File.WriteAllText(_testFilePath, string.Empty);

            var reader = new JsonlReplayReader(_testFilePath);
            var commands = reader.ReadCommands();

            Assert.Empty(commands);
        }

        [Fact]
        public void ReadCommands_WithNonExistentFile_ReturnsEmptyList()
        {
            var reader = new JsonlReplayReader("non_existent_file.jsonl");
            var commands = reader.ReadCommands();

            Assert.Empty(commands);
        }

        [Fact]
        public void ReadCommands_WithEndTurnCommand_ReturnsCommand()
        {
            var json = JsonSerializer.Serialize(new
            {
                commandType = "EndTurnCommand",
                command = new EndTurnCommand(1, 0)
            });

            File.WriteAllText(_testFilePath, json + Environment.NewLine);

            var reader = new JsonlReplayReader(_testFilePath);
            var commands = reader.ReadCommands();

            Assert.Single(commands);
            Assert.IsType<EndTurnCommand>(commands[0]);
        }

        [Fact]
        public void ReadCommands_WithMultipleCommands_ReturnsAllCommands()
        {
            var lines = new List<string>
            {
                JsonSerializer.Serialize(new { commandType = "StartCombatCommand", command = new StartCombatCommand(12345) }),
                JsonSerializer.Serialize(new { commandType = "EndTurnCommand", command = new EndTurnCommand(1, 0) }),
                JsonSerializer.Serialize(new { commandType = "EndTurnCommand", command = new EndTurnCommand(2, 1) })
            };

            File.WriteAllLines(_testFilePath, lines);

            var reader = new JsonlReplayReader(_testFilePath);
            var commands = reader.ReadCommands();

            Assert.Equal(3, commands.Count);
            Assert.IsType<StartCombatCommand>(commands[0]);
            Assert.IsType<EndTurnCommand>(commands[1]);
            Assert.IsType<EndTurnCommand>(commands[2]);
        }

        [Fact]
        public void ReadCommands_WithInvalidJson_SkipsInvalidLine()
        {
            var lines = new List<string>
            {
                "invalid json line",
                JsonSerializer.Serialize(new { commandType = "EndTurnCommand", command = new EndTurnCommand(1, 0) })
            };

            File.WriteAllLines(_testFilePath, lines);

            var reader = new JsonlReplayReader(_testFilePath);
            var commands = reader.ReadCommands();

            Assert.Single(commands);
        }

        [Fact]
        public void ReadCommands_WithUnknownCommandType_SkipsCommand()
        {
            var lines = new List<string>
            {
                JsonSerializer.Serialize(new { commandType = "UnknownCommand", command = new { } }),
                JsonSerializer.Serialize(new { commandType = "EndTurnCommand", command = new EndTurnCommand(1, 0) })
            };

            File.WriteAllLines(_testFilePath, lines);

            var reader = new JsonlReplayReader(_testFilePath);
            var commands = reader.ReadCommands();

            Assert.Single(commands);
        }
    }
}
