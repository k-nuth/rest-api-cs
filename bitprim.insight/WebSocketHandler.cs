using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace api
{
    public class WebSocketHandler
    {
        private ConcurrentDictionary<WebSocket, BlockingCollection<string>> subscriberQueues_;
        private const int RECEPTION_BUFFER_SIZE = 1024 * 4;
        private const string SERVER_ABORT_MESSAGE = "ServerAbort";
        private const string SERVER_CLOSE_MESSAGE = "ServerClose";
        private const string SUBSCRIPTION_END_MESSAGE = "UnsubscribeFromBlocks;
        private const string SUBSCRIPTION_MESSAGE = "SubscribeToBlocks";

        private ILogger logger_;

        public WebSocketHandler()
        {
            subscriberQueues_ = new ConcurrentDictionary<WebSocket, BlockingCollection<string>>();
        }

        public ILogger Logger
        {
            set
            {
                logger_ = value;
            }
        }

        ///<summary>
        /// Wait for a subscription message, and start subscription loop. Close
        /// connection when loop ends
        ///</summary>
        public async Task SubscribeToBlocks(HttpContext context, WebSocket webSocket)
        {
            var buffer = new byte[RECEPTION_BUFFER_SIZE];
            var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            LogFrame(result, buffer);
            // If the client sends "ServerClose", then they want a server-originated close to take place
            string content = "";
            if (result.MessageType == WebSocketMessageType.Text)
            {
                content = Encoding.UTF8.GetString(buffer, 0, result.Count);
                if (content.Equals(SERVER_CLOSE_MESSAGE))
                {
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing from Server", CancellationToken.None);
                    logger_.LogDebug($"Sent Frame Close: {WebSocketCloseStatus.NormalClosure} Closing from Server");
                    return;
                }
                else if (content.Equals(SERVER_ABORT_MESSAGE))
                {
                    context.Abort();
                }
                else if(content.Equals(SUBSCRIPTION_MESSAGE))
                {
                    //Enter subscription loop for this connection until subscription ends
                    await BlockSubscriberLoop(webSocket);
                    await webSocket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
                }
            }
            else
            {
                await webSocket.CloseAsync(result.CloseStatus.Value, "Closing from server due to invalid request", CancellationToken.None);
            }
        }

        public void PublishBlock(string item)
        {
            foreach(BlockingCollection<string> queue in subscriberQueues_.Values)
            {
                queue.Add(item);
            }
        }

        public void CancelAllSubscriptions()
        {
            lock(subscriberQueues_)
            {
                foreach(BlockingCollection<string> queue in subscriberQueues_.Values)
                {
                    queue.Add(SUBSCRIPTION_END_MESSAGE);
                }
            }
        }

        private async Task BlockSubscriberLoop(WebSocket webSocket)
        {
            lock(subscriberQueues_)
            {
                if( ! subscriberQueues_.ContainsKey(webSocket) )
                {
                    subscriberQueues_[webSocket] = new BlockingCollection<string>();
                }
            }
            bool subscribed = true;
            while(subscribed)
            {
                //This call blocks on an empty queue
                string queueItem = subscriberQueues_[webSocket].Take();
                subscribed = (queueItem != SUBSCRIPTION_END_MESSAGE);
                if(subscribed)
                {
                    await webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(queueItem), 0, queueItem.Length), WebSocketMessageType.Text, true, CancellationToken.None);
                    logger_.LogDebug($"Sent Frame {WebSocketMessageType.Text}: Len={queueItem.Length}, Fin={true}: {queueItem}");
                }
            }
        }

        private void LogFrame(WebSocketReceiveResult frame, byte[] buffer)
        {
            var close = frame.CloseStatus != null;
            string message;
            if (close)
            {
                message = $"Close: {frame.CloseStatus.Value} {frame.CloseStatusDescription}";
            }
            else
            {
                string content = "<<binary>>";
                if (frame.MessageType == WebSocketMessageType.Text)
                {
                    content = Encoding.UTF8.GetString(buffer, 0, frame.Count);
                }
                message = $"{frame.MessageType}: Len={frame.Count}, Fin={frame.EndOfMessage}: {content}";
            }
            logger_.LogDebug("Received Frame " + message);
        }
    }
}