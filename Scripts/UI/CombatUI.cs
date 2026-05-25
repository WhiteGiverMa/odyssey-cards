using System.Collections.Generic;
using Godot;
using OdysseyCards.Character;
using OdysseyCards.Localization;

namespace OdysseyCards.UI
{
    /// <summary>
    /// 战斗主界面组件。
    /// Phase 5 完整重写。当前为可编译存根。
    /// </summary>
    public partial class CombatUI : Control
    {
        [Export] public PackedScene HealthBarScene { get; set; }

        private HealthBar _playerHealthBar;
        private HandUI _handUI;
        private Button _endTurnButton;
        private Label _manaLabel;
        private Button _drawPileButton;
        private Button _discardPileButton;
        private Label _drawPileCountLabel;
        private Label _discardPileCountLabel;
        private DeckViewUI _deckViewUI;
        private Player _player;

        public override void _Ready()
        {
            GD.Print("[CombatUI] _Ready called");
            AddToGroup("CombatUI");
        }

        public void Initialize(Player player, object combatManager)
        {
            _player = player;
            // Phase 5: Full implementation
        }

        public void UpdateMana(int current, int max)
        {
            // Phase 5: Full implementation
        }

        public void UpdateHealth(int current, int max)
        {
            // Phase 5: Full implementation
        }
    }
}
