

namespace bitprim.insight.Websockets
{
    internal enum BitprimWebSocketMessageType
    {
        PUBLICATION,
        SHUTDOWN
    };

    internal struct BitprimWebSocketMessage
    {
        public BitprimWebSocketMessageType MessageType { get; set; }
        public string ChannelName { get; set; }
        public string Content { get; set; }
    }
}