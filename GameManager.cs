using Godot;
using System;

/// <summary>
/// Coordinates the transition from UI/lobby to gameplay level.
/// </summary>
public partial class GameManager : Node2D
{

	/// <summary>
	/// Level scene instantiated when the start-game signal is received.
	/// </summary>
	[Export]
	public PackedScene LevelToLoad;
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		GetNode<NakamaClient>("UI/Node2D").StartGame += onStartGame;
	}

    private void onStartGame(string data)
    {
        var level = LevelToLoad.Instantiate();
		GetNode<Control>("UI").Hide();
		GetNode<Node2D>("World").AddChild(level);
    }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta)
	{
	}
}
