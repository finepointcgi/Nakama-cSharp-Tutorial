using System;

namespace NakamacSharpTutorial;

/// <summary>
/// Stores lobby and profile information for a connected player.
/// </summary>
public class PlayerInfo
{
    /// <summary>
    /// Display name used in social/group features.
    /// </summary>
    public string Name;

    /// <summary>
    /// Sample progression level.
    /// </summary>
    public int Level;

    /// <summary>
    /// Sample matchmaking skill value.
    /// </summary>
    public int Skill;

    /// <summary>
    /// Unique runtime identifier (mapped to username in this sample).
    /// </summary>
    public string Id; // players unique id

    /// <summary>
    /// Ready/status flag used during pre-game flow.
    /// </summary>
    public int Status = 0; // if the player is ready or not or any other status
}
