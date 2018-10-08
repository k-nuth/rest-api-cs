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
using System.Numerics;

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
            public UInt64 TxCount { get; set; }
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
            if( !PaymentAddress.TryParse(paymentAddress, out PaymentAddress paymentAddressObject) )
            {
                return BadRequest(paymentAddress + " is not a valid address");
            }
            var result = await GetUnconfirmedSummary(paymentAddressObject);
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

            if (!PaymentAddress.TryParse(paymentAddress, out PaymentAddress paymentAddressObject))
            {
                return BadRequest(paymentAddress + " is not a valid address");
            }

            statsGetAddressHistory[0] = stopWatch.ElapsedMilliseconds;

            Utils.CheckIfChainIsFresh(chain_, config_.AcceptStaleRequests);

            var balance = await GetBalance(paymentAddress, noTxList == 0, stopWatch);

            statsGetAddressHistory[4] = stopWatch.ElapsedMilliseconds;

            var historyJson = new GetAddressHistoryResponse
            {
                addrStr = Utils.FormatAddress(paymentAddressObject, returnLegacyAddresses),
                balance = Utils.SatoshisToCoinUnits(balance.Balance),
                balanceSat = balance.Balance,
                totalReceived = Utils.SatoshisToCoinUnits(balance.Received),
                totalReceivedSat = balance.Received,
                totalSent = Utils.SatoshisToCoinUnits(balance.Sent),
                totalSentSat = balance.Sent,
                txAppearances = balance.TxCount,
                txApperances = balance.TxCount
            };

            Tuple<UInt64, Int64> unconfirmedSummary = await GetUnconfirmedSummary(paymentAddressObject);

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
        [SwaggerResponse((int)System.Net.HttpStatusCode.OK, typeof(DTOs.Utxo[]))]
        public async Task<ActionResult> GetUtxoForMultipleAddresses([FromRoute] string paymentAddresses, [FromQuery] bool returnLegacyAddresses = false)
        {
            var utxo = new List<DTOs.Utxo>();
            var addresses = paymentAddresses.Split(",");
            if (addresses.Length > config_.MaxAddressesPerQuery)
            {
                return BadRequest("Max addresses per query: " + config_.MaxAddressesPerQuery + " (" + addresses.Length + " requested)");
            }
            var addressesList = new List<PaymentAddress>();
            foreach (var address in addresses)
            {
                if (!PaymentAddress.TryParse(address, out PaymentAddress paymentAddressObject))
                {
                    return BadRequest(address + " is not a valid address");
                }
                addressesList.Add(paymentAddressObject);
            }

            var getLastHeightResult = await chain_.FetchLastHeightAsync();
            Utils.CheckBitprimApiErrorCode(getLastHeightResult.ErrorCode, "FetchLastHeightAsync() failed, check error log");
            var topHeight = getLastHeightResult.Result;

            foreach (var address in addressesList)
            {
                utxo.AddRange(GetUtxo(address, topHeight, returnLegacyAddresses));
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
        [SwaggerResponse((int)System.Net.HttpStatusCode.OK, typeof(DTOs.Utxo[]))]
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
        [SwaggerResponse((int)System.Net.HttpStatusCode.OK, typeof(DTOs.Utxo[]))]
        public async Task<ActionResult> GetUtxoForSingleAddress(string paymentAddress, [FromQuery] bool legacyAddresFormat = false)
        {
            if (!PaymentAddress.TryParse(paymentAddress, out PaymentAddress paymentAddressObject))
            {
                return BadRequest(paymentAddress + " is not a valid address");
            }

            var getLastHeightResult = await chain_.FetchLastHeightAsync();
            Utils.CheckBitprimApiErrorCode(getLastHeightResult.ErrorCode, "FetchLastHeightAsync() failed, check error log");
            var topHeight = getLastHeightResult.Result;

            var utxo = GetUtxo(paymentAddressObject, topHeight, legacyAddresFormat);
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
                var txCount = history.Count;

                if (includeTransactionIds)
                {
                    foreach (HistoryCompact compact in history)
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
                }
                else
                {
                    foreach (HistoryCompact compact in history)
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

                    }
                }

                statsGetAddressHistory[3] = stopWatch?.ElapsedMilliseconds ?? -1;

                UInt64 totalSent = received - addressBalance;
                return new AddressBalance
                {
                    Balance = addressBalance,
                    Received = received,
                    Sent = totalSent,
                    Transactions = txs,
                    TxCount = txCount
                };
            }
        }

        private List<DTOs.Utxo> GetUtxo(PaymentAddress paymentAddress, UInt64 topHeight, bool returnLegacyAddresses)
        {
            Utils.CheckIfChainIsFresh(chain_, config_.AcceptStaleRequests);

            INativeList<IUtxo> utxos = chain_.GetUtxos(paymentAddress, nodeExecutor_.UseTestnetRules);
            var utxosDto = new List<DTOs.Utxo>();
            foreach(IUtxo utxo in utxos)
            {
                var blockHeight = utxo.BlockHeight == UInt64.MaxValue? new BigInteger(-1) : new BigInteger(utxo.BlockHeight);
                byte[] scriptData = utxo.Script.ToData(false);
                Array.Reverse(scriptData, 0, scriptData.Length);
                utxosDto.Add(new DTOs.Utxo 
                {
                    address = Utils.FormatAddress(paymentAddress, returnLegacyAddresses),
                    txid = Binary.ByteArrayToHexString(utxo.TxHash),
                    vout = utxo.Index,
                    scriptPubKey = Binary.ByteArrayToHexString(scriptData),
                    amount = Utils.SatoshisToCoinUnits(utxo.Amount),
                    satoshis = (long) utxo.Amount,
                    height = blockHeight,
                    confirmations = topHeight - ((UInt64)blockHeight) + 1
                });
            }
            return utxosDto;
        }

        private async Task<Tuple<UInt64, Int64>> GetUnconfirmedSummary(PaymentAddress paymentAddress)
        {
            using (var paymentAddresses = new PaymentAddressList())
            {
                paymentAddresses.Add(paymentAddress);
                using (INativeList<ITransaction> unconfirmedTxs = chain_.GetMempoolTransactions(paymentAddresses, nodeExecutor_.UseTestnetRules))
                {
                    Int64 unconfirmedBalance = 0;
                    foreach (ITransaction unconfirmedTx in unconfirmedTxs)
                    {
                        using (var getTxResult = await chain_.FetchTransactionAsync(unconfirmedTx.Hash, requireConfirmed: false))
                        {
                            Utils.CheckBitprimApiErrorCode(getTxResult.ErrorCode, "FetchTransactionAsync(" + Binary.ByteArrayToHexString(unconfirmedTx.Hash) + ") failed, check error log");
                            ITransaction tx = getTxResult.Result.Tx;
                            unconfirmedBalance += await Utils.CalculateBalanceDelta(tx, paymentAddress, chain_, nodeExecutor_.UseTestnetRules);
                        }
                    }
                    return new Tuple<UInt64, Int64>(unconfirmedTxs.Count, unconfirmedBalance);
                }
            }
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