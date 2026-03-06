using System;
using System.Collections.Generic;
using OdysseyCards.Application.Combat.UseCases;
using OdysseyCards.Application.Ports;
using OdysseyCards.Domain.Combat.Events;
using Xunit;

namespace OdysseyCards.Tests.Application
{
    public class ProcessRewardUseCaseTests
    {
        [Fact]
        public void Execute_WithVictoryEvent_GeneratesRewards()
        {
            var mockRewardService = new MockRewardService();
            var useCase = new ProcessRewardUseCase(mockRewardService);

            var combatEndedEvent = new CombatEndedEvent(
                Guid.NewGuid(),
                5,
                0,
                "Victory",
                true
            );

            var rewards = useCase.Execute(combatEndedEvent);

            Assert.Single(rewards);
            Assert.Equal("TestCard", rewards[0].CardName);
        }

        [Fact]
        public void Execute_WithDefeatEvent_ReturnsEmptyRewards()
        {
            var mockRewardService = new MockRewardService();
            var useCase = new ProcessRewardUseCase(mockRewardService);

            var combatEndedEvent = new CombatEndedEvent(
                Guid.NewGuid(),
                3,
                1,
                "Defeat",
                false
            );

            var rewards = useCase.Execute(combatEndedEvent);

            Assert.Empty(rewards);
        }

        [Fact]
        public void Execute_WithNullEvent_ThrowsArgumentNullException()
        {
            var mockRewardService = new MockRewardService();
            var useCase = new ProcessRewardUseCase(mockRewardService);

            Assert.Throws<ArgumentNullException>(() => useCase.Execute(null));
        }

        [Fact]
        public void SelectReward_CallsGrantReward()
        {
            var mockRewardService = new MockRewardService();
            var useCase = new ProcessRewardUseCase(mockRewardService);

            var option = new CardRewardOption("test_id", "TestCard", "Common", null);
            useCase.SelectReward(0, option);

            Assert.True(mockRewardService.GrantRewardCalled);
        }

        [Fact]
        public void OnRewardsGenerated_FiresWhenRewardsGenerated()
        {
            var mockRewardService = new MockRewardService();
            var useCase = new ProcessRewardUseCase(mockRewardService);

            IReadOnlyList<CardRewardOption> capturedRewards = null;
            useCase.OnRewardsGenerated += rewards => capturedRewards = rewards;

            var combatEndedEvent = new CombatEndedEvent(
                Guid.NewGuid(),
                5,
                0,
                "Victory",
                true
            );

            useCase.Execute(combatEndedEvent);

            Assert.NotNull(capturedRewards);
            Assert.Single(capturedRewards);
        }
    }

    internal class MockRewardService : IRewardService
    {
        public bool GrantRewardCalled { get; private set; }

        public IReadOnlyList<CardRewardOption> GenerateRewards(CombatResult result)
        {
            if (!result.IsVictory)
            {
                return Array.Empty<CardRewardOption>();
            }

            return new List<CardRewardOption>
            {
                new CardRewardOption("test_id", "TestCard", "Common", null)
            };
        }

        public void GrantReward(int actorId, CardRewardOption option)
        {
            GrantRewardCalled = true;
        }
    }
}
