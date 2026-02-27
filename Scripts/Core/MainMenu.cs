using Godot;

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
        GetTree().ChangeSceneToFile("res://Scenes/Combat.tscn");
    }
}
