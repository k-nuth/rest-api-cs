﻿namespace bitprim.insight.Websockets
{
    /// <summary>
    /// Json Dto for stats
    /// </summary>
    public class WebSocketStatsDto
    {
        public long wss_input_messages { get; set; }
        public long wss_output_messages { get; set; }
        public long wss_send_messages { get; set; }
        public long wss_subscriber_count { get; set; }
        public long wss_pending_queue_size { get; set; }
    }
}