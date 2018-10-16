using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using bitprim.insight.Websockets;
using Bitprim;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace bitprim.insight
{
    internal class BlockChainObserver
    {
        private readonly Executor.BlockHandler blockHandler_;
        private readonly Executor.TransactionHandler txHandler_;
        private readonly Executor executor_;
        private readonly WebSocketHandler webSocketHandler_;
        private readonly NodeConfig config_;
        private readonly ILogger<BlockChainObserver> logger_;

        public BlockChainObserver(Executor executor, WebSocketHandler webSocketHandler, NodeConfig config, ILogger<BlockChainObserver> logger)
        {
            executor_ = executor;
            webSocketHandler_ = webSocketHandler;
            config_ = config;
            logger_ = logger;

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
                logger_.LogInformation($"New block arrived ({height}). {incoming.Count} blocks arrived.Block zero TxCount:{incoming[0].TransactionCount}"); 
                string blockHash;
                string coinbaseTxHash;
                string destinationAddress;
                using(var getBlockResult = executor_.Chain.FetchBlockByHeightAsync(height).Result )
                {
                    Utils.CheckBitprimApiErrorCode(getBlockResult.ErrorCode, "FetchBlockByHeightAsync(" + height + ") failed");
                    Block newBlock = getBlockResult.Result.BlockData;
                    blockHash = Binary.ByteArrayToHexString(newBlock.Hash);
                    Transaction coinbaseTx = newBlock.GetNthTransaction(0);
                    coinbaseTxHash = Binary.ByteArrayToHexString(coinbaseTx.Hash);
                    destinationAddress = coinbaseTx.Outputs[0].PaymentAddress(executor_.UseTestnetRules).Encoded;
                }
                var newBlocksNotification = new
                {
                    eventname = "block",
                    blockhash = blockHash,
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
                logger_.LogDebug("New tx arrived. "); 
                var txid = Binary.ByteArrayToHexString(newTransaction.Hash);
                HashSet<string> addresses = Utils.GetTransactionAddresses(executor_,newTransaction).GetAwaiter().GetResult();
                Dictionary<string, decimal> balanceDeltas = null;

                if (config_.WebsocketsMsgAddressTxEnabled)
                {
                    balanceDeltas = PublishAddressTxMessages(newTransaction, txid, addresses);
                }
                else if (config_.WebsocketsMsgTxEnabled)
                {
                    balanceDeltas = new Dictionary<string, decimal>();
                    foreach(string addr in addresses)
                    {
                        var addressBalanceDelta = Utils.SatoshisToCoinUnits(Utils.CalculateBalanceDelta
                        (
                            newTransaction, addr, executor_.Chain, executor_.UseTestnetRules
                        ).Result);
                        balanceDeltas[addr] = addressBalanceDelta;    
                    }
                }

                if (config_.WebsocketsMsgTxEnabled)
                {
                    PublishTxMessage(newTransaction, txid, addresses, balanceDeltas);
                }
            }
            return true;
        }

        private Dictionary<string, decimal> PublishAddressTxMessages(Transaction newTransaction, string txid, HashSet<string> addresses)
        {
            var balanceDeltas = new Dictionary<string, decimal>();
            var addressesToPublish = new List<Tuple<string, string>>(addresses.Count);
            foreach(string addr in addresses)
            {
                var addressBalanceDelta = Utils.SatoshisToCoinUnits(Utils.CalculateBalanceDelta
                (
                    newTransaction, addr, executor_.Chain, executor_.UseTestnetRules
                ).Result);
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
            return balanceDeltas;
        }

        private void PublishTxMessage(Transaction newTransaction, string txid, HashSet<string> addresses,
                                      Dictionary<string, decimal> balanceDeltas)
        {
            dynamic tx = new ExpandoObject();
            tx.eventname = "tx";
            tx.txid = txid;
            tx.valueOut = Utils.SatoshisToCoinUnits(newTransaction.TotalOutputValue);
            tx.addresses = addresses.ToArray();
            tx.balanceDeltas = balanceDeltas;

            if(config_.WebsocketsMsgTxIncludeVout)
            {
                var vouts = new object[newTransaction.Outputs.Count];
                for(uint i =0; i<newTransaction.Outputs.Count; i++)
                {
                    Output output = newTransaction.Outputs[i];
                    string addr = output.PaymentAddress(executor_.UseTestnetRules).Encoded;
                    var outputJson = new ExpandoObject() as IDictionary<string, object>;
                    outputJson.Add(addr, output.Value);
                    vouts[i] = outputJson;
                }
                tx.vout = vouts;
            }

            var task = webSocketHandler_.PublishTransaction(JsonConvert.SerializeObject(tx));
            task.Wait();
        }
    }
}
