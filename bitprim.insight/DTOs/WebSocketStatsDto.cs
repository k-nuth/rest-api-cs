namespace bitprim.insight.DTOs
{
    /// <summary>
    /// Json Dto for stats
    /// </summary>
    public class WebSocketStatsDto
    {
        /// <summary>
        /// Count of ingoing messages
        /// </summary>
        public long wss_input_messages { get; set; }
        
        /// <summary>
        /// Count of outgoing messages
        /// </summary>
        public long wss_output_messages { get; set; }
        
        /// <summary>
        /// Count of sent messages
        /// </summary>
        public long wss_sent_messages { get; set; }
        
        /// <summary>
        /// Count of websocket subscribers
        /// </summary>
        public long wss_subscriber_count { get; set; }
        
        /// <summary>
        /// Count of pending websocket messages
        /// </summary>
        public long wss_pending_queue_size { get; set; }
    }
}