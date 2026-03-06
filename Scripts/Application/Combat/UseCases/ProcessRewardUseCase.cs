using System;
using System.Collections.Generic;
using OdysseyCards.Application.Ports;
using OdysseyCards.Domain.Combat.Events;

namespace OdysseyCards.Application.Combat.UseCases
{
    public sealed class ProcessRewardUseCase
    {
        private readonly IRewardService _rewardService;

        public event Action<IReadOnlyList<CardRewardOption>> OnRewardsGenerated;

        public ProcessRewardUseCase(IRewardService rewardService)
        {
            _rewardService = rewardService ?? throw new ArgumentNullException(nameof(rewardService));
        }

        public IReadOnlyList<CardRewardOption> Execute(CombatEndedEvent combatEndedEvent)
        {
            if (combatEndedEvent == null)
            {
                throw new ArgumentNullException(nameof(combatEndedEvent));
            }

            if (!combatEndedEvent.IsVictory)
            {
                return Array.Empty<CardRewardOption>();
            }

            var result = new CombatResult(
                isVictory: combatEndedEvent.IsVictory,
                winnerActorId: combatEndedEvent.WinnerActorId,
                turnCount: combatEndedEvent.Turn,
                enemyCount: 1,
                remainingPlayerHQHealth: 0,
                remainingEnemyHQHealth: 0
            );

            var rewards = _rewardService.GenerateRewards(result);
            OnRewardsGenerated?.Invoke(rewards);
            return rewards;
        }

        public void SelectReward(int actorId, CardRewardOption option)
        {
            if (option == null)
            {
                throw new ArgumentNullException(nameof(option));
            }

            _rewardService.GrantReward(actorId, option);
        }
    }
}
