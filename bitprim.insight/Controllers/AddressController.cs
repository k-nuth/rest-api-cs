using System;
using System.Collections.Generic;
using bitprim.insight.DTOs;
using Bitprim;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Dynamic;
using System.Threading.Tasks;

namespace bitprim.insight.Controllers
{
    [Route("[controller]")]
    public class AddressController : Controller
    {
        private Chain chain_;
        private readonly NodeConfig config_;

        private struct AddressBalance
        {
            public List<string> Transactions { get; set;}
            public UInt64 Balance { get; set;}
            public UInt64 Received { get; set; }
            public UInt64 Sent { get; set; }
        }

        public AddressController(IOptions<NodeConfig> config, Chain chain)
        {
            chain_ = chain;
            config_ = config.Value;
        }

        // GET: addr/{paymentAddress}
        [ResponseCache(CacheProfileName = Constants.SHORT_CACHE_PROFILE_NAME)]
        [HttpGet("addr/{paymentAddress}")]
        public async Task<ActionResult> GetAddressHistory(string paymentAddress, bool noTxList = false, int from = 0, int? to = null)
        {
            if(!Validations.IsValidPaymentAddress(paymentAddress))
            {
                return StatusCode((int)System.Net.HttpStatusCode.BadRequest, paymentAddress + " is not a valid Base58 address");
            }

            Utils.CheckIfChainIsFresh(chain_, config_.AcceptStaleRequests);
            var balance = await GetBalance(paymentAddress);
            
            dynamic historyJson = new ExpandoObject();
            historyJson.addrStr = paymentAddress;
            historyJson.balance = Utils.SatoshisToCoinUnits(balance.Balance);
            historyJson.balanceSat = balance.Balance;
            historyJson.totalReceived = Utils.SatoshisToCoinUnits(balance.Received);
            historyJson.totalReceivedSat = balance.Received;
            historyJson.totalSent = balance.Sent;
            historyJson.totalSentSat = Utils.SatoshisToCoinUnits(balance.Sent);
            historyJson.unconfirmedBalance = 0; //We don't handle unconfirmed txs
            historyJson.unconfirmedBalanceSat = 0; //We don't handle unconfirmed txs
            historyJson.unconfirmedTxApperances = 0; //We don't handle unconfirmed txs
            historyJson.txApperances = balance.Transactions.Count;
            
            if( ! noTxList )
            {
                if(to == null || to.Value >= balance.Transactions.Count )
                {
                    to = balance.Transactions.Count - 1;
                }
                
                var validationResult = ValidateParameters(from, to.Value);
                if( ! validationResult.Item1 )
                {
                    return StatusCode((int)System.Net.HttpStatusCode.BadRequest, validationResult.Item2);
                }

                historyJson.transactions = balance.Transactions.GetRange(from, to.Value - from + 1).ToArray();
            }
            return Json(historyJson);
        }

        // GET: addr/{paymentAddress}/balance
        [HttpGet("addr/{paymentAddress}/balance")]
        public async Task<ActionResult> GetAddressBalance(string paymentAddress)
        {
            return await GetBalanceProperty(paymentAddress, "Balance");
        }

        // GET: addr/{paymentAddress}/totalReceived
        [HttpGet("addr/{paymentAddress}/totalReceived")]
        public async Task<ActionResult> GetTotalReceived(string paymentAddress)
        {
            return await GetBalanceProperty(paymentAddress, "Received");
        }

        // GET: addr/{paymentAddress}/totalSent
        [HttpGet("addr/{paymentAddress}/totalSent")]
        public async Task<ActionResult> GetTotalSent(string paymentAddress)
        {
            return await GetBalanceProperty(paymentAddress, "Sent");
        }

        // GET: addr/{paymentAddress}/unconfirmedBalance
        [HttpGet("addr/{paymentAddress}/unconfirmedBalance")]
        public ActionResult GetUnconfirmedBalance(string paymentAddress)
        {
            Utils.CheckIfChainIsFresh(chain_, config_.AcceptStaleRequests);
            return Json(0); //We don't handle unconfirmed transactions
        }

        // GET: addr/{paymentAddress}/utxo
        [ResponseCache(CacheProfileName = Constants.SHORT_CACHE_PROFILE_NAME)]
        [HttpGet("addr/{paymentAddress}/utxo")]
        public async Task<ActionResult> GetUtxoForSingleAddress(string paymentAddress)
        {
            var utxo = await GetUtxo(paymentAddress);
            return Json(utxo.ToArray());
        }

        [ResponseCache(CacheProfileName = Constants.SHORT_CACHE_PROFILE_NAME)]
        [HttpGet("addrs/{paymentAddresses}/utxo")]
        public async Task<ActionResult> GetUtxoForMultipleAddresses(string paymentAddresses)
        {
            var utxo = new List<object>();
            foreach(var address in paymentAddresses.Split(","))
            {
                utxo.AddRange(await GetUtxo(address));
            }
            return Json(utxo.ToArray());   
        }

        [HttpPost("addrs/utxo")]
        public async Task<ActionResult> GetUtxoForMultipleAddressesPost([FromBody]GetUtxosForMultipleAddressesRequest requestParams)
        {
            return await GetUtxoForMultipleAddresses(requestParams.addrs);
        }

        private async Task<List<object>> GetUtxo(string paymentAddress)
        {
            Utils.CheckIfChainIsFresh(chain_, config_.AcceptStaleRequests);

            using (var address = new PaymentAddress(paymentAddress))
            using (var getAddressHistoryResult = await chain_.FetchHistoryAsync(address, UInt64.MaxValue, 0))
            {
                Utils.CheckBitprimApiErrorCode(getAddressHistoryResult.ErrorCode, "FetchHistoryAsync(" + paymentAddress + ") failed, check error log.");
                
                var history = getAddressHistoryResult.Result;
                
                var utxo = new List<dynamic>();
                
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
                            var outputPoint = getSpendResult.Result;
                            
                            if(getSpendResult.ErrorCode == ErrorCode.NotFound) //Unspent = it's an utxo
                            {
                                //Get the tx to get the script
                                using(var getTxResult = await chain_.FetchTransactionAsync(outputPoint.Hash, true))
                                {
                                    utxo.Add(UtxoToJSON(paymentAddress, outputPoint, getTxResult.ErrorCode, getTxResult.Result.Tx, compact, topHeight));
                                }
                            }
                        }                        
                    }
                }
                return utxo;
            }
        }

        private static object UtxoToJSON(string paymentAddress, Point outputPoint, ErrorCode getTxEc, Transaction tx, HistoryCompact compact, UInt64 topHeight)
        {
            return new
            {
                address = paymentAddress,
                txid = Binary.ByteArrayToHexString(outputPoint.Hash),
                vout = outputPoint.Index,
                scriptPubKey = getTxEc == ErrorCode.Success? tx.Outputs[outputPoint.Index].Script.ToData(false) : null,
                amount = Utils.SatoshisToCoinUnits(compact.ValueOrChecksum),
                satoshis = compact.ValueOrChecksum,
                height = compact.Height,
                confirmations = topHeight - compact.Height
            };
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
                var txs = new List<string>();

                foreach(HistoryCompact compact in history)
                {
                    if(compact.PointKind == PointKind.Output)
                    {
                        received += compact.ValueOrChecksum;

                        using (var outPoint = new OutputPoint(compact.Point.Hash, compact.Point.Index))
                        {
                            var getSpendResult = await chain_.FetchSpendAsync(outPoint);
                        
                            txs.Add(Binary.ByteArrayToHexString(compact.Point.Hash));
                            if(getSpendResult.ErrorCode == ErrorCode.NotFound)
                            {
                                addressBalance += compact.ValueOrChecksum;
                            }
                        }
                    }
                }

                UInt64 totalSent = received - addressBalance;
                return new AddressBalance{ Balance = addressBalance, Received = received, Sent = totalSent, Transactions = txs };
            }
        }

        private async Task<ActionResult> GetBalanceProperty(string paymentAddress, string propertyName)
        {
            Utils.CheckIfChainIsFresh(chain_, config_.AcceptStaleRequests);
            var balance = await GetBalance(paymentAddress);
            return Json(balance.GetType().GetProperty(propertyName).GetValue(balance, null));
        }

        private Tuple<bool, string> ValidateParameters(int from, int to)
        {
            if(from < 0)
            {
                return new Tuple<bool, string>(false, "from(" + from + ") must be greater than or equal to zero");
            }

            if(to < 0)
            {
                return new Tuple<bool, string>(false, "to(" + to + ") must not be negative");
            }

            if(from > to)
            {
                return new Tuple<bool, string>(false, "to(" + to +  ") must be greater than from(" + from + ")");
            }
            return new Tuple<bool, string>(true, "");
        }
    }
}