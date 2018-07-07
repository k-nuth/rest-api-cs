using System;
using System.Collections.Generic;
using bitprim.insight.DTOs;
using Bitprim;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Dynamic;
using System.Threading.Tasks;

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

        private struct AddressBalance
        {
            public OrderedSet<string> Transactions { get; set;}
            public UInt64 Balance { get; set;}
            public UInt64 Received { get; set; }
            public UInt64 Sent { get; set; }
        }

        public AddressController(IOptions<NodeConfig> config, Executor executor)
        {
            nodeExecutor_ = executor;
            chain_ = nodeExecutor_.Chain;
            config_ = config.Value;
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
        /// <param name="noTxList"> If 1, include transaction id list; otherwise, do not include it. </param>
        /// <param name="from"> Allows selecting a subrange of transaction ids from the full list; starts in zero (0). </param>
        /// <param name="to"> Allows selecting a subrange of transactions from the full list; max value is (txCount - 1). </param>
        /// <returns> Confirmed balance, unconfirmed balance and transaction id list (if requested). </returns>
        [HttpGet("addr/{paymentAddress}")]
        [ResponseCache(CacheProfileName = Constants.Cache.SHORT_CACHE_PROFILE_NAME)]
        [SwaggerOperation("GetAddressHistory")]
        [SwaggerResponse((int)System.Net.HttpStatusCode.OK, typeof(GetAddressHistoryResponse))]
        public async Task<ActionResult> GetAddressHistory(string paymentAddress, int noTxList = 0, int? from = null, int? to = null)
        {
            if(!Validations.IsValidPaymentAddress(paymentAddress))
            {
                return StatusCode((int)System.Net.HttpStatusCode.BadRequest, paymentAddress + " is not a valid Base58 address");
            }

            Utils.CheckIfChainIsFresh(chain_, config_.AcceptStaleRequests);
            var balance = await GetBalance(paymentAddress);
            
            var historyJson = new GetAddressHistoryResponse();
            historyJson.addrStr = paymentAddress;
            historyJson.balance = Utils.SatoshisToCoinUnits(balance.Balance);
            historyJson.balanceSat = balance.Balance;
            historyJson.totalReceived = Utils.SatoshisToCoinUnits(balance.Received);
            historyJson.totalReceivedSat = balance.Received;
            historyJson.totalSent = Utils.SatoshisToCoinUnits(balance.Sent);
            historyJson.totalSentSat = balance.Sent;
            historyJson.txApperances = balance.Transactions.Count;
            Tuple<uint, Int64> unconfirmedSummary = await GetUnconfirmedSummary(paymentAddress);
            historyJson.unconfirmedBalance = Utils.SatoshisToCoinUnits(unconfirmedSummary.Item2);
            historyJson.unconfirmedBalanceSat = unconfirmedSummary.Item2;
            historyJson.unconfirmedTxAppearances = unconfirmedSummary.Item1;
            if( noTxList == 0 )
            {
                Tuple<string[], string> addressTxs = GetAddressTransactions(balance.Transactions, from, to);
                if(addressTxs.Item1 == null)
                {
                    return StatusCode((int)System.Net.HttpStatusCode.BadRequest, addressTxs.Item2);
                }
                historyJson.transactions = addressTxs.Item1;
            }
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
            return Json(0); // TODO Implement (see GetAddressHistory)
        }

        /// <summary>
        /// Given a list of addresses, get their combined unspent outputs.
        /// </summary>
        /// <param name="paymentAddresses"> Comma separated list of addresses. For BCH, cashaddr format is accepted. </param>
        /// <returns> List of all utxos for address1, followed by all utxos for address2, and so on. </returns>
        [HttpGet("addrs/{paymentAddresses}/utxo")]
        [ResponseCache(CacheProfileName = Constants.Cache.SHORT_CACHE_PROFILE_NAME)]
        [SwaggerOperation("GetUtxoForMultipleAddresses")]
        [SwaggerResponse((int)System.Net.HttpStatusCode.OK, typeof(Utxo[]))]
        public async Task<ActionResult> GetUtxoForMultipleAddresses(string paymentAddresses)
        {
            var utxo = new List<Utxo>();
            foreach(var address in paymentAddresses.Split(","))
            {
                utxo.AddRange(await GetUtxo(address));
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
            return await GetUtxoForMultipleAddresses(requestParams.addrs);
        }

        /// <summary>
        /// Given an address, get all of its currently unspent outputs.
        /// </summary>
        /// <param name="paymentAddress"> The address of interest. For BCH, cashaddr format is accepted. </param>
        /// <returns> A list of all utxos for the given address. </returns>
        [HttpGet("addr/{paymentAddress}/utxo")]
        [ResponseCache(CacheProfileName = Constants.Cache.SHORT_CACHE_PROFILE_NAME)]
        [SwaggerOperation("GetUtxoForSingleAddress")]
        [SwaggerResponse((int)System.Net.HttpStatusCode.OK, typeof(Utxo[]))]
        public async Task<ActionResult> GetUtxoForSingleAddress(string paymentAddress)
        {
            var utxo = await GetUtxo(paymentAddress);
            return Json(utxo.ToArray());
        }

        private async Task<ActionResult> GetBalanceProperty(string paymentAddress, string propertyName)
        {
            Utils.CheckIfChainIsFresh(chain_, config_.AcceptStaleRequests);
            var balance = await GetBalance(paymentAddress);
            return Json(balance.GetType().GetProperty(propertyName).GetValue(balance, null));
        }

        private async Task<AddressBalance> GetBalance(string paymentAddress)
        {
            using (var address = new PaymentAddress(paymentAddress))
            using (var getAddressHistoryResult = await chain_.FetchHistoryAsync(address, UInt64.MaxValue, 0))
            {
                Utils.CheckBitprimApiErrorCode(getAddressHistoryResult.ErrorCode, "FetchHistoryAsync(" + paymentAddress + ") failed, check error log.");
                
                var history = getAddressHistoryResult.Result;
                
                UInt64 received = 0;
                UInt64 addressBalance = 0;
                var txs = new OrderedSet<string>();

                foreach(HistoryCompact compact in history)
                {
                    if(compact.PointKind == PointKind.Output)
                    {
                        received += compact.ValueOrChecksum;

                        using (var outPoint = new OutputPoint(compact.Point.Hash, compact.Point.Index))
                        {
                            var getSpendResult = await chain_.FetchSpendAsync(outPoint);
                            if(getSpendResult.ErrorCode == ErrorCode.NotFound)
                            {
                                addressBalance += compact.ValueOrChecksum;
                            }
                        }
                    }
                    txs.Add(Binary.ByteArrayToHexString(compact.Point.Hash));
                }

                UInt64 totalSent = received - addressBalance;
                return new AddressBalance{ Balance = addressBalance, Received = received, Sent = totalSent, Transactions = txs };
            }
        }

        private async Task<List<Utxo>> GetUtxo(string paymentAddress)
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

                foreach(HistoryCompact compact in history)
                {
                    if(compact.PointKind == PointKind.Output)
                    {
                        using (var outPoint = new OutputPoint(compact.Point.Hash, compact.Point.Index))
                        {
                            var getSpendResult = await chain_.FetchSpendAsync(outPoint);
                            
                            if(getSpendResult.ErrorCode == ErrorCode.NotFound) //Unspent = it's an utxo
                            {
                                //Get the tx to get the script
                                using(var getTxResult = await chain_.FetchTransactionAsync(compact.Point.Hash, true))
                                {
                                    Utils.CheckBitprimApiErrorCode(getTxResult.ErrorCode, "FetchTransactionAsync (" + Binary.ByteArrayToHexString(outPoint.Hash)  + ") failed, check error log");
                                    utxo.Add(new Utxo(address, compact.Point, getTxResult.ErrorCode, getTxResult.Result.Tx, compact, topHeight));
                                }
                            }
                        }                        
                    }
                }
                utxo.AddRange(GetUnconfirmedUtxo(address));
                return utxo;
            }
        }

        private async Task<Tuple<uint, Int64>> GetUnconfirmedSummary(string address)
        {
            using(var paymentAddress = new PaymentAddress(address))
            using(MempoolTransactionList unconfirmedTxs = chain_.GetMempoolTransactions(paymentAddress, nodeExecutor_.UseTestnetRules))
            {
                Int64 unconfirmedBalance = 0;
                foreach(MempoolTransaction unconfirmedTx in unconfirmedTxs)
                {
                    using(var getTxResult = await chain_.FetchTransactionAsync(Binary.HexStringToByteArray(unconfirmedTx.Hash), requireConfirmed: false))
                    {
                        Utils.CheckBitprimApiErrorCode(getTxResult.ErrorCode, "FetchTransactionAsync(" + unconfirmedTx.Hash + ") failed, check error log");
                        Transaction tx = getTxResult.Result.Tx;
                        unconfirmedBalance += await Utils.CalculateBalanceDelta(tx, address, chain_, nodeExecutor_.UseTestnetRules);
                    }
                }
                return new Tuple<uint, Int64>(unconfirmedTxs.Count, unconfirmedBalance);
            }
        }

        private List<Utxo> GetUnconfirmedUtxo(PaymentAddress address)
        {
            var unconfirmedUtxo = new List<Utxo>();
            using(MempoolTransactionList unconfirmedTxs = chain_.GetMempoolTransactions(address, nodeExecutor_.UseTestnetRules))
            {
                foreach(MempoolTransaction unconfirmedTx in unconfirmedTxs)
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

        private static Tuple<string[], string> GetAddressTransactions(OrderedSet<string> transactionIds, int? from = null, int? to = null)
        {
            if (from == null && to == null)
            {
                from = 0;
                to = transactionIds.Count;
            }
            else
            {
                from = Math.Max(from ?? 0, 0); 
                to = Math.Min(to ?? transactionIds.Count, transactionIds.Count);
            
                var validationResult = ValidateParameters(from.Value, to.Value);
                if( ! validationResult.Item1 )
                {
                    return new Tuple<string[], string>(null, validationResult.Item2);
                }
            }
            return new Tuple<string[], string>(transactionIds.GetRange(from.Value, to.Value - from.Value).ToArray(), "");
        }

        private static Tuple<bool, string> ValidateParameters(int from, int to)
        {
            if(from >= to)
            {
                return new Tuple<bool, string>(false, "'from' must be lower than 'to'");
            }

            return new Tuple<bool, string>(true, "");
        }
    }
}