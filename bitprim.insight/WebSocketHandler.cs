using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace api
{
    public class WebSocketHandler
    {
        private ConcurrentDictionary<WebSocket, Dictionary<string, BlockingCollection<string>>> subscriberQueues_;
        private const int CHANNEL_REMOVAL_HOLDOFF = 5;
        private const int RECEPTION_BUFFER_SIZE = 1024 * 4;
        private const string BLOCKS_CHANNEL_NAME = "BlocksChannel";
        private const string BLOCKS_SUBSCRIPTION_MESSAGE = "SubscribeToBlocks";
        private const string SERVER_ABORT_MESSAGE = "ServerAbort";
        private const string SERVER_CLOSE_MESSAGE = "ServerClose";
        private const string SUBSCRIPTION_END_MESSAGE = "Unsubscribe";
        private const string TXS_CHANNEL_NAME = "TxsChannel";
        private const string TXS_SUBSCRIPTION_MESSAGE = "SubscribeToTxs";

        private ILogger logger_;

        public WebSocketHandler()
        {
            subscriberQueues_ = new ConcurrentDictionary<WebSocket, Dictionary<string, BlockingCollection<string>>>();
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
        public async Task Subscribe(HttpContext context, WebSocket webSocket)
        {
            var buffer = new byte[RECEPTION_BUFFER_SIZE];
            var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            LogFrame(result, buffer);
            // If the client sends "ServerClose", then they want a server-originated close to take place
            string content = "";
            while(true) //TODO Check shutdown even when client doesnt send close message
            {
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
                    else if(content.Equals(BLOCKS_SUBSCRIPTION_MESSAGE))
                    {
                        Task.Run( ()=> SubscriberLoop(webSocket, BLOCKS_CHANNEL_NAME) );
                    }else if(content.Equals(TXS_SUBSCRIPTION_MESSAGE))
                    {
                        Task.Run( ()=> SubscriberLoop(webSocket, TXS_CHANNEL_NAME) );
                    }
                }
                result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                LogFrame(result, buffer);
            }
        }

        public void PublishBlock(string block)
        {
            Publish(BLOCKS_CHANNEL_NAME, block);
        }

        public void PublishTransaction(string tx)
        {
            Publish(TXS_CHANNEL_NAME, tx);
        }

        public void CancelAllSubscriptions()
        {
            lock(subscriberQueues_)
            {
                foreach(Dictionary<string, BlockingCollection<string>> connection in subscriberQueues_.Values)
                {
                    foreach(BlockingCollection<string> channelQueue in connection.Values)
                    {
                        channelQueue.Add(SUBSCRIPTION_END_MESSAGE);
                    }
                }
            }
        }

        private async void SubscriberLoop(WebSocket webSocket, string channelName)
        {
            if( ! RegisterChannel(webSocket, channelName) )
            {
                return;
            }
            bool subscribed = true;
            while(subscribed)
            {
                //This call blocks on an empty queue
                string queueItem = subscriberQueues_[webSocket][channelName].Take();
                subscribed = (queueItem != SUBSCRIPTION_END_MESSAGE);
                if(subscribed)
                {
                    await webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(queueItem), 0, queueItem.Length), WebSocketMessageType.Text, true, CancellationToken.None);
                    logger_.LogDebug($"Sent Frame {WebSocketMessageType.Text}: Len={queueItem.Length}, Fin={true}: {queueItem}");
                }
            }
            UnregisterChannel(webSocket, channelName);
        }

        private async void UnregisterChannel(WebSocket webSocket, string channelName)
        {
            bool closedAllChannels = false;
            lock(subscriberQueues_)
            {
                subscriberQueues_[webSocket].Remove(channelName);
                if( subscriberQueues_[webSocket].Count == 0)
                {
                    bool removedChannel = false;
                    while(!removedChannel)
                    {
                        Dictionary<string, BlockingCollection<string>> removed;
                        subscriberQueues_.TryRemove(webSocket, out removed);
                        if(!removedChannel)
                        {
                            Thread.Sleep(TimeSpan.FromSeconds(CHANNEL_REMOVAL_HOLDOFF));
                        }
                    }
                    closedAllChannels = true;
                }
            }
            if(closedAllChannels)
            {
                await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "All subscriptions cancelled", CancellationToken.None);
            }
        }

        private bool RegisterChannel(WebSocket webSocket, string channelName)
        {
            lock(subscriberQueues_)
            {
                bool registeredChannel = false;
                if( ! subscriberQueues_.ContainsKey(webSocket) )
                {
                    subscriberQueues_[webSocket] = new Dictionary<string, BlockingCollection<string>>();
                    subscriberQueues_[webSocket].Add(channelName, new BlockingCollection<string>());
                    registeredChannel = true;
                }
                else
                {
                    if(subscriberQueues_[webSocket].ContainsKey(channelName))
                    {
                        registeredChannel = false; //Channel already exists
                    }else
                    {
                        subscriberQueues_[webSocket].Add(channelName, new BlockingCollection<string>());
                        registeredChannel = true;
                    }
                }
                return registeredChannel;
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
            Console.WriteLine("Received frame: " + message);
            logger_.LogDebug("Received Frame " + message);
        }

        private void Publish(string channelName, string item)
        {
            foreach(Dictionary<string, BlockingCollection<string>> connection in subscriberQueues_.Values)
            {
                if(connection.ContainsKey(channelName))
                {
                    connection[channelName].Add(item);
                }
            }
        }

    }
}