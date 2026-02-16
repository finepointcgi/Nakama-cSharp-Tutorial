# Nakama cSharp Tutorial (Godot + C#)

A multiplayer sample project built with **Godot (C#/.NET)** and **Nakama**.

This project demonstrates:
- Authentication with Nakama
- Match join/host flow
- Multiplayer peer bridging (`NakamaMultiplayerPeer` + `NakamaMultiplayerBridge`)
- RPC-based gameplay communication
- Player authority assignment per peer
- Basic chat, group, and storage examples

---

## Requirements

Before running the project, install:

- **Godot 4.6 .NET** (or compatible 4.x .NET version)
- **.NET SDK 8.0+**
- A running **Nakama server**

Optional but recommended:
- Git
- VS Code (or JetBrains Rider)

---

## Clone the project

```bash
git clone <your-repository-url>
cd Nakama-cSharp-Tutorial
```

---

## Install dependencies (NuGet)

This project uses NuGet packages (including `NakamaClient`).

### 1) Restore packages

```bash
dotnet restore
```

### 2) Install/Update NakamaClient package (if needed)

```bash
dotnet add package NakamaClient
```

If you want to pin the version used by this project:

```bash
dotnet add package NakamaClient --version 3.21.2
```

The project also uses Newtonsoft.Json:

```bash
dotnet add package Newtonsoft.Json --version 13.0.3
```

---

## Configure Nakama connection

Open the project in Godot and select the `NakamaClient` node (script: `NakamaClient.cs`).

Set exported fields as needed:
- `NakamaScheme` (example: `http`)
- `NakamaHost` (example: `127.0.0.1`)
- `NakamaPort` (example: `7350`)
- `NakamaKey` (example: `defaultkey`)

---

## Run the project

### Option A: Run from Godot
1. Open project in Godot.
2. Build C# solution when prompted.
3. Run the main scene.

### Option B: Build with dotnet

```bash
dotnet build
```

Then run from Godot editor.

---

## Multiplayer quick test (2 clients)

1. Start Nakama server.
2. Launch two game instances.
3. Login with two different users.
4. In both clients, use the same lobby name and click **Join**.
5. Click **Ready/Start**.

Expected behavior:
- Both players are created in the scene.
- Authority is assigned to each player peer.
- No `MultiplayerSynchronizer` missing-node startup errors.

---

## Project structure (important files)

- `NakamaClient.cs` - login, matchmaking, ready/start, and high-level network flow
- `Nakama/NakamaMultiplayerBridge.cs` - maps Nakama match traffic to Godot multiplayer
- `Nakama/NakamaMultiplayerPeer.cs` - custom `MultiplayerPeerExtension`
- `SceneManager.cs` - player instantiation and authority setup
- `CharacterController.cs` - movement logic with authority checks

---

## Troubleshooting

### NuGet package not found
Run:

```bash
dotnet restore
dotnet nuget locals all --clear
dotnet restore
```

### Build errors in C# scripts
Make sure your .NET SDK is 8.0+:

```bash
dotnet --version
```

### Cannot connect to Nakama
Check:
- Server is running
- Host/Port/Scheme/Key values in `NakamaClient` exports
- Firewall/network access

---

## License

This project is provided under the terms in [LICENSE](LICENSE).
