using bitprim.insight.DTOs;
using Bitprim;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Swashbuckle.AspNetCore.SwaggerGen;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using bitprim.insight.Exceptions;
using Microsoft.Extensions.Caching.Memory;

namespace bitprim.insight.Controllers
{
    /// <summary>
    /// Transaction related operations.
    /// </summary>
    [Route("[controller]")]
    public class TransactionController : Controller
    {
        private readonly Chain chain_;
        private readonly Executor nodeExecutor_;
        private readonly ILogger<TransactionController> logger_;
        private readonly IMemoryCache memoryCache_;
        private readonly NodeConfig config_;
        private static readonly TxPositionComparer txPositionComparer_ = new TxPositionComparer();

        private readonly List<long> statsGetTransactions = new List<long>();

        /// <summary>
        /// Build this controller.
        /// </summary>
        /// <param name="config"> Higher level API configuration. </param>
        /// <param name="executor"> Node executor from bitprim-cs library. </param>
        /// <param name="logger"> Abstract logger. </param>
        /// <param name="memoryCache">Memory cache</param>
        public TransactionController(IOptions<NodeConfig> config, Executor executor, ILogger<TransactionController> logger, IMemoryCache memoryCache)
        {
            config_ = config.Value;
            nodeExecutor_ = executor;
            chain_ = executor.Chain;
            logger_ = logger;
            memoryCache_ = memoryCache;
        }

        /// <summary>
        /// This method exists only to warn clients who call GET instead of POST.
        /// </summary>
        /// <param name="request"> See RawTxRequest DTO. </param>
        /// <returns> Error message advising client to use POST version instead. </returns>
        [ApiExplorerSettings(IgnoreApi=true)]
        [HttpGet("tx/send")]
        public ActionResult GetBroadcastTransaction(RawTxRequest request)
        {
            return BadRequest("tx/send method only accepts POST requests");
        }

        /// <summary>
        /// Publish a transaction to the P2P network.
        /// </summary>
        /// <param name="request"> See RawTxRequest DTO. </param>
        /// <returns> See BroadcastTransactionResponse DTO. </returns>
        [HttpPost("tx/send")]
        [SwaggerOperation("BroadcastTransaction")]
        [SwaggerResponse((int)System.Net.HttpStatusCode.OK, typeof(BroadcastTransactionResponse))]
        [SwaggerResponse((int)System.Net.HttpStatusCode.BadRequest, typeof(string))]
        public async Task<ActionResult> BroadcastTransaction([FromBody] RawTxRequest request)
        {
            Utils.CheckIfChainIsFresh(chain_, config_.AcceptStaleRequests);

            if(string.IsNullOrWhiteSpace(request?.rawtx))
            {
                return BadRequest("Invalid post body: check structure, or payload size");
            }

            Transaction tx;
            try
            {
                tx = new Transaction(Constants.TRANSACTION_VERSION_PROTOCOL, request.rawtx);
            }
            catch(Exception e) //TODO Use a BitprimException from bitprim-cs to avoid this
            {
                return BadRequest("Invalid transaction: " + e.Message);
            }

            try
            {
                var ec = await chain_.OrganizeTransactionAsync(tx);

                if (ec != ErrorCode.Success)
                {
                    return BadRequest(ec.ToString());
                }
                    
                return Json
                (
                    new BroadcastTransactionResponse
                    {
                         //TODO Check if this should be returned by organize call
                        txid = Binary.ByteArrayToHexString(tx.Hash)
                    }
                );
            }
            catch (Exception e)
            {
                return StatusCode((int) System.Net.HttpStatusCode.InternalServerError, "Error broadcasting transaction: " + e.Message);
            }
            finally
            {
                tx?.Dispose();
            }
        }

        /// <summary>
        /// Given a transaction hash, retrieve its representation as a hex string.
        /// </summary>
        /// <param name="hash"> 64-character (32 bytes) hex string which univocally identifies the transaction in the network. </param>
        /// <returns> See GetRawTransactionResponse DTO. </returns>
        [HttpGet("rawtx/{hash}")]
        [ResponseCache(CacheProfileName = Constants.Cache.LONG_CACHE_PROFILE_NAME)]
        [SwaggerOperation("GetRawTransactionByHash")]
        [SwaggerResponse((int)System.Net.HttpStatusCode.OK, typeof(GetRawTransactionResponse))]
        public async Task<ActionResult> GetRawTransactionByHash(string hash)
        {
            if(!Validations.IsValidHash(hash))
            {
                return BadRequest(hash + " is not a valid transaction hash");
            }
            Utils.CheckIfChainIsFresh(chain_, config_.AcceptStaleRequests);
            var binaryHash = Binary.HexStringToByteArray(hash);
            
            using(var getTxResult = await chain_.FetchTransactionAsync(binaryHash, false))
            {
                Utils.CheckBitprimApiErrorCode(getTxResult.ErrorCode, "FetchTransactionAsync(" + hash + ") failed, check error log");
                
                var tx = getTxResult.Result.Tx;
                return Json(new GetRawTransactionResponse
                {
                    rawtx = Binary.ByteArrayToHexString(tx.ToData(false).Reverse().ToArray())
                });
            }
        }

        /// <summary>
        /// Given a transaction hash, retrieve its representation as a hex string.
        /// </summary>
        /// <param name="hash"> 64-character (32 bytes) hex string which univocally identifies the transaction in the network. </param>
        /// <param name="requireConfirmed"> 1 = only confirmed transactions, otherwise include unconfirmed as well. </param>
        /// <returns> See TransactionSummary DTO. </returns>
        [HttpGet("tx/{hash}")]
        [ResponseCache(CacheProfileName = Constants.Cache.SHORT_CACHE_PROFILE_NAME)]
        [SwaggerOperation("GetTransactionByHash")]
        [SwaggerResponse((int)System.Net.HttpStatusCode.OK, typeof(TransactionSummary))]
        [SwaggerResponse((int)System.Net.HttpStatusCode.BadRequest, typeof(string))]
        public async Task<ActionResult> GetTransactionByHash([FromRoute] string hash, [FromQuery] int requireConfirmed)
        {
            if( !ModelState.IsValid )
            {
                return BadRequest("requireConfirmed must be an integer number");
            }
            if( !Validations.IsValidHash(hash) )
            {
                return BadRequest(hash + " is not a valid transaction hash");
            }
            Utils.CheckIfChainIsFresh(chain_, config_.AcceptStaleRequests);
            var binaryHash = Binary.HexStringToByteArray(hash);

            using(var getTxResult = await chain_.FetchTransactionAsync(binaryHash, requireConfirmed == 1))
            {
                if (getTxResult.ErrorCode == ErrorCode.NotFound)
                {
                    throw new HttpStatusCodeException(HttpStatusCode.NotFound, "Not Found");
                }
                
                Utils.CheckBitprimApiErrorCode(getTxResult.ErrorCode, "FetchTransactionAsync(" + hash + ") failed, check error log");
                bool confirmed = CheckIfTransactionIsConfirmed(getTxResult.Result.TxPosition);
                return Json(await TxToJSON
                (
                    getTxResult.Result.Tx, getTxResult.Result.TxPosition.BlockHeight, confirmed, noAsm: false, noScriptSig: false, noSpend: false)
                );
            }
        }

        /// <summary>
        /// Returns all transactions from a block, or an address (only one source at a time).
        /// </summary>
        /// <param name="block"> 64-character (32 bytes) hex string which univocally identifies a block. </param>
        /// <param name="address"> Address to get transactions from. When selecting by address, unconfirmed
        /// transactions are included.
        /// </param>
        /// <param name="pageNum"> Results page number to select; starts in zero. Page size is configurable via
        /// appsettings.json and command line. By default, page size is 10 transactions. See TransactionsByAddressPageSize key.
        /// </param>
        /// <returns> See GetTransactionsResponse DTO. </returns>
        [HttpGet("txs")]
        [ResponseCache(CacheProfileName = Constants.Cache.SHORT_CACHE_PROFILE_NAME)]
        [SwaggerOperation("GetTransactions")]
        [SwaggerResponse((int)System.Net.HttpStatusCode.OK, typeof(GetTransactionsResponse))]
        [SwaggerResponse((int)System.Net.HttpStatusCode.BadRequest, typeof(string))]
        public async Task<ActionResult> GetTransactions(string block = null, string address = null, uint pageNum = 0)
        {
            Stopwatch stopWatch = Stopwatch.StartNew();

            if(block == null && address == null)
            {
                return BadRequest("Specify block or address");
            }

            if(block != null && address != null)
            {
                return BadRequest("Specify either block or address, but not both");
            }

            if(block != null)
            {
                if( !Validations.IsValidHash(block) )
                {
                    return BadRequest(block + " is not a valid block hash");
                }
                return await GetTransactionsByBlockHash(block, pageNum);
            }
            
            if( !Validations.IsValidPaymentAddress(address) )
            {
                return BadRequest(address + " is not a valid address");
            }

            //1
            statsGetTransactions.Add(stopWatch.ElapsedMilliseconds);

            var pageSize = config_.TransactionsByAddressPageSize;
            var from = (int)(pageSize * pageNum);
            int to = (int)(from + pageSize - 1);

            var result = await DoGetTransactionsForMultipleAddresses( new[] {address}, from, to, stopWatch,false, false, false);

            //9
            statsGetTransactions.Add(stopWatch.ElapsedMilliseconds);

            var pageCount = (UInt64) Math.Ceiling((double)result.Item2/(double)pageSize);


            logger_.LogDebug("Finish process addr request (ms): " + String.Join("\t", statsGetTransactions) );

            return Json( new GetTransactionsResponse
            {
                pagesTotal = pageCount, txs = result.Item1.ToArray()
            } );
        }

        /// <summary>
        /// Returns all transactions from a set of addresses.
        /// </summary>
        /// <param name="paymentAddresses"> Comma-separated list of addresses. For BCH, cashaddr format is accepted.
        /// The maximum amount of addresses is determined by the MaxAddressesPerQuery configuration key. </param>
        /// <param name="from"> Results selection starting point; first item is 0 (zero). Default to said value. </param>
        /// <param name="to"> Results selection ending point. Default to 10.</param>
        /// <returns> See GetTransactionsForMultipleAddressesResponse DTO. </returns>
        [HttpGet("addrs/{paymentAddresses}/txs")]
        [ResponseCache(CacheProfileName = Constants.Cache.SHORT_CACHE_PROFILE_NAME)]
        [SwaggerOperation("GetTransactionsForMultipleAddresses")]
        [SwaggerResponse((int)System.Net.HttpStatusCode.OK, typeof(GetTransactionsForMultipleAddressesResponse))]
        [SwaggerResponse((int)System.Net.HttpStatusCode.BadRequest, typeof(string))]
        public async Task<ActionResult> GetTransactionsForMultipleAddresses([FromRoute] string paymentAddresses, [FromQuery] int from = 0, [FromQuery] int to = 10)
        {
            Stopwatch stopWatch = Stopwatch.StartNew();

            var validationResult = ValidateGetTransactionsFromMultipleAddressesInput(paymentAddresses, from, to);
            if( !validationResult.Item1 )
            {
                return BadRequest(validationResult.Item2);
            }
            var result = await DoGetTransactionsForMultipleAddresses(validationResult.Item3, from, to, stopWatch, false, false, false);
            return Json(new GetTransactionsForMultipleAddressesResponse
            {
                totalItems = result.Item2,
                from = from,
                to = to,
                items = result.Item1.ToArray()
            });
        }

        /// <summary>
        /// Returns all transactions from a set of adresses.
        /// </summary>
        /// <param name="request"> See GetTxsForMultipleAddressesRequest DTO. </param>
        /// <returns> See GetTransactionsForMultipleAddressesResponse DTO. </returns>
        [HttpPost("addrs/txs")]
        [ResponseCache(CacheProfileName = Constants.Cache.SHORT_CACHE_PROFILE_NAME)]
        [SwaggerOperation("GetTransactionsForMultipleAddresses_Post")]
        [SwaggerResponse((int)System.Net.HttpStatusCode.OK, typeof(GetTransactionsForMultipleAddressesResponse))]
        [SwaggerResponse((int)System.Net.HttpStatusCode.BadRequest, typeof(string))]
        public async Task<ActionResult> GetTransactionsForMultipleAddresses([FromBody] GetTxsForMultipleAddressesRequest request)
        {
            Stopwatch stopWatch = Stopwatch.StartNew();

            if(request == null)
            {
                //TODO Point user to documentation once docs include DTOs (RA-176)
                return BadRequest
                (
                    "Invalid request format. Expected JSON format: \n{\n\t\"addrs\": \"addr1,addr2,addrN\",\n \t\"from\": 0,\n\t\"to\": M,\n\t\"noAsm\": 1, \n\t\"noScriptSig\": 1, \n\t\"noSpend\": 1\n}"
                );
            }
            
            var validationResult = ValidateGetTransactionsFromMultipleAddressesInput(request.addrs, request.from, request.to);
            if( !validationResult.Item1 )
            {
                return BadRequest(validationResult.Item2);
            }

            var result = await DoGetTransactionsForMultipleAddresses(validationResult.Item3, request.from, request.to, stopWatch ,request.noAsm == 1, request.noScriptSig == 1, request.noSpend == 1);
            return Json(new GetTransactionsForMultipleAddressesResponse
            {
                totalItems = result.Item2,
                from = request.from,
                to = result.Item3,
                items = result.Item1.ToArray()
            });
        }

        private async Task SetInputNonCoinbaseFields(TransactionInputSummary jsonInput, Input input, bool noAsm, bool noScriptSig)
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

        private async Task SetOutputSpendInfo(TransactionOutputSummary jsonOutput, byte[] txHash, UInt32 index)
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

        private async Task<Tuple<List<TransactionSummary>, int, int>>
        DoGetTransactionsForMultipleAddresses(string[] addresses, int from, int to, Stopwatch stopWatch,
                                              bool noAsm = true, bool noScriptSig = true, bool noSpend = true)
        {
            var fromCache = false;
            string cacheKey = string.Join("",addresses);
            SortedSet<Tuple<Int64, string>> txPositions=null;
            if ( from > 0 )
            {
                if ( memoryCache_.TryGetValue(cacheKey, out txPositions))
                {
                    fromCache = true;
                }
            }
            //2
            statsGetTransactions.Add(stopWatch.ElapsedMilliseconds);

            if (!fromCache)
            {
                txPositions = new SortedSet<Tuple<Int64, string>>( txPositionComparer_ );
                foreach(string address in addresses)
                {
                    await GetTransactionPositionsBySingleAddress(address, txPositions, stopWatch);
                }
            }

            //6
            statsGetTransactions.Add(stopWatch.ElapsedMilliseconds);

            if (txPositions.Count > Constants.Cache.TXID_LIST_CACHE_MIN)
            {
                memoryCache_.Set(cacheKey, txPositions, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds( Constants.Cache.TXID_LIST_CACHE_EXPIRATION_SECONDS )
                    ,Size = Constants.Cache.TXID_LIST_CACHE_ITEM_SIZE
                });
            }

            //7
            statsGetTransactions.Add(stopWatch.ElapsedMilliseconds);

            var finalTo = Math.Min(to, txPositions.Count);

            //Fetch selected range and convert to JSON
            var txsDigest = new List<TransactionSummary>();
            foreach(var txPosition in txPositions.Skip(from).Take(finalTo-from)) //txPositions.ToList().GetRange(from, finalTo-from))
            {
                using(var getTxResult = await chain_.FetchTransactionAsync( Binary.HexStringToByteArray(txPosition.Item2), false))
                {
                    Utils.CheckBitprimApiErrorCode(getTxResult.ErrorCode, "FetchTransactionAsync(" + txPosition.Item2 + ") failed, check error log");
                    txsDigest.Add( await TxToJSON(getTxResult.Result.Tx, (UInt64) txPosition.Item1, txPosition.Item1 > 0, noAsm, noScriptSig, noSpend) );
                }
            }

            //8
            statsGetTransactions.Add(stopWatch.ElapsedMilliseconds);

            return new Tuple<List<TransactionSummary>, int, int>(txsDigest, txPositions.Count, finalTo);
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
                    return BadRequest("pageNum cannot exceed " + (pageCount - 1) + " (zero-indexed)");
                }
                
                var txs = new List<TransactionSummary>();
                for(UInt64 i=0; i<pageSize && pageNum * pageSize + i < fullBlock.TransactionCount; i++)
                {
                    var tx = fullBlock.GetNthTransaction(pageNum * pageSize + i);
                    txs.Add(await TxToJSON(tx, blockHeight, confirmed: true, noAsm: false, noScriptSig: false, noSpend: false));
                }
                
                return Json(new GetTransactionsResponse
                {
                    pagesTotal = pageCount,
                    txs = txs.ToArray()
                });
            }
        }

        private async Task GetTransactionPositionsBySingleAddress(string paymentAddress, SortedSet<Tuple<Int64, string>> txPositions, Stopwatch stopWatch)
        {
            Utils.CheckIfChainIsFresh(chain_, config_.AcceptStaleRequests);

            using (var address = new PaymentAddress(paymentAddress))
            {
                //Unconfirmed first
                GetUnconfirmedTransactionPositions(address, txPositions);    
                //3
                statsGetTransactions.Add(stopWatch.ElapsedMilliseconds);

                using (var getTransactionResult = await chain_.FetchConfirmedTransactionsAsync(address, UInt64.MaxValue, 0))
                {
                    Utils.CheckBitprimApiErrorCode(getTransactionResult.ErrorCode, "FetchConfirmedTransactionsAsync(" + paymentAddress + ") failed, check error log.");

                    //4
                    statsGetTransactions.Add(stopWatch.ElapsedMilliseconds);

                    HashList confirmedTxIds = getTransactionResult.Result;
                   
                    //Confirmed
                    foreach (byte[] txHash in confirmedTxIds)
                    {
                        ApiCallResult<GetTxPositionResult> getTxPosResult = await chain_.FetchTransactionPositionAsync(txHash, true);
                        string txHashStr = Binary.ByteArrayToHexString(txHash);
                        Utils.CheckBitprimApiErrorCode(getTxPosResult.ErrorCode, "FetchTransactionPositionAsync(" + txHashStr + ") failed, check error log");
                        txPositions.Add(new Tuple<Int64, string>((Int64) getTxPosResult.Result.BlockHeight , txHashStr)); 
                    }
                    
                    //5
                    statsGetTransactions.Add(stopWatch.ElapsedMilliseconds);
                }
            }
        }

        private void GetUnconfirmedTransactionPositions(PaymentAddress address, SortedSet<Tuple<Int64, string>> txPositions)
        {
            using(MempoolTransactionList unconfirmedTxIds = chain_.GetMempoolTransactions(address, nodeExecutor_.UseTestnetRules))
            {
                foreach(MempoolTransaction unconfirmedTxId in unconfirmedTxIds)
                {
                    txPositions.Add( new Tuple<Int64, string>(-1, unconfirmedTxId.Hash) );
                }
            }
        }

        private async Task<TransactionInputSummary[]> TxInputsToJSON(Transaction tx, bool noAsm, bool noScriptSig)
        {
            var inputs = tx.Inputs;
            var jsonInputs = new List<TransactionInputSummary>();
            for(uint i=0; i<inputs.Count; i++)
            {
                var input = inputs[i];
                
                dynamic jsonInput = new TransactionInputSummary();
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

        private async Task<TransactionOutputSummary[]> TxOutputsToJSON(Transaction tx, bool noAsm, bool noSpend)
        {
            var outputs = tx.Outputs;
            var jsonOutputs = new List<TransactionOutputSummary>();
            for(uint i=0; i<outputs.Count; i++)
            {
                var output = outputs[i];
                dynamic jsonOutput = new TransactionOutputSummary();
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

        private async Task<TransactionSummary> TxToJSON(Transaction tx, UInt64 blockHeight, bool confirmed,
                                                        bool noAsm, bool noScriptSig, bool noSpend)
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
            
            var txJson = new TransactionSummary();
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
                txJson.blockheight = (Int64) blockHeight;
            }
            else
            {
                txJson.blockheight = -1;
                txJson.time = (UInt32) DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            }
            if ( !tx.IsCoinbase )
            {
                txJson.fees = Utils.SatoshisToCoinUnits(inputsTotal - tx.TotalOutputValue); //TODO Solve at native layer
            }
            return txJson;
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

        private OutputScriptSummary OutputScriptToJSON(Output output, bool noAsm)
        {
            var script = output.Script;
            var scriptData = script.ToData(false);
            Array.Reverse(scriptData, 0, scriptData.Length);
            dynamic result = new OutputScriptSummary();
            if(!noAsm)
            {
                result.asm = script.ToString(0);
            }
            result.hex = Binary.ByteArrayToHexString(scriptData);
            var outputAddress = output.PaymentAddress(nodeExecutor_.UseTestnetRules);
            if(outputAddress.IsValid)
            {
                result.addresses = new string[] { outputAddress.Encoded };
            }
            result.type = GetScriptType(script.Type);
            return result;
        }

        //TODO Move this logic to node-cint and expose via a property (Transaction.Confirmed)
        private static bool CheckIfTransactionIsConfirmed(GetTxPositionResult txPosition)
        {
            switch( NodeSettings.CurrencyType )
            {
                case CurrencyType.BitcoinCash: return txPosition.Index != UInt32.MaxValue;
                default: return txPosition.Index != UInt16.MaxValue;
            }
        }

        private static InputScriptSummary InputScriptToJSON(Script inputScript, bool noAsm)
        {
            byte[] scriptData = inputScript.ToData(false);
            Array.Reverse(scriptData, 0, scriptData.Length);
            dynamic result = new InputScriptSummary();
            result.hex = Binary.ByteArrayToHexString(scriptData);
            if(!noAsm)
            {
                result.asm = inputScript.ToString(0);
            }
            return result;
        }
        
        private static string GetScriptType(string type)
        {
            if (type == "pay_key_hash")
            {
                return "pubkeyhash";
            }
            if (type == "pay_script_hash")
            {
                return "scripthash";
            }
            return type;
        }

        private Tuple<bool, string, string[]> ValidateGetTransactionsFromMultipleAddressesInput(string paymentAddresses, int from, int to)
        {
            if (string.IsNullOrWhiteSpace(paymentAddresses))
            {
                return new Tuple<bool, string, string[]>
                (
                    false,
                    "'paymentAddresses' must be not empty",
                    null
                );
            }
            
            if(from < 0)
            {
                return new Tuple<bool, string, string[]>(false, "'from' must be greater than or equal to zero", null);
            }
            if(from >= to)
            {
                return new Tuple<bool, string, string[]>(false, "'from' must be lower than 'to'", null);
            }
            var addresses = System.Web.HttpUtility.UrlDecode(paymentAddresses).Split(",").Distinct().ToArray();
            if(addresses.Length > config_.MaxAddressesPerQuery)
            {
                return new Tuple<bool, string, string[]>
                (
                    false,
                    "Max addresses per query: " + config_.MaxAddressesPerQuery + " (" + addresses.Length + " requested)",
                    null
                );
            }
            foreach(string address in addresses)
            {
                if( !Validations.IsValidPaymentAddress(address) )
                {
                    return new Tuple<bool, string, string[]>(false, address + " is not a valid address", null);
                }
            }
            return new Tuple<bool, string, string[]>(true, "", addresses);
        }
    }

    internal class TxPositionComparer : IComparer<Tuple<Int64, string>>
    {
        public int Compare(Tuple<Int64, string> lv, Tuple<Int64, string> rv)
        {
            if(lv.Item1 != rv.Item1)
            {
                //When sorting by block height, we want descending order, so we invert the comparison
                return rv.Item1.CompareTo( lv.Item1 );
            }

            //For equal block height, order by ascending txId
            return string.Compare( lv.Item2, rv.Item2, StringComparison.Ordinal );
        }
    }
}
