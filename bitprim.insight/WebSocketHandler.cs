using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Nito.AsyncEx;
using Serilog;

namespace api
{
    public class WebSocketHandler
    {
        private AsyncProducerConsumerQueue<BitprimWebSocketMessage> messageQueue_;
        private ConcurrentDictionary<WebSocket, ConcurrentDictionary<string, byte>> subscriptions_;
        private const int DICT_REMOVAL_HOLDOFF = 5;
        private const int MAX_CHANNEL_REMOVAL_TRIES = 5;
        private const int RECEPTION_BUFFER_SIZE = 1024 * 4;
        private const string BLOCKS_CHANNEL_NAME = "BlocksChannel";
        private const string BLOCKS_SUBSCRIPTION_MESSAGE = "SubscribeToBlocks";
        private const string SERVER_ABORT_MESSAGE = "ServerAbort";
        private const string SERVER_CLOSE_MESSAGE = "ServerClose";
        private const string SUBSCRIPTION_END_MESSAGE = "Unsubscribe";
        private const string TXS_CHANNEL_NAME = "TxsChannel";
        private const string TXS_SUBSCRIPTION_MESSAGE = "SubscribeToTxs";
        private int acceptSubscriptions_ = 1; //It is an int because Interlocked does not support bool 
        private ILogger<WebSocketHandler> logger_;

        public WebSocketHandler(ILogger<WebSocketHandler> logger)
        {
            messageQueue_ = new AsyncProducerConsumerQueue<BitprimWebSocketMessage>();
            subscriptions_ = new ConcurrentDictionary<WebSocket, ConcurrentDictionary<string, byte>>();
            logger_ = logger;
        }

        ///<summary>
        /// Wait for a subscription message, and start subscription loop. Close
        /// connection when loop ends
        ///</summary>
        public async Task Subscribe(HttpContext context, WebSocket webSocket)
        {
            try
            {
                var buffer = new byte[RECEPTION_BUFFER_SIZE];
                var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                LogFrame(result, buffer);
                // If the client sends "ServerClose", then they want a server-originated close to take place
                string content = "";
                var subLoops = new List<Task>();
                bool keepListening = true;
                while(keepListening) //TODO Check shutdown even when client doesnt send close message
                {
                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        content = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        if (content.Equals(SERVER_CLOSE_MESSAGE))
                        {
                            await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing from Server", CancellationToken.None);
                            logger_.LogDebug($"Sent Frame Close: {WebSocketCloseStatus.NormalClosure} Closing from Server");
                            keepListening = false;
                        }
                        else if (content.Equals(SERVER_ABORT_MESSAGE))
                        {
                            context.Abort();
                            keepListening = false;
                        }
                        else if(content.Equals(BLOCKS_SUBSCRIPTION_MESSAGE) && Interlocked.CompareExchange(ref acceptSubscriptions_, 0, 0) > 0)
                        {
                            RegisterChannel(webSocket, BLOCKS_CHANNEL_NAME);
                        }else if(content.Equals(TXS_SUBSCRIPTION_MESSAGE) && Interlocked.CompareExchange(ref acceptSubscriptions_, 0, 0) > 0)
                        {
                            RegisterChannel(webSocket, TXS_CHANNEL_NAME);
                        }
                    }
                    if(keepListening)
                    {
                        result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                        LogFrame(result, buffer);
                    }
                }
                await UnregisterChannels(webSocket);
            }
            catch(WebSocketException ex)
            {
                logger_.LogWarning("Subscribe - Web socket error, closing connection; " + ex);
                context.Abort();
            }
        }

        public async Task PublishBlock(string block)
        {
            await Publish(BLOCKS_CHANNEL_NAME, block);
        }

        public async Task PublishTransaction(string tx)
        {
            await Publish(TXS_CHANNEL_NAME, tx);
        }

        public async Task Shutdown()
        {
            Interlocked.Decrement(ref acceptSubscriptions_);
            foreach(WebSocket ws in subscriptions_.Keys)
            {
                try
                {
                    await UnregisterChannels(ws);
                }
                catch(WebSocketException ex)
                {
                    logger_.LogWarning("Error unregistering channel: " + ex.ToString());
                }
            }
            await messageQueue_.EnqueueAsync
            (
                new BitprimWebSocketMessage
                {
                    MessageType = BitprimWebSocketMessageType.SHUTDOWN
                }
            );
        }

        private async Task PublisherThread()
        {
            try
            {
                bool keepRunning = true;
                while(keepRunning)
                {
                    //This call blocks on an empty queue
                    BitprimWebSocketMessage message = await messageQueue_.DequeueAsync();
                    keepRunning = (message.MessageType != BitprimWebSocketMessageType.SHUTDOWN);
                    if(keepRunning)
                    {
                        foreach(KeyValuePair<WebSocket, ConcurrentDictionary<string, byte>> ws in subscriptions_)
                        {
                            byte dummy;
                            if(ws.Value.TryGetValue(message.ChannelName, out dummy))
                            {
                                try
                                {
                                    await ws.Key.SendAsync
                                    (
                                        new ArraySegment<byte>(Encoding.UTF8.GetBytes(message.Content), 0, message.Content.Length),
                                        WebSocketMessageType.Text,
                                        true,
                                        CancellationToken.None
                                    );
                                    logger_.LogDebug($"Sent Frame {WebSocketMessageType.Text}: Len={message.Content.Length}, Fin={true}: {message.Content}");
                                }
                                catch(WebSocketException ex)
                                {
                                    logger_.LogWarning("Error sending to client: " + ex.ToString());
                                }
                            }
                        }
                    }
                }
            }
            catch(Exception ex)
            {
                logger_.LogError("PublisherThread - Error: " + ex);
                await UnregisterChannel(webSocket, channelName);
            }
        }

        private async Task UnregisterChannels(WebSocket webSocket)
        {
            bool removedSocket = false;
            int tries = 0;
            while(!removedSocket && tries < MAX_CHANNEL_REMOVAL_TRIES)
            {
                ConcurrentDictionary<string, byte> removed;
                removedSocket = subscriptions_.TryRemove(webSocket, out removed);
                if( ! removedSocket )
                {
                    ++tries;
                    await Task.Delay(TimeSpan.FromSeconds(DICT_REMOVAL_HOLDOFF));
                }
            }
            await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "All subscriptions cancelled", CancellationToken.None);
        }

        private bool RegisterChannel(WebSocket webSocket, string channelName)
        {
            // Wait for first subscription to launch publisher worker thread  
            if(subscriptions_.Count == 0)
            {
                Task.Run( () => PublisherThread() );
            }
            subscriptions_.TryAdd(webSocket, new ConcurrentDictionary<string, byte>());
            return subscriptions_[webSocket].TryAdd(channelName, 1);
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

        private async Task Publish(string channelName, string item)
        {
            if(subscriptions_.Count > 0)
            {
                await messageQueue_.EnqueueAsync
                (
                    new BitprimWebSocketMessage
                    {
                        ChannelName = channelName,
                        Content = item,
                        MessageType = BitprimWebSocketMessageType.PUBLICATION
                    }
                );
            }
        }

    }
}