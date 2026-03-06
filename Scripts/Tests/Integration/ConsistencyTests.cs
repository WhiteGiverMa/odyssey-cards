using System;
using System.Collections.Generic;
using System.Linq;
using OdysseyCards.Domain.Combat.Commands;
using OdysseyCards.Domain.Combat.Events;
using OdysseyCards.Domain.Combat.Engine;
using OdysseyCards.Legacy.Adapters;

namespace OdysseyCards.Tests.Integration
{
    public static class ConsistencyTests
    {
        private static int _passedTests = 0;
        private static int _failedTests = 0;
        private static readonly List<string> _failures = new();

        public static void RunAll()
        {
            Console.WriteLine("=== Consistency Tests ===\n");

            Test_SameSeed_SameEvents();
            Test_SameCommandsSameEvents();
            Test_NewEngineProducesSameEventTypes();
            Test_DamageValuesConsistent();
            Test_TurnSequenceConsistent();

            Console.WriteLine($"\n=== Results: {_passedTests} passed, {_failedTests} failed ===");
            if (_failures.Count > 0)
            {
                Console.WriteLine("\nFailures:");
                foreach (var f in _failures)
                {
                    Console.WriteLine($"  - {f}");
                }
            }
        }

        private static CombatSetup CreateTestSetup(int playerHealth = 80, int enemyHealth = 8)
        {
            return new CombatSetup
            {
                PlayerId = 0,
                PlayerStartingHealth = playerHealth,
                PlayerStartingEnergy = 3,
                PlayerMaxEnergy = 3,
                EnemyIds = new List<int> { 1 },
                EnemyStartingHealths = new List<int> { enemyHealth },
                EnemyStartingEnergies = new List<int> { 3 },
                EnemyMaxEnergies = new List<int> { 3 },
                IsPlayerFirst = true
            };
        }

        private static void Assert(bool condition, string testName, string message = "")
        {
            if (condition)
            {
                _passedTests++;
                Console.WriteLine($"  [PASS] {testName}");
            }
            else
            {
                _failedTests++;
                _failures.Add($"{testName}: {message}");
                Console.WriteLine($"  [FAIL] {testName} - {message}");
            }
        }

        private static void Test_SameSeed_SameEvents()
        {
            int seed = 12345;
            var oldEngine = new LegacyCombatEngine(null);
            var newEngine = new DomainCombatEngine();

            var setup = CreateTestSetup();
            oldEngine.StartCombat(setup, seed);
            newEngine.StartCombat(setup, seed);

            var oldEvents = new List<CombatEvent>();
            var newEvents = new List<CombatEvent>();
            oldEngine.OnEvent += e => oldEvents.Add(e);
            newEngine.OnEvent += e => newEvents.Add(e);

            for (int i = 0; i < 10; i++)
            {
                var command = new DeployUnitCommand(1, 0, 100 + i, 0);
                var oldResult = oldEngine.Submit(command);
                var newResult = newEngine.Submit(command);

                Assert(oldResult.Count == newResult.Count, $"DeployUnit iteration {i} event count match");
            }

            Assert(oldEvents.Count == newEvents.Count, "Total event count match");
        }

        private static void Test_SameCommandsSameEvents()
        {
            var oldEngine = new LegacyCombatEngine(null);
            var newEngine = new DomainCombatEngine();

            var setup = CreateTestSetup();
            oldEngine.StartCombat(setup, 12345);
            newEngine.StartCombat(setup, 12345);

            var commands = new List<CombatCommand>
            {
                new DeployUnitCommand(1, 0, 100, 0),
                new EndTurnCommand(1, 0),
                new DeployUnitCommand(2, 0, 200, 6),
                new EndTurnCommand(2, 0)
            };

            var oldEvents = new List<CombatEvent>();
            var newEvents = new List<CombatEvent>();

            foreach (var cmd in commands)
            {
                oldEvents.AddRange(oldEngine.Submit(cmd));
                newEvents.AddRange(newEngine.Submit(cmd));
            }

            Assert(oldEvents.Count == newEvents.Count, "Total events count match");
            Assert(EventsSequenceMatch(oldEvents, newEvents), "Event sequence match");
        }

        private static void Test_NewEngineProducesSameEventTypes()
        {
            var engine = new DomainCombatEngine();
            var setup = CreateTestSetup();
            var events = new List<CombatEvent>();
            engine.OnEvent += e => events.Add(e);

            engine.StartCombat(setup, 12345);
            engine.Submit(new DeployUnitCommand(1, 0, 100, 0));
            engine.Submit(new EndTurnCommand(1, 0));

            bool hasCombatStarted = events.Any(e => e is CombatStartedEvent);
            bool hasTurnStarted = events.Any(e => e is TurnStartedEvent);
            bool hasUnitDeployed = events.Any(e => e is UnitDeployedEvent);
            bool hasTurnEnded = events.Any(e => e is TurnEndedEvent);

            Assert(hasCombatStarted, "CombatStartedEvent produced");
            Assert(hasTurnStarted, "TurnStartedEvent produced");
            Assert(hasUnitDeployed, "UnitDeployedEvent produced");
            Assert(hasTurnEnded, "TurnEndedEvent produced");
        }

        private static void Test_DamageValuesConsistent()
        {
            var oldEngine = new LegacyCombatEngine(null);
            var newEngine = new DomainCombatEngine();

            var setup = CreateTestSetup(10, 10);

            oldEngine.StartCombat(setup, 12345);
            newEngine.StartCombat(setup, 12345);

            var deployCmd = new DeployUnitCommand(1, 0, 100, 0);
            oldEngine.Submit(deployCmd);
            newEngine.Submit(deployCmd);

            var attackCmd = new AttackCommand(1, 0, 1, 6, null);
            var oldEvents = oldEngine.Submit(attackCmd);
            var newEvents = newEngine.Submit(attackCmd);

            var oldDamage = oldEvents.OfType<DamageAppliedEvent>().FirstOrDefault();
            var newDamage = newEvents.OfType<DamageAppliedEvent>().FirstOrDefault();

            Assert(oldDamage != null && newDamage != null, "Damage events produced");
            if (oldDamage != null && newDamage != null)
            {
                Assert(oldDamage.Amount == newDamage.Amount, "Damage amount matches");
            }
        }

        private static void Test_TurnSequenceConsistent()
        {
            var engine = new DomainCombatEngine();
            var setup = CreateTestSetup();
            var events = new List<CombatEvent>();
            engine.OnEvent += e => events.Add(e);

            engine.StartCombat(setup, 12345);

            Assert(events.Count == 2, "CombatStarted + TurnStarted events");
            Assert(events[0] is CombatStartedEvent, "First event is CombatStartedEvent");
            Assert(events[1] is TurnStartedEvent, "Second event is TurnStartedEvent");

            events.Clear();
            engine.Submit(new EndTurnCommand(1, 0));

            Assert(events.Count == 2, "TurnEnded + TurnStarted events after EndTurn");
            Assert(events[0] is TurnEndedEvent, "First event after EndTurn is TurnEndedEvent");
            Assert(events[1] is TurnStartedEvent, "Second event after EndTurn is TurnStartedEvent");
        }

        private static bool EventsSequenceMatch(List<CombatEvent> oldEvents, List<CombatEvent> newEvents)
        {
            if (oldEvents.Count != newEvents.Count)
            {
                return false;
            }

            for (int i = 0; i < oldEvents.Count; i++)
            {
                var oldEvt = oldEvents[i];
                var newEvt = newEvents[i];

                if (oldEvt.GetType() != newEvt.GetType())
                {
                    return false;
                }
            }

            return true;
        }
    }
}
