using System;
using System.Collections.Generic;
using bitprim.insight.Websockets;
using Bitprim;
using Newtonsoft.Json;

namespace bitprim.insight
{
    public class BlockChainObserver
    {
        private readonly Executor.BlockHandler blockHandler_;
        private readonly Executor.TransactionHandler txHandler_;
        private readonly Executor executor_;
        private readonly WebSocketHandler webSocketHandler_;

        public BlockChainObserver(Executor executor, WebSocketHandler webSocketHandler)
        {
            executor_ = executor;
            webSocketHandler_ = webSocketHandler;
            blockHandler_ = new Executor.BlockHandler(OnBlockReceived);
            txHandler_ = new Executor.TransactionHandler(OnTransactionReceived);
            executor.SubscribeToBlockChain(blockHandler_);
            executor.SubscribeToTransaction(txHandler_);
        }

        private bool OnBlockReceived(ErrorCode error, UInt64 height, BlockList incoming, BlockList outgoing)
        {
            if(error == ErrorCode.Success && incoming != null && incoming.Count > 0)
            {
                var newBlocksNotification = new
                {
                    eventname = "block"
                };
                var task = webSocketHandler_.PublishBlock(JsonConvert.SerializeObject(newBlocksNotification));
                task.Wait();
            }
            return true;
        }

        private bool OnTransactionReceived(ErrorCode error, Transaction newTransaction)
        {
            if(error == ErrorCode.Success && newTransaction != null)
            {
                var txid = Binary.ByteArrayToHexString(newTransaction.Hash);

                List<string> addresses = Utils.GetTransactionAddresses(executor_,newTransaction).GetAwaiter().GetResult();

                var tx = new
                {
                    eventname = "tx",
                    txid = txid,
                    valueOut = Utils.SatoshisToCoinUnits(newTransaction.TotalOutputValue),
                    addresses = addresses.ToArray()
                };

                var task = webSocketHandler_.PublishTransaction(JsonConvert.SerializeObject(tx));
                task.Wait();

                task = webSocketHandler_.PublishTransactionAddress(txid,addresses);
                task.Wait();
            }
            return true;
        }
    }
}