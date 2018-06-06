using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Threading.Tasks;
using bitprim.insight.DTOs;
using Bitprim;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace bitprim.insight.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class TransactionController : Controller
    {
        private readonly Chain chain_;
        private readonly Executor nodeExecutor_;
        private readonly ILogger<TransactionController> logger_;
        private readonly NodeConfig config_;

        public TransactionController(IOptions<NodeConfig> config, Executor executor, ILogger<TransactionController> logger)
        {
            config_ = config.Value;
            nodeExecutor_ = executor;
            chain_ = executor.Chain;
            logger_ = logger;
        }

        // GET: tx/{hash}
        [ResponseCache(CacheProfileName = Constants.Cache.SHORT_CACHE_PROFILE_NAME)]
        [HttpGet("tx/{hash}")]
        public async Task<ActionResult> GetTransactionByHash(string hash, int requireConfirmed)
        {
            if(!Validations.IsValidHash(hash))
            {
                return StatusCode((int)System.Net.HttpStatusCode.BadRequest, hash + " is not a valid transaction hash");
            }

            Utils.CheckIfChainIsFresh(chain_, config_.AcceptStaleRequests);
            var binaryHash = Binary.HexStringToByteArray(hash);

            using(var getTxResult = await chain_.FetchTransactionAsync(binaryHash, requireConfirmed == 1))
            {
                Utils.CheckBitprimApiErrorCode(getTxResult.ErrorCode, "FetchTransactionAsync(" + hash + ") failed, check error log");
                bool confirmed = CheckIfTransactionIsConfirmed(getTxResult.Result);
                return Json(await TxToJSON
                (
                    getTxResult.Result.Tx, getTxResult.Result.TxPosition.BlockHeight, confirmed, noAsm: false, noScriptSig: false, noSpend: false)
                );
            }
        }

        // GET: rawtx/{hash}
        [ResponseCache(CacheProfileName = Constants.Cache.LONG_CACHE_PROFILE_NAME)]
        [HttpGet("rawtx/{hash}")]
        public async Task<ActionResult> GetRawTransactionByHash(string hash)
        {
            Utils.CheckIfChainIsFresh(chain_, config_.AcceptStaleRequests);
            var binaryHash = Binary.HexStringToByteArray(hash);
            
            using(var getTxResult = await chain_.FetchTransactionAsync(binaryHash, false))
            {
                Utils.CheckBitprimApiErrorCode(getTxResult.ErrorCode, "FetchTransactionAsync(" + hash + ") failed, check error log");
                
                var tx = getTxResult.Result.Tx;
                return Json
                (
                    new
                    {
                        rawtx = Binary.ByteArrayToHexString(tx.ToData(false).Reverse().ToArray())
                    }
                );
            }
        }

        // GET: txs/?block=HASH
        [ResponseCache(CacheProfileName = Constants.Cache.SHORT_CACHE_PROFILE_NAME)]
        [HttpGet("txs")]
        public async Task<ActionResult> GetTransactions(string block = null, string address = null, uint pageNum = 0)
        {
            if(block == null && address == null)
            {
                return StatusCode((int)System.Net.HttpStatusCode.BadRequest, "Specify block or address");
            }

            if(block != null && address != null)
            {
                return StatusCode((int)System.Net.HttpStatusCode.BadRequest, "Specify either block or address, but not both");
            }

            if(block != null)
            {
                return await GetTransactionsByBlockHash(block, pageNum);
            }

            return await GetTransactionsByAddress(address, pageNum);
        }

        [ResponseCache(CacheProfileName = Constants.Cache.SHORT_CACHE_PROFILE_NAME)]
        [HttpGet("addrs/{paymentAddresses}/txs")]
        public async Task<ActionResult> GetTransactionsForMultipleAddresses([FromRoute] string paymentAddresses, [FromQuery] int from = 0, [FromQuery] int to = 10)
        {
            return await DoGetTransactionsForMultipleAddresses(paymentAddresses, from, to, false, false, false);
        }

        [ResponseCache(CacheProfileName = Constants.Cache.SHORT_CACHE_PROFILE_NAME)]
        [HttpPost("addrs/txs")]
        public async Task<ActionResult> GetTransactionsForMultipleAddresses([FromBody] GetTxsForMultipleAddressesRequest request)
        {
            return await DoGetTransactionsForMultipleAddresses(request.addrs, request.from, request.to, request.noAsm == 1, request.noScriptSig == 1, request.noSpend == 1);
        }

        [HttpGet("tx/send")]
        public ActionResult GetBroadcastTransaction(RawTxRequest request)
        {
            return StatusCode((int)System.Net.HttpStatusCode.BadRequest, "tx/send method only accept POST requests");
        }

        [HttpPost("tx/send")]
        public async Task<ActionResult> BroadcastTransaction([FromBody] RawTxRequest request)
        {
            Utils.CheckIfChainIsFresh(chain_, config_.AcceptStaleRequests);

            using (var tx = new Transaction(Constants.TRANSACTION_VERSION_PROTOCOL,request.rawtx))
            {
                var ec = await chain_.OrganizeTransactionAsync(tx);

                if (ec != ErrorCode.Success)
                {
                    return StatusCode((int) System.Net.HttpStatusCode.BadRequest, ec.ToString());
                }
                    
                return Json
                (
                    new
                    {
                        txid =
                            Binary.ByteArrayToHexString(tx
                                .Hash) //TODO Check if this should be returned by organize call
                    }
                );
            }
        }

        private async Task<ActionResult> DoGetTransactionsForMultipleAddresses(string addrs, int from, int to,
                                                                   bool noAsm = true, bool noScriptSig = true, bool noSpend = true)
        { 
            if(from < 0)
            {
                from = 0;
            }

            if(from >= to)
            {
                return StatusCode((int)System.Net.HttpStatusCode.BadRequest, "'from' must be lower than 'to'");
            }
            
            var txs = new List<dynamic>();
            foreach(string address in System.Web.HttpUtility.UrlDecode(addrs).Split(","))
            {
                var txList = await GetTransactionsBySingleAddress(address, false, 0, noAsm, noScriptSig, noSpend);
                txs.AddRange(txList.Item1);
            }
            //Sort by descending blocktime
            txs.Sort((tx1, tx2) => tx2.blocktime.CompareTo(tx1.blocktime) );
            
            to = Math.Min(to, txs.Count);

            return Json(new{
                totalItems = txs.Count,
                from = from,
                to = to,
                items = txs.GetRange(from, to-from).ToArray()
            });   
        }

        private async Task<ActionResult> GetTransactionsByBlockHash(string blockHash, UInt64 pageNum)
        {
            Utils.CheckIfChainIsFresh(chain_, config_.AcceptStaleRequests);
            using(var getBlockResult = await chain_.FetchBlockByHashAsync(Binary.HexStringToByteArray(blockHash)))
            {
                Utils.CheckBitprimApiErrorCode(getBlockResult.ErrorCode, "FetchBlockByHashAsync(" + blockHash + ") failed, check error log");
                
                Block fullBlock = getBlockResult.Result.BlockData;
                UInt64 blockHeight = getBlockResult.Result.BlockHeight;
                UInt64 pageSize = (UInt64) config_.TransactionsByAddressPageSize;
                UInt64 pageCount = (UInt64) Math.Ceiling((double)fullBlock.TransactionCount/(double)pageSize);
                if(pageNum >= pageCount)
                {
                    return StatusCode
                    (
                        (int)System.Net.HttpStatusCode.BadRequest,
                        "pageNum cannot exceed " + (pageCount - 1) + " (zero-indexed)"
                    );
                }
                
                var txs = new List<object>();
                for(UInt64 i=0; i<pageSize && pageNum * pageSize + i < fullBlock.TransactionCount; i++)
                {
                    var tx = fullBlock.GetNthTransaction(pageNum * pageSize + i);
                    txs.Add(await TxToJSON(tx, blockHeight, confirmed: true, noAsm: false, noScriptSig: false, noSpend: false));
                }
                
                return Json(new
                {
                    pagesTotal = pageCount,
                    txs = txs.ToArray()
                });
            }
        }

        private async Task<ActionResult> GetTransactionsByAddress(string address, uint pageNum)
        {
            var txsByAddress = await GetTransactionsBySingleAddress(address, true, pageNum, false, false, false);
            
            return Json(new{
                pagesTotal = txsByAddress.Item2,
                txs = txsByAddress.Item1.ToArray()
            });
        }

        private async Task<Tuple<List<object>, UInt64>> GetTransactionsBySingleAddress(string paymentAddress, bool pageResults, uint pageNum,bool noAsm, bool noScriptSig, bool noSpend)
        {
            Utils.CheckIfChainIsFresh(chain_, config_.AcceptStaleRequests);

            using (var address = new PaymentAddress(paymentAddress))
            using (var getTransactionResult = await chain_.FetchConfirmedTransactionsAsync(address, UInt64.MaxValue, 0))
            {
                Utils.CheckBitprimApiErrorCode(getTransactionResult.ErrorCode, "FetchTransactionAsync(" + paymentAddress + ") failed, check error log.");
                
                var txIds = getTransactionResult.Result;
                var txs = new List<object>();
                var pageSize = pageResults ? (uint) config_.TransactionsByAddressPageSize : txIds.Count;
                
                for(uint i=0; i<pageSize && (pageNum * pageSize + i < txIds.Count); i++)
                {
                    var txHash = txIds[(pageNum * pageSize + i)];
                    using(var getTxResult = await chain_.FetchTransactionAsync(txHash, true))
                    {
                        Utils.CheckBitprimApiErrorCode(getTxResult.ErrorCode, "FetchTransactionAsync(" + Binary.ByteArrayToHexString(txHash) + ") failed, check error log");
                        bool confirmed = CheckIfTransactionIsConfirmed(getTxResult.Result);
                        txs.Add(await TxToJSON(getTxResult.Result.Tx, getTxResult.Result.TxPosition.BlockHeight, confirmed, noAsm, noScriptSig, noSpend));
                    }
                }
                UInt64 pageCount = (UInt64) Math.Ceiling((double)txIds.Count/(double)pageSize);
                List<object> unconfirmedTxs = await GetUnconfirmedTransactions(address);
                txs = unconfirmedTxs.Concat(txs).ToList(); //Unconfirmed txs go first
                return new Tuple<List<object>, UInt64>(txs, pageCount);
            }
        }

        private async Task<object> TxToJSON(Transaction tx, UInt64 blockHeight, bool confirmed, bool noAsm, bool noScriptSig, bool noSpend)
        {
            UInt32 blockTimestamp = 0;
            string blockHash = "";
            if(confirmed)
            {
                using(var getBlockHeaderResult = await chain_.FetchBlockHeaderByHeightAsync(blockHeight))
                {
                    Utils.CheckBitprimApiErrorCode(getBlockHeaderResult.ErrorCode, "FetchBlockHeaderByHeightAsync(" + blockHeight + ") failed, check error log");
                    Header blockHeader = getBlockHeaderResult.Result.BlockData;
                    blockTimestamp = blockHeader.Timestamp;
                    blockHash = Binary.ByteArrayToHexString(blockHeader.Hash);
                }    
            }
            ApiCallResult<UInt64> getLastHeightResult = await chain_.FetchLastHeightAsync();
            Utils.CheckBitprimApiErrorCode(getLastHeightResult.ErrorCode, "FetchLastHeightAsync failed, check error log");
            
            dynamic txJson = new ExpandoObject();
            txJson.txid = Binary.ByteArrayToHexString(tx.Hash);
            txJson.version = tx.Version;
            txJson.locktime = tx.Locktime;
            txJson.vin = await TxInputsToJSON(tx, noAsm, noScriptSig);
            txJson.vout = await TxOutputsToJSON(tx, noAsm, noSpend);
            txJson.confirmations = confirmed? getLastHeightResult.Result - blockHeight + 1 : 0;
            txJson.isCoinBase = tx.IsCoinbase;
            txJson.valueOut = Utils.SatoshisToCoinUnits(tx.TotalOutputValue);
            txJson.size = tx.GetSerializedSize();
            UInt64 inputsTotal = await ManuallyCalculateInputsTotal(tx); //TODO Solve at native layer
            txJson.valueIn = Utils.SatoshisToCoinUnits(inputsTotal);
            if(confirmed)
            {
                txJson.blockhash = blockHash;
                txJson.time = blockTimestamp;
                txJson.blocktime = blockTimestamp;
                txJson.blockheight = blockHeight;
            }
            else
            {
                txJson.blockheight = -1;
                txJson.time = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            }
            if ( !tx.IsCoinbase )
            {
                txJson.fees = Utils.SatoshisToCoinUnits(inputsTotal - tx.TotalOutputValue); //TODO Solve at native layer
            }
            return txJson;
        }

        private async Task<object> TxInputsToJSON(Transaction tx, bool noAsm, bool noScriptSig)
        {
            var inputs = tx.Inputs;
            var jsonInputs = new List<object>();
            for(uint i=0; i<inputs.Count; i++)
            {
                var input = inputs[i];
                
                dynamic jsonInput = new ExpandoObject();
                if(tx.IsCoinbase)
                {
                    byte[] scriptData = input.Script.ToData(false);
                    Array.Reverse(scriptData, 0, scriptData.Length);
                    jsonInput.coinbase = Binary.ByteArrayToHexString(scriptData);
                }
                else
                {
                    await SetInputNonCoinbaseFields(jsonInput, input, noAsm, noScriptSig);
                }
                jsonInput.sequence = input.Sequence;
                jsonInput.n = i;
                jsonInputs.Add(jsonInput);
            }
            return jsonInputs.ToArray();
        }

        private async Task SetInputNonCoinbaseFields(dynamic jsonInput, Input input, bool noAsm, bool noScriptSig)
        {
            var previousOutput = input.PreviousOutput;
            jsonInput.txid = Binary.ByteArrayToHexString(previousOutput.Hash);
            jsonInput.vout = previousOutput.Index;
            if(!noScriptSig)
            {
                jsonInput.scriptSig = InputScriptToJSON(input.Script, noAsm);
            }
            using(var getTxResult = await chain_.FetchTransactionAsync(previousOutput.Hash, false))
            {
                Utils.CheckBitprimApiErrorCode(getTxResult.ErrorCode, "FetchTransactionAsync(" + Binary.ByteArrayToHexString(previousOutput.Hash) + ") failed, check errog log");
                
                var output = getTxResult.Result.Tx.Outputs[previousOutput.Index];
                var outputAddress = output.PaymentAddress(nodeExecutor_.UseTestnetRules);
                if(outputAddress.IsValid)
                {
                    jsonInput.addr =  outputAddress.Encoded;
                }
                jsonInput.valueSat = output.Value;
                jsonInput.value = Utils.SatoshisToCoinUnits(output.Value);
                jsonInput.doubleSpentTxID = null; //We don't handle double spent transactions
            }
        }

        private async Task<List<object>> GetUnconfirmedTransactions(PaymentAddress address)
        {
            var unconfirmedTxsJson = new List<object>();
            using(MempoolTransactionList unconfirmedTxs = chain_.GetMempoolTransactions(address, nodeExecutor_.UseTestnetRules))
            {
                foreach(MempoolTransaction unconfirmedTx in unconfirmedTxs)
                {
                    using(var getTxResult = await chain_.FetchTransactionAsync(Binary.HexStringToByteArray(unconfirmedTx.Hash), requireConfirmed: false))
                    {
                        Utils.CheckBitprimApiErrorCode(getTxResult.ErrorCode, "FetchTransactionAsync(" + unconfirmedTx.Hash + ") failed, check error log");
                        unconfirmedTxsJson.Add
                        (
                            await TxToJSON(getTxResult.Result.Tx, 0, confirmed: false, noAsm: true, noScriptSig: true, noSpend: true)
                        );
                    }
                }
                return unconfirmedTxsJson;
            }
        }

        private async Task<object> TxOutputsToJSON(Transaction tx, bool noAsm, bool noSpend)
        {
            var outputs = tx.Outputs;
            var jsonOutputs = new List<object>();
            for(uint i=0; i<outputs.Count; i++)
            {
                var output = outputs[i];
                dynamic jsonOutput = new ExpandoObject();
                jsonOutput.value = Utils.SatoshisToCoinUnits(output.Value);
                jsonOutput.n = i;
                jsonOutput.scriptPubKey = OutputScriptToJSON(output, noAsm);
                if(!noSpend)
                {
                    await SetOutputSpendInfo(jsonOutput, tx.Hash, (UInt32)i);
                }
                jsonOutputs.Add(jsonOutput);
            }
            return jsonOutputs.ToArray();
        }

        private async Task SetOutputSpendInfo(dynamic jsonOutput, byte[] txHash, UInt32 index)
        {
            using (var outPoint = new OutputPoint(txHash, index))
            {
                var fetchSpendResult = await chain_.FetchSpendAsync(outPoint);
                if(fetchSpendResult.ErrorCode == ErrorCode.NotFound)
                {
                    jsonOutput.spentTxId = null;
                    jsonOutput.spentIndex = null;
                    jsonOutput.spentHeight = null;
                }
                else
                {
                    Utils.CheckBitprimApiErrorCode(fetchSpendResult.ErrorCode, "FetchSpendAsync failed, check error log");
                    var spend = fetchSpendResult.Result;
                    jsonOutput.spentTxId = Binary.ByteArrayToHexString(spend.Hash);
                    jsonOutput.spentIndex = spend.Index;
                    using(var getTxResult = await chain_.FetchTransactionAsync(spend.Hash, false))
                    {
                        Utils.CheckBitprimApiErrorCode(getTxResult.ErrorCode, "FetchTransactionAsync(" + Binary.ByteArrayToHexString(spend.Hash) + "), check error log");
                        jsonOutput.spentHeight = getTxResult.Result.TxPosition.BlockHeight;
                    }
                }
            }
            
        }

        private async Task<UInt64> ManuallyCalculateInputsTotal(Transaction tx)
        {
            UInt64 inputs_total = 0;
            foreach(Input txInput in tx.Inputs)
            {
                using(var getTxResult = await chain_.FetchTransactionAsync(txInput.PreviousOutput.Hash, true))
                {
                    if(getTxResult.ErrorCode != ErrorCode.NotFound)
                    {
                        Utils.CheckBitprimApiErrorCode(getTxResult.ErrorCode, "FetchTransactionAsync(" + Binary.ByteArrayToHexString(txInput.PreviousOutput.Hash) + "), check error log");
                        inputs_total += getTxResult.Result.Tx.Outputs[txInput.PreviousOutput.Index].Value;
                    }
                }
            }
            return inputs_total;
        }

        //TODO Move this logic to node-cint and expose via a property (Transaction.Confirmed)
        private bool CheckIfTransactionIsConfirmed(GetTxDataResult txResult)
        {
            switch( NodeSettings.CurrencyType )
            {
                case CurrencyType.BitcoinCash: return txResult.TxPosition.Index != UInt32.MaxValue;
                default: return txResult.TxPosition.Index != UInt16.MaxValue;
            }
        }

        private object InputScriptToJSON(Script inputScript, bool noAsm)
        {
            byte[] scriptData = inputScript.ToData(false);
            Array.Reverse(scriptData, 0, scriptData.Length);
            dynamic result = new ExpandoObject();
            if(!noAsm)
            {
                result.asm = inputScript.ToString(0);
            }
            result.hex = Binary.ByteArrayToHexString(scriptData);
            return result;
        }

        private object OutputScriptToJSON(Output output, bool noAsm)
        {
            var script = output.Script;
            var scriptData = script.ToData(false);
            Array.Reverse(scriptData, 0, scriptData.Length);
            dynamic result = new ExpandoObject();
            if(!noAsm)
            {
                result.asm = script.ToString(0);
            }
            result.hex = Binary.ByteArrayToHexString(scriptData);
            var outputAddress = output.PaymentAddress(nodeExecutor_.UseTestnetRules);
            if(outputAddress.IsValid)
            {
                result.addresses = new List<object> {outputAddress.Encoded}.ToArray();
            }
            result.type = GetScriptType(script.Type);
            return result;
        }

        private string GetScriptType(string type)
        {
            if (type == "pay_key_hash")
                return "pubkeyhash";

            if (type == "pay_script_hash")
                return "scripthash";

            return type;
        }

    }
}
