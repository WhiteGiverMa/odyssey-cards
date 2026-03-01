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
        var cardData = new List<(string name, Color color)>
        {
            ("Strike", new Color(0.85f, 0.25f, 0.25f)),
            ("Strike_Upgraded", new Color(0.85f, 0.25f, 0.25f)),
            ("Bash", new Color(0.85f, 0.25f, 0.25f)),
            ("Bash_Upgraded", new Color(0.85f, 0.25f, 0.25f)),
            ("Cleave", new Color(0.85f, 0.25f, 0.25f)),
            ("Cleave_Upgraded", new Color(0.85f, 0.25f, 0.25f)),
            ("IronWave", new Color(0.85f, 0.25f, 0.25f)),
            ("PommelStrike", new Color(0.85f, 0.25f, 0.25f)),
            ("TwinStrike", new Color(0.85f, 0.25f, 0.25f)),
            ("Anger", new Color(0.85f, 0.25f, 0.25f)),
            ("Clothesline", new Color(0.85f, 0.25f, 0.25f)),
            ("HeavyBlade", new Color(0.85f, 0.25f, 0.25f)),
            ("BodySlam", new Color(0.85f, 0.25f, 0.25f)),
            ("Rage", new Color(0.85f, 0.25f, 0.25f)),
            ("Clash", new Color(0.85f, 0.25f, 0.25f)),
            ("Bloodletting", new Color(0.85f, 0.25f, 0.25f)),

            ("Defend", new Color(0.25f, 0.45f, 0.85f)),
            ("Defend_Upgraded", new Color(0.25f, 0.45f, 0.85f)),
            ("ShrugItOff", new Color(0.25f, 0.45f, 0.85f)),
            ("Armaments", new Color(0.25f, 0.45f, 0.85f)),
            ("Flex", new Color(0.25f, 0.45f, 0.85f)),
            ("Impervious", new Color(0.25f, 0.45f, 0.85f)),
            ("Intimidate", new Color(0.25f, 0.45f, 0.85f)),

            ("FlameBarrier", new Color(0.5f, 0.25f, 0.7f)),
            ("BattleTrance", new Color(0.5f, 0.25f, 0.7f)),
            ("DemonForm", new Color(0.5f, 0.25f, 0.7f)),
            ("LimitBreak", new Color(0.5f, 0.25f, 0.7f)),
            ("Metallicize", new Color(0.5f, 0.25f, 0.7f)),
            ("PowerThrough", new Color(0.5f, 0.25f, 0.7f)),
            ("SpotWeakness", new Color(0.5f, 0.25f, 0.7f)),
            ("TrueGrit", new Color(0.5f, 0.25f, 0.7f)),
            ("UpperCut", new Color(0.5f, 0.25f, 0.7f)),
            ("Warcry", new Color(0.5f, 0.25f, 0.7f)),
            ("DualWield", new Color(0.5f, 0.25f, 0.7f)),
            ("GhostlyArmor", new Color(0.5f, 0.25f, 0.7f)),
            ("Shockwave", new Color(0.5f, 0.25f, 0.7f)),
            ("DoubleTap", new Color(0.5f, 0.25f, 0.7f)),
            ("Exhume", new Color(0.5f, 0.25f, 0.7f)),
            ("FiendFire", new Color(0.5f, 0.25f, 0.7f)),
            ("Hemorrage", new Color(0.5f, 0.25f, 0.7f)),
            ("Immolate", new Color(0.5f, 0.25f, 0.7f)),
            ("Inflame", new Color(0.5f, 0.25f, 0.7f)),
            ("PowerTransfer", new Color(0.5f, 0.25f, 0.7f)),
            ("Reckless", new Color(0.5f, 0.25f, 0.7f)),
            ("SearingBlow", new Color(0.5f, 0.25f, 0.7f)),
            ("SecondWind", new Color(0.5f, 0.25f, 0.7f)),
            ("SeeingRed", new Color(0.5f, 0.25f, 0.7f)),
            ("Thermos", new Color(0.5f, 0.25f, 0.7f)),
            ("Whirlwind", new Color(0.5f, 0.25f, 0.7f))
        };

        string outputPath = "res://Assets/Cards";
        EnsureDirectoryExists(outputPath);

        foreach (var card in cardData)
        {
            string fileName = $"{outputPath}/{card.name}.png";
            string globalPath = ProjectSettings.GlobalizePath(fileName);
            if (!File.Exists(globalPath))
            {
                GeneratePlaceholderImage(fileName, 256, 256, card.color);
            }
        }
        
        GD.Print("Card placeholders ready");
    }

    public static void GenerateEnemyPlaceholders()
    {
        var enemyData = new List<(string name, Color color)>
        {
            ("Slime_Small", new Color(0.3f, 0.8f, 0.4f)),
            ("Slime_Medium", new Color(0.3f, 0.7f, 0.35f)),
            ("Slime_Large", new Color(0.3f, 0.6f, 0.3f)),
            ("Goblin", new Color(0.5f, 0.7f, 0.3f)),
            ("GoblinElite", new Color(0.45f, 0.6f, 0.25f)),
            ("Cultist", new Color(0.6f, 0.3f, 0.7f)),
            ("JawWorm", new Color(0.65f, 0.5f, 0.35f)),
            ("Louse", new Color(0.7f, 0.4f, 0.5f)),
            ("FungiBeast", new Color(0.4f, 0.6f, 0.35f)),
            ("Gremlin", new Color(0.55f, 0.45f, 0.3f)),
            ("Slaver", new Color(0.6f, 0.4f, 0.35f)),
            ("SlaverElite", new Color(0.55f, 0.35f, 0.3f)),
            ("Boss_Guardian", new Color(0.7f, 0.3f, 0.3f)),
            ("Boss_Hexaghost", new Color(0.5f, 0.2f, 0.8f)),
            ("Boss_SlimeBoss", new Color(0.25f, 0.65f, 0.3f))
        };

        string outputPath = "res://Assets/Enemies";
        EnsureDirectoryExists(outputPath);

        foreach (var enemy in enemyData)
        {
            string fileName = $"{outputPath}/{enemy.name}.png";
            string globalPath = ProjectSettings.GlobalizePath(fileName);
            if (!File.Exists(globalPath))
            {
                GeneratePlaceholderImage(fileName, 256, 256, enemy.color);
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
