using Bitprim;
using System;
using System.Net.WebSockets;
using System.Threading.Tasks;

namespace api
{
    public class BlockChainObserver
    {

        public BlockChainObserver(Executor executor)
        {
            executor.SubscribeToBlockChain(OnBlockReceived);
            executor.SubscribeToTransaction(OnTransactionReceived);
            //TODO Subscribe to tx and test on top of chain
        }

        private bool OnBlockReceived(ErrorCode error, UInt64 height, BlockList incoming, BlockList outgoing)
        {
            Console.WriteLine("Block received!"); //TODO Send via web socket
            return true;
        }

        private bool OnTransactionReceived(ErrorCode error, Transaction newTransaction)
        {
            Console.WriteLine("Transaction received!"); //TODO Send via web socket
            return true;
        }
    }
}