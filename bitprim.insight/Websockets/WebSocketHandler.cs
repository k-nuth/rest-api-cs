using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Bitprim;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Nito.AsyncEx;
using Polly;

namespace bitprim.insight.Websockets
{
    internal class WebSocketHandler
    {
        private readonly AsyncProducerConsumerQueue<BitprimWebSocketMessage> messageQueue_;
        private readonly ConcurrentDictionary<WebSocket, ConcurrentDictionary<string, byte>> subscriptions_;
        
        private const int DICT_REMOVAL_HOLDOFF = 5;
        private const int MAX_CHANNEL_REMOVAL_TRIES = 5;
        private const int RECEPTION_BUFFER_SIZE = 1024 * 4;
        
        private const string BLOCKS_CHANNEL_NAME = "BlocksChannel";
        private const string BLOCKS_SUBSCRIPTION_MESSAGE = "SubscribeToBlocks";
        
        private const string SERVER_ABORT_MESSAGE = "ServerAbort";
        private const string SERVER_CLOSE_MESSAGE = "ServerClose";
        
        private const string TXS_CHANNEL_NAME = "TxsChannel";
        private const string TXS_SUBSCRIPTION_MESSAGE = "SubscribeToTxs";
        
        private int acceptSubscriptions_ = 1; //It is an int because Interlocked does not support bool 
        
        private readonly ILogger<WebSocketHandler> logger_;
        private readonly NodeConfig config_;
        private readonly Policy retryPolicy_;

        public WebSocketHandler(ILogger<WebSocketHandler> logger, NodeConfig config)
        {
            messageQueue_ = new AsyncProducerConsumerQueue<BitprimWebSocketMessage>();
            subscriptions_ = new ConcurrentDictionary<WebSocket, ConcurrentDictionary<string, byte>>();
            logger_ = logger;
            config_ = config;
            retryPolicy_ = Policy.Handle<Exception>().WaitAndRetryAsync
            (
                config_.MaxSocketPublishRetries,
                retryAttempt => TimeSpan.FromSeconds(config_.SocketPublishRetryIntervalInSeconds),
                (exception, timeSpan, retryCount, context) => 
                {
                    logger_.LogWarning("Sending to socket failed, retry " + retryCount + "/" + config_.MaxSocketPublishRetries);
                }
            );
        }

        public void Init()
        {
            logger_.LogInformation("Initializing websocket handler");
            _ = PublisherThread();
        }

        ///<summary>
        /// Wait for a subscription message, and start subscription loop. Close
        /// connection when loop ends
        ///</summary>
        public async Task Subscribe(HttpContext context, WebSocket webSocket)
        {
            logger_.LogInformation("New websocket subscription arrived");
            try
            {
                var buffer = new byte[RECEPTION_BUFFER_SIZE];
                
                // If the client sends "ServerClose", then they want a server-originated close to take place
                bool keepListening = true;
                while (keepListening)
                {
                    var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                
                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var content = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        LogFrame(result, content);
                        
                        if (content.Equals(SERVER_CLOSE_MESSAGE))
                        {
                            logger_.LogDebug("Server close message received");
                            keepListening = false;
                        }
                        else if (content.Equals(SERVER_ABORT_MESSAGE))
                        {
                            logger_.LogDebug("Server abort message received");
                            keepListening = false;
                        }
                        else if (content.Equals(BLOCKS_SUBSCRIPTION_MESSAGE) &&
                                 Interlocked.CompareExchange(ref acceptSubscriptions_, 0, 0) > 0)
                        {
                            logger_.LogInformation("Registering new block channel");
                            RegisterChannel(webSocket, BLOCKS_CHANNEL_NAME);
                        }
                        else if (content.Equals(TXS_SUBSCRIPTION_MESSAGE) &&
                                 Interlocked.CompareExchange(ref acceptSubscriptions_, 0, 0) > 0)
                        {
                            logger_.LogInformation("Registering new tx channel");
                            RegisterChannel(webSocket, TXS_CHANNEL_NAME);
                        }
                        else 
                        {
                            using (var address = new PaymentAddress(content))
                            {
                                if (address.IsValid)
                                {
                                    logger_.LogInformation("Registering new addresstx channel");
                                    RegisterChannel(webSocket, content);
                                }
                            }
                        }
                    }
                    else if (result.MessageType == WebSocketMessageType.Close)
                    {
                        logger_.LogInformation("Weboscket close message arrived");
                        LogFrame(result, "Close message");
                        keepListening = false;
                    }
                }

                
                await UnregisterChannels(webSocket);
            }
            catch (WebSocketException ex)
            {
                logger_.LogDebug("Status " + webSocket.State);
                logger_.LogDebug("Close Status " + webSocket.CloseStatus);
                logger_.LogDebug("WebSocketErrorCode " + ex.WebSocketErrorCode);

                if (webSocket.State != WebSocketState.CloseSent &&
                    ex.WebSocketErrorCode != WebSocketError.ConnectionClosedPrematurely)
                {
                    logger_.LogWarning(ex,"Subscribe - Web socket error, closing connection");
                }
                
                await UnregisterChannels(webSocket);
            }
            catch (Exception e)
            {
                logger_.LogWarning(e,"Subscribe - error, closing connection");
                await UnregisterChannels(webSocket);
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

        public async Task PublishTransactionAddresses(List<Tuple<string, string>> addresses)
        {
            foreach (Tuple<string, string> address in addresses)
            {
                await Publish(address.Item1, address.Item2);
            }
        }

        public async Task Shutdown()
        {
            logger_.LogInformation("Closing websocket handler");
            Interlocked.Decrement(ref acceptSubscriptions_);
            
            logger_.LogInformation("Unregistering channels");
            foreach(var ws in subscriptions_.Keys)
            {
                try
                {
                    await UnregisterChannels(ws);
                }
                catch(WebSocketException ex)
                {
                    logger_.LogWarning(ex,"Error unregistering channel");
                }
            }

            logger_.LogInformation("Sending shutdown message");

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
            logger_.LogInformation("Initializing websocket publisher thread");
            try
            {
                var keepRunning = true;
                while(keepRunning)
                {
                    //This call blocks on an empty queue
                    var message = await messageQueue_.DequeueAsync();
                    
                    WebSocketStats.IncrementOutputMessages();
                    WebSocketStats.DecrementPendingQueueSize();

                    keepRunning = message.MessageType != BitprimWebSocketMessageType.SHUTDOWN;
                    if(keepRunning)
                    {
                        var buffer = Encoding.UTF8.GetBytes(message.Content);
                       
                        foreach(var ws in subscriptions_)
                        {
                            if(ws.Value.TryGetValue(message.ChannelName, out var dummy))
                            {
                                try
                                {
                                    await retryPolicy_.ExecuteAsync( async () => 
                                    { 
                                        await ws.Key.SendAsync
                                        (
                                            new ArraySegment<byte>(buffer, 0, message.Content.Length),
                                            WebSocketMessageType.Text,
                                            true,
                                            CancellationToken.None
                                        );
                                        WebSocketStats.IncrementSentMessages();
                                        logger_.LogDebug($"Sent Frame {WebSocketMessageType.Text}: Len={message.Content.Length}, Fin={true}: {message.Content}");
                                    });
                                }
                                catch(WebSocketException ex)
                                {
                                    logger_.LogWarning(ex,"Maxed out retries sending to client, closing channels");
                                    await UnregisterChannels(ws.Key);
                                }
                            }
                        }
                    }
                }
            }
            catch(Exception ex)
            {
                logger_.LogError(ex,"PublisherThread error");
            }
            logger_.LogInformation("Closing websocket publisher thread");
        }

        private async Task UnregisterChannels(WebSocket webSocket)
        {
            logger_.LogInformation("Unregistering websockets channels");
            bool removedSocket = false;
            int tries = 0;
            logger_.LogInformation("Removing websocket");
            while(!removedSocket && tries < MAX_CHANNEL_REMOVAL_TRIES)
            {
                removedSocket = subscriptions_.TryRemove(webSocket, out _);
                if( ! removedSocket )
                {
                    ++tries;
                    await Task.Delay(TimeSpan.FromSeconds(DICT_REMOVAL_HOLDOFF));
                }
            }

            WebSocketStats.DecrementSubscriberCount();
            
            logger_.LogDebug("WebSocket status " + webSocket.State);

            logger_.LogInformation("Closing websocket");
            if (WebSocketCanSend(webSocket))
            {
                await webSocket.CloseOutputAsync (WebSocketCloseStatus.EndpointUnavailable, "All subscriptions cancelled", CancellationToken.None);
                logger_.LogInformation("Websocket closed");
            }
           
            logger_.LogInformation("Channel unregistered");

            //TODO
            // If the last subscriber is removed , clear the pending queue
        }

        private void RegisterChannel(WebSocket webSocket, string channelName)
        {
            if (subscriptions_.TryAdd(webSocket, new ConcurrentDictionary<string, byte>()))
            {
                WebSocketStats.IncrementSubscriberCount();
            }
            subscriptions_[webSocket].TryAdd(channelName, 1);
        }

        private void LogFrame(WebSocketReceiveResult frame, string msgContent)
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
                    content = msgContent;
                }
                message = $"{frame.MessageType}: Len={frame.Count}, Fin={frame.EndOfMessage}: {content}";
            }
            logger_.LogDebug("Received Frame " + message);
        }

        private async Task Publish(string channelName, string item)
        {
            if(subscriptions_.Count > 0)
            {
                logger_.LogDebug("Adding websocket message to queue");

                await messageQueue_.EnqueueAsync
                (
                    new BitprimWebSocketMessage
                    {
                        ChannelName = channelName,
                        Content = item,
                        MessageType = BitprimWebSocketMessageType.PUBLICATION
                    }
                );
                WebSocketStats.IncrementInputMessages();
                WebSocketStats.IncrementPendingQueueSize();
            }
        }

        private static bool WebSocketCanSend(WebSocket ws)
        {
            return !(ws.State == WebSocketState.Aborted ||
                     ws.State == WebSocketState.Closed ||
                     ws.State == WebSocketState.CloseSent);
        }
    }
}