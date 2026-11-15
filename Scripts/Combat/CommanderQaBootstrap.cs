#if DEBUG
using System;
using Godot;
using OdysseyCards.AI;
using OdysseyCards.Combat;
using OdysseyCards.Core;

namespace OdysseyCards.Combat;

/// <summary>headless QA 引导：创建玩家 → 覆写指挥官遭遇 → 挂载 Combat 场景 → 等待 Bootstrap 后跑门禁。</summary>
public partial class CommanderQaBootstrap : Node
{
	private int _frames;
	private bool _ran;

	public override void _Ready()
	{
		var gm = GameManager.Instance;
		if (gm == null)
		{
			GD.PrintErr("M3_QA_FAILED: GameManager.Instance 为 null");
			GetTree().Quit(1);
			return;
		}

		gm.SelectedHeroId = "ayame";
		gm.CreateNewPlayer();
		string mode = OS.GetEnvironment("M3_QA_MODE");
		bool frontlineRegression = string.Equals(mode, "frontline", StringComparison.OrdinalIgnoreCase);
		string commanderId = OS.GetEnvironment("M3_COMMANDER_ID");
		if (string.IsNullOrWhiteSpace(commanderId))
			commanderId = "commander_rie";
		gm.FightOverride = frontlineRegression
			? EnemyRegistry.Create("cultist")
			: EnemyRegistry.Create(commanderId);
		var scene = GD.Load<PackedScene>("res://Scenes/Combat.tscn");
		AddChild(scene.Instantiate());
	}

	public override void _Process(double delta)
	{
		if (_ran)
			return;
		_frames++;
		var combat = CombatManager.Instance;
		if (combat?.State == null || combat.State.Phase == CombatPhase.NotStarted || combat.EnemyUnits.Count == 0)
		{
			if (_frames > 1200)
			{
				GD.PrintErr("M3_QA_FAILED: 等待 CombatManager Bootstrap 超时");
				System.Environment.Exit(1);
			}
			return;
		}

		_ran = true;
		try
		{
			string mode = OS.GetEnvironment("M3_QA_MODE");
			if (string.Equals(mode, "frontline", StringComparison.OrdinalIgnoreCase))
			{
				GD.Print(FrontlineRuntimeQa.Run(combat));
				GD.Print("M1_M2_REGRESSION_OK");
			}
			else
			{
				GD.Print(CommanderRuntimeQa.Run(combat,
					OS.GetEnvironment("M3_COMMANDER_ID") is { Length: > 0 } id ? id.Replace("commander_", "", StringComparison.Ordinal) : "rie"));
				GD.Print("M3_COMMANDER_QA_EXIT 0");
			}
			GetTree().Quit(0);
		}
		catch (Exception exception)
		{
			GD.PrintErr(exception.Message);
			GD.PrintErr(exception.ToString());
			GD.Print("M3_COMMANDER_QA_EXIT 1");
			System.Environment.Exit(1);
		}
	}
}

#endif
