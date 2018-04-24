using System;
using System.Collections.Generic;
using System.Linq;
using bitprim.insight.DTOs;
using Bitprim;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Dynamic;

namespace bitprim.insight.Controllers
{

    [Route("api/[controller]")]
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

        // GET: api/addr/{paymentAddress}
        [HttpGet("/api/addr/{paymentAddress}")]
        public ActionResult GetAddressHistory(string paymentAddress, bool? noTxList = false, int? from = 0, int? to = null)
        {
            if(!Validations.IsValidPaymentAddress(paymentAddress))
            {
                return StatusCode((int)System.Net.HttpStatusCode.BadRequest, paymentAddress + " is not a valid Base58 address");
            }
            Utils.CheckIfChainIsFresh(chain_, config_.AcceptStaleRequests);
            AddressBalance balance = GetBalance(paymentAddress);
            dynamic historyJson = new ExpandoObject();
            historyJson.addrStr = paymentAddress;
            historyJson.balance = balance.Balance;
            historyJson.balanceSat = Utils.SatoshisToCoinUnits(balance.Balance);
            historyJson.totalReceived = Utils.SatoshisToCoinUnits(balance.Received);
            historyJson.totalReceivedSat = balance.Received;
            historyJson.totalSent = balance.Sent;
            historyJson.totalSentSat = Utils.SatoshisToCoinUnits(balance.Sent);
            historyJson.unconfirmedBalance = 0; //We don't handle unconfirmed txs
            historyJson.unconfirmedBalanceSat = 0; //We don't handle unconfirmed txs
            historyJson.unconfirmedTxApperances = 0; //We don't handle unconfirmed txs
            historyJson.txApperances = balance.Transactions.Count;
            if( ! noTxList.Value )
            {
                if(from == null)
                {
                    from = 0;
                }
                if(to == null || (to != null && to.Value >= balance.Transactions.Count) )
                {
                    to = balance.Transactions.Count() - 1;
                }
                Tuple<bool, string> validationResult = ValidateParameters(from.Value, to.Value);
                if( ! validationResult.Item1 )
                {
                    return StatusCode((int)System.Net.HttpStatusCode.BadRequest, validationResult.Item2);
                }
                historyJson.transactions = balance.Transactions.GetRange(from.Value, to.Value).ToArray();
            }
            return Json(historyJson);
        }

        // GET: api/addr/{paymentAddress}/balance
        [HttpGet("/api/addr/{paymentAddress}/balance")]
        public ActionResult GetAddressBalance(string paymentAddress)
        {
            return GetBalanceProperty(paymentAddress, "Balance");
        }

        // GET: api/addr/{paymentAddress}/totalReceived
        [HttpGet("/api/addr/{paymentAddress}/totalReceived")]
        public ActionResult GetTotalReceived(string paymentAddress)
        {
            return GetBalanceProperty(paymentAddress, "Received");
        }

        // GET: api/addr/{paymentAddress}/totalSent
        [HttpGet("/api/addr/{paymentAddress}/totalSent")]
        public ActionResult GetTotalSent(string paymentAddress)
        {
            return GetBalanceProperty(paymentAddress, "Sent");
        }

        // GET: api/addr/{paymentAddress}/unconfirmedBalance
        [HttpGet("/api/addr/{paymentAddress}/unconfirmedBalance")]
        public ActionResult GetUnconfirmedBalance(string paymentAddress)
        {
            Utils.CheckIfChainIsFresh(chain_, config_.AcceptStaleRequests);
            return Json(0); //We don't handle unconfirmed transactions
        }

        // GET: api/addr/{paymentAddress}/utxo
        [HttpGet("/api/addr/{paymentAddress}/utxo")]
        public ActionResult GetUtxoForSingleAddress(string paymentAddress)
        {
            List<object> utxo = GetUtxo(paymentAddress);
            return Json(utxo.ToArray());
        }


        [HttpGet("/api/addrs/{paymentAddresses}/utxo")]
        public ActionResult GetUtxoForMultipleAddresses(string paymentAddresses)
        {
            IEnumerable<object> utxo = new List<object>();
            foreach(string address in paymentAddresses.Split(","))
            {
                utxo = utxo.Concat(GetUtxo(address));
            }
            return Json(utxo.ToArray());   
        }

        [HttpPost("/api/addrs/utxo")]
        public ActionResult GetUtxoForMultipleAddressesPost([FromBody]GetUtxosForMultipleAddressesRequest requestParams)
        {
            return GetUtxoForMultipleAddresses(requestParams.addrs);
        }

        private List<object> GetUtxo(string paymentAddress)
        {
            Utils.CheckIfChainIsFresh(chain_, config_.AcceptStaleRequests);
            using(DisposableApiCallResult<HistoryCompactList> getAddressHistoryResult = chain_.GetHistory(new PaymentAddress(paymentAddress), UInt64.MaxValue, 0))
            {
                Utils.CheckBitprimApiErrorCode(getAddressHistoryResult.ErrorCode, "GetHistory(" + paymentAddress + ") failed, check error log.");
                HistoryCompactList history = getAddressHistoryResult.Result;
                var utxo = new List<dynamic>();
                ApiCallResult<UInt64> getLastHeightResult = chain_.GetLastHeight();
                Utils.CheckBitprimApiErrorCode(getLastHeightResult.ErrorCode, "GetLastHeight failed, check error log");
                UInt64 topHeight = getLastHeightResult.Result;
                foreach(HistoryCompact compact in history)
                {
                    if(compact.PointKind == PointKind.Output)
                    {
                        ApiCallResult<Point> getSpendResult = chain_.GetSpend(new OutputPoint(compact.Point.Hash, compact.Point.Index));
                        Point outputPoint = getSpendResult.Result;
                        if(getSpendResult.ErrorCode == ErrorCode.NotFound) //Unspent = it's an utxo
                        {
                            //Get the tx to get the script
                            using(DisposableApiCallResult<GetTxDataResult> getTxResult = chain_.GetTransaction(outputPoint.Hash, true))
                            {
                                utxo.Add(UtxoToJSON
                                (
                                    paymentAddress, outputPoint, getTxResult.ErrorCode, getTxResult.Result.Tx, compact, topHeight)
                                );
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
                scriptPubKey = getTxEc == ErrorCode.Success? tx.Outputs[(int)outputPoint.Index].Script.ToData(false) : null,
                amount = Utils.SatoshisToCoinUnits(compact.ValueOrChecksum),
                satoshis = compact.ValueOrChecksum,
                height = compact.Height,
                confirmations = topHeight - compact.Height
            };
        }

        private AddressBalance GetBalance(string paymentAddress)
        {
            using(DisposableApiCallResult<HistoryCompactList> getAddressHistoryResult = chain_.GetHistory(new PaymentAddress(paymentAddress), UInt64.MaxValue, 0))
            {
                Utils.CheckBitprimApiErrorCode(getAddressHistoryResult.ErrorCode, "GetHistory(" + paymentAddress + ") failed, check error log.");
                HistoryCompactList history = getAddressHistoryResult.Result;
                UInt64 received = 0;
                UInt64 addressBalance = 0;
                var txs = new List<string>();
                foreach(HistoryCompact compact in history)
                {
                    if(compact.PointKind == PointKind.Output)
                    {
                        received += compact.ValueOrChecksum;
                        ApiCallResult<Point> getSpendResult = chain_.GetSpend(new OutputPoint(compact.Point.Hash, compact.Point.Index));
                        txs.Add(Binary.ByteArrayToHexString(compact.Point.Hash));
                        if(getSpendResult.ErrorCode == ErrorCode.NotFound)
                        {
                            addressBalance += compact.ValueOrChecksum;
                        }
                    }
                }
                UInt64 totalSent = received - addressBalance;
                return new AddressBalance{ Balance = addressBalance, Received = received, Sent = totalSent, Transactions = txs };
            }
        }

        private ActionResult GetBalanceProperty(string paymentAddress, string propertyName)
        {
            try
            {
                Utils.CheckIfChainIsFresh(chain_, config_.AcceptStaleRequests);
                AddressBalance balance = GetBalance(paymentAddress);
                return Json(balance.GetType().GetProperty(propertyName).GetValue(balance, null));
            }
            catch(Exception ex)
            {
                return StatusCode((int)System.Net.HttpStatusCode.InternalServerError, ex.Message);
            }
        }

        private Tuple<bool, string> ValidateParameters(int from, int to)
        {
            if(from < 0)
            {
                return new Tuple<bool, string>(false, "from(" + from + ") must be greater than or equal to zero");
            }
            if(to <= 0)
            {
                return new Tuple<bool, string>(false, "to(" + to + ") must be greater than zero");
            }
            if(from >= to)
            {
                return new Tuple<bool, string>(false, "to(" + to +  ") must be greater than from(" + from + ")");
            }
            return new Tuple<bool, string>(true, "");
        }
    }
}