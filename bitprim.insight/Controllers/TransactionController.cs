using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using bitprim.insight.DTOs;
using Bitprim;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace bitprim.insight.Controllers
{
    [Route("api/[controller]")]
    public class TransactionController : Controller
    {
        private Chain chain_;
        private Executor nodeExecutor_;
        private readonly NodeConfig config_;

        public TransactionController(IOptions<NodeConfig> config, Executor executor)
        {
            config_ = config.Value;
            nodeExecutor_ = executor;
            chain_ = executor.Chain;
        }

        // GET: api/tx/{hash}
        [HttpGet("/api/tx/{hash}")]
        public ActionResult GetTransactionByHash(string hash, bool requireConfirmed)
        {
            if(!Validations.IsValidHash(hash))
            {
                return StatusCode((int)System.Net.HttpStatusCode.BadRequest, hash + " is not a valid transaction hash");
            }
            Utils.CheckIfChainIsFresh(chain_, config_.AcceptStaleRequests);
            byte[] binaryHash = Binary.HexStringToByteArray(hash);
            using(DisposableApiCallResult<GetTxDataResult> getTxResult = chain_.GetTransaction(binaryHash, requireConfirmed))
            {
                Utils.CheckBitprimApiErrorCode(getTxResult.ErrorCode, "GetTransaction(" + hash + ") failed, check error log");
                return Json(TxToJSON
                (
                    getTxResult.Result.Tx, getTxResult.Result.TxPosition.BlockHeight, noAsm: false, noScriptSig: false, noSpend: false)
                );
            }
        }

        // GET: api/rawtx/{hash}
        [HttpGet("/api/rawtx/{hash}")]
        public ActionResult GetRawTransactionByHash(string hash)
        {
            Utils.CheckIfChainIsFresh(chain_, config_.AcceptStaleRequests);
            byte[] binaryHash = Binary.HexStringToByteArray(hash);
            using(DisposableApiCallResult<GetTxDataResult> getTxResult = chain_.GetTransaction(binaryHash, false))
            {
                Utils.CheckBitprimApiErrorCode(getTxResult.ErrorCode, "GetTransaction(" + hash + ") failed, check error log");
                Transaction tx = getTxResult.Result.Tx;
                return Json
                (
                    new
                    {
                        rawtx = Binary.ByteArrayToHexString(tx.ToData(false).Reverse().ToArray())
                    }
                );
            }
        }

        // GET: api/txs/?block=HASH
        [HttpGet("/api/txs")]
        public ActionResult GetTransactions(string block = null, string address = null, UInt64? pageNum = 0)
        {
            if(block == null && address == null)
            {
                return StatusCode((int)System.Net.HttpStatusCode.BadRequest, "Specify block or address");
            }
            else if(block != null && address != null)
            {
                return StatusCode((int)System.Net.HttpStatusCode.BadRequest, "Specify either block or address, but not both");
            }
            else if(block != null)
            {
                return GetTransactionsByBlockHash(block, pageNum.Value);
            }
            else
            {
                return GetTransactionsByAddress(address, pageNum.Value);
            }
        }

        [HttpGet("/api/addrs/{paymentAddresses}/txs")]
        public ActionResult GetTransactionsForMultipleAddresses([FromRoute] string paymentAddresses, [FromQuery] int? from = 0, [FromQuery] int? to = 20)
        {
            return DoGetTransactionsForMultipleAddresses(paymentAddresses, from.Value, to.Value, false, false, false);
        }

        [HttpPost("/api/addrs/txs")]
        public ActionResult GetTransactionsForMultipleAddresses([FromBody] GetTxsForMultipleAddressesRequest request)
        {
            return DoGetTransactionsForMultipleAddresses(request.addrs, request.from.Value, request.to.Value, request.noAsm.Value, request.noScriptSig.Value, request.noSpend.Value);
        }

        [HttpPost("/api/tx/send")]
        public ActionResult BroadcastTransaction([FromBody] string rawtx)
        {
            Utils.CheckIfChainIsFresh(chain_, config_.AcceptStaleRequests);
            var tx = new Transaction(rawtx);
            ErrorCode ec = chain_.OrganizeTransactionSync(tx);
            Utils.CheckBitprimApiErrorCode(ec, "OrganizeTransaction(" + rawtx + ") failed");
            return Json
            (
                new
                {
                    txid = Binary.ByteArrayToHexString(tx.Hash) //TODO Check if this should be returned by organize call
                }
            );
        }

        private ActionResult DoGetTransactionsForMultipleAddresses(string addrs, int from, int to,
                                                                   bool noAsm = true, bool noScriptSig = true, bool noSpend = true)
        {
            if(from < 0)
            {
                return StatusCode((int)System.Net.HttpStatusCode.BadRequest, "'from' must be non negative");
            }
            if(from > to)
            {
                return StatusCode((int)System.Net.HttpStatusCode.BadRequest, "'from' must be lower or equal than 'to'");
            }
            var txs = new List<dynamic>();
            foreach(string address in System.Web.HttpUtility.UrlDecode(addrs).Split(","))
            {
                txs = txs.Concat(GetTransactionsBySingleAddress(address, false, 0, noAsm, noScriptSig, noSpend).Item1).ToList();
            }
            //Sort by descending blocktime
            txs.Sort((tx1, tx2) => tx2.blocktime.CompareTo(tx1.blocktime) );
            to = (int) Math.Min(to, txs.Count - 1);
            return Json(new{
                totalItems = txs.Count,
                from = from,
                to = to,
                items = txs.GetRange(from, to - from + 1).ToArray()
            });   
        }

        private ActionResult GetTransactionsByBlockHash(string blockHash, UInt64 pageNum)
        {
            Utils.CheckIfChainIsFresh(chain_, config_.AcceptStaleRequests);
            using(DisposableApiCallResult<GetBlockDataResult<Block>> getBlockResult = chain_.GetBlockByHash(Binary.HexStringToByteArray(blockHash)))
            {
                Utils.CheckBitprimApiErrorCode(getBlockResult.ErrorCode, "GetBlockByHash(" + blockHash + ") failed, check error log");
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
                List<object> txs = new List<object>();
                for(UInt64 i=0; i<pageSize && pageNum * pageSize + i < fullBlock.TransactionCount; i++)
                {
                    Transaction tx = fullBlock.GetNthTransaction(pageNum * pageSize + i);
                    txs.Add(TxToJSON(tx, blockHeight, noAsm: false, noScriptSig: false, noSpend: false));
                }
                return Json(new
                {
                    pagesTotal = pageCount,
                    txs = txs.ToArray()
                });
            }
        }

        private ActionResult GetTransactionsByAddress(string address, UInt64 pageNum)
        {
            Tuple<List<object>, UInt64> txsByAddress = GetTransactionsBySingleAddress(address, true, pageNum, true, true, true);
            UInt64 pageCount = txsByAddress.Item2;
            if(pageNum >= pageCount)
            {
                return StatusCode
                (
                    (int)System.Net.HttpStatusCode.BadRequest,
                    "pageNum cannot exceed " + (pageCount - 1) + " (zero-indexed)"
                );
            }
            return Json(new{
                pagesTotal = pageCount,
                txs = txsByAddress.Item1.ToArray()
            });
        }

        private Tuple<List<object>, UInt64> GetTransactionsBySingleAddress(string paymentAddress, bool pageResults, UInt64 pageNum,
                                                                           bool noAsm, bool noScriptSig, bool noSpend)
        {
            Utils.CheckIfChainIsFresh(chain_, config_.AcceptStaleRequests);
            using(DisposableApiCallResult<HistoryCompactList> getAddressHistoryResult = chain_.GetHistory(new PaymentAddress(paymentAddress), UInt64.MaxValue, 0))
            {
                Utils.CheckBitprimApiErrorCode(getAddressHistoryResult.ErrorCode, "GetHistory(" + paymentAddress + ") failed, check error log.");
                HistoryCompactList history = getAddressHistoryResult.Result;
                var txs = new List<object>();
                UInt64 pageSize = pageResults? (UInt64) config_.TransactionsByAddressPageSize : history.Count;
                for(UInt64 i=0; i<pageSize && (pageNum * pageSize + i < history.Count); i++)
                {
                    HistoryCompact compact = history[(int)(pageNum * pageSize + i)];
                    using(DisposableApiCallResult<GetTxDataResult> getTxResult = chain_.GetTransaction(compact.Point.Hash, true))
                    {
                        Utils.CheckBitprimApiErrorCode(getTxResult.ErrorCode, "GetTransaction(" + Binary.ByteArrayToHexString(compact.Point.Hash) + ") failed, check error log");
                        txs.Add(TxToJSON(getTxResult.Result.Tx, getTxResult.Result.TxPosition.BlockHeight, noAsm, noScriptSig, noSpend));
                    }
                }
                UInt64 pageCount = (UInt64) Math.Ceiling((double)history.Count/(double)pageSize);
                return new Tuple<List<object>, UInt64>(txs, pageCount);
            }
        }

        private object TxToJSON(Transaction tx, UInt64 blockHeight, bool noAsm, bool noScriptSig, bool noSpend)
        {
            using(DisposableApiCallResult<GetBlockDataResult<Header>> getBlockHeaderResult = chain_.GetBlockHeaderByHeight(blockHeight))
            {
                Utils.CheckBitprimApiErrorCode(getBlockHeaderResult.ErrorCode, "GetBlockHeaderByHeight(" + blockHeight + ") failed, check error log");
                Header blockHeader = getBlockHeaderResult.Result.BlockData;
                ApiCallResult<UInt64> getLastHeightResult = chain_.GetLastHeight();
                Utils.CheckBitprimApiErrorCode(getLastHeightResult.ErrorCode, "GetLastHeight failed, check error log");
                return new
                {
                    txid = Binary.ByteArrayToHexString(tx.Hash),
                    version = tx.Version,
                    locktime = tx.Locktime,
                    vin = TxInputsToJSON(tx, noAsm, noScriptSig),
                    vout = TxOutputsToJSON(tx, noAsm, noSpend),
                    blockhash = Binary.ByteArrayToHexString(blockHeader.Hash),
                    blockheight = blockHeight,
                    confirmations = getLastHeightResult.Result - blockHeight + 1,
                    time = blockHeader.Timestamp,
                    blocktime = blockHeader.Timestamp,
                    isCoinBase = tx.IsCoinbase,
                    valueOut = Utils.SatoshisToBTC(tx.TotalOutputValue),
                    size = tx.GetSerializedSize()
                };
            }
        }

        private object TxInputsToJSON(Transaction tx, bool noAsm, bool noScriptSig)
        {
            var inputs = tx.Inputs;
            var jsonInputs = new List<object>();
            for(var i=0; i<inputs.Count; i++)
            {
                Input input = inputs[i];
                dynamic jsonInput = new ExpandoObject();
                if(tx.IsCoinbase)
                {
                    byte[] scriptData = input.Script.ToData(false);
                    Array.Reverse(scriptData, 0, scriptData.Length);
                    jsonInput.coinbase = Binary.ByteArrayToHexString(scriptData);
                }
                else
                {
                    SetInputNonCoinbaseFields(jsonInput, input, noAsm, noScriptSig);
                }
                jsonInput.sequence = input.Sequence;
                jsonInput.n = i;
                jsonInputs.Add(jsonInput);
            }
            return jsonInputs.ToArray();
        }

        private void SetInputNonCoinbaseFields(dynamic jsonInput, Input input, bool noAsm, bool noScriptSig)
        {
            OutputPoint previousOutput = input.PreviousOutput;
            jsonInput.txid = Binary.ByteArrayToHexString(previousOutput.Hash);
            jsonInput.vout = previousOutput.Index;
            if(!noScriptSig)
            {
                jsonInput.scriptSig = InputScriptToJSON(input.Script, noAsm);
            }
            using(DisposableApiCallResult<GetTxDataResult> getTxResult = chain_.GetTransaction(previousOutput.Hash, false))
            {
                Utils.CheckBitprimApiErrorCode(getTxResult.ErrorCode, "GetTransaction(" + Binary.ByteArrayToHexString(previousOutput.Hash) + ") failed, check errog log");
                Output output = getTxResult.Result.Tx.Outputs[(int)previousOutput.Index];
                jsonInput.addr =  output.PaymentAddress(nodeExecutor_.UseTestnetRules).Encoded;
                jsonInput.valueSat = output.Value;
                jsonInput.value = Utils.SatoshisToBTC(output.Value);
                jsonInput.doubleSpentTxID = null; //We don't handle double spent transactions
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

        private object TxOutputsToJSON(Transaction tx, bool noAsm, bool noSpend)
        {
            var outputs = tx.Outputs;
            var jsonOutputs = new List<object>();
            for(var i=0; i<outputs.Count; i++)
            {
                Output output = outputs[i];
                dynamic jsonOutput = new ExpandoObject();
                jsonOutput.value = Utils.SatoshisToBTC(output.Value);
                jsonOutput.n = i;
                jsonOutput.scriptPubKey = OutputScriptToJSON(output, noAsm);
                if(!noSpend)
                {
                    SetOutputSpendInfo(jsonOutput, tx.Hash, (UInt32)i);
                }
                jsonOutputs.Add(jsonOutput);
            }
            return jsonOutputs.ToArray();
        }

        private void SetOutputSpendInfo(dynamic jsonOutput, byte[] txHash, UInt32 index)
        {
            ApiCallResult<Point> fetchSpendResult = chain_.GetSpend(new OutputPoint(txHash, index));
            if(fetchSpendResult.ErrorCode == ErrorCode.NotFound)
            {
                jsonOutput.spentTxId = null;
                jsonOutput.spentIndex = null;
                jsonOutput.spentHeight = null;
            }
            else
            {
                Utils.CheckBitprimApiErrorCode(fetchSpendResult.ErrorCode, "GetSpend failed, check error log");
                Point spend = fetchSpendResult.Result;
                jsonOutput.spentTxId = Binary.ByteArrayToHexString(spend.Hash);
                jsonOutput.spentIndex = spend.Index;
                using(DisposableApiCallResult<GetTxDataResult> getTxResult = chain_.GetTransaction(spend.Hash, false))
                {
                    Utils.CheckBitprimApiErrorCode(getTxResult.ErrorCode, "GetTransaction(" + Binary.ByteArrayToHexString(spend.Hash) + "), check error log");
                    jsonOutput.spentHeight = getTxResult.Result.TxPosition.BlockHeight;
                }
            }
        }

        private object OutputScriptToJSON(Output output, bool noAsm)
        {
            Script script = output.Script;
            byte[] scriptData = script.ToData(false);
            Array.Reverse(scriptData, 0, scriptData.Length);
            dynamic result = new ExpandoObject();
            if(!noAsm)
            {
                result.asm = script.ToString(0);
            }
            result.hex = Binary.ByteArrayToHexString(scriptData);
            result.addresses = ScriptAddressesToJSON(output);
            result.type = script.Type;
            return result;
        }

        private object ScriptAddressesToJSON(Output output)
        {
            var jsonAddresses = new List<object>();
            jsonAddresses.Add(output.PaymentAddress(nodeExecutor_.UseTestnetRules).Encoded);
            return jsonAddresses.ToArray();
        }

    }
}