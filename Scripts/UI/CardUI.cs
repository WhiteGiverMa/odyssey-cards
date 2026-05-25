using System;
using Godot;

namespace OdysseyCards.UI
{
    /// <summary>
    /// 卡牌视觉组件。
    /// Phase 5 完整重写。当前为可编译存根。
    /// </summary>
    public partial class CardUI : Control
    {
        public OdysseyCards.Card.Card Card { get; private set; }
        public event Action<CardUI> OnCardSelected;
        public bool IsSelected { get; private set; }

        public override void _Ready()
        {
            MouseFilter = MouseFilterEnum.Stop;
        }

        public void SetCard(OdysseyCards.Card.Card card)
        {
            Card = card;
        }

        public void Select()
        {
            IsSelected = true;
            OnCardSelected?.Invoke(this);
        }

        public void Deselect()
        {
            IsSelected = false;
        }
    }
}
