using System.Threading;

namespace bitprim.insight.Websockets
{
    /// <summary>
    /// Store websocket's internal state
    /// </summary>
    public static class WebSocketStats
    {
        private static long _inputMessages;
        private static long _outputMessages;
        private static long _sentMessages;
        private static long _subscriberCount;
        private static long _pendingQueueSize;

        /// <summary>
        /// Count of ingoing messages
        /// </summary>
        public static long InputMessages => Interlocked.Read(ref _inputMessages);
        
        /// <summary>
        /// Count of outgoing messages
        /// </summary>
        public static long OutputMessages => Interlocked.Read(ref _outputMessages);
        
        /// <summary>
        /// Count of sent messages
        /// </summary>
        public static long SentMessages => Interlocked.Read(ref _sentMessages);
        
        /// <summary>
        /// Count of websocket subscribers
        /// </summary>
        public static long SubscriberCount => Interlocked.Read(ref _subscriberCount);
        
        /// <summary>
        /// Count of pending websocket messages
        /// </summary>
        public static long PendingQueueSize => Interlocked.Read(ref _pendingQueueSize);

        /// <summary>
        /// Increment _inputMessages in one unit
        /// </summary>
        public static void IncrementInputMessages()
        {
            Interlocked.Increment(ref _inputMessages);
        }

        /// <summary>
        /// Increment _outputMessages in one unit
        /// </summary>
        public static void IncrementOutputMessages()
        {
            Interlocked.Increment(ref _outputMessages);
        }

        /// <summary>
        /// Increment _sentMessages in one unit
        /// </summary>
        public static void IncrementSentMessages()
        {
            Interlocked.Increment(ref _sentMessages);
        }

        /// <summary>
        /// Increment _subscriberCount in one unit
        /// </summary>
        public static void IncrementSubscriberCount()
        {
            Interlocked.Increment(ref _subscriberCount);
        }

        /// <summary>
        /// Decrement _subscriberCount in one unit
        /// </summary>
        public static void DecrementSubscriberCount()
        {
            Interlocked.Decrement(ref _subscriberCount);
        }

        /// <summary>
        /// Increment _pendingQueueSize in one unit
        /// </summary>
        public static void IncrementPendingQueueSize()
        {
            Interlocked.Increment(ref _pendingQueueSize);
        }

        /// <summary>
        /// Decrement _pendingQueueSize in one unit
        /// </summary>
        public static void DecrementPendingQueueSize()
        {
            Interlocked.Decrement(ref _pendingQueueSize);
        }

        //TODO: Add setting to deactivate stats
    }
}