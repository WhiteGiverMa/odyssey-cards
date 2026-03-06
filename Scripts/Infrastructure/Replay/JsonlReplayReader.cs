using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using OdysseyCards.Domain.Combat.Commands;

namespace OdysseyCards.Infrastructure.Replay
{
    public sealed class JsonlReplayReader : IDisposable
    {
        private readonly string _filePath;
        private readonly JsonSerializerOptions _jsonOptions;
        private bool _disposed;

        private static readonly Dictionary<string, Type> CommandTypeMap = new()
        {
            { nameof(StartCombatCommand), typeof(StartCombatCommand) },
            { nameof(PlayCardCommand), typeof(PlayCardCommand) },
            { nameof(DeployUnitCommand), typeof(DeployUnitCommand) },
            { nameof(MoveUnitCommand), typeof(MoveUnitCommand) },
            { nameof(AttackCommand), typeof(AttackCommand) },
            { nameof(EndTurnCommand), typeof(EndTurnCommand) },
            { nameof(CancelSelectionCommand), typeof(CancelSelectionCommand) }
        };

        public JsonlReplayReader(string filePath)
        {
            _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            };
        }

        public List<CombatCommand> ReadCommands()
        {
            var commands = new List<CombatCommand>();

            if (!File.Exists(_filePath))
            {
                return commands;
            }

            foreach (string line in File.ReadLines(_filePath))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                CombatCommand command = DeserializeCommand(line);
                if (command != null)
                {
                    commands.Add(command);
                }
            }

            return commands;
        }

        private CombatCommand DeserializeCommand(string json)
        {
            try
            {
                using JsonDocument doc = JsonDocument.Parse(json);
                JsonElement root = doc.RootElement;

                if (!root.TryGetProperty("commandType", out JsonElement typeElement))
                {
                    return null;
                }

                string commandType = typeElement.GetString();
                if (!CommandTypeMap.TryGetValue(commandType, out Type type))
                {
                    return null;
                }

                if (!root.TryGetProperty("command", out JsonElement commandElement))
                {
                    return null;
                }

                return (CombatCommand)JsonSerializer.Deserialize(commandElement.GetRawText(), type, _jsonOptions);
            }
            catch (JsonException ex)
            {
                System.Console.WriteLine($"[JsonlReplayReader] Failed to deserialize: {ex.Message}");
                return null;
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
