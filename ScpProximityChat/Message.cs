using System.ComponentModel;
using ScpProximityChat.Enums;

namespace ScpProximityChat
{
    public class Message
    {
        [Description("The message type between Broadcast and Hint.")]
        public MessageType Type { get; set; }

        [Description("The content of the message.")]
        public string Content { get; set; }

        [Description("The duration of the message.")]
        public ushort Duration { get; set; }

        [Description("A bool indicating whether to show this message or not.")]
        public bool Show { get; set; }
    }
}