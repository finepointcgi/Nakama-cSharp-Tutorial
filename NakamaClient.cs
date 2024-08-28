using Godot;
using System;
using Nakama;
using System.Text;
using System.Collections.Generic;
using NakamacSharpTutorial;
using Nakama.TinyJson;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualBasic;
using System.Threading.Tasks;

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

	private IApiGroup currentSelectedGroup;
	private IGroupUserListGroupUser currentlySelectedUser;
	private IChannel currentChat;
	private List<ChatChannel> chatChannels = new List<ChatChannel>();
	public override void _Ready()
	{
		if (Client != null)
		{
			GD.Print("removing second instance");

			QueueFree();
		}
		else
		{

			Client = this;
		}
		readyAsync();
	}

	private async void readyAsync()
	{
		client = new Client("http", "198.199.80.118", 7350, "defaultkey");
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
		
		socket = Socket.From(client);
		await socket.ConnectAsync(Session);

		socket.ReceivedChannelMessage += onChannelMessage;
		subToFriendsChannels();
	}

    
    public async void _on_join_button_down()
	{
		//socket = Socket.From(client);
		//await socket.ConnectAsync(Session);

		socket.ReceivedMatchPresence += onMatchPresence;
		socket.ReceivedMatchState += onMatchState;

		


		match = await socket.CreateMatchAsync(GetNode<LineEdit>("MatchMaking/LobbyName").Text);
		
		AddToChat(GetNode<LineEdit>("MatchMaking/LobbyName").Text, GetNode<LineEdit>("MatchMaking/LobbyName").Text, ChannelType.Room, false, false);
		
		GD.Print($"Created Match with ID: {match.Id}");

		await socket.JoinMatchAsync(match.Id);

		GD.Print($"Joined Match with id: {match.Id}");

		foreach (var item in match.Presences)
		{
			if (!Players.ContainsKey(item.Username))
			{
				Players.Add(item.Username, new PlayerInfo { Id = item.Username });
				CallDeferred(nameof(EmitPlayerJoinGameSignal), item.Username);
			}
		}
		if (match.Presences.Count() == 0)
		{
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
				if (IsHost)
				{
					if (Players.Any(x => x.Value.Status == 0))
					{
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
			if (!Players.ContainsKey(item.Username))
			{
				Players.Add(item.Username, new PlayerInfo { Id = item.Username });
				CallDeferred(nameof(EmitPlayerJoinGameSignal), item.Username);
			}
		}

		foreach (var item in @event.Leaves)
		{
			if (Players.ContainsKey(item.Username))
			{
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
	#region Store Section
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
	#endregion
	#region Friend Section
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

	public void _on_ready_start_button_down()
	{
		Players[Session.Username].Status = 1;
		SyncData(Session.Username, 3);
	}
	#endregion
	#region Group Section
	public async void _on_create_group_button_down()
	{
		var group = await client.CreateGroupAsync(Session, GetNode<LineEdit>("Group/GroupName").Text, "test desc", langTag: "en", open: true, maxCount: 32);
		GD.Print(group);
	}
	public async void _on_delete_group_button_down()
	{
		await client.DeleteGroupAsync(Session, currentSelectedGroup.Id);
	}
	public async void _on_get_group_button_down()
	{
		var result = await client.ListGroupsAsync(Session, null, limit: 5, cursor: "", open: false);
		foreach (var item in result.Groups)
		{

			var vbox = new VBoxContainer();
			var hbox = new HBoxContainer();
			var nameLabel = new Label();

			var button = new Button();

			nameLabel.Text = item.Name;
			hbox.AddChild(nameLabel);
			button.ButtonDown += () => onSelectGroupButtonDown(item);
			button.Text = "Select Group";
			hbox.AddChild(button);
			vbox.AddChild(hbox);
			GetNode<VBoxContainer>("Group/VBoxContainer").AddChild(vbox);
		}
	}

	private async void onSelectGroupButtonDown(IApiGroup group)
	{
		currentSelectedGroup = group;
		GetNode<LineEdit>("Group/EditGroup/GroupName").Text = group.Name;
		GetNode<CheckButton>("Group/EditGroup/CloseOpenGroup").ButtonPressed = group.Open;
		GetNode<LineEdit>("Group/EditGroup/GroupDescription").Text = group.Description;
		GetNode<LineEdit>("Group/EditGroup/AvatarURL").Text = group.AvatarUrl;
		int languageChoice = -1;
		switch (group.LangTag)
		{
			case "en":
				languageChoice = 0;
				break;
			case "es":
				languageChoice = 1;
				break;
			case "ja":
				languageChoice = 2;
				break;
			default:
				languageChoice = -1;
				break;
		}
		GetNode<OptionButton>("Group/EditGroup/OptionButton").Selected = languageChoice;

		var result = await client.ListGroupUsersAsync(Session, currentSelectedGroup.Id, null, 100, "");
		
		foreach (var item in GetNode<VBoxContainer>("Group/GroupMembers/VBoxContainer").GetChildren())
		{
			item.QueueFree();
		}
		
		foreach (var item in result.GroupUsers)
		{

			var vbox = new VBoxContainer();
			var hbox = new HBoxContainer();
			var nameLabel = new Label();
			var stateLabel = new Label();

			var button = new Button();

			nameLabel.Text = item.User.DisplayName;
			string state = "";
			switch (item.State)
			{
				case 0:
					state = "Super Admin";
				break;
				case 1:
					state = "Admin";
				break;
				case 2:
					state = "Member";
				break;
				case 3:
					state = "Join Request";
				break;
				default:
					state = "Error";
					break;
			}

			stateLabel.Text = state;
			hbox.AddChild(nameLabel);
			hbox.AddChild(stateLabel);
			
				button.ButtonDown += () => onGroupUserButtonDown(item, item.State);
				button.Text = "Add User";
				hbox.AddChild(button);
			
			vbox.AddChild(hbox);
			GetNode<VBoxContainer>("Group/GroupMembers/VBoxContainer").AddChild(vbox);
		}
	}

	public void onGroupUserButtonDown(IGroupUserListGroupUser user, int state){
		if(state == 3){
			client.AddGroupUsersAsync(Session, currentSelectedGroup.Id, new [] {user.User.Id});
		}
		currentlySelectedUser = user;
		onSelectGroupButtonDown(currentSelectedGroup);
	}

	public async void _on_join_group_button_down()
	{
		await client.JoinGroupAsync(Session, currentSelectedGroup.Id);
	}

	public async void _on_leave_group_button_down()
	{
		await client.LeaveGroupAsync(Session, currentSelectedGroup.Id);
	}

	public async void _on_edit_group_button_button_down()
	{
		var selectedOption = GetNode<OptionButton>("Group/EditGroup/OptionButton").Selected;
		string languageChoice = "";
		switch (selectedOption)
		{
			case 0:
				languageChoice = "en";
				break;
			case 1:
				languageChoice = "es";
				break;
			case 2:
				languageChoice = "ja";
				break;
			default:
				languageChoice = "";
				break;
		}

		await client.UpdateGroupAsync(Session, currentSelectedGroup.Id, 
					GetNode<LineEdit>("Group/EditGroup/GroupName").Text, 
					GetNode<CheckButton>("Group/EditGroup/CloseOpenGroup").ButtonPressed, 
					GetNode<LineEdit>("Group/EditGroup/GroupDescription").Text, 
					GetNode<LineEdit>("Group/EditGroup/AvatarURL").Text, 
					languageChoice);
	}

	public async void _on_promote_user_button_down(){
		await client.PromoteGroupUsersAsync(Session, currentSelectedGroup.Id,  new [] {currentlySelectedUser.User.Id});
		onSelectGroupButtonDown(currentSelectedGroup);
	}

	public async void _on_demote_user_button_down(){
		await client.DemoteGroupUsersAsync(Session, currentSelectedGroup.Id,  new [] {currentlySelectedUser.User.Id});
		onSelectGroupButtonDown(currentSelectedGroup);
	}
	public async void _on_kick_user_button_down(){
		await client.KickGroupUsersAsync(Session, currentSelectedGroup.Id,  new [] {currentlySelectedUser.User.Id});
		onSelectGroupButtonDown(currentSelectedGroup);
	}
	public async void _on_ban_user_button_down(){
		await client.BanGroupUsersAsync(Session, currentSelectedGroup.Id,  new [] {currentlySelectedUser.User.Id});
		onSelectGroupButtonDown(currentSelectedGroup);
	}
	#endregion

	#region Chat

	private void onChannelMessage(IApiChannelMessage message)
    {
		GD.Print(message);
        ChatMessage currentMessage = JsonParser.FromJson<ChatMessage>(message.Content);
		if(currentMessage.Type == 0){
			CallDeferred(nameof(onChannelMessageDeffered), currentMessage.ID, currentMessage.Message, currentMessage.User);
		}
	}

	private void onChannelMessageDeffered(string id, string message, string user){
		if(!chatChannels.Any(x => x.ID == id)){
				GetNode<TabContainer>("Chat/TabContainer").AddChild(new TextEdit(){
					Name = user
				});

				chatChannels.Add(new ChatChannel(){
					Label = user,
					ID = id
				});
			}
			GetNode<TextEdit>("Chat/TabContainer/" + chatChannels.Where(x => x.ID == id).First().Label).Text += $"{user} : {message} \n";
	}

	public void _on_join_chat_button_down(){

	}
	public void _on_join_group_chat_button_down(){

	}

	public async void _on_join_direct_chat_button_down(){
		var user = await client.GetUsersAsync(Session, null, new[] {GetNode<LineEdit>("Chat/SelectedChat").Text});
		if(user.Users.Count() > 0){
			currentChat = await socket.JoinChatAsync(user.Users.FirstOrDefault().Id, ChannelType.DirectMessage, true, false);
		}
	}

	public async void _on_submit_chat_button_down(){
		ChatMessage chatMessage = new ChatMessage(){
			Message = GetNode<LineEdit>("Chat/ChatText").Text,
			ID = currentChat.Id,
			Type = 0,
			User = Session.Username
		};
		GD.Print(currentChat);
		var ack = await socket.WriteChatMessageAsync(currentChat.Id, chatMessage.ToJson());
		GD.Print(ack);
	}

	private async void subToFriendsChannels(){
		var groupResult = await client.ListGroupsAsync(Session, null, 100, null);
		foreach (var item in groupResult.Groups)
        {
            await AddToChat(item.Id, item.Name, ChannelType.Group);
        }

		var result = await client.ListFriendsAsync(Session, null, 100, "");
		foreach (var item in result.Friends)
        {
            await AddToChat(item.User.Id, item.User.DisplayName, ChannelType.DirectMessage);
        }
    }

    private async Task AddToChat(string id, string displayName, ChannelType channelType, bool presistant = true, bool publicRoom = false)
    {
		try
		{
			var channel = await socket.JoinChatAsync(id, channelType, presistant, publicRoom);
			GD.Print(channel.Id);
			chatChannels.Add(new ChatChannel()
			{
				ID = channel.Id,
				Label = displayName
			});
			

			var currentEdit = new TextEdit();

			currentEdit.Name = displayName;
			GetNode<TabContainer>("Chat/TabContainer").AddChild(currentEdit);

			currentEdit.Text = await listMessages(channel);
		

			currentChat = channel;

			GetNode<TabContainer>("Chat/TabContainer").CurrentTab = chatChannels.Count() - 1;
		
		
			GetNode<TabContainer>("Chat/TabContainer").TabChanged += (index) => onChatTabChanged(channel, index);
		}
		catch (System.Exception)
		{
			GD.Print("Error cant join chat " + displayName);
			
		}
        
    }

    private async void onChatTabChanged(IChannel channel, long index){

		if((chatChannels[(int)index].ID ) == channel.Id){
			currentChat = channel;
			await listMessages(channel);
		}
	}

	private async Task<string> listMessages(IChannel channel){
		var result = await client.ListChannelMessagesAsync(Session, channel, 15, false);
		string text = "";
		foreach (var item in result.Messages.Reverse())
		{
			ChatMessage message = JsonParser.FromJson<ChatMessage>(item.Content);
			text +=  $"{message.User} : {message.Message} \n";
		}
		return text;
	}
	#endregion
}
