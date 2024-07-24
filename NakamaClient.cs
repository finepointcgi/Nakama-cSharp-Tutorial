using Godot;
using System;
using Nakama;
using System.Text;
using System.Collections.Generic;
using NakamacSharpTutorial;
using Nakama.TinyJson;
using System.Linq;

public partial class NakamaClient : Control
{
	// Called when the node enters the scene tree for the first time.

	private static Client client;
	public static ISession Session;
	private static ISocket socket;
	private static IMatch match;

	public static NakamaClient Client;

	public static Dictionary<string, PlayerInfo> Players = new(); // houses all the players in the game

	[Signal]
	public delegate void PlayerDataSyncEventHandler(string data);

	[Signal]
	public delegate void PlayerJoinGameEventHandler(string data);
	[Signal]
	public delegate void PlayerLeaveGameEventHandler(string data);

	[Signal]
	public delegate void StartGameEventHandler(string data);

	[Signal]
	public delegate void ReadyGameEventHandler(string data);

	public static bool IsHost;

	public override void _Ready()
	{
		if(Client != null){
			GD.Print("removing second instance");

			QueueFree();
		}else{
			
			Client = this;
		}
		readyAsync();
	}

	private async void readyAsync()
	{
		client = new Client("http", "127.0.0.1", 7350, "defaultkey");
		client.Timeout = 500;
		//var session = await client.AuthenticateDeviceAsync(OS.GetUniqueId());

		//GD.Print($"Authenticated with session token: {session.AuthToken}" );
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}

	public async void _on_login_button_button_down()
	{
		Session = await client.AuthenticateEmailAsync(GetNode<LineEdit>("LoginPanel/Username").Text,
									GetNode<LineEdit>("LoginPanel/Password").Text, GetNode<LineEdit>("LoginPanel/Username").Text.Split("@")[0]);
		GD.Print($"Authenticated with session token: {Session.AuthToken}");

		await client.UpdateAccountAsync(Session, GetNode<LineEdit>("LoginPanel/Username").Text.Split("@")[0], "testdisplayname", "http://test.com/test.png", "EN", "US", "EST");
	}

	public async void _on_join_button_down()
	{
		socket = Socket.From(client);
		await socket.ConnectAsync(Session);

		socket.ReceivedMatchPresence += onMatchPresence;
		socket.ReceivedMatchState += onMatchState;

		match = await socket.CreateMatchAsync(GetNode<LineEdit>("MatchMaking/LobbyName").Text);
		GD.Print($"Created Match with ID: {match.Id}");

		await socket.JoinMatchAsync(match.Id);

		GD.Print($"Joined Match with id: {match.Id}");

		foreach (var item in match.Presences)
		{
			if(!Players.ContainsKey(item.Username)){
				Players.Add(item.Username, new PlayerInfo{Id = item.Username});
				CallDeferred(nameof(EmitPlayerJoinGameSignal), item.Username);
			}
		}
		if(match.Presences.Count() == 0){
			IsHost = true;
		}
	}

	private void onMatchState(IMatchState state)
	{
		string data = Encoding.UTF8.GetString(state.State);
		GD.Print($"Recieved data from user: {data}");
		switch (state.OpCode)
		{
			case 0:
				CallDeferred(nameof(EmitPlayerJoinGameSignal), data);
				break;

			case 1:
				CallDeferred(nameof(EmitPlayerSyncDataSignal), data);
				break;

			case 2:
				CallDeferred(nameof(EmitStartGameSignal), data);
				break;

			case 3:
				CallDeferred(nameof(EmitReadyGameSignal), data);
				Players[data].Status = 1;
				if(IsHost){
					if(Players.Any(x => x.Value.Status == 0)){
						return;
					}
					GD.Print("Host start Game");
					SyncData("", 2);
					CallDeferred(nameof(EmitStartGameSignal), data);
					
				}
				break;
		}
	}

	public void EmitPlayerJoinGameSignal(string data) => EmitSignal(SignalName.PlayerJoinGame, data);
	public void EmitPlayerLeaveGameSignal(string data) => EmitSignal(SignalName.PlayerLeaveGame, data);
	public void EmitPlayerSyncDataSignal(string data) => EmitSignal(SignalName.PlayerDataSync, data);
	public void EmitStartGameSignal(string data) => EmitSignal(SignalName.StartGame, data);
	public void EmitReadyGameSignal(string data) => EmitSignal(SignalName.ReadyGame, data);



	private void onMatchPresence(IMatchPresenceEvent @event)
	{
		foreach (var item in @event.Joins)
		{
			if(!Players.ContainsKey(item.Username)){
				Players.Add(item.Username, new PlayerInfo{Id = item.Username});
				CallDeferred(nameof(EmitPlayerJoinGameSignal), item.Username);
			}
		}

		foreach (var item in @event.Leaves)
		{
			if(Players.ContainsKey(item.Username)){
				Players.Remove(item.Username);
				CallDeferred(nameof(EmitPlayerLeaveGameSignal), item.Username);
			}
		}
	}

	public async void _on_ping_button_down()
	{
		var data = Encoding.UTF8.GetBytes("Hello World!");

		await socket.SendMatchStateAsync(match.Id, 1, data);
	}

	public async void _on_start_matchmake_button_down()
	{
		socket = Socket.From(client);
		await socket.ConnectAsync(Session);

		socket.ReceivedMatchPresence += onMatchPresence;
		socket.ReceivedMatchState += onMatchState;
		socket.ReceivedMatchmakerMatched += onMatchmakerMatched;
		var query = "+properties.skill:>50 properties.mode:deathmatch";
		var stringProps = new Dictionary<string, string> { { "mode", "deathmatch" } };
		var numericProps = new Dictionary<string, double> { { "skill", 100 } };

		var matchmakerTicket = await socket.AddMatchmakerAsync(query, 2, 8, stringProps, numericProps);
		GD.Print($"created match ticket with ticket {matchmakerTicket.Ticket}");

	}

	private async void onMatchmakerMatched(IMatchmakerMatched matched)
	{

		match = await socket.JoinMatchAsync(matched);
		GD.Print($"joined match with id {match.Id}");
	}

	private async void _on_store_data_button_down()
	{
		PlayerInfo playerInfo = new PlayerInfo
		{
			Name = "test",
			Skill = 100,
			Level = 10
		};
		WriteStorageObject writeStorageObject = new WriteStorageObject
		{
			Collection = "playerinfo",
			Key = "player2Info",
			Value = JsonWriter.ToJson(playerInfo),
			PermissionRead = 1,
			PermissionWrite = 1
		};

		await client.WriteStorageObjectsAsync(Session, new[] { writeStorageObject });

	}

	private async void _on_get_data_button_down()
	{
		var storageobject = new StorageObjectId
		{
			Collection = "playerinfo",
			Key = "player1Info",
			UserId = Session.UserId
		};
		var result = await client.ReadStorageObjectsAsync(Session, new[] { storageobject });
		if (result.Objects.Any())
		{
			var obj = result.Objects.First();
			GD.Print($"data receved is {obj.Value} {JsonParser.FromJson<PlayerInfo>(obj.Value)}");
		}
	}

	private async void _on_list_data_button_down()
	{
		var limit = 2;
		var playerDataList = await client.ListUsersStorageObjectsAsync(Session, "playerinfo", Session.UserId, limit);

		foreach (var data in playerDataList.Objects)
		{
			GD.Print($"data is {data.Value}");
		}
	}

	private async void _on_get_friends_button_down()
	{
		var result = await client.ListFriendsAsync(Session, 0, 10, null);
		foreach (var item in result.Friends)
		{
			GD.Print(item.User.Username);
		}
	}

	private async void _on_add_friend_button_down()
	{
		await client.AddFriendsAsync(Session, null, new[] { GetNode<LineEdit>("Friends/FriendName").Text });
	}
	private async void _on_block_friend_button_down()
	{
		await client.BlockFriendsAsync(Session, null, new[] { GetNode<LineEdit>("Friends/FriendName").Text });
	}
	private async void _on_delete_friend_button_down()
    {
        await client.DeleteFriendsAsync(Session, null, new[] { GetNode<LineEdit>("Friends/FriendName").Text });
	}

	public static async void SyncData(string data, int opcode) => await socket.SendMatchStateAsync(match.Id, opcode, data);
	
	public void _on_ready_start_button_down(){
		Players[Session.Username].Status = 1;
		SyncData(Session.Username, 3);
	}
}
