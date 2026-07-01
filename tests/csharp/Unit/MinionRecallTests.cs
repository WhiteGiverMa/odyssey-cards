using System.Reflection;
using System.Runtime.CompilerServices;
using OdysseyCards.Card;
using OdysseyCards.Combat;
using Xunit;

namespace OdysseyCards.Tests.Unit
{
	public class MinionRecallTests
	{
		[Fact]
		public void RecallMinion_RemovesFromBoard_withoutDeathEvent()
		{
			var board = new Board();
			Minion minion = CreateMinion(isPlayerSide: true);
			bool removed = false;
			bool died = false;
			board.OnMinionRemoved += _ => removed = true;
			board.OnMinionDied += _ => died = true;
			board.PlaceMinion(minion, 0);

			bool recalled = board.RecallMinion(minion);

			Assert.True(recalled);
			Assert.True(removed);
			Assert.False(died);
			Assert.Null(board.GetMinionAt(0, isPlayerSide: true));
			Assert.Equal(-1, minion.BoardSlotIndex);
		}

		[Fact]
		public void CopyRuntimeModifiersFrom_CopiesEndTurnReturnCounter()
		{
			Card.Card source = CreateCard();
			Card.Card target = CreateCard();
			source.EndTurnDrawPileReturnsRemaining = 1;

			target.CopyRuntimeModifiersFrom(source);

			Assert.Equal(1, target.EndTurnDrawPileReturnsRemaining);
		}

		private static Minion CreateMinion(bool isPlayerSide)
		{
			var minion = (Minion)RuntimeHelpers.GetUninitializedObject(typeof(Minion));
			SetBackingField(minion, nameof(Minion.IsPlayerSide), isPlayerSide);
			minion.BoardSlotIndex = -1;
			return minion;
		}

		private static Card.Card CreateCard()
		{
			return (Card.Card)RuntimeHelpers.GetUninitializedObject(typeof(Card.Card));
		}

		private static void SetBackingField<T>(object instance, string propertyName, T value)
		{
			FieldInfo field = instance.GetType().GetField($"<{propertyName}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
			Assert.NotNull(field);
			field.SetValue(instance, value);
		}
	}
}
