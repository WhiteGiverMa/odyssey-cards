using System;
using System.Collections.Generic;
using OdysseyCards.Application.Combat.UseCases;
using OdysseyCards.Domain.Combat.Commands;
using OdysseyCards.Domain.Combat.Engine;
using OdysseyCards.Domain.Combat.Events;

namespace OdysseyCards.Application.Combat
{
    public class CombatApplicationService
    {
        private readonly ICombatEngine _engine;
        private readonly IReplayWriter _replayWriter;
        private readonly ProcessRewardUseCase _rewardUseCase;

        public event Action<CombatEvent> OnEvent;

        public CombatApplicationService(ICombatEngine engine, IReplayWriter replayWriter = null, ProcessRewardUseCase rewardUseCase = null)
        {
            _engine = engine ?? throw new ArgumentNullException(nameof(engine));
            _replayWriter = replayWriter;
            _rewardUseCase = rewardUseCase;

            _engine.OnEvent += OnEngineEvent;
        }

        private void OnEngineEvent(CombatEvent evt)
        {
            OnEvent?.Invoke(evt);

            if (evt is CombatEndedEvent combatEndedEvent && _rewardUseCase != null)
            {
                _ = _rewardUseCase.Execute(combatEndedEvent);
            }
        }

        public void StartCombat(CombatSetup setup, int seed)
        {
            _engine.StartCombat(setup, seed);
        }

        public IReadOnlyList<CombatEvent> Submit(CombatCommand command)
        {
            _replayWriter?.WriteCommand(command);

            return _engine.Submit(command);
        }

        public CombatSnapshot GetSnapshot()
        {
            return _engine.GetSnapshot();
        }

        public bool IsFinished => _engine.IsFinished;
    }

    public interface IReplayWriter
    {
        void WriteCommand(CombatCommand command);
        void Flush();
    }
}
