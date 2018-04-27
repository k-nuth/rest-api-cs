using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Net.Http;
using System.Threading.Tasks;
using Bitprim;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System.Linq;

namespace bitprim.insight.Controllers
{
    [Route("api/[controller]")]
    public class ChainController : Controller
    {
        private Chain chain_;
        private DateTime lastTimeHeightExternallyFetched;
        private Executor nodeExecutor_;
        private static readonly HttpClient httpClient_ = new HttpClient();
        private readonly NodeConfig config_;
        private const int MAX_BLOCKCHAIN_HEIGHT_AGE_IN_SECONDS = 60;
        private const string BLOCKCHAIR_BCC_URL = "https://api.blockchair.com/bitcoin-cash";
        private const string BLOCKCHAIR_BTC_URL = "https://api.blockchair.com/bitcoin";
        private const string BLOCKTRAIL_TBCC_URL = "https://www.blocktrail.com/tBCC/json/blockchain/homeStats";
        private const string GET_BEST_BLOCK_HASH = "getBestBlockHash";
        private const string GET_LAST_BLOCK_HASH = "getLastBlockHash";
        private const string GET_DIFFICULTY = "getDifficulty";
        private const string SOCHAIN_LTC_URL = "https://chain.so/api/v2/get_info/LTC";
        private const string SOCHAIN_TBTC_URL = "https://chain.so/api/v2/get_info/BTCTEST";
        private const string SOCHAIN_TLTC_URL = "https://chain.so/api/v2/get_info/LTCTEST";
        private UInt64 blockChainHeight_;

        public ChainController(IOptions<NodeConfig> config, Executor executor)
        {
            config_ = config.Value;
            nodeExecutor_ = executor;
            chain_ = executor.Chain;
            lastTimeHeightExternallyFetched = DateTime.MinValue;
            blockChainHeight_ = 0;
        }

        [HttpGet("/api/sync")]
        public async Task<ActionResult> GetSyncStatus()
        {
            var getLastHeightResult = await chain_.FetchLastHeightAsync();
            Utils.CheckBitprimApiErrorCode(getLastHeightResult.ErrorCode, "GetLastHeight() failed");
            
            var currentHeight = getLastHeightResult.Result;
            var blockChainHeight = await GetCurrentBlockChainHeight();
            var synced = currentHeight >= blockChainHeight;
            
            dynamic syncStatus = new ExpandoObject();
            syncStatus.status = synced? "finished" : "synchronizing";
            syncStatus.blockChainHeight = blockChainHeight;
            syncStatus.syncPercentage = Math.Min((double)currentHeight / (double)blockChainHeight * 100.0, 100).ToString("N2");
            syncStatus.height = currentHeight;
            syncStatus.error = null;
            syncStatus.type = config_.NodeType;
            return Json(syncStatus);   
        }

        [HttpGet("/api/status")]
        public async Task<ActionResult> GetStatus(string method)
        {
            switch (method)
            {
                case GET_DIFFICULTY:
                    return await GetDifficulty();
                case GET_BEST_BLOCK_HASH:
                    return await GetBestBlockHash();
                case GET_LAST_BLOCK_HASH:
                    return await GetLastBlockHash();
            }

            return await GetInfo();
        }

        [HttpGet("/api/utils/estimatefee")]
        public ActionResult GetEstimateFee([FromQuery] int nbBlocks = 2)
        {
            var estimateFee = new ExpandoObject() as IDictionary<string, Object>;
            //TODO Check which algorithm to use (see bitcoin-abc's median, at src/policy/fees.cpp for an example)
            estimateFee.Add(nbBlocks.ToString(), 1.0);
            return Json(estimateFee);   
        }

        [HttpGet("/api/currency")]
        public ActionResult GetCurrency()
        {
            //TODO Implement in node-cint? Or here? Ask
            return Json(new{
                status = 200,
                data = new
                {
                    bistamp = 8025.3f
                }
            });
        }

        private async Task<ActionResult> GetDifficulty()
        {
            using(var getLastBlockResult = await GetLastBlock())
            {
                return Json
                (
                    new
                    {
                        difficulty = Utils.BitsToDifficulty(getLastBlockResult.Result.BlockData.Header.Bits)
                    }
                );
            }
        }

        private async Task<ActionResult> GetBestBlockHash()
        {
            using(var getLastBlockResult = await GetLastBlock())
            {
                return Json
                (
                    new
                    {
                        bestblockhash = Binary.ByteArrayToHexString(getLastBlockResult.Result.BlockData.Hash)
                    }
                );
            }
        }

        private async Task<ActionResult> GetLastBlockHash()
        {
            using(var getLastBlockResult = await GetLastBlock())
            {
                var hashHexString = Binary.ByteArrayToHexString(getLastBlockResult.Result.BlockData.Hash); 
                return Json
                (
                    new
                    {
                        syncTipHash = hashHexString,
                        lastblockhash = hashHexString
                    }
                );
            }
        }

        private async Task<ActionResult> GetInfo()
        {
            using(var getLastBlockResult = await GetLastBlock())
            {
                return Json
                (
                    new
                    {
                        info = new 
                        {
                            //version = 120100, //TODO
                            //protocolversion = 70012, //TODO
                            blocks = getLastBlockResult.Result.BlockHeight,
                            //timeoffset = 0, //TODO
                            //connections = 8, //TODO
                            //proxy = "", //TODO
                            difficulty = Utils.BitsToDifficulty(getLastBlockResult.Result.BlockData.Header.Bits),
                            testnet = nodeExecutor_.UseTestnetRules,
                            //relayfee = 0.00001, //TODO
                            //errors = "Warning: unknown new rules activated (versionbit 28)", //TODO
                            network = nodeExecutor_.NetworkType.ToString()
                        }
                    }
                );
            }
        }

        private async Task<DisposableApiCallResult<GetBlockDataResult<Block>>> GetLastBlock()
        {
            var getLastHeightResult = await chain_.FetchLastHeightAsync();
            Utils.CheckBitprimApiErrorCode(getLastHeightResult.ErrorCode, "FetchLastHeightAsync() failed");
            
            var currentHeight = getLastHeightResult.Result;
            var getBlockResult = await chain_.FetchBlockByHeightAsync(currentHeight);
            Utils.CheckBitprimApiErrorCode(getBlockResult.ErrorCode, "FetchBlockByHeightAsync(" + currentHeight + ") failed");

            return getBlockResult;
        }

        //TODO Avoid consulting external sources; get this information from bitprim network
        private async Task<UInt64> GetCurrentBlockChainHeight()
        {
            if((DateTime.Now - lastTimeHeightExternallyFetched).TotalSeconds <= MAX_BLOCKCHAIN_HEIGHT_AGE_IN_SECONDS)
            {
                return blockChainHeight_;
            }
            switch(NodeSettings.CurrencyType)
            {
                case CurrencyType.BitcoinCash:
                    blockChainHeight_ = await GetBCCBlockchainHeight();
                    break;
                case CurrencyType.Bitcoin:
                    blockChainHeight_ = await GetBTCBlockchainHeight();
                    break;
                case CurrencyType.Litecoin:
                    blockChainHeight_ = await GetLTCBlockchainHeight();
                    break;
                default:
                    throw new InvalidOperationException("Only BCH, BTC and LTC support this operation");
            }
            lastTimeHeightExternallyFetched = DateTime.Now;
            return blockChainHeight_;
        }

        private async Task<UInt64> GetBCCBlockchainHeight()
        {
            if(nodeExecutor_.UseTestnetRules)
            {
                var syncDataString = await httpClient_.GetStringAsync(BLOCKTRAIL_TBCC_URL);
                dynamic syncData = JsonConvert.DeserializeObject<dynamic>(syncDataString);
                return syncData.last_blocks[0].height;
            }
            else
            {
                var syncDataString = await httpClient_.GetStringAsync(BLOCKCHAIR_BCC_URL);
                dynamic syncData = JsonConvert.DeserializeObject<dynamic>(syncDataString);
                return ((IEnumerable<dynamic>)syncData.data).Where( r => r.e == "blocks" ).First().c;
            }
        }

        private async Task<UInt64> GetBTCBlockchainHeight()
        {
            if(nodeExecutor_.UseTestnetRules)
            {
                var syncDataString = await httpClient_.GetStringAsync(SOCHAIN_TBTC_URL);
                dynamic syncData = JsonConvert.DeserializeObject<dynamic>(syncDataString);
                return syncData.data.blocks;
            }
            else
            {
                var syncDataString = await httpClient_.GetStringAsync(BLOCKCHAIR_BTC_URL);
                dynamic syncData = JsonConvert.DeserializeObject<dynamic>(syncDataString);
                return ((IEnumerable<dynamic>)syncData.data).Where( r => r.e == "blocks" ).First().c;
            }
        }

        private async Task<UInt64> GetLTCBlockchainHeight()
        {
            if(nodeExecutor_.UseTestnetRules)
            {
                var syncDataString = await httpClient_.GetStringAsync(SOCHAIN_TLTC_URL);
                dynamic syncData = JsonConvert.DeserializeObject<dynamic>(syncDataString);
                return syncData.data.blocks;
            }
            else
            {
                var syncDataString = await httpClient_.GetStringAsync(SOCHAIN_LTC_URL);
                dynamic syncData = JsonConvert.DeserializeObject<dynamic>(syncDataString);
                return syncData.data.blocks;
            }
        }

    }
}