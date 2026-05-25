using System.Collections.Generic;
using Godot;

namespace OdysseyCards.UI
{
    /// <summary>
    /// 牌堆查看弹窗。
    /// Phase 5 完整重写。当前为可编译存根。
    /// </summary>
    public partial class DeckViewUI : Control
    {
        public override void _Ready()
        {
            MouseFilter = MouseFilterEnum.Stop;
            Visible = false;
        }

        public void ShowDeckList(string title, IReadOnlyList<OdysseyCards.Card.Card> cards)
        {
            // Phase 5: Full implementation
        }

        public void ShowDiscardList(string title, IReadOnlyList<OdysseyCards.Card.Card> cards)
        {
            // Phase 5: Full implementation
        }
    }
}
