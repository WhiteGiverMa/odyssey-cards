using System;
using Godot;
using OdysseyCards.Application.Ports;

namespace OdysseyCards.Infrastructure.Godot.Save
{
    public sealed class GodotSaveRepository : ISaveRepository
    {
        private const string SavePath = "user://save.cfg";
        private const string SettingsSection = "settings";
        private const string ProgressSection = "progress";
        private const string PlayerSection = "player";

        private readonly string _globalSavePath;

        public GodotSaveRepository()
        {
            _globalSavePath = ProjectSettings.GlobalizePath(SavePath);
        }

        public SaveData Load()
        {
            var data = new SaveData();
            var config = new ConfigFile();

            if (config.Load(SavePath) != Error.Ok)
            {
                return data;
            }

            data.Language = (string)config.GetValue(SettingsSection, "language", "zh");
            data.CurrentFloor = (int)config.GetValue(ProgressSection, "current_floor", 1);
            data.CurrentAct = (int)config.GetValue(ProgressSection, "current_act", 1);
            data.PlayerHQCurrentHealth = (int)config.GetValue(PlayerSection, "hq_current_health", 8);
            data.PlayerHQMaxHealth = (int)config.GetValue(PlayerSection, "hq_max_health", 8);
            data.PlayerMaxHealth = (int)config.GetValue(PlayerSection, "max_health", 80);
            data.PlayerMaxEnergy = (int)config.GetValue(PlayerSection, "max_energy", 3);
            data.PlayerCharacterName = (string)config.GetValue(PlayerSection, "character_name", "Ironclad");

            return data;
        }

        public void Save(SaveData data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            var config = new ConfigFile();

            config.SetValue(SettingsSection, "language", data.Language);
            config.SetValue(ProgressSection, "current_floor", data.CurrentFloor);
            config.SetValue(ProgressSection, "current_act", data.CurrentAct);
            config.SetValue(PlayerSection, "hq_current_health", data.PlayerHQCurrentHealth);
            config.SetValue(PlayerSection, "hq_max_health", data.PlayerHQMaxHealth);
            config.SetValue(PlayerSection, "max_health", data.PlayerMaxHealth);
            config.SetValue(PlayerSection, "max_energy", data.PlayerMaxEnergy);
            config.SetValue(PlayerSection, "character_name", data.PlayerCharacterName);

            Error error = config.Save(SavePath);
            if (error != Error.Ok)
            {
                GD.PrintErr($"[GodotSaveRepository] Failed to save: {error}");
            }
        }

        public bool Exists()
        {
            return FileAccess.FileExists(SavePath);
        }

        public void Delete()
        {
            if (Exists())
            {
                DirAccess.RemoveAbsolute(_globalSavePath);
            }
        }
    }
}
