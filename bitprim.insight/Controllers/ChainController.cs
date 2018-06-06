using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Net.Http;
using System.Threading.Tasks;
using Bitprim;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Polly;
using System.Linq;
using System.Globalization;

namespace bitprim.insight.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class ChainController : Controller
    {
        private readonly Chain chain_;
        private readonly Executor nodeExecutor_;
        private static readonly HttpClient httpClient_ = new HttpClient();
        private readonly NodeConfig config_;
        private readonly ILogger<ChainController> logger_;
        private readonly IMemoryCache memoryCache_;
        private readonly Policy breakerPolicy_ = Policy.Handle<Exception>().CircuitBreakerAsync(2, TimeSpan.FromMinutes(1));
        private readonly Policy retryPolicy_ = Policy.Handle<Exception>()
            .WaitAndRetryAsync(RetryUtils.DecorrelatedJitter
                (Constants.MAX_RETRIES, TimeSpan.FromMilliseconds(Constants.SEED_DELAY), TimeSpan.FromSeconds(Constants.MAX_DELAY)));
        private readonly Policy execPolicy_;

        public ChainController(IOptions<NodeConfig> config, Executor executor, ILogger<ChainController> logger, IMemoryCache memoryCache)
        {
            config_ = config.Value;
            nodeExecutor_ = executor;
            chain_ = executor.Chain;
            memoryCache_ = memoryCache;
            execPolicy_ = Policy.WrapAsync(retryPolicy_, breakerPolicy_);
            logger_ = logger;
        }

        [HttpGet("healthcheck")]
        public async Task<ActionResult> GetHealthCheck(float minimumSync)
        {
            dynamic syncStatus = await DoGetSyncStatus();
            bool isNumeric = Double.TryParse(syncStatus.syncPercentage, out double syncPercentage);
            bool isHealthy = isNumeric && syncPercentage > minimumSync;
            return isHealthy? 
                StatusCode((int)System.Net.HttpStatusCode.OK, "OK"):
                StatusCode((int)System.Net.HttpStatusCode.PreconditionFailed, "NOK");
        }

        [ResponseCache(CacheProfileName = Constants.Cache.SHORT_CACHE_PROFILE_NAME)]
        [HttpGet("sync")]
        public async Task<ActionResult> GetSyncStatus()
        {
            return Json(await DoGetSyncStatus());
        }

        [ResponseCache(CacheProfileName = Constants.Cache.SHORT_CACHE_PROFILE_NAME)]
        [HttpGet("status")]
        public async Task<ActionResult> GetStatus([Bind(Prefix = "q")] string method)
        {
            switch (method)
            {
                case Constants.GET_DIFFICULTY:
                    return await GetDifficulty();
                case Constants.GET_BEST_BLOCK_HASH:
                    return await GetBestBlockHash();
                case Constants.GET_LAST_BLOCK_HASH:
                    return await GetLastBlockHash();
            }

            return await GetInfo();
        }

        [HttpGet("utils/estimatefee")]
        public ActionResult GetEstimateFee([FromQuery] int nbBlocks = 2)
        {
            var estimateFee = new ExpandoObject() as IDictionary<string, Object>;
            //TODO Check which algorithm to use (see bitcoin-abc's median, at src/policy/fees.cpp for an example)
            estimateFee.Add(nbBlocks.ToString(), config_.EstimateFeeDefault.ToString("N8"));
            return Json(estimateFee);
        }

        [HttpGet("currency")]
        public async Task<ActionResult> GetCurrency()
        {
            var usdPrice = 1.0f;
            try
            {
                usdPrice = await execPolicy_.ExecuteAsync<float>(() => GetCurrentCoinPriceInUsd());
                memoryCache_.Set
                (
                    Constants.Cache.CURRENT_PRICE_CACHE_KEY, usdPrice,
                    new MemoryCacheEntryOptions { Size = Constants.Cache.CURRENT_PRICE_CACHE_ENTRY_SIZE }
                );
            }
            catch (Exception ex)
            {
                logger_.LogWarning(ex, "Failed to get latest currency price from external service; returning last read value");
                if (!memoryCache_.TryGetValue(Constants.Cache.CURRENT_PRICE_CACHE_KEY, out usdPrice))
                {
                    logger_.LogWarning("No cached value available, returning default (1.0)");
                }
            }
            return Json(new
            {
                status = 200,
                data = new
                {
                    bitstamp = usdPrice
                }
            });
        }

        private async Task<ActionResult> GetDifficulty()
        {
            using (var getLastBlockResult = await GetLastBlock())
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
            using (var getLastBlockResult = await GetLastBlock())
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
            using (var getLastBlockResult = await GetLastBlock())
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

        private string GetNetworkType(NetworkType networkType)
        {
            switch (networkType)
            {
                case NetworkType.Mainnet:
                    return "livenet";
                default:
                    return networkType.ToString().ToLower();

            }
        }

        private async Task<object> DoGetSyncStatus()
        {
            var getLastHeightResult = await chain_.FetchLastHeightAsync();
            Utils.CheckBitprimApiErrorCode(getLastHeightResult.ErrorCode, "GetLastHeight() failed");

            var currentHeight = getLastHeightResult.Result;
            UInt64? blockChainHeight = await GetCurrentBlockChainHeight();
            dynamic syncStatus = new ExpandoObject();
            if (blockChainHeight.HasValue)
            {
                var synced = currentHeight >= blockChainHeight;
                syncStatus.status = synced ? "finished" : "synchronizing";
                syncStatus.blockChainHeight = blockChainHeight;
                syncStatus.syncPercentage = Math.Min((double)currentHeight / (double)blockChainHeight * 100.0, 100).ToString("N2");
                syncStatus.error = null;
            }
            else
            {
                syncStatus.status = "unknown";
                syncStatus.blockChainHeight = "unknown";
                syncStatus.syncPercentage = "unknown";
                syncStatus.error = "Could not determine max blockchain height; check log";
            }
            syncStatus.height = currentHeight;
            syncStatus.type = config_.NodeType;
            return syncStatus;
        }

        private async Task<ActionResult> GetInfo()
        {
            using (var getLastBlockResult = await GetLastBlock())
            {
                return Json
                (
                    new
                    {
                        info = new
                        {
                            //TODO Some of these values should be retrieved from node-cint
                            version = config_.Version,
                            protocolversion = config_.ProtocolVersion,
                            blocks = getLastBlockResult.Result.BlockHeight,
                            timeoffset = config_.TimeOffset,
                            connections = config_.Connections,
                            proxy = config_.Proxy,
                            difficulty = Utils.BitsToDifficulty(getLastBlockResult.Result.BlockData.Header.Bits),
                            testnet = nodeExecutor_.UseTestnetRules,
                            relayfee = config_.RelayFee,
                            errors = "",
                            network = GetNetworkType(nodeExecutor_.NetworkType),
                            coin = GetCoin()
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

        //TODO Consider moving this down to node-cint for other APIs to reuse
        private async Task<float> GetCurrentCoinPriceInUsd()
        {
            string currencyPair = "";
            switch (NodeSettings.CurrencyType)
            {
                case CurrencyType.Bitcoin: currencyPair = Constants.BITSTAMP_BTCUSD; break;
                case CurrencyType.BitcoinCash: currencyPair = Constants.BITSTAMP_BCCUSD; break;
                case CurrencyType.Litecoin: currencyPair = Constants.BITSTAMP_LTCUSD; break;
                default: throw new InvalidOperationException("Unsupported currency: " + NodeSettings.CurrencyType);
            }
            string bitstampUrl = Constants.BITSTAMP_URL.Replace(Constants.BITSTAMP_CURRENCY_PAIR_PLACEHOLDER, currencyPair);
            var priceDataString = await httpClient_.GetStringAsync(bitstampUrl);
            dynamic priceData = JsonConvert.DeserializeObject<dynamic>(priceDataString);
            float price = 1.0f;
            if (!float.TryParse(priceData.last.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out price))
            {
                throw new FormatException("Invalid price value: " + priceData.last.Value);
            }
            return price;
        }

        //TODO Avoid consulting external sources; get this information from bitprim network
        private async Task<UInt64?> GetCurrentBlockChainHeight()
        {
            try
            {
                UInt64 blockChainHeight = 0;
                if (memoryCache_.TryGetValue(Constants.Cache.BLOCKCHAIN_HEIGHT_CACHE_KEY, out blockChainHeight))
                {
                    return blockChainHeight;
                };
                switch (NodeSettings.CurrencyType)
                {
                    case CurrencyType.BitcoinCash:
                        blockChainHeight = await execPolicy_.ExecuteAsync<UInt64>(() => GetBCCBlockchainHeight());
                        break;
                    case CurrencyType.Bitcoin:
                        blockChainHeight = await execPolicy_.ExecuteAsync<UInt64>(() => GetBTCBlockchainHeight());
                        break;
                    case CurrencyType.Litecoin:
                        blockChainHeight = await execPolicy_.ExecuteAsync<UInt64>(() => GetLTCBlockchainHeight());
                        break;
                    default:
                        throw new InvalidOperationException("Only BCH, BTC and LTC support this operation");
                }
                memoryCache_.Set
                (
                    Constants.Cache.BLOCKCHAIN_HEIGHT_CACHE_KEY, blockChainHeight, new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(Constants.Cache.MAX_BLOCKCHAIN_HEIGHT_AGE_IN_SECONDS),
                        Size = Constants.Cache.BLOCKCHAIN_HEIGHT_CACHE_ENTRY_SIZE
                    }
                );
                return blockChainHeight;
            }
            catch (Exception ex)
            {
                logger_.LogWarning(ex, "Failed to retrieve blockchain height from external service");
                return null;
            }
        }

        private async Task<UInt64> GetBCCBlockchainHeight()
        {
            if (nodeExecutor_.UseTestnetRules)
            {
                var syncDataString = await httpClient_.GetStringAsync(Constants.BLOCKTRAIL_TBCC_URL);
                dynamic syncData = JsonConvert.DeserializeObject<dynamic>(syncDataString);
                return syncData.last_blocks[0].height;
            }
            else
            {
                var syncDataString = await httpClient_.GetStringAsync(Constants.BLOCKCHAIR_BCC_URL);
                dynamic syncData = JsonConvert.DeserializeObject<dynamic>(syncDataString);
                return ((IEnumerable<dynamic>)syncData.data).Where(r => r.e == "blocks").First().c - 1;
            }
        }

        private async Task<UInt64> GetBTCBlockchainHeight()
        {
            if (nodeExecutor_.UseTestnetRules)
            {
                var syncDataString = await httpClient_.GetStringAsync(Constants.SOCHAIN_TBTC_URL);
                dynamic syncData = JsonConvert.DeserializeObject<dynamic>(syncDataString);
                return syncData.data.blocks;
            }
            else
            {
                var syncDataString = await httpClient_.GetStringAsync(Constants.BLOCKCHAIR_BTC_URL);
                dynamic syncData = JsonConvert.DeserializeObject<dynamic>(syncDataString);
                return ((IEnumerable<dynamic>)syncData.data).First(r => r.e == "blocks").c;
            }
        }

        private async Task<UInt64> GetLTCBlockchainHeight()
        {
            if (nodeExecutor_.UseTestnetRules)
            {
                var syncDataString = await httpClient_.GetStringAsync(Constants.SOCHAIN_TLTC_URL);
                dynamic syncData = JsonConvert.DeserializeObject<dynamic>(syncDataString);
                return syncData.data.blocks;
            }
            else
            {
                var syncDataString = await httpClient_.GetStringAsync(Constants.SOCHAIN_LTC_URL);
                dynamic syncData = JsonConvert.DeserializeObject<dynamic>(syncDataString);
                return syncData.data.blocks;
            }
        }

        private string GetCoin()
        {
            switch( NodeSettings.CurrencyType )
            {
                case CurrencyType.Bitcoin: return nodeExecutor_.UseTestnetRules? "tbtc" : "btc";
                case CurrencyType.BitcoinCash: return nodeExecutor_.UseTestnetRules? "tbch" : "bch";
                case CurrencyType.Litecoin: return nodeExecutor_.UseTestnetRules? "tltc" : "ltc";
                default: throw new InvalidOperationException("Invalid coin: " + NodeSettings.CurrencyType);
            }
        }

    }
}