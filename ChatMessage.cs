/// <summary>
/// Serializable chat payload exchanged through Nakama chat channels.
/// </summary>
public class ChatMessage
{
    /// <summary>
    /// Message body content.
    /// </summary>
    public string Message;

    /// <summary>
    /// Sender username.
    /// </summary>
    public string User;

    /// <summary>
    /// Logical room/channel identifier used by the sample.
    /// </summary>
    public string ID;

    /// <summary>
    /// Message type marker used by the UI router.
    /// </summary>
    public int Type;
}