using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Custom <see cref="MultiplayerPeerExtension"/> implementation backed by Nakama match messages.
/// </summary>
public partial class NakamaMultiplayerPeer : MultiplayerPeerExtension
{
    private const int MaxPacketSize = 1 << 24;

    private int _selfId = 0;
    private ConnectionStatus _connectionStatus = ConnectionStatus.Disconnected;
    private bool _refusingNewConnections = false;
    private int _targetId = 0;

    // Using a readonly struct to prevent GC allocations and maximize performance
    public readonly struct Packet
    {
        public readonly byte[] Data;
        public readonly int From;

        public Packet(byte[] data, int from)
        {
            Data = data;
            From = from;
        }
    }

    // Queue<T> gives us O(1) Enqueue and Dequeue, far outperforming GDScript's Array pop_front()
    private readonly Queue<Packet> _incomingPackets = new Queue<Packet>();

    /// <summary>
    /// Raised when Godot generates an outgoing packet.
    /// First argument is target peer id, second is raw payload.
    /// </summary>
    public event Action<int, byte[]> PacketGenerated;

    // --- MultiplayerPeerExtension Overrides ---

    public override byte[] _GetPacketScript()
    {
        if (_incomingPackets.Count == 0)
        {
            return Array.Empty<byte>(); // More efficient than new byte[0]
        }
        
        return _incomingPackets.Dequeue().Data;
    }

    public override MultiplayerPeer.TransferModeEnum _GetPacketMode()
    {
        return MultiplayerPeer.TransferModeEnum.Reliable;
    }

    public override int _GetPacketChannel()
    {
        return 0;
    }

    public override Error _PutPacketScript(byte[] pBuffer)
    {
        PacketGenerated?.Invoke(_targetId, pBuffer);
        return Error.Ok;
    }

    public override int _GetAvailablePacketCount()
    {
        return _incomingPackets.Count;
    }

    public override int _GetMaxPacketSize()
    {
        return MaxPacketSize;
    }

    public override void _SetTransferChannel(int pChannel)
    {
        // Not implemented/needed for this Nakama structure
    }

    public override int _GetTransferChannel()
    {
        return 0;
    }

    public override void _SetTransferMode(MultiplayerPeer.TransferModeEnum pMode)
    {
        // Not implemented/needed for this Nakama structure
    }

    public override MultiplayerPeer.TransferModeEnum _GetTransferMode()
    {
        return MultiplayerPeer.TransferModeEnum.Reliable;
    }

    public override void _SetTargetPeer(int pPeerId)
    {
        _targetId = pPeerId;
    }

    public override int _GetPacketPeer()
    {
        if (_connectionStatus != ConnectionStatus.Connected)
        {
            return 1;
        }

        if (_incomingPackets.Count == 0)
        {
            return 1;
        }

        // We Peek() here because Godot extracts the sender's peer ID 
        // right BEFORE it extracts the actual packet data.
        return _incomingPackets.Peek().From;
    }

    public override bool _IsServer()
    {
        return _selfId == 1;
    }

    public override void _Poll()
    {
        // Polling is handled via the socket connection dynamically
    }

    public override int _GetUniqueId()
    {
        return _selfId;
    }

    public override void _SetRefuseNewConnections(bool pEnable)
    {
        _refusingNewConnections = pEnable;
    }

    public override bool _IsRefusingNewConnections()
    {
        return _refusingNewConnections;
    }

    public override ConnectionStatus _GetConnectionStatus()
    {
        return _connectionStatus;
    }

    // --- Custom Initialization and Delivery Methods ---

    /// <summary>
    /// Initializes this peer with the local unique peer id.
    /// </summary>
    /// <param name="pSelfId">Assigned local peer id.</param>
    public void Initialize(int pSelfId)
    {
        if (_connectionStatus != ConnectionStatus.Connecting)
        {
            return;
        }

        _selfId = pSelfId;
        if (_selfId == 1)
        {
            _connectionStatus = ConnectionStatus.Connected;
        }
    }

    /// <summary>
    /// Updates the underlying connection status.
    /// </summary>
    /// <param name="pConnectionStatus">Integer value of <see cref="ConnectionStatus"/>.</param>
    public void SetConnectionStatus(int pConnectionStatus)
    {
        // Safe cast from int to Godot's built-in enum
        _connectionStatus = (ConnectionStatus)pConnectionStatus;
    }

    /// <summary>
    /// Queues an incoming packet so Godot can consume it on polling.
    /// </summary>
    /// <param name="pData">Raw packet bytes.</param>
    /// <param name="pFromPeerId">Sender peer id.</param>
    public void DeliverPacket(byte[] pData, int pFromPeerId)
    {
        var packet = new Packet(pData, pFromPeerId);
        _incomingPackets.Enqueue(packet);
    }
}