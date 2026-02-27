using Godot;
using System;
using System.Collections.Generic;
using System.IO;

namespace OdysseyCards.Core;

public static class PlaceholderAssetGenerator
{
    public static void GenerateAllPlaceholders()
    {
        GenerateCardPlaceholders();
        GenerateEnemyPlaceholders();
        GenerateIconPlaceholders();
        GenerateUIPlaceholders();
        GD.Print("All placeholder assets generated!");
    }

    public static void GenerateCardPlaceholders()
    {
        var cardNames = new List<string>
        {
            "Strike", "Defend", "Bash", "Cleave", "IronWave",
            "PommelStrike", "TwinStrike", "Anger", "Clothesline",
            "HeavyBlade", "ShrugItOff", "Armaments", "Flex",
            "BattleTrance", "Bloodletting", "FlameBarrier", "Impervious",
            "Intimidate", "BodySlam", "Rage", "Clash",
            "Cleave_Upgraded", "Defend_Upgraded", "Strike_Upgraded", "Bash_Upgraded"
        };

        string outputPath = "res://Assets/Cards";
        EnsureDirectoryExists(outputPath);

        foreach (var cardName in cardNames)
        {
            string fileName = $"{outputPath}/{cardName}.png";
            string globalPath = ProjectSettings.GlobalizePath(fileName);
            if (!File.Exists(globalPath))
            {
                GeneratePlaceholderImage(fileName, 256, 256, new Color(0.95f, 0.95f, 0.95f));
            }
        }
        
        GD.Print("Card placeholders ready");
    }

    public static void GenerateEnemyPlaceholders()
    {
        var enemyNames = new List<string>
        {
            "Slime_Small", "Slime_Medium", "Slime_Large",
            "Goblin", "GoblinElite", "Cultist",
            "JawWorm", "Louse", "FungiBeast",
            "Gremlin", "Slaver", "SlaverElite",
            "Boss_Guardian", "Boss_Hexaghost", "Boss_SlimeBoss"
        };

        string outputPath = "res://Assets/Enemies";
        EnsureDirectoryExists(outputPath);

        foreach (var enemyName in enemyNames)
        {
            string fileName = $"{outputPath}/{enemyName}.png";
            string globalPath = ProjectSettings.GlobalizePath(fileName);
            if (!File.Exists(globalPath))
            {
                GeneratePlaceholderImage(fileName, 256, 256, new Color(0.85f, 0.75f, 0.65f));
            }
        }
        
        GD.Print("Enemy placeholders ready");
    }

    public static void GenerateIconPlaceholders()
    {
        var icons = new List<(string name, Color bgColor)>
        {
            ("Attack", new Color(0.9f, 0.4f, 0.3f)),
            ("Skill", new Color(0.3f, 0.5f, 0.9f)),
            ("Power", new Color(0.6f, 0.3f, 0.8f)),
            ("Vulnerable", new Color(0.9f, 0.6f, 0.2f)),
            ("Weak", new Color(0.7f, 0.7f, 0.4f)),
            ("Strength", new Color(0.9f, 0.3f, 0.3f)),
            ("Dexterity", new Color(0.3f, 0.7f, 0.4f)),
            ("Block", new Color(0.4f, 0.6f, 0.8f)),
            ("Energy", new Color(0.9f, 0.8f, 0.2f)),
            ("Gold", new Color(0.9f, 0.8f, 0.2f)),
            ("Draw", new Color(0.5f, 0.7f, 0.9f)),
            ("Exhaust", new Color(0.5f, 0.5f, 0.5f)),
            ("Ethereal", new Color(0.6f, 0.6f, 0.9f)),
            ("Common", new Color(0.6f, 0.6f, 0.6f)),
            ("Uncommon", new Color(0.3f, 0.7f, 0.9f)),
            ("Rare", new Color(0.7f, 0.4f, 0.9f)),
            ("Legendary", new Color(0.9f, 0.7f, 0.2f)),
            ("Player", new Color(0.3f, 0.5f, 0.7f)),
            ("Health", new Color(0.9f, 0.3f, 0.3f)),
            ("Card_Back", new Color(0.4f, 0.4f, 0.5f))
        };

        string outputPath = "res://Assets/Icons";
        EnsureDirectoryExists(outputPath);

        foreach (var icon in icons)
        {
            string fileName = $"{outputPath}/{icon.name}.png";
            string globalPath = ProjectSettings.GlobalizePath(fileName);
            if (!File.Exists(globalPath))
            {
                GeneratePlaceholderImage(fileName, 64, 64, icon.bgColor);
            }
        }
        
        GD.Print("Icon placeholders ready");
    }

    public static void GenerateUIPlaceholders()
    {
        var uiElements = new List<(string name, int width, int height, Color bgColor)>
        {
            ("Card_Frame", 256, 384, new Color(0.3f, 0.3f, 0.35f)),
            ("Card_Frame_Attack", 256, 384, new Color(0.5f, 0.25f, 0.25f)),
            ("Card_Frame_Skill", 256, 384, new Color(0.25f, 0.35f, 0.5f)),
            ("Card_Frame_Power", 256, 384, new Color(0.4f, 0.25f, 0.5f)),
            ("Panel_Background", 512, 512, new Color(0.15f, 0.15f, 0.2f)),
            ("Button_Normal", 200, 50, new Color(0.25f, 0.25f, 0.3f)),
            ("Button_Pressed", 200, 50, new Color(0.35f, 0.35f, 0.4f)),
            ("HealthBar_Background", 200, 24, new Color(0.2f, 0.2f, 0.2f)),
            ("HealthBar_Fill", 200, 24, new Color(0.8f, 0.2f, 0.2f)),
            ("Energy_Orb", 64, 64, new Color(0.2f, 0.5f, 0.8f))
        };

        string outputPath = "res://Assets/UI";
        EnsureDirectoryExists(outputPath);

        foreach (var ui in uiElements)
        {
            string fileName = $"{outputPath}/{ui.name}.png";
            string globalPath = ProjectSettings.GlobalizePath(fileName);
            if (!File.Exists(globalPath))
            {
                GeneratePlaceholderImage(fileName, ui.width, ui.height, ui.bgColor);
            }
        }
        
        GD.Print("UI placeholders ready");
    }

    private static void GeneratePlaceholderImage(string resPath, int width, int height, Color bgColor)
    {
        var image = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);
        image.Fill(bgColor);

        string globalPath = ProjectSettings.GlobalizePath(resPath);
        string directory = Path.GetDirectoryName(globalPath);
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        
        image.SavePng(globalPath);
        GD.Print($"Generated: {resPath}");
    }

    private static void EnsureDirectoryExists(string resPath)
    {
        string globalPath = ProjectSettings.GlobalizePath(resPath);
        if (!Directory.Exists(globalPath))
        {
            Directory.CreateDirectory(globalPath);
        }
    }
}
