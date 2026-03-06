using System;
using System.Collections.Generic;
using OdysseyCards.Application.Combat;
using OdysseyCards.Domain.Combat.Commands;
using OdysseyCards.Domain.Combat.Events;

namespace OdysseyCards.Presentation.Input
{
    public class CombatInputAdapter
    {
        private static CombatInputAdapter _instance;
        public static CombatInputAdapter Instance
        {
            get => _instance;
            set => _instance = value;
        }

        private readonly CombatApplicationService _applicationService;

        public event Action<CombatEvent> OnEvent;

        public CombatInputAdapter(CombatApplicationService applicationService)
        {
            _applicationService = applicationService ?? throw new ArgumentNullException(nameof(applicationService));
            _applicationService.OnEvent += evt => OnEvent?.Invoke(evt);
            Instance = this;
        }

        public IReadOnlyList<CombatEvent> Submit(CombatCommand command)
        {
            return _applicationService.Submit(command);
        }

        public CombatApplicationService GetApplicationService()
        {
            return _applicationService;
        }
    }
}
