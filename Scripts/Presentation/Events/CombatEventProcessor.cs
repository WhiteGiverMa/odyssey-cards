using System;
using System.Collections.Generic;
using Godot;
using OdysseyCards.Application.Combat;
using OdysseyCards.Domain.Combat.Events;
using OdysseyCards.Domain.Combat.Engine;

namespace OdysseyCards.Presentation.Events
{
    public sealed class CombatEventProcessor : IDisposable
    {
        private readonly CombatApplicationService _applicationService;
        private readonly CombatEventUIBridge _uiBridge;
        private bool _disposed;

        public event Action<UnitDeployedEvent> OnUnitDeployed;
        public event Action<UnitMovedEvent> OnUnitMoved;
        public event Action<DamageAppliedEvent> OnDamageApplied;
        public event Action<UnitDestroyedEvent> OnUnitDestroyed;
        public event Action<TurnStartedEvent> OnTurnStarted;
        public event Action<TurnEndedEvent> OnTurnEnded;
        public event Action<CombatStartedEvent> OnCombatStarted;
        public event Action<CombatEndedEvent> OnCombatEnded;

        public CombatEventProcessor(CombatApplicationService applicationService, CombatEventUIBridge uiBridge)
        {
            _applicationService = applicationService ?? throw new ArgumentNullException(nameof(applicationService));
            _uiBridge = uiBridge;
            _applicationService.OnEvent += HandleEvent;
        }

        private void HandleEvent(CombatEvent evt)
        {
            GD.Print($"[CombatEventProcessor] Received event: {evt.GetType().Name}");

            switch (evt)
            {
                case CombatStartedEvent combatStarted:
                    OnCombatStarted?.Invoke(combatStarted);
                    _uiBridge?.HandleCombatStarted(combatStarted);
                    break;

                case TurnStartedEvent turnStarted:
                    OnTurnStarted?.Invoke(turnStarted);
                    _uiBridge?.HandleTurnStarted(turnStarted);
                    break;

                case TurnEndedEvent turnEnded:
                    OnTurnEnded?.Invoke(turnEnded);
                    _uiBridge?.HandleTurnEnded(turnEnded);
                    break;

                case UnitDeployedEvent unitDeployed:
                    OnUnitDeployed?.Invoke(unitDeployed);
                    _uiBridge?.HandleUnitDeployed(unitDeployed);
                    break;

                case UnitMovedEvent unitMoved:
                    OnUnitMoved?.Invoke(unitMoved);
                    _uiBridge?.HandleUnitMoved(unitMoved);
                    break;

                case DamageAppliedEvent damageApplied:
                    OnDamageApplied?.Invoke(damageApplied);
                    _uiBridge?.HandleDamageApplied(damageApplied);
                    break;

                case UnitDestroyedEvent unitDestroyed:
                    OnUnitDestroyed?.Invoke(unitDestroyed);
                    _uiBridge?.HandleUnitDestroyed(unitDestroyed);
                    break;

                case CombatEndedEvent combatEnded:
                    OnCombatEnded?.Invoke(combatEnded);
                    _uiBridge?.HandleCombatEnded(combatEnded);
                    break;
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _applicationService.OnEvent -= HandleEvent;
                _disposed = true;
            }
        }
    }
}
