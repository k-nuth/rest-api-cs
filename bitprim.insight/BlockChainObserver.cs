using System;
using System.Collections.Generic;
using System.Linq;
using bitprim.insight.Websockets;
using Bitprim;
using Newtonsoft.Json;

namespace bitprim.insight
{
    internal class BlockChainObserver
    {
        private IChain chain_;
        private readonly Executor.BlockHandler blockHandler_;
        private readonly Executor.TransactionHandler txHandler_;
        private readonly Executor executor_;
        private readonly WebSocketHandler webSocketHandler_;
        private readonly NodeConfig config_;

        public BlockChainObserver(Executor executor, WebSocketHandler webSocketHandler, NodeConfig config)
        {
            executor_ = executor;
            chain_ = executor.Chain;
            webSocketHandler_ = webSocketHandler;
            config_ = config;

            if (config_.WebsocketsMsgBlockEnabled)
            {
                blockHandler_ = new Executor.BlockHandler(OnBlockReceived);
                executor.SubscribeToBlockChain(blockHandler_);
            }

            if (config_.WebsocketsMsgTxEnabled || config_.WebsocketsMsgAddressTxEnabled) 
            {
                txHandler_ = new Executor.TransactionHandler(OnTransactionReceived);
                executor.SubscribeToTransaction(txHandler_);
            }
        }

        private bool OnBlockReceived(ErrorCode error, UInt64 height, BlockList incoming, BlockList outgoing)
        {
            //TODO Avoid event processing if subscribers do not exist
            if(error == ErrorCode.Success && incoming != null && incoming.Count > 0)
            {
                string coinbaseTxHash;
                string destinationAddress;
                using(var getBlockResult = executor_.Chain.FetchBlockByHeightAsync(height).Result )
                {
                    Utils.CheckBitprimApiErrorCode(getBlockResult.ErrorCode, "FetchBlockByHeightAsync(" + height + ") failed");
                    ITransaction coinbaseTx = getBlockResult.Result.BlockData.GetNthTransaction(0);
                    coinbaseTxHash = Binary.ByteArrayToHexString(coinbaseTx.Hash);
                    destinationAddress = coinbaseTx.Outputs[0].PaymentAddress(executor_.UseTestnetRules).Encoded;
                }
                var newBlocksNotification = new
                {
                    eventname = "block",
                    coinbasetxid = coinbaseTxHash,
                    destinationaddr = destinationAddress
                };
                var task = webSocketHandler_.PublishBlock(JsonConvert.SerializeObject(newBlocksNotification));
                task.Wait();
            }
            return true;
        }

        private bool OnTransactionReceived(ErrorCode error, Transaction newTransaction)
        {
            //TODO Avoid event processing if subscribers do not exist
            if(error == ErrorCode.Success && newTransaction != null)
            {
                var txid = Binary.ByteArrayToHexString(newTransaction.Hash);
 
                HashSet<string> addresses = Utils.GetTransactionAddresses(executor_,newTransaction).GetAwaiter().GetResult();
                
                var addressesToPublish = new List<Tuple<string, string>>(addresses.Count);
                var balanceDeltas = new Dictionary<string, decimal>();

                if (config_.WebsocketsMsgAddressTxEnabled)
                {
                    foreach(string addr in addresses)
                    {
                        var addressBalanceDelta = Utils.SatoshisToCoinUnits(Utils.CalculateBalanceDelta(newTransaction, addr, executor_.Chain, executor_.UseTestnetRules).Result);
                        balanceDeltas[addr] = addressBalanceDelta;
                    
                        var addresstx = new
                        {
                            eventname = "addresstx",
                            txid = txid,
                            balanceDelta = addressBalanceDelta
                        };
                        addressesToPublish.Add(new Tuple<string, string>(addr, JsonConvert.SerializeObject(addresstx)));
                    }

                    var task = webSocketHandler_.PublishTransactionAddresses(addressesToPublish);
                    task.Wait();
                }
                else
                {   
                    if (config_.WebsocketsMsgTxEnabled)
                    {    
                        foreach(string addr in addresses)
                        {
                            var addressBalanceDelta = Utils.SatoshisToCoinUnits(Utils.CalculateBalanceDelta(newTransaction, addr, executor_.Chain, executor_.UseTestnetRules).Result);
                            balanceDeltas[addr] = addressBalanceDelta;    
                        }
                    }
                }

                if (config_.WebsocketsMsgTxEnabled)
                {    
                    var tx = new
                    {
                        eventname = "tx",
                        txid = txid,
                        valueOut = Utils.SatoshisToCoinUnits(newTransaction.TotalOutputValue),
                        addresses = addresses.ToArray(),
                        balanceDeltas = balanceDeltas
                    };

                    var task = webSocketHandler_.PublishTransaction(JsonConvert.SerializeObject(tx));
                    task.Wait();
                }
            }
            return true;
        }
    }
}
