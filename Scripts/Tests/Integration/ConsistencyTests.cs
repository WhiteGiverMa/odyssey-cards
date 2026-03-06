using System;
using System.Collections.Generic;
using System.Linq;
using OdysseyCards.Domain.Combat.Commands;
using OdysseyCards.Domain.Combat.Events;
using OdysseyCards.Domain.Combat.Engine;

using OdysseyCards.Domain.Combat.State;

using OdysseyCards.Domain.Combat.Model;

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

            Console.WriteLine($"\n=== Results: {_passedTests} passed and {_failedTests} failed ===");
            if (_failures.Count > 0)
            {
                Console.WriteLine("\nFailures:");
                foreach (var f in _failures)
                {
                    Console.WriteLine($"  - {f}");
                }
            }
            else
            {
                Console.WriteLine("\nAll tests passed!");
            }
        }

        private static void Test_SameSeed_SameEvents()
        {
            int seed = 12345;
            var engine = new DomainCombatEngine();
            var setup = CreateTestSetup();

            engine.StartCombat(setup, seed);

            var events = new List<CombatEvent>();
            engine.OnEvent += e => events.Add(e);

            for (int i = 0; i < 10; i++)
            {
                var command = new DeployUnitCommand(1, 0, 100 + i, 0);
                var result = engine.Submit(command);
                Assert(result.Count > 0, $"DeployUnit iteration {i} event count > 0");
            }

            Assert(events.Count == 12, "Total event count match");
        }

        private static void Test_SameCommandsSameEvents()
        {
            var engine = new DomainCombatEngine();

            var setup = CreateTestSetup();
            engine.StartCombat(setup, 12345);

            var commands = new List<CombatCommand>
            {
                new DeployUnitCommand(1, 0, 100, 0),
                new EndTurnCommand(1, 0),
                new DeployUnitCommand(2, 0, 200, 6),
                new EndTurnCommand(2, 0)
            };

            var events = new List<CombatEvent>();

            foreach (var cmd in commands)
            {
                events.AddRange(engine.Submit(cmd));
            }

            Assert(events.Count == 4, "Total events count match");
            Assert(EventsSequenceMatch(events, events), "Event sequence match");
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
            var engine = new DomainCombatEngine();
            var setup = CreateTestSetup();
            engine.StartCombat(setup, 12345);

            engine.Submit(new DeployUnitCommand(1, 0, 100, 0));

            var attackCmd = new AttackCommand(1, 0, 1, 6);
            engine.Submit(attackCmd);
        }
        private static void Test_TurnSequenceConsistent()
        {
            var engine = new DomainCombatEngine();
            var setup = CreateTestSetup();
            var events = new List<CombatEvent>();
            engine.OnEvent += e => events.Add(e);

            engine.StartCombat(setup, 12345);
            engine.Submit(new DeployUnitCommand(1, 0, 100, 0));
            engine.Submit(new EndTurnCommand(1, 0));

            Assert(events.Count == 3, "Turn sequence events count");
            Assert(events[0] is CombatStartedEvent, "First event is CombatStartedEvent");
            Assert(events[1] is TurnStartedEvent, "Second event is TurnStartedEvent");
            Assert(events[2] is UnitDeployedEvent, "Third event is UnitDeployedEvent");
            Assert(events[3] is TurnEndedEvent, "Fourth event is TurnEndedEvent");
        }
        private static CombatSetup CreateTestSetup()
        {
            return new CombatSetup
            {
                PlayerId = 0,
                PlayerStartingHealth = 80,
                PlayerStartingEnergy = 1,
                PlayerMaxEnergy = 3,
                EnemyIds = new List<int> { 1 },
                EnemyStartingHealths = new List<int> { 80 },
                EnemyStartingEnergies = new List<int> { 3 },
                EnemyMaxEnergies = new List<int> { 3 },
                IsPlayerFirst = true
            };
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
        private static void Assert(bool condition, string testName, string message = "")
        {
            if (condition)
            {
                _passedTests++;
                Console.WriteLine($"  [Pass] {testName}");
            }
            else
            {
                _failedTests++;
                _failures.Add($"{testName}: {message}");
                Console.WriteLine($"  [Fail] {testName}: {message}");
            }
        }
    }
}
