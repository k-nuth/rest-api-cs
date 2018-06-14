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
    [ApiController]
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

        // GET: addr/{paymentAddress}
        [ResponseCache(CacheProfileName = Constants.Cache.SHORT_CACHE_PROFILE_NAME)]
        [HttpGet("addr/{paymentAddress}")]
        public async Task<ActionResult> GetAddressHistory(string paymentAddress, int noTxList = 0, int? from = null, int? to = null)
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
            historyJson.totalSent = Utils.SatoshisToCoinUnits(balance.Sent);
            historyJson.totalSentSat = balance.Sent;
            historyJson.txApperances = balance.Transactions.Count;
            Tuple<uint, Int64> unconfirmedSummary = await GetUnconfirmedSummary(paymentAddress);
            historyJson.unconfirmedBalance = Utils.SatoshisToCoinUnits(unconfirmedSummary.Item2);
            historyJson.unconfirmedBalanceSat = unconfirmedSummary.Item2;
            historyJson.unconfirmedTxApperances = unconfirmedSummary.Item1;
            
            if( noTxList == 0 )
            {
                if (from == null && to == null)
                {
                    from = 0;
                    to = balance.Transactions.Count;
                }
                else
                {
                    from = Math.Max(from ?? 0, 0); 
                    to = Math.Min(to ?? balance.Transactions.Count, balance.Transactions.Count);
                
                    var validationResult = ValidateParameters(from.Value, to.Value);
                    if( ! validationResult.Item1 )
                    {
                        return StatusCode((int)System.Net.HttpStatusCode.BadRequest, validationResult.Item2);
                    }
                }
                
                historyJson.transactions = balance.Transactions.GetRange(from.Value, to.Value - from.Value).ToArray();
            }
            
            return Json(historyJson);
        }

        // GET: addr/{paymentAddress}/balance
        [ResponseCache(CacheProfileName = Constants.Cache.SHORT_CACHE_PROFILE_NAME)]
        [HttpGet("addr/{paymentAddress}/balance")]
        public async Task<ActionResult> GetAddressBalance(string paymentAddress)
        {
            return await GetBalanceProperty(paymentAddress, "Balance");
        }

        // GET: addr/{paymentAddress}/totalReceived
        [ResponseCache(CacheProfileName = Constants.Cache.SHORT_CACHE_PROFILE_NAME)]
        [HttpGet("addr/{paymentAddress}/totalReceived")]
        public async Task<ActionResult> GetTotalReceived(string paymentAddress)
        {
            return await GetBalanceProperty(paymentAddress, "Received");
        }

        // GET: addr/{paymentAddress}/totalSent
        [ResponseCache(CacheProfileName = Constants.Cache.SHORT_CACHE_PROFILE_NAME)]
        [HttpGet("addr/{paymentAddress}/totalSent")]
        public async Task<ActionResult> GetTotalSent(string paymentAddress)
        {
            return await GetBalanceProperty(paymentAddress, "Sent");
        }

        // GET: addr/{paymentAddress}/unconfirmedBalance
        [ResponseCache(CacheProfileName = Constants.Cache.SHORT_CACHE_PROFILE_NAME)]
        [HttpGet("addr/{paymentAddress}/unconfirmedBalance")]
        public ActionResult GetUnconfirmedBalance(string paymentAddress)
        {
            Utils.CheckIfChainIsFresh(chain_, config_.AcceptStaleRequests);
            return Json(0); //We don't handle unconfirmed transactions
        }

        // GET: addr/{paymentAddress}/utxo
        [ResponseCache(CacheProfileName = Constants.Cache.SHORT_CACHE_PROFILE_NAME)]
        [HttpGet("addr/{paymentAddress}/utxo")]
        public async Task<ActionResult> GetUtxoForSingleAddress(string paymentAddress)
        {
            var utxo = await GetUtxo(paymentAddress);
            return Json(utxo.ToArray());
        }

        [ResponseCache(CacheProfileName = Constants.Cache.SHORT_CACHE_PROFILE_NAME)]
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

        [ResponseCache(CacheProfileName = Constants.Cache.SHORT_CACHE_PROFILE_NAME)]
        [HttpPost("addrs/utxo")]
        public async Task<ActionResult> GetUtxoForMultipleAddressesPost([FromBody]GetUtxosForMultipleAddressesRequest requestParams)
        {
            return await GetUtxoForMultipleAddresses(requestParams.addrs);
        }

        private async Task<UInt64> SumAddressInputs(Transaction tx, PaymentAddress address)
        {
            UInt64 inputSum = 0;
            foreach(Input input in tx.Inputs)
            {
                if(input.PreviousOutput == null)
                {
                    continue;
                }
                using(var getTxResult = await chain_.FetchTransactionAsync(input.PreviousOutput.Hash, false))
                {
                    if(getTxResult.ErrorCode == ErrorCode.NotFound)
                    {
                        continue;
                    }
                    Utils.CheckBitprimApiErrorCode(getTxResult.ErrorCode, "FetchTransactionAsync(" + Binary.ByteArrayToHexString(input.PreviousOutput.Hash) + ") failed, check error log");
                    Output referencedOutput = getTxResult.Result.Tx.Outputs[input.PreviousOutput.Index];
                    if(referencedOutput.PaymentAddress(nodeExecutor_.UseTestnetRules).Encoded == address.Encoded)
                    {
                        inputSum += referencedOutput.Value;
                    }
                }
            }
            return inputSum;
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
                            
                            if(getSpendResult.ErrorCode == ErrorCode.NotFound) //Unspent = it's an utxo
                            {
                                //Get the tx to get the script
                                using(var getTxResult = await chain_.FetchTransactionAsync(compact.Point.Hash, true))
                                {
                                    Utils.CheckBitprimApiErrorCode(getTxResult.ErrorCode, "FetchTransactionAsync (" + Binary.ByteArrayToHexString(outPoint.Hash)  + ") failed, check error log");
                                    utxo.Add(UtxoToJSON(address, compact.Point, getTxResult.ErrorCode, getTxResult.Result.Tx, compact, topHeight));
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
                        unconfirmedBalance += (Int64)SumAddressOutputs(tx, paymentAddress);
                        unconfirmedBalance -=  (Int64)await SumAddressInputs(tx, paymentAddress);
                    }
                }
                return new Tuple<uint, Int64>(unconfirmedTxs.Count, unconfirmedBalance);
            }
        }

        private UInt64 SumAddressOutputs(Transaction tx, PaymentAddress address)
        {
            UInt64 outputSum = 0;
            foreach(Output output in tx.Outputs)
            {
                if(output.PaymentAddress(nodeExecutor_.UseTestnetRules).Encoded == address.Encoded)
                {
                    outputSum += output.Value;
                }
            }
            return outputSum;
        }

        private List<object> GetUnconfirmedUtxo(PaymentAddress address)
        {
            var unconfirmedUtxo = new List<object>();
            using(MempoolTransactionList unconfirmedTxs = chain_.GetMempoolTransactions(address, nodeExecutor_.UseTestnetRules))
            {
                foreach(MempoolTransaction unconfirmedTx in unconfirmedTxs)
                {
                    var satoshis = Int64.Parse(unconfirmedTx.Satoshis);

                    unconfirmedUtxo.Add(new
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

        private static object UtxoToJSON(PaymentAddress paymentAddress, Point outputPoint, ErrorCode getTxEc, Transaction tx, HistoryCompact compact, UInt64 topHeight)
        {
            return new
            {
                address = paymentAddress.Encoded,
                txid = Binary.ByteArrayToHexString(outputPoint.Hash),
                vout = outputPoint.Index,
                scriptPubKey = getTxEc == ErrorCode.Success ? GetOutputScript(tx.Outputs[outputPoint.Index]) : null,
                amount = Utils.SatoshisToCoinUnits(compact.ValueOrChecksum),
                satoshis = compact.ValueOrChecksum,
                height = compact.Height,
                confirmations = topHeight - compact.Height + 1
            };
        }

        private static string GetOutputScript(Output output)
        {
            var script = output.Script;
            var scriptData = script.ToData(false);
            Array.Reverse(scriptData, 0, scriptData.Length);
            return Binary.ByteArrayToHexString(scriptData);
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

        private async Task<ActionResult> GetBalanceProperty(string paymentAddress, string propertyName)
        {
            Utils.CheckIfChainIsFresh(chain_, config_.AcceptStaleRequests);
            var balance = await GetBalance(paymentAddress);
            return Json(balance.GetType().GetProperty(propertyName).GetValue(balance, null));
        }

        private Tuple<bool, string> ValidateParameters(int from, int to)
        {
            if(from >= to)
            {
                return new Tuple<bool, string>(false, "'from' must be lower than 'to'");
            }

            return new Tuple<bool, string>(true, "");
        }
    }
}