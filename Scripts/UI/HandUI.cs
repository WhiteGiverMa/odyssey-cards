using System;
using System.Collections.Generic;
using Godot;

namespace OdysseyCards.UI
{
    /// <summary>
    /// 手牌管理组件。
    /// Phase 5 完整重写。当前为可编译存根。
    /// </summary>
    public partial class HandUI : Control
    {
        [Export] public PackedScene CardScene { get; set; }

        public event Action<OdysseyCards.Card.Card, OdysseyCards.Character.ICommander> OnCardPlayRequested;

        private HBoxContainer _cardContainer;
        private OdysseyCards.Character.Player _player;

        public override void _Ready()
        {
            _cardContainer = GetNode<HBoxContainer>("CardContainer");
        }

        public void Initialize(OdysseyCards.Character.Player player)
        {
            _player = player;
        }

        public void UpdateHand(IReadOnlyList<OdysseyCards.Card.Card> hand)
        {
            // Phase 5: Full implementation
        }
    }
}
