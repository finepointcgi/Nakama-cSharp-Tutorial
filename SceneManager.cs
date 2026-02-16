using Godot;
using Godot.Collections;
using System;
using System.Linq;
using Nakama;

/// <summary>
/// Spawns player nodes for current lobby users and assigns multiplayer authority.
/// </summary>
public partial class SceneManager : Node2D
{
	[Export]
	public PackedScene PlayerScene;
	private Array<Node> spawnPoints;
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		if (PlayerScene == null)
		{
			GD.PushError("[SPAWN] PlayerScene is null; cannot spawn players.");
			return;
		}

		spawnPoints = GetTree().GetNodesInGroup("SpawnPoint");
		int index = 0;
		var usernames = NakamaClient.Players.Keys.OrderBy(k => k).ToList();

		// 1 2
		// 2 1
		foreach (var key in usernames)
		{
			var player = PlayerScene.Instantiate<CharacterController>();
			player.Name = NakamaClient.Players[key].Id;
			var authorityPeerId = ResolvePeerIdForUsername(NakamaClient.Players[key].Id);
			if (authorityPeerId > 0)
			{
				player.SetMultiplayerAuthority(authorityPeerId);
				GD.Print($"[AUTH] Player '{player.Name}' authority set to peer {authorityPeerId}.");
			}
			else
			{
				GD.PushWarning($"[AUTH] Could not resolve authority peer for '{player.Name}'.");
			}

			Vector2 spawnPosition = Vector2.Zero;
			if (index < spawnPoints.Count && spawnPoints[index] is Node2D spawnpoint)
			{
				spawnPosition = spawnpoint.GlobalPosition;
			}
			else
			{
				GD.PushWarning($"[SPAWN] Missing spawn point for player '{player.Name}' at index {index}. Using Vector2.Zero.");
			}

			player.SetupPlayer(NakamaClient.Players[key].Id, spawnPosition);
			AddChild(player);
			index++;
		}

	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}

	/// <summary>
	/// Resolves a Godot peer id from a username using local multiplayer state and bridge mappings.
	/// </summary>
	/// <param name="username">Username to resolve.</param>
	/// <returns>Peer id when resolved; otherwise 0.</returns>
	private int ResolvePeerIdForUsername(string username)
	{
		if (string.IsNullOrWhiteSpace(username) || Multiplayer == null || Multiplayer.MultiplayerPeer == null)
		{
			return 0;
		}

		if (NakamaClient.Session != null && username == NakamaClient.Session.Username)
		{
			return Multiplayer.GetUniqueId();
		}

		var bridge = NakamaClient.MultiplayerBridge;
		if (bridge == null)
		{
			return 0;
		}

		foreach (var peerId in Multiplayer.GetPeers())
		{
			var presence = bridge.GetUserPresenceForPeer(peerId);
			if (presence != null && presence.Username == username)
			{
				return peerId;
			}
		}

		return 0;
	}
}
