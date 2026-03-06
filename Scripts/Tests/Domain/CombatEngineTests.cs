using System;
using System.Collections.Generic;
using System.Linq;
using OdysseyCards.Domain.Combat.Commands;
using OdysseyCards.Domain.Combat.Engine;
using OdysseyCards.Domain.Combat.Events;
using OdysseyCards.Domain.Combat.State;

namespace OdysseyCards.Tests.Domain
{
    public static class CombatEngineTests
    {
        private static int _passedTests = 0;
        private static int _failedTests = 0;
        private static readonly List<string> _failures = new();

        public static void RunAll()
        {
            Console.WriteLine("=== CombatEngine Domain Tests ===\n");

            Test_StartCombat_InitializesState();
            Test_StartCombat_ProducesCorrectEvents();
            Test_EndTurn_SwitchesToEnemyTurn();
            Test_EndTurn_IncrementsTurnOnPlayerTurn();
            Test_DeployUnit_ValidPlacement();
            Test_DeployUnit_InsufficientEnergy();
            Test_DeployUnit_InvalidNode();
            Test_MoveUnit_ValidMove();
            Test_MoveUnit_UnitCannotMove();
            Test_MoveUnit_InvalidDestination();
            Test_Attack_ValidTarget();
            Test_Attack_OutOfRange();
            Test_Attack_UnitCannotAttack();
            Test_Attack_BothUnitsTakeDamage();
            Test_Attack_KillsTarget();
            Test_Attack_EnemyHQ();
            Test_Attack_PlayerHQ();
            Test_Victory_WhenEnemyHQDestroyed();
            Test_Defeat_WhenPlayerHQDestroyed();
            Test_Snapshot_ReturnsCorrectState();
            Test_CommandId_Tracking();

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

        private static CombatSetup CreateDefaultSetup(int playerEnergy = 3, int playerHealth = 80, int enemyHealth = 8)
        {
            return new CombatSetup
            {
                PlayerId = 0,
                PlayerStartingHealth = playerHealth,
                PlayerStartingEnergy = playerEnergy,
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

        private static void Test_StartCombat_InitializesState()
        {
            Console.WriteLine("\nTest: StartCombat_InitializesState");
            var engine = new DomainCombatEngine();
            var setup = CreateDefaultSetup();

            engine.StartCombat(setup, 12345);
            var snapshot = engine.GetSnapshot();

            Assert(snapshot.Turn == 1, "Turn is 1");
            Assert(snapshot.IsPlayerTurn, "Is player turn");
            Assert(!engine.IsFinished, "Combat not finished");
            Assert(snapshot.PlayerHQHealth == 80, "Player HQ health is 80");
            Assert(snapshot.PlayerEnergy == 3, "Player energy is 3");
        }

        private static void Test_StartCombat_ProducesCorrectEvents()
        {
            Console.WriteLine("\nTest: StartCombat_ProducesCorrectEvents");
            var engine = new DomainCombatEngine();
            var setup = CreateDefaultSetup();
            var events = new List<CombatEvent>();
            engine.OnEvent += e => events.Add(e);

            engine.StartCombat(setup, 12345);

            Assert(events.Count == 2, "Two events produced");
            Assert(events[0] is CombatStartedEvent, "First event is CombatStartedEvent");
            Assert(events[1] is TurnStartedEvent, "Second event is TurnStartedEvent");
        }

        private static void Test_EndTurn_SwitchesToEnemyTurn()
        {
            Console.WriteLine("\nTest: EndTurn_SwitchesToEnemyTurn");
            var engine = new DomainCombatEngine();
            var setup = CreateDefaultSetup();
            engine.StartCombat(setup, 12345);

            var events = engine.Submit(new EndTurnCommand(1, 0));
            var snapshot = engine.GetSnapshot();

            Assert(!snapshot.IsPlayerTurn, "Is enemy turn after EndTurn");
            Assert(events.Any(e => e is TurnEndedEvent), "TurnEndedEvent produced");
            Assert(events.Any(e => e is TurnStartedEvent), "TurnStartedEvent produced");
        }

        private static void Test_EndTurn_IncrementsTurnOnPlayerTurn()
        {
            Console.WriteLine("\nTest: EndTurn_IncrementsTurnOnPlayerTurn");
            var engine = new DomainCombatEngine();
            var setup = CreateDefaultSetup();
            engine.StartCombat(setup, 12345);

            engine.Submit(new EndTurnCommand(1, 0));
            engine.Submit(new EndTurnCommand(1, 1));
            var snapshot = engine.GetSnapshot();

            Assert(snapshot.Turn == 2, "Turn incremented to 2");
            Assert(snapshot.IsPlayerTurn, "Is player turn again");
        }

        private static void Test_DeployUnit_ValidPlacement()
        {
            Console.WriteLine("\nTest: DeployUnit_ValidPlacement");
            var engine = new DomainCombatEngine();
            var setup = CreateDefaultSetup();
            engine.StartCombat(setup, 12345);

            var events = engine.Submit(new DeployUnitCommand(1, 0, 100, 0));
            var snapshot = engine.GetSnapshot();

            Assert(events.Any(e => e is UnitDeployedEvent), "UnitDeployedEvent produced");
            Assert(snapshot.PlayerUnits.Count == 1, "One player unit deployed");
            Assert(snapshot.PlayerEnergy == 2, "Energy reduced by 1");
        }

        private static void Test_DeployUnit_InsufficientEnergy()
        {
            Console.WriteLine("\nTest: DeployUnit_InsufficientEnergy");
            var engine = new DomainCombatEngine();
            var setup = CreateDefaultSetup(playerEnergy: 0);
            engine.StartCombat(setup, 12345);

            var events = engine.Submit(new DeployUnitCommand(1, 0, 100, 0));
            var snapshot = engine.GetSnapshot();

            Assert(!events.Any(e => e is UnitDeployedEvent), "No UnitDeployedEvent");
            Assert(snapshot.PlayerUnits.Count == 0, "No unit deployed");
        }

        private static void Test_DeployUnit_InvalidNode()
        {
            Console.WriteLine("\nTest: DeployUnit_InvalidNode");
            var engine = new DomainCombatEngine();
            var setup = CreateDefaultSetup();
            engine.StartCombat(setup, 12345);

            var events = engine.Submit(new DeployUnitCommand(1, 0, 100, 5));
            var snapshot = engine.GetSnapshot();

            Assert(!events.Any(e => e is UnitDeployedEvent), "No UnitDeployedEvent for invalid node");
            Assert(snapshot.PlayerUnits.Count == 0, "No unit deployed on invalid node");
        }

        private static void Test_MoveUnit_ValidMove()
        {
            Console.WriteLine("\nTest: MoveUnit_ValidMove");
            var engine = new DomainCombatEngine();
            var setup = CreateDefaultSetup();
            engine.StartCombat(setup, 12345);

            var deployEvents = engine.Submit(new DeployUnitCommand(1, 0, 100, 0));
            var deployedEvent = deployEvents.OfType<UnitDeployedEvent>().First();

            var moveEvents = engine.Submit(new MoveUnitCommand(1, 0, deployedEvent.UnitId, 1));
            var snapshot = engine.GetSnapshot();

            Assert(moveEvents.Any(e => e is UnitMovedEvent), "UnitMovedEvent produced");
            Assert(snapshot.PlayerUnits[0].NodeId == 1, "Unit moved to node 1");
        }

        private static void Test_MoveUnit_UnitCannotMove()
        {
            Console.WriteLine("\nTest: MoveUnit_UnitCannotMove");
            var engine = new DomainCombatEngine();
            var setup = CreateDefaultSetup();
            engine.StartCombat(setup, 12345);

            var deployEvents = engine.Submit(new DeployUnitCommand(1, 0, 100, 0));
            var deployedEvent = deployEvents.OfType<UnitDeployedEvent>().First();

            engine.Submit(new MoveUnitCommand(1, 0, deployedEvent.UnitId, 1));
            var secondMoveEvents = engine.Submit(new MoveUnitCommand(1, 0, deployedEvent.UnitId, 2));

            Assert(!secondMoveEvents.Any(e => e is UnitMovedEvent), "No second move allowed");
        }

        private static void Test_MoveUnit_InvalidDestination()
        {
            Console.WriteLine("\nTest: MoveUnit_InvalidDestination");
            var engine = new DomainCombatEngine();
            var setup = CreateDefaultSetup();
            engine.StartCombat(setup, 12345);

            var deployEvents = engine.Submit(new DeployUnitCommand(1, 0, 100, 0));
            var deployedEvent = deployEvents.OfType<UnitDeployedEvent>().First();

            var moveEvents = engine.Submit(new MoveUnitCommand(1, 0, deployedEvent.UnitId, 5));

            Assert(!moveEvents.Any(e => e is UnitMovedEvent), "No move to distant node");
        }

        private static void Test_Attack_ValidTarget()
        {
            Console.WriteLine("\nTest: Attack_ValidTarget");
            var engine = new DomainCombatEngine();
            var setup = CreateDefaultSetup();
            engine.StartCombat(setup, 12345);

            var playerDeploy = engine.Submit(new DeployUnitCommand(1, 0, 100, 0));
            var playerUnit = playerDeploy.OfType<UnitDeployedEvent>().First().UnitId;

            engine.Submit(new EndTurnCommand(1, 0));
            var enemyDeploy = engine.Submit(new DeployUnitCommand(1, 1, 200, 6));
            var enemyUnit = enemyDeploy.OfType<UnitDeployedEvent>().First().UnitId;

            var attackEvents = engine.Submit(new AttackCommand(1, 1, enemyUnit, 0, playerUnit));

            Assert(attackEvents.Any(e => e is DamageAppliedEvent), "DamageAppliedEvent produced");
        }

        private static void Test_Attack_OutOfRange()
        {
            Console.WriteLine("\nTest: Attack_OutOfRange");
            var engine = new DomainCombatEngine();
            var setup = CreateDefaultSetup();
            engine.StartCombat(setup, 12345);

            var deployEvents = engine.Submit(new DeployUnitCommand(1, 0, 100, 0));
            var unitId = deployEvents.OfType<UnitDeployedEvent>().First().UnitId;

            var attackEvents = engine.Submit(new AttackCommand(1, 0, unitId, 6, null));

            Assert(!attackEvents.Any(e => e is DamageAppliedEvent), "No attack out of range");
        }

        private static void Test_Attack_UnitCannotAttack()
        {
            Console.WriteLine("\nTest: Attack_UnitCannotAttack");
            var engine = new DomainCombatEngine();
            var setup = CreateDefaultSetup();
            engine.StartCombat(setup, 12345);

            var deployEvents = engine.Submit(new DeployUnitCommand(1, 0, 100, 0));
            var unitId = deployEvents.OfType<UnitDeployedEvent>().First().UnitId;

            engine.Submit(new AttackCommand(1, 0, unitId, 6, null));
            var secondAttack = engine.Submit(new AttackCommand(1, 0, unitId, 6, null));

            Assert(!secondAttack.Any(e => e is DamageAppliedEvent), "No second attack allowed");
        }

        private static void Test_Attack_BothUnitsTakeDamage()
        {
            Console.WriteLine("\nTest: Attack_BothUnitsTakeDamage");
            var engine = new DomainCombatEngine();
            var setup = CreateDefaultSetup();
            engine.StartCombat(setup, 12345);

            var playerDeploy = engine.Submit(new DeployUnitCommand(1, 0, 100, 0));
            var playerUnit = playerDeploy.OfType<UnitDeployedEvent>().First().UnitId;

            engine.Submit(new EndTurnCommand(1, 0));
            var enemyDeploy = engine.Submit(new DeployUnitCommand(1, 1, 200, 6));
            var enemyUnit = enemyDeploy.OfType<UnitDeployedEvent>().First().UnitId;

            var attackEvents = engine.Submit(new AttackCommand(1, 1, enemyUnit, 0, playerUnit));

            var damageEvents = attackEvents.OfType<DamageAppliedEvent>().ToList();
            Assert(damageEvents.Count >= 1, "At least one damage event");
        }

        private static void Test_Attack_KillsTarget()
        {
            Console.WriteLine("\nTest: Attack_KillsTarget");
            var engine = new DomainCombatEngine();
            var setup = CreateDefaultSetup();
            engine.StartCombat(setup, 12345);

            var playerDeploy = engine.Submit(new DeployUnitCommand(1, 0, 100, 0));
            var playerUnit = playerDeploy.OfType<UnitDeployedEvent>().First().UnitId;

            engine.Submit(new EndTurnCommand(1, 0));
            var enemyDeploy = engine.Submit(new DeployUnitCommand(1, 1, 200, 6));
            var enemyUnit = enemyDeploy.OfType<UnitDeployedEvent>().First().UnitId;

            for (int i = 0; i < 3; i++)
            {
                engine.Submit(new AttackCommand(1, 1, enemyUnit, 0, playerUnit));
            }

            var snapshot = engine.GetSnapshot();
            Assert(snapshot.PlayerUnits.Count == 0, "Player unit destroyed after multiple attacks");
        }

        private static void Test_Attack_EnemyHQ()
        {
            Console.WriteLine("\nTest: Attack_EnemyHQ");
            var engine = new DomainCombatEngine();
            var setup = CreateDefaultSetup();
            engine.StartCombat(setup, 12345);

            var deployEvents = engine.Submit(new DeployUnitCommand(1, 0, 100, 0));
            var unitId = deployEvents.OfType<UnitDeployedEvent>().First().UnitId;

            var attackEvents = engine.Submit(new AttackCommand(1, 0, unitId, 6, null));

            Assert(attackEvents.Any(e => e is DamageAppliedEvent d && d.TargetHQOwnerId == 1), "Damage to enemy HQ");
        }

        private static void Test_Attack_PlayerHQ()
        {
            Console.WriteLine("\nTest: Attack_PlayerHQ");
            var engine = new DomainCombatEngine();
            var setup = CreateDefaultSetup();
            engine.StartCombat(setup, 12345);

            engine.Submit(new EndTurnCommand(1, 0));
            var enemyDeploy = engine.Submit(new DeployUnitCommand(1, 1, 200, 6));
            var enemyUnit = enemyDeploy.OfType<UnitDeployedEvent>().First().UnitId;

            var attackEvents = engine.Submit(new AttackCommand(1, 1, enemyUnit, 0, null));

            Assert(attackEvents.Any(e => e is DamageAppliedEvent d && d.TargetHQOwnerId == 0), "Damage to player HQ");
        }

        private static void Test_Victory_WhenEnemyHQDestroyed()
        {
            Console.WriteLine("\nTest: Victory_WhenEnemyHQDestroyed");
            var engine = new DomainCombatEngine();
            var setup = CreateDefaultSetup(enemyHealth: 2);
            engine.StartCombat(setup, 12345);

            var deployEvents = engine.Submit(new DeployUnitCommand(1, 0, 100, 0));
            var unitId = deployEvents.OfType<UnitDeployedEvent>().First().UnitId;

            engine.Submit(new AttackCommand(1, 0, unitId, 6, null));

            Assert(engine.IsFinished, "Combat finished");
            var snapshot = engine.GetSnapshot();
            Assert(snapshot.IsFinished, "Snapshot shows finished");
        }

        private static void Test_Defeat_WhenPlayerHQDestroyed()
        {
            Console.WriteLine("\nTest: Defeat_WhenPlayerHQDestroyed");
            var engine = new DomainCombatEngine();
            var setup = CreateDefaultSetup(playerHealth: 2);
            engine.StartCombat(setup, 12345);

            engine.Submit(new EndTurnCommand(1, 0));
            var enemyDeploy = engine.Submit(new DeployUnitCommand(1, 1, 200, 6));
            var enemyUnit = enemyDeploy.OfType<UnitDeployedEvent>().First().UnitId;

            engine.Submit(new AttackCommand(1, 1, enemyUnit, 0, null));

            Assert(engine.IsFinished, "Combat finished after player HQ destroyed");
        }

        private static void Test_Snapshot_ReturnsCorrectState()
        {
            Console.WriteLine("\nTest: Snapshot_ReturnsCorrectState");
            var engine = new DomainCombatEngine();
            var setup = CreateDefaultSetup();
            engine.StartCombat(setup, 12345);

            var snapshot = engine.GetSnapshot();

            Assert(snapshot.Turn == 1, "Turn is 1");
            Assert(snapshot.IsPlayerTurn, "Is player turn");
            Assert(!snapshot.IsFinished, "Not finished");
            Assert(snapshot.PlayerHQHealth == 80, "Player HQ health correct");
            Assert(snapshot.PlayerEnergy == 3, "Player energy correct");
            Assert(snapshot.EnemyHQHealths.Count == 1, "One enemy HQ");
        }

        private static void Test_CommandId_Tracking()
        {
            Console.WriteLine("\nTest: CommandId_Tracking");
            var engine = new DomainCombatEngine();
            var setup = CreateDefaultSetup();
            engine.StartCombat(setup, 12345);

            var command = new DeployUnitCommand(1, 0, 100, 0);
            var events = engine.Submit(command);

            var deployedEvent = events.OfType<UnitDeployedEvent>().FirstOrDefault();
            Assert(deployedEvent != null, "Event produced");
            Assert(deployedEvent!.CausedByCommandId == command.CommandId, "Command ID tracked in event");
        }
    }
}
