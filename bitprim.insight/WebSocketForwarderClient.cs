using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using Polly;

namespace bitprim.insight
{
    public sealed class WebSocketForwarderClient : IDisposable
    {
        private readonly IOptions<NodeConfig> config_;
        private readonly ILogger<WebSocketForwarderClient> logger_;
        private readonly WebSocketHandler webSocketHandler_;
        private const string SUBSCRIPTION_MESSAGE_BLOCKS = "SubscribeToBlocks";
        private const string SUBSCRIPTION_MESSAGE_TXS = "SubscribeToTxs";
        private const int RECEPTION_BUFFER_SIZE = 1024 * 4;

        private ClientWebSocket webSocket_;

        private readonly Policy breakerPolicy_ = Policy.Handle<Exception>().CircuitBreakerAsync(2, TimeSpan.FromMinutes(1));
        private readonly Policy retryPolicy_ = Policy.Handle<Exception>().RetryForeverAsync();
        private readonly Policy execPolicy_;

        private int active_ = 1;

        public WebSocketForwarderClient(IOptions<NodeConfig> config, ILogger<WebSocketForwarderClient> logger,WebSocketHandler webSocketHandler)
        {
            config_ = config;
            logger_ = logger;
            webSocketHandler_ = webSocketHandler;
            execPolicy_ = Policy.WrapAsync(retryPolicy_,breakerPolicy_);
        }

        private async Task ReceiveHandler()
        {
            var buffer = new byte[RECEPTION_BUFFER_SIZE];
          
            while (Interlocked.CompareExchange(ref active_, 0, 0) > 0)
            {
                try
                {
                    var result = await webSocket_.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        if (Interlocked.CompareExchange(ref active_, 0, 0) > 0)
                        {
                            logger_.LogWarning("Reinitializing connection to websocket");
                            await ReInit();
                        }
                    }
                    else if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var content = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        logger_.LogInformation("Message received " + content);
                        
                        var obj = JObject.Parse(content);

                        switch (obj["eventname"].ToString())
                        {
                            case "block":
                                await webSocketHandler_.PublishBlock(content);
                                break;
                            case "tx":
                                await webSocketHandler_.PublishTransaction(content);
                                break;

                        }
                    }
                }
                catch (Exception e)
                {
                    if (Interlocked.CompareExchange(ref active_, 0, 0) > 0)
                    {
                        logger_.LogWarning(e,"Error processing ReceiveHandler");
                        logger_.LogWarning("Reinitializing connection to websocket");
                        await ReInit();
                    }   
                }
            }
        }

        private async Task CreateAndOpen()
        {
            webSocket_ = new ClientWebSocket();
            await webSocket_.ConnectAsync(
                new Uri(config_.Value.ForwardUrl.Replace("http://", "ws://")), CancellationToken.None);
        }

        private async Task SendSubscriptions()
        {
            await webSocket_.SendAsync
            (
                new ArraySegment<byte>(Encoding.UTF8.GetBytes(SUBSCRIPTION_MESSAGE_BLOCKS), 0, SUBSCRIPTION_MESSAGE_BLOCKS.Length),
                WebSocketMessageType.Text,
                true,
                CancellationToken.None
            );

            await webSocket_.SendAsync
            (
                new ArraySegment<byte>(Encoding.UTF8.GetBytes(SUBSCRIPTION_MESSAGE_TXS), 0, SUBSCRIPTION_MESSAGE_TXS.Length),
                WebSocketMessageType.Text,
                true,
                CancellationToken.None
            );
        }

        private async Task ReInit()
        {
            await execPolicy_.ExecuteAsync(CreateAndOpen);
            await SendSubscriptions();
        }

        public async Task Init()
        {
            await execPolicy_.ExecuteAsync(async ()=> await CreateAndOpen());

            Task receiverTask = ReceiveHandler();

            await SendSubscriptions();
        }

        private static bool WebSocketCanSend(WebSocket ws)
        {
            return !(ws.State == WebSocketState.Aborted ||
                     ws.State == WebSocketState.Closed ||
                     ws.State == WebSocketState.CloseSent);
        }

        public async Task Close()
        {
            Interlocked.Decrement(ref active_);
            try
            {
                if (WebSocketCanSend(webSocket_))
                {
                    await webSocket_.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
                }
            }
            catch (Exception e)
            {
                logger_.LogInformation(e,"Error closing websocket connection");
            }
        }

        public void Dispose()
        {
            webSocket_?.Dispose();
        }
    }
}