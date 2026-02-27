using Godot;
using OdysseyCards.Character;

namespace OdysseyCards.Core;

public partial class MainMenu : Control
{
    private Button _startButton;

    public override void _Ready()
    {
        PlaceholderAssetGenerator.GenerateAllPlaceholders();
        
        _startButton = GetNode<Button>("StartButton");
        _startButton.Pressed += OnStartPressed;
    }

    private void OnStartPressed()
    {
        if (GameManager.Instance == null)
        {
            var manager = new Node();
            manager.Name = "GameManager";
            GetTree().Root.AddChild(manager);
            var gmScript = new GameManager();
            manager.AddChild(gmScript);
            gmScript._Ready();
        }

        GameManager.Instance.CreateNewPlayer();
        GetTree().ChangeSceneToFile("res://Scenes/Combat.tscn");
    }
}
