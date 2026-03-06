namespace OdysseyCards.Application.Ports
{
    public interface ISaveRepository
    {
        SaveData Load();
        void Save(SaveData data);
        bool Exists();
        void Delete();
    }

    public sealed class SaveData
    {
        public string Language { get; set; } = "zh";
        public int CurrentFloor { get; set; } = 1;
        public int CurrentAct { get; set; } = 1;
        public int PlayerHQCurrentHealth { get; set; } = 8;
        public int PlayerHQMaxHealth { get; set; } = 8;
        public int PlayerMaxHealth { get; set; } = 80;
        public int PlayerMaxEnergy { get; set; } = 3;
        public string PlayerCharacterName { get; set; } = "Ironclad";

        public SaveData() { }

        public SaveData Clone()
        {
            return new SaveData
            {
                Language = Language,
                CurrentFloor = CurrentFloor,
                CurrentAct = CurrentAct,
                PlayerHQCurrentHealth = PlayerHQCurrentHealth,
                PlayerHQMaxHealth = PlayerHQMaxHealth,
                PlayerMaxHealth = PlayerMaxHealth,
                PlayerMaxEnergy = PlayerMaxEnergy,
                PlayerCharacterName = PlayerCharacterName
            };
        }
    }
}
