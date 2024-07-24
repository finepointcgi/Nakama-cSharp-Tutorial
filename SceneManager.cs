using Godot;
using Godot.Collections;
using System;
using System.Linq;

public partial class SceneManager : Node2D
{
	[Export]
	public PackedScene PlayerScene;
	private Array<Node> spawnPoints;
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		spawnPoints = GetTree().GetNodesInGroup("SpawnPoint");
		int index = 0;

		// 1 2
		// 2 1
		foreach (var key in NakamaClient.Players.Keys.OrderBy(k => k))
		{
			var player = PlayerScene.Instantiate<CharacterController>();
			player.Name = NakamaClient.Players[key].Id;
			if(spawnPoints[index] is Node2D spawnpoint){
				player.SetupPlayer(NakamaClient.Players[key].Id, spawnpoint.GlobalPosition);
			}
			AddChild(player);
			index++;
		}

	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}
}
