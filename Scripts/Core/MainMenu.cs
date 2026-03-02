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
        GD.Print("[MainMenu] OnStartPressed called");
        GD.Print($"[MainMenu] GameManager.Instance is null: {GameManager.Instance == null}");

        GameManager.Instance?.CreateNewPlayer();
        GetTree().ChangeSceneToFile("res://Scenes/Combat.tscn");
    }
}
