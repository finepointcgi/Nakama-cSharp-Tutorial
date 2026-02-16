using Godot;
using Nakama;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Bridges Nakama realtime match traffic to Godot's multiplayer API.
/// It handles peer-id assignment, presence tracking, and RPC packet routing.
/// </summary>
public partial class NakamaMultiplayerBridge : RefCounted
{
    public enum MatchState
    {
        Disconnected,
        Joining,
        Connected,
        SocketClosed
    }

    public enum MetaMessageType
    {
        ClaimHost,
        AssignPeerId
    }

    // JSON Structure for lightning-fast Meta Message parsing
    public struct MetaMessage
    {
        [JsonPropertyName("type")]
        public MetaMessageType Type { get; set; }

        [JsonPropertyName("session_id")]
        public string SessionId { get; set; }

        [JsonPropertyName("peer_id")]
        public int PeerId { get; set; }
    }

    public class User
    {
        public IUserPresence Presence { get; set; }
        public int PeerId { get; set; } = 0;

        public User(IUserPresence presence)
        {
            Presence = presence;
        }
    }

    /// <summary>
    /// Emitted when match join/setup fails.
    /// </summary>
    [Signal] public delegate void MatchJoinErrorEventHandler(string exceptionMessage);

    /// <summary>
    /// Emitted when bridge setup is complete and the multiplayer peer is ready.
    /// </summary>
    [Signal] public delegate void MatchJoinedEventHandler();

    private readonly ISocket _nakamaSocket;
    public ISocket NakamaSocket => _nakamaSocket;

    private MatchState _matchState = MatchState.Disconnected;
    public MatchState State => _matchState;

    private string _matchId = "";
    public string MatchId => _matchId;

    // Assuming NakamaMultiplayerPeer is either ported to C# or wrapped
    private NakamaMultiplayerPeer _multiplayerPeer = new NakamaMultiplayerPeer();
    public NakamaMultiplayerPeer MultiplayerPeer => _multiplayerPeer;

    public long MetaOpCode { get; set; } = 9001;
    public long RpcOpCode { get; set; } = 9002;

    private string _mySessionId = "";
    private int _myPeerId = 0;
    private string _matchmakerTicket = "";

    // Internal data structures optimized for C#
    private readonly Dictionary<int, string> _idMap = new();
    private readonly Dictionary<string, User> _users = new();

    public NakamaMultiplayerBridge(ISocket socket)
    {
        _nakamaSocket = socket;
        
        // Native C# Event subscriptions for Nakama SDK
        _nakamaSocket.ReceivedMatchPresence += OnNakamaSocketReceivedMatchPresence;
        _nakamaSocket.ReceivedMatchmakerMatched += OnNakamaSocketReceivedMatchmakerMatched;
        _nakamaSocket.ReceivedMatchState += OnNakamaSocketReceivedMatchState;
        _nakamaSocket.Closed += OnNakamaSocketClosed;

        _multiplayerPeer.PacketGenerated += OnMultiplayerPeerPacketGenerated;
        _multiplayerPeer.SetConnectionStatus((int)Godot.MultiplayerPeer.ConnectionStatus.Connecting);
    }

    /// <summary>
    /// Creates a new Nakama match and configures the local user as host.
    /// </summary>
    public async void CreateMatch()
    {
        if (_matchState != MatchState.Disconnected)
        {
            GD.PushError($"Cannot create match when state is {_matchState}");
            return;
        }

        _matchState = MatchState.Joining;
        _multiplayerPeer.SetConnectionStatus((int)Godot.MultiplayerPeer.ConnectionStatus.Connecting);

        try
        {
            var res = await _nakamaSocket.CreateMatchAsync();
            SetupMatch(res);
            SetupHost();
        }
        catch (Exception e)
        {
            EmitSignal(SignalName.MatchJoinError, e.Message);
            Leave();
        }
    }

    /// <summary>
    /// Joins an existing match by id.
    /// </summary>
    /// <param name="pMatchId">Target match id.</param>
    public async void JoinMatch(string pMatchId)
    {
        if (_matchState != MatchState.Disconnected)
        {
            GD.PushError($"Cannot join match when state is {_matchState}");
            return;
        }

        _matchState = MatchState.Joining;
        _multiplayerPeer.SetConnectionStatus((int)Godot.MultiplayerPeer.ConnectionStatus.Connecting);

        try
        {
            var res = await _nakamaSocket.JoinMatchAsync(pMatchId);
            SetupMatch(res);
        }
        catch (Exception e)
        {
            EmitSignal(SignalName.MatchJoinError, e.Message);
            Leave();
        }
    }

    /// <summary>
    /// Creates or joins a named match (server-side match label/id flow).
    /// </summary>
    /// <param name="matchName">Named lobby/match value.</param>
    public async void JoinNamedMatch(string matchName)
    {
        if (_matchState != MatchState.Disconnected)
        {
            GD.PushError($"Cannot join match when state is {_matchState}");
            return;
        }

        _matchState = MatchState.Joining;
        _multiplayerPeer.SetConnectionStatus((int)Godot.MultiplayerPeer.ConnectionStatus.Connecting);

        try
        {
            var res = await _nakamaSocket.CreateMatchAsync(matchName);
            SetupMatch(res);

            if (res.Size == 0 || (res.Size == 1 && !res.Presences.Any()))
            {
                SetupHost();
            }
        }
        catch (Exception e)
        {
            EmitSignal(SignalName.MatchJoinError, e.Message);
            Leave();
        }
    }

    /// <summary>
    /// Starts bridge matchmaking flow from a previously created matchmaker ticket.
    /// </summary>
    /// <param name="ticket">Nakama matchmaker ticket.</param>
    public void StartMatchmaking(IMatchmakerTicket ticket)
    {
        if (_matchState != MatchState.Disconnected)
        {
            GD.PushError($"Cannot start matchmaking when state is {_matchState}");
            return;
        }

        if (ticket == null)
        {
            GD.PushError("Null ticket passed into StartMatchmaking()");
            return;
        }

        _matchState = MatchState.Joining;
        _multiplayerPeer.SetConnectionStatus((int)Godot.MultiplayerPeer.ConnectionStatus.Connecting);
        _matchmakerTicket = ticket.Ticket;
    }

    private async void OnNakamaSocketReceivedMatchmakerMatched(IMatchmakerMatched matchmakerMatched)
    {
        if (_matchmakerTicket != matchmakerMatched.Ticket) return;

        var sessionIds = matchmakerMatched.Users.Select(u => u.Presence.SessionId).ToList();
        sessionIds.Sort();

        try
        {
            var res = await _nakamaSocket.JoinMatchAsync(matchmakerMatched);
            SetupMatch(res);

            // If our session is the first alphabetically, then we'll be the host.
            if (_mySessionId == sessionIds[0])
            {
                SetupHost();
                foreach (var presence in res.Presences)
                {
                    if (presence.SessionId != _mySessionId)
                    {
                        HostAddPeer(presence);
                    }
                }
            }
        }
        catch (Exception e)
        {
            EmitSignal(SignalName.MatchJoinError, e.Message);
            Leave();
        }
    }

    private void OnNakamaSocketClosed(string reason)
    {
        _matchState = MatchState.SocketClosed;
        Cleanup();
    }

    /// <summary>
    /// Gets the Nakama presence associated with a Godot peer id.
    /// </summary>
    /// <param name="peerId">Godot peer id.</param>
    /// <returns>User presence when found; otherwise <c>null</c>.</returns>
    public IUserPresence GetUserPresenceForPeer(int peerId)
    {
        if (_idMap.TryGetValue(peerId, out string sessionId))
        {
            if (_users.TryGetValue(sessionId, out User user))
            {
                return user.Presence;
            }
        }
        return null;
    }

    /// <summary>
    /// Returns a snapshot of currently known user presences for roster hydration.
    /// </summary>
    public IReadOnlyList<IUserPresence> GetKnownPresences()
    {
        return _users.Values
            .Select(user => user.Presence)
            .Where(presence => presence != null)
            .ToList();
    }

    /// <summary>
    /// Leaves current match/matchmaker context and resets bridge state.
    /// </summary>
    public async void Leave()
    {
        if (_matchState == MatchState.Disconnected) return;
        _matchState = MatchState.Disconnected;

        try
        {
            if (!string.IsNullOrEmpty(_matchId))
                await _nakamaSocket.LeaveMatchAsync(_matchId);

            if (!string.IsNullOrEmpty(_matchmakerTicket))
                await _nakamaSocket.RemoveMatchmakerAsync(_matchmakerTicket);
        }
        catch (Exception e)
        {
            GD.PushError($"Error leaving match: {e.Message}");
        }

        Cleanup();
    }

    private void Cleanup()
    {
        foreach (var peerId in _idMap.Keys)
        {
            _multiplayerPeer.EmitSignal("peer_disconnected", peerId);
        }

        _matchId = "";
        _matchmakerTicket = "";
        _mySessionId = "";
        _myPeerId = 0;
        
        _idMap.Clear();
        _users.Clear();

        _multiplayerPeer.SetConnectionStatus((int)Godot.MultiplayerPeer.ConnectionStatus.Disconnected);
    }

    private void SetupMatch(IMatch res)
    {
        _matchId = res.Id;
        _mySessionId = res.Self.SessionId;

        _users[_mySessionId] = new User(res.Self);

        foreach (var presence in res.Presences)
        {
            if (!_users.ContainsKey(presence.SessionId))
            {
                _users[presence.SessionId] = new User(presence);
            }
        }
    }

    private void SetupHost()
    {
        _myPeerId = 1;
        MapIdToSession(1, _mySessionId);
        _matchState = MatchState.Connected;
        
        _multiplayerPeer.Initialize(_myPeerId);
        EmitSignal(SignalName.MatchJoined);
    }

    private int GenerateId(string sessionId)
    {
        int peerId = sessionId.GetHashCode() & 0x7FFFFFFF;

        while (peerId <= 1 || _idMap.ContainsKey(peerId))
        {
            peerId++;
            if (peerId > 0x7FFFFFFF || peerId <= 0)
            {
                peerId = new Random().Next() & 0x7FFFFFFF;
            }
        }
        return peerId;
    }

    private void MapIdToSession(int peerId, string sessionId)
    {
        _idMap[peerId] = sessionId;
        if (_users.TryGetValue(sessionId, out User user))
        {
            user.PeerId = peerId;
        }
    }

    private void HostAddPeer(IUserPresence presence)
    {
        int peerId = GenerateId(presence.SessionId);
        MapIdToSession(peerId, presence.SessionId);

        // Tell them we are the host
        var hostMsg = new MetaMessage { Type = MetaMessageType.ClaimHost };
        _nakamaSocket.SendMatchStateAsync(_matchId, MetaOpCode, JsonSerializer.SerializeToUtf8Bytes(hostMsg), new[] { presence });

        // Tell them about all the other connected peers
        foreach (var kvp in _idMap)
        {
            int otherPeerId = kvp.Key;
            string otherSessionId = kvp.Value;

            if (otherSessionId == presence.SessionId || otherSessionId == _mySessionId)
                continue;

            var assignOtherMsg = new MetaMessage {
                Type = MetaMessageType.AssignPeerId,
                SessionId = otherSessionId,
                PeerId = otherPeerId
            };
            _nakamaSocket.SendMatchStateAsync(_matchId, MetaOpCode, JsonSerializer.SerializeToUtf8Bytes(assignOtherMsg), new[] { presence });
        }

        // Assign them a peer_id (tell everyone about it)
        var assignSelfMsg = new MetaMessage {
            Type = MetaMessageType.AssignPeerId,
            SessionId = presence.SessionId,
            PeerId = peerId
        };
        _nakamaSocket.SendMatchStateAsync(_matchId, MetaOpCode, JsonSerializer.SerializeToUtf8Bytes(assignSelfMsg));

        _multiplayerPeer.EmitSignal("peer_connected", peerId);
    }

    private void OnNakamaSocketReceivedMatchPresence(IMatchPresenceEvent ev)
    {
        if (_matchState == MatchState.Disconnected || ev.MatchId != _matchId) return;

        foreach (var presence in ev.Joins)
        {
            if (!_users.ContainsKey(presence.SessionId))
            {
                _users[presence.SessionId] = new User(presence);
            }

            if (_myPeerId == 1 && _users[presence.SessionId].PeerId == 0)
            {
                HostAddPeer(presence);
            }
        }

        foreach (var presence in ev.Leaves)
        {
            if (!_users.TryGetValue(presence.SessionId, out User user)) continue;

            int peerId = user.PeerId;
            _multiplayerPeer.EmitSignal("peer_disconnected", peerId);

            _users.Remove(presence.SessionId);
            _idMap.Remove(peerId);
        }
    }

    private void OnNakamaSocketReceivedMatchState(IMatchState data)
    {
        if (_matchState == MatchState.Disconnected || data.MatchId != _matchId) return;

        if (data.OpCode == MetaOpCode)
        {
            MetaMessage content;
            try
            {
                // Instantly deserialize directly from the byte array
                content = JsonSerializer.Deserialize<MetaMessage>(data.State);
            }
            catch
            {
                return;
            }

            if (content.Type == MetaMessageType.ClaimHost)
            {
                if (_idMap.ContainsKey(1))
                {
                    GD.PushError($"User {data.UserPresence.SessionId} claiming to be host, when user {_idMap[1]} has already claimed it");
                }
                else
                {
                    MapIdToSession(1, data.UserPresence.SessionId);
                }
                return;
            }

            if (!_idMap.TryGetValue(1, out string hostSession) || data.UserPresence.SessionId != hostSession)
            {
                GD.PushError($"Received meta message from user {data.UserPresence.SessionId} who isn't the host.");
                return;
            }

            if (content.Type == MetaMessageType.AssignPeerId)
            {
                string sessionId = content.SessionId;
                int peerId = content.PeerId;

                if (_users.TryGetValue(sessionId, out User user) && user.PeerId != 0)
                {
                    GD.PushError($"Attempting to assign peer id {peerId} to {sessionId} which already has id {user.PeerId}");
                    return;
                }

                MapIdToSession(peerId, sessionId);

                if (_mySessionId == sessionId)
                {
                    _matchState = MatchState.Connected;
                    _multiplayerPeer.Initialize(peerId);
                    _multiplayerPeer.SetConnectionStatus((int)Godot.MultiplayerPeer.ConnectionStatus.Connected);
                    EmitSignal(SignalName.MatchJoined);
                    _multiplayerPeer.EmitSignal("peer_connected", 1);
                }
                else
                {
                    _multiplayerPeer.EmitSignal("peer_connected", peerId);
                }
            }
            else
            {
                GD.PushError($"Received meta message with unknown type: {content.Type}");
            }
        }
        else if (data.OpCode == RpcOpCode)
        {
            string fromSessionId = data.UserPresence.SessionId;
            if (!_users.TryGetValue(fromSessionId, out User fromUser) || fromUser.PeerId == 0)
            {
                GD.PushError($"Received RPC from {fromSessionId} which isn't assigned a peer id");
                return;
            }
            
            _multiplayerPeer.DeliverPacket(data.State, fromUser.PeerId);
        }
    }

    private void OnMultiplayerPeerPacketGenerated(int peerId, byte[] buffer)
    {
        if (_matchState == MatchState.Connected)
        {
            IEnumerable<IUserPresence> targetPresences = null;
            if (peerId > 0)
            {
                if (!_idMap.TryGetValue(peerId, out string targetSession))
                {
                    GD.PushError($"Attempting to send RPC to unknown peer id: {peerId}");
                    return;
                }
                targetPresences = new[] { _users[targetSession].Presence };
            }
            
            _nakamaSocket.SendMatchStateAsync(_matchId, RpcOpCode, buffer, targetPresences);
        }
        else
        {
            GD.PushError("RPC sent while the NakamaMultiplayerBridge isn't connected!");
        }
    }
}