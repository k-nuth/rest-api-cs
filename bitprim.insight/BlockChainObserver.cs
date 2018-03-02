using Bitprim;
using System;
using System.Net.WebSockets;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace api
{
    public class BlockChainObserver
    {
        private WebSocketHandler webSocketHandler_;

        public BlockChainObserver(Executor executor, WebSocketHandler webSocketHandler)
        {
            webSocketHandler_ = webSocketHandler;
            executor.SubscribeToBlockChain(OnBlockReceived);
            executor.SubscribeToTransaction(OnTransactionReceived);
        }

        private bool OnBlockReceived(ErrorCode error, UInt64 height, BlockList incoming, BlockList outgoing)
        {
            if(error == ErrorCode.Success && incoming.Count > 0)
            {
                var newBlocksNotification = new
                {
                    eventname = "block"
                };
                webSocketHandler_.PublishBlock(JsonConvert.SerializeObject(newBlocksNotification));
            }
            return true;
        }

        private bool OnTransactionReceived(ErrorCode error, Transaction newTransaction)
        {
            if(error == ErrorCode.Success && newTransaction != null)
            {
                var tx = new
                {
                    eventname = "tx",
                    txid = Binary.ByteArrayToHexString(newTransaction.Hash),
                    valueOut = Utils.SatoshisToBTC(newTransaction.TotalOutputValue)
                };
                webSocketHandler_.PublishTransaction(JsonConvert.SerializeObject(tx));
            }
            return true;
        }
    }
}