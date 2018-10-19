using System;
using System.Collections.Generic;
using System.Diagnostics;
using bitprim.insight.DTOs;
using Bitprim;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SharpCashAddr;

namespace bitprim.insight.Controllers
{
    /// <summary>
    /// Address related operations.
    /// </summary>
    [Route("[controller]")]
    public class AddressController : Controller
    {
        private readonly Chain chain_;
        private readonly Executor nodeExecutor_;
        private readonly NodeConfig config_;
        private readonly ILogger<AddressController> logger_;

        private struct AddressBalance
        {
            public OrderedSet<string> Transactions { get; set; }
            public UInt64 Balance { get; set; }
            public UInt64 Received { get; set; }
            public UInt64 Sent { get; set; }
            public uint TxCount { get; set; }
        }

        private readonly long[] statsGetAddressHistory = new long[8];

        /// <summary>
        /// Build this controller.
        /// </summary>
        /// <param name="config"> Higher level API configuration. </param>
        /// <param name="executor"> Node executor from bitprim-cs library. </param>
        /// <param name="logger">  Abstract logger. </param>
        public AddressController(IOptions<NodeConfig> config, Executor executor, ILogger<AddressController> logger)
        {
            nodeExecutor_ = executor;
            chain_ = nodeExecutor_.Chain;
            config_ = config.Value;
            logger_ = logger;
        }

        /// <summary>
        /// Given an address, get unconfirmed balance in coin units.
        /// </summary>
        /// <param name="paymentAddress"> The address of interest. For BCH, it can be in cashaddr format. </param>
        /// <returns> Unconfirmed balance, in coin units. </returns>
        [HttpGet("addr/{paymentAddress}/unconfirmedBalance")]
        [ResponseCache(CacheProfileName = Constants.Cache.SHORT_CACHE_PROFILE_NAME)]
        [SwaggerOperation("GetUnconfirmedBalance")]
        public async Task<ActionResult> GetUnconfirmedBalance(string paymentAddress)
        {
            Utils.CheckIfChainIsFresh(chain_, config_.AcceptStaleRequests);
            if (!Validations.IsValidPaymentAddress(paymentAddress))
            {
                return BadRequest(paymentAddress + " is not a valid address");
            }
            var result = await GetUnconfirmedSummary(paymentAddress);
            return Json(new { unconfirmedBalanceSat = result.Item2 });
        }

        /// <summary>
        /// Given an address, get current confirmed balance in coin units.
        /// </summary>
        /// <param name="paymentAddress"> The address of interest. For BCH, it can be in cashaddr format. </param>
        /// <returns> Confirmed balance, in coin units. </returns>
        [HttpGet("addr/{paymentAddress}/balance")]
        [ResponseCache(CacheProfileName = Constants.Cache.SHORT_CACHE_PROFILE_NAME)]
        [SwaggerOperation("GetAddressBalance")]
        public async Task<ActionResult> GetAddressBalance(string paymentAddress)
        {
            return await GetBalanceProperty(paymentAddress, "Balance");
        }

        /// <summary>
        /// Given an address, get current confirmed and unconfirmed balance, and optionally, a list of all
        /// transaction ids involved in the address.
        /// </summary>
        /// <param name="paymentAddress"> The address of interest. For BCH, it can be in cashaddr format. </param>
        /// <param name="noTxList"> If 0, include transaction id list; otherwise, do not include it. </param>
        /// <param name="from"> Allows selecting a subrange of transaction ids from the full list; starts in zero (0). </param>
        /// <param name="to"> Allows selecting a subrange of transactions from the full list; max value is (txCount - 1). </param>
        /// <param name="returnLegacyAddresses"> If and only if true, use legacy address format in response (BCH only). </param>
        /// <returns> Confirmed balance, unconfirmed balance and transaction id list (if requested). </returns>
        [HttpGet("addr/{paymentAddress}")]
        [ResponseCache(CacheProfileName = Constants.Cache.SHORT_CACHE_PROFILE_NAME)]
        [SwaggerOperation("GetAddressHistory")]
        [SwaggerResponse((int)System.Net.HttpStatusCode.OK, typeof(GetAddressHistoryResponse))]
        [SwaggerResponse((int)System.Net.HttpStatusCode.BadRequest, typeof(string))]
        public async Task<ActionResult> GetAddressHistory([FromRoute] string paymentAddress, [FromQuery] int noTxList = 0, [FromQuery] int from = 0,
                                                          [FromQuery] int to = 0, [FromQuery] bool returnLegacyAddresses = false)
        {
            Stopwatch stopWatch = Stopwatch.StartNew();

            if (!PaymentAddress.TryParsePaymentAddress(paymentAddress, out PaymentAddress paymentAddressObject))
            {
                return BadRequest(paymentAddress + " is not a valid address");
            }

            statsGetAddressHistory[0] = stopWatch.ElapsedMilliseconds;

            Utils.CheckIfChainIsFresh(chain_, config_.AcceptStaleRequests);

            var balance = await GetBalance(paymentAddress, noTxList == 0, stopWatch);

            statsGetAddressHistory[4] = stopWatch.ElapsedMilliseconds;

            string convertedAddress;
            try
            {
                convertedAddress = Utils.FormatAddress(paymentAddressObject, returnLegacyAddresses);
            }
            catch (CashAddrConversionException)
            {
                convertedAddress = paymentAddressObject.Encoded;
            }

            var historyJson = new GetAddressHistoryResponse
            {
                addrStr = convertedAddress,
                balance = Utils.SatoshisToCoinUnits(balance.Balance),
                balanceSat = balance.Balance,
                totalReceived = Utils.SatoshisToCoinUnits(balance.Received),
                totalReceivedSat = balance.Received,
                totalSent = Utils.SatoshisToCoinUnits(balance.Sent),
                totalSentSat = balance.Sent,
                txAppearances = balance.TxCount,
                txApperances = balance.TxCount
            };

            Tuple<ulong, Int64> unconfirmedSummary = await GetUnconfirmedSummary(paymentAddress);

            statsGetAddressHistory[5] = stopWatch.ElapsedMilliseconds;

            historyJson.unconfirmedBalance = Utils.SatoshisToCoinUnits(unconfirmedSummary.Item2);
            historyJson.unconfirmedBalanceSat = unconfirmedSummary.Item2;
            historyJson.unconfirmedTxAppearances = unconfirmedSummary.Item1;
            historyJson.unconfirmedTxApperances = unconfirmedSummary.Item1;

            if (noTxList == 0)
            {
                Tuple<string[], string> addressTxs = GetAddressTransactions(balance.Transactions, from, to);
                statsGetAddressHistory[6] = stopWatch.ElapsedMilliseconds;

                if (addressTxs.Item1 == null)
                {
                    return BadRequest(addressTxs.Item2);
                }
                historyJson.transactions = addressTxs.Item1;
            }
            else
            {
                historyJson.transactions = new string[0];
            }

            statsGetAddressHistory[7] = stopWatch.ElapsedMilliseconds;
            logger_.LogDebug("Finished process addr request (ms): " + statsGetAddressHistory[0] + "\t" + statsGetAddressHistory[1] + "\t" + statsGetAddressHistory[2] + "\t" + statsGetAddressHistory[3]
                             + "\t" + statsGetAddressHistory[4] + "\t" + statsGetAddressHistory[5] + "\t" + statsGetAddressHistory[6] + "\t" + statsGetAddressHistory[7]);
            return Json(historyJson);
        }

        /// <summary>
        /// Given an address, get total received amount in coin units.
        /// </summary>
        /// <param name="paymentAddress"> The address of interest. For BCH, it can be in cashaddr format. </param>
        /// <returns> Total received amount, in coin units. </returns>
        [HttpGet("addr/{paymentAddress}/totalReceived")]
        [ResponseCache(CacheProfileName = Constants.Cache.SHORT_CACHE_PROFILE_NAME)]
        [SwaggerOperation("GetTotalReceived")]
        public async Task<ActionResult> GetTotalReceived(string paymentAddress)
        {
            return await GetBalanceProperty(paymentAddress, "Received");
        }

        /// <summary>
        /// Given an address, get total sent amount in coin units.
        /// </summary>
        /// <param name="paymentAddress"> The address of interest. For BCH, it can be in cashaddr format. </param>
        /// <returns> Total sent amount, in coin units. </returns>
        [HttpGet("addr/{paymentAddress}/totalSent")]
        [ResponseCache(CacheProfileName = Constants.Cache.SHORT_CACHE_PROFILE_NAME)]
        [SwaggerOperation("GetTotalSent")]
        public async Task<ActionResult> GetTotalSent(string paymentAddress)
        {
            return await GetBalanceProperty(paymentAddress, "Sent");
        }

        /// <summary>
        /// Given a list of addresses, get their combined unspent outputs.
        /// </summary>
        /// <param name="paymentAddresses"> Comma separated list of addresses. For BCH, cashaddr format is accepted. </param>
        /// <param name="returnLegacyAddresses"> If and only if true, return addresses in legacy format. By default, use cashaddr. </param>
        /// <returns> List of all utxos for address1, followed by all utxos for address2, and so on. </returns>
        [HttpGet("addrs/{paymentAddresses}/utxo")]
        [ResponseCache(CacheProfileName = Constants.Cache.SHORT_CACHE_PROFILE_NAME)]
        [SwaggerOperation("GetUtxoForMultipleAddresses")]
        [SwaggerResponse((int)System.Net.HttpStatusCode.OK, typeof(Utxo[]))]
        public async Task<ActionResult> GetUtxoForMultipleAddresses([FromRoute] string paymentAddresses, [FromQuery] bool returnLegacyAddresses = false)
        {
            var utxo = new List<Utxo>();
            var addresses = paymentAddresses.Split(",");
            if (addresses.Length > config_.MaxAddressesPerQuery)
            {
                return BadRequest("Max addresses per query: " + config_.MaxAddressesPerQuery + " (" + addresses.Length + " requested)");
            }
            foreach (var address in addresses)
            {
                if (!Validations.IsValidPaymentAddress(address))
                {
                    return BadRequest(address + " is not a valid address");
                }
            }
            foreach (var address in addresses)
            {
                utxo.AddRange(await GetUtxo(address, returnLegacyAddresses));
            }
            return Json(utxo.ToArray());
        }

        /// <summary>
        /// Given a list of addresses, get their combined unspent outputs.
        /// </summary>
        /// <param name="requestParams"> In params.addrs, a comma separated list of addresses. For BCH, cashaddr format is accepted. </param>
        /// <returns> List of all utxos for address1, followed by all utxos for address2, and so on. </returns>
        [HttpPost("addrs/utxo")]
        [ResponseCache(CacheProfileName = Constants.Cache.SHORT_CACHE_PROFILE_NAME)]
        [SwaggerOperation("GetUtxoForMultipleAddressesPost")]
        [SwaggerResponse((int)System.Net.HttpStatusCode.OK, typeof(Utxo[]))]
        public async Task<ActionResult> GetUtxoForMultipleAddressesPost([FromBody]GetUtxosForMultipleAddressesRequest requestParams)
        {
            if (requestParams == null || string.IsNullOrWhiteSpace(requestParams.addrs))
            {
                return BadRequest("Invalid POST body; incorrect format, or payload too large.");
            }
            var addresses = requestParams.addrs.Split(",");
            if (addresses.Length > config_.MaxAddressesPerQuery)
            {
                return BadRequest("Max addresses per query: " + config_.MaxAddressesPerQuery + " (" + addresses.Length + " requested)");
            }
            foreach (var address in addresses)
            {
                if (!Validations.IsValidPaymentAddress(address))
                {
                    return BadRequest(address + " is not a valid address");
                }
            }
            return await GetUtxoForMultipleAddresses(requestParams.addrs, requestParams.legacy_addr);
        }

        /// <summary>
        /// Given an address, get all of its currently unspent outputs.
        /// </summary>
        /// <param name="paymentAddress"> The address of interest. For BCH, cashaddr format is accepted. </param>
        /// <param name="legacyAddresFormat"> If and only if true, use legacy address format in returned object. By
        /// default, cash addr is used. </param>
        /// <returns> A list of all utxos for the given address. </returns>
        [HttpGet("addr/{paymentAddress}/utxo")]
        [ResponseCache(CacheProfileName = Constants.Cache.SHORT_CACHE_PROFILE_NAME)]
        [SwaggerOperation("GetUtxoForSingleAddress")]
        [SwaggerResponse((int)System.Net.HttpStatusCode.OK, typeof(Utxo[]))]
        public async Task<ActionResult> GetUtxoForSingleAddress(string paymentAddress, [FromQuery] bool legacyAddresFormat = false)
        {
            if (!Validations.IsValidPaymentAddress(paymentAddress))
            {
                return BadRequest(paymentAddress + " is not a valid address");
            }
            var utxo = await GetUtxo(paymentAddress, legacyAddresFormat);
            return Json(utxo.ToArray());
        }

        private async Task<ActionResult> GetBalanceProperty(string paymentAddress, string propertyName)
        {
            Utils.CheckIfChainIsFresh(chain_, config_.AcceptStaleRequests);
            if (!Validations.IsValidPaymentAddress(paymentAddress))
            {
                return BadRequest("Invalid address: " + paymentAddress);
            }
            var balance = await GetBalance(paymentAddress, false);
            return Json(balance.GetType().GetProperty(propertyName).GetValue(balance, null));
        }

        private async Task<AddressBalance> GetBalance(string paymentAddress, bool includeTransactionIds, Stopwatch stopWatch = null)
        {
            statsGetAddressHistory[1] = stopWatch?.ElapsedMilliseconds ?? -1;

            using (var address = new PaymentAddress(paymentAddress))
            using (var getAddressHistoryResult = await chain_.FetchHistoryAsync(address, UInt64.MaxValue, 0))
            {
                statsGetAddressHistory[2] = stopWatch?.ElapsedMilliseconds ?? -1;

                Utils.CheckBitprimApiErrorCode(getAddressHistoryResult.ErrorCode, "FetchHistoryAsync(" + paymentAddress + ") failed, check error log.");

                var history = getAddressHistoryResult.Result;

                UInt64 received = 0;
                UInt64 addressBalance = 0;
                var txs = new OrderedSet<string>();
                
                foreach (IHistoryCompact compact in history)
                {
                    if (compact.PointKind == PointKind.Output)
                    {
                        received += compact.ValueOrChecksum;

                        using (var outPoint = new OutputPoint(compact.Point.Hash, compact.Point.Index))
                        {
                            var getSpendResult = await chain_.FetchSpendAsync(outPoint);
                            if (getSpendResult.ErrorCode == ErrorCode.NotFound)
                            {
                                addressBalance += compact.ValueOrChecksum;
                            }
                        }
                    }
                    txs.Add(Binary.ByteArrayToHexString(compact.Point.Hash));
                }

                

                statsGetAddressHistory[3] = stopWatch?.ElapsedMilliseconds ?? -1;

                UInt64 totalSent = received - addressBalance;
                return new AddressBalance
                {
                    Balance = addressBalance,
                    Received = received,
                    Sent = totalSent,
                    Transactions = includeTransactionIds ? txs : null,
                    TxCount = (uint)txs.Count
                };
            }
        }

        private async Task<List<Utxo>> GetUtxo(string paymentAddress, bool returnLegacyAddresses)
        {
            Utils.CheckIfChainIsFresh(chain_, config_.AcceptStaleRequests);

            using (var address = new PaymentAddress(paymentAddress))
            using (var getAddressHistoryResult = await chain_.FetchHistoryAsync(address, UInt64.MaxValue, 0))
            {
                Utils.CheckBitprimApiErrorCode(getAddressHistoryResult.ErrorCode, "FetchHistoryAsync(" + paymentAddress + ") failed, check error log.");

                var history = getAddressHistoryResult.Result;

                var utxo = new List<Utxo>();

                var getLastHeightResult = await chain_.FetchLastHeightAsync();
                Utils.CheckBitprimApiErrorCode(getLastHeightResult.ErrorCode, "FetchLastHeightAsync failed, check error log");

                var topHeight = getLastHeightResult.Result;

                foreach (IHistoryCompact compact in history)
                {
                    if (compact.PointKind == PointKind.Output)
                    {
                        using (var outPoint = new OutputPoint(compact.Point.Hash, compact.Point.Index))
                        {
                            var getSpendResult = await chain_.FetchSpendAsync(outPoint);

                            if (getSpendResult.ErrorCode == ErrorCode.NotFound) //Unspent = it's an utxo
                            {
                                //Get the tx to get the script
                                using (var getTxResult = await chain_.FetchTransactionAsync(compact.Point.Hash, true))
                                {
                                    Utils.CheckBitprimApiErrorCode(getTxResult.ErrorCode, "FetchTransactionAsync (" + Binary.ByteArrayToHexString(outPoint.Hash)  + ") failed, check error log");
                                    utxo.Add(new Utxo(address, compact.Point, getTxResult.ErrorCode, getTxResult.Result.Tx, compact, topHeight, returnLegacyAddresses));
                                }
                            }
                        }
                    }
                }
                utxo.AddRange(GetUnconfirmedUtxo(address));
                return utxo;
            }
        }

        private async Task<Tuple<ulong, Int64>> GetUnconfirmedSummary(string address)
        {
            using (var paymentAddress = new PaymentAddress(address))
            using (INativeList<IMempoolTransaction> unconfirmedTxs = chain_.GetMempoolTransactions(paymentAddress, nodeExecutor_.UseTestnetRules))
            {
                Int64 unconfirmedBalance = 0;
                foreach (IMempoolTransaction unconfirmedTx in unconfirmedTxs)
                {
                    using (var getTxResult = await chain_.FetchTransactionAsync(Binary.HexStringToByteArray(unconfirmedTx.Hash), requireConfirmed: false))
                    {
                        Utils.CheckBitprimApiErrorCode(getTxResult.ErrorCode, "FetchTransactionAsync(" + unconfirmedTx.Hash + ") failed, check error log");
                        ITransaction tx = getTxResult.Result.Tx;
                        unconfirmedBalance += await Utils.CalculateBalanceDelta(tx, address, chain_, nodeExecutor_.UseTestnetRules);
                    }
                }
                return new Tuple<ulong, Int64>(unconfirmedTxs.Count, unconfirmedBalance);
            }
        }

        private List<Utxo> GetUnconfirmedUtxo(PaymentAddress address)
        {
            var unconfirmedUtxo = new List<Utxo>();
            using (INativeList<IMempoolTransaction> unconfirmedTxs = chain_.GetMempoolTransactions(address, nodeExecutor_.UseTestnetRules))
            {
                foreach (IMempoolTransaction unconfirmedTx in unconfirmedTxs)
                {
                    var satoshis = Int64.Parse(unconfirmedTx.Satoshis);

                    unconfirmedUtxo.Add(new Utxo
                    {
                        address = address.Encoded,
                        txid = unconfirmedTx.Hash,
                        vout = unconfirmedTx.Index,
                        //scriptPubKey = getTxEc == ErrorCode.Success ? GetOutputScript(tx.Outputs[outputPoint.Index]) : null,
                        amount = Utils.SatoshisToCoinUnits(satoshis),
                        satoshis = satoshis,
                        height = -1,
                        confirmations = 0
                    });
                }
            }
            return unconfirmedUtxo;
        }

        private static Tuple<string[], string> GetAddressTransactions(OrderedSet<string> transactionIds, int from, int to)
        {
            if (transactionIds.Count == 0)
            {
                return new Tuple<string[], string>(new string[0], "");
            }

            if (to == 0)
            {
                to = Math.Min(transactionIds.Count, from + Constants.MAX_TX_COUNT_BY_ADDRESS);
            }

            var validationResult = ValidateParameters(from, to, transactionIds.Count);
            if (!validationResult.Item1)
            {
                return new Tuple<string[], string>(null, validationResult.Item2);
            }

            return new Tuple<string[], string>(transactionIds.GetRange(from, to - from).ToArray(), "");
        }

        private static Tuple<bool, string> ValidateParameters(int from, int to, int total)
        {
            if (from < 0)
            {
                return new Tuple<bool, string>(false, "'from' must be greater than or equal to zero");
            }

            if (from >= to)
            {
                return new Tuple<bool, string>(false, "'from' must be lower than 'to'");
            }

            if (to - from > Constants.MAX_TX_COUNT_BY_ADDRESS)
            {
                return new Tuple<bool, string>(false, "The items count returned must be fewer than " + Constants.MAX_TX_COUNT_BY_ADDRESS);
            }

            if (total - from < to - from)
            {
                return new Tuple<bool, string>(false, "The range requested is outside the collection bounds");
            }

            return new Tuple<bool, string>(true, "");
        }
    }
}