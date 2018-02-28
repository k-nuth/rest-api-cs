using Bitprim;
using System;
using System.Net.WebSockets;
using System.Threading.Tasks;

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
            webSocketHandler_.PublishBlock("Block received"); //TODO Send block data
            return true;
        }

        private bool OnTransactionReceived(ErrorCode error, Transaction newTransaction)
        {
            webSocketHandler_.PublishTransaction("Transaction received"); //TODO Send tx data
            return true;
        }
    }
}