using Bitprim;
using System;

namespace api
{
    public class BlockChainObserver
    {
        public BlockChainObserver(Executor executor)
        {
            executor.SubscribeToBlockChain(OnBlockReceived);
            //TODO Subscribe to tx and test on top of chain
        }

        private bool OnBlockReceived(ErrorCode error, UInt64 height, BlockList incoming, BlockList outgoing)
        {
            Console.WriteLine("Block received!"); //TODO Send via web socket
            return true;
        }
    }
}