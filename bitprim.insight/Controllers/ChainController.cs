using bitprim.insight.DTOs;
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
using Newtonsoft.Json.Linq;
using Polly;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Linq;
using System.Globalization;

namespace bitprim.insight.Controllers
{
    /// <summary>
    /// Blockchain related operations.
    /// </summary>
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

        /// <summary>
        /// Build this controller.
        /// </summary>
        /// <param name="config"> Higher level API configuration. </param>
        /// <param name="executor"> Node executor from bitprim-cs library. </param>
        /// <param name="logger"> Abstract logger. </param>
        /// <param name="memoryCache"> Abstract memory cache. </param>
        public ChainController(IOptions<NodeConfig> config, Executor executor, ILogger<ChainController> logger, IMemoryCache memoryCache)
        {
            config_ = config.Value;
            nodeExecutor_ = executor;
            chain_ = executor.Chain;
            memoryCache_ = memoryCache;
            execPolicy_ = Policy.WrapAsync(retryPolicy_, breakerPolicy_);
            logger_ = logger;
        }

        /// <summary>
        /// Get an estimate value for current block fee.
        /// </summary>
        /// <param name="nbBlocks"> Number of blocks to consider for estimation; a higher number
        /// implies higher precision, but will take longer to calculate.
        /// </param>
        /// <returns> Current estimation for block fee. </returns>
        [HttpGet("utils/estimatefee")]
        [SwaggerOperation("GetEstimateFee")]
        [SwaggerResponse((int)System.Net.HttpStatusCode.OK, typeof(IDictionary<string, string>))]
        public ActionResult GetEstimateFee([FromQuery] int nbBlocks = 2)
        {
            var estimateFee = new ExpandoObject() as IDictionary<string, Object>;
            //TODO Check which algorithm to use (see bitcoin-abc's median, at src/policy/fees.cpp for an example)
            estimateFee.Add(nbBlocks.ToString(), config_.EstimateFeeDefault.ToString("N8"));
            return Json(estimateFee);
        }

        /// <summary>
        /// Get best block hash.
        /// </summary>
        /// <returns> Best block hash. </returns>
        [HttpGet("status/bestblockhash")]
        [SwaggerOperation("GetBestBlockHash")]
        [SwaggerResponse((int)System.Net.HttpStatusCode.OK, typeof(GetBestBlockHashResponse))]
        public async Task<ActionResult> GetBestBlockHash()
        {
            using (var getLastBlockResult = await GetLastBlock())
            {
                return Json
                (
                    new GetBestBlockHashResponse
                    {
                        bestblockhash = Binary.ByteArrayToHexString(getLastBlockResult.Result.BlockData.Hash)
                    }
                );
            }
        }

        /// <summary>
        /// Get current coin price in US dollars.
        /// </summary>
        /// <returns> Current coin price in USD. </returns>
        [HttpGet("currency")]
        [SwaggerOperation("GetCurrency")]
        [SwaggerResponse((int)System.Net.HttpStatusCode.OK, typeof(GetCurrencyResponse))]
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
            return Json(new GetCurrencyResponse
            {
                status = 200,
                data = new CurrencyData
                {
                    bitstamp = usdPrice
                }
            });
        }

        /// <summary>
        /// Get latest block difficulty.
        /// </summary>
        /// <returns> Latest block difficulty. </returns>
        [HttpGet("status/difficulty")]
        [SwaggerOperation("GetDifficulty")]
        [SwaggerResponse((int)System.Net.HttpStatusCode.OK, typeof(GetDifficultyResponse))]
        public async Task<ActionResult> GetDifficulty()
        {
            using (var getLastBlockResult = await GetLastBlock())
            {
                return Json
                (
                    new GetDifficultyResponse
                    {
                        difficulty = Utils.BitsToDifficulty(getLastBlockResult.Result.BlockData.Header.Bits)
                    }
                );
            }
        }

        /// <summary>
        /// Check if the underlying bitprim node is running correctly.
        /// </summary>
        /// <param name="minimumSync"> Minimum required sync percentage (from 0 to 100) to consider node healthy. </param>
        /// <returns> "OK" if node healty, "NOK otherwise". </returns>
        [HttpGet("healthcheck")]
        [SwaggerOperation("GetHealthCheck")]
        [SwaggerResponse((int)System.Net.HttpStatusCode.OK, typeof(string))]
        public async Task<ActionResult> GetHealthCheck(float minimumSync)
        {
            dynamic syncStatus = await DoGetSyncStatus();
            bool isNumeric = Double.TryParse(syncStatus.syncPercentage, out double syncPercentage);
            bool isHealthy = isNumeric && syncPercentage > minimumSync;
            return isHealthy?
                StatusCode((int)System.Net.HttpStatusCode.OK, "OK"):
                StatusCode((int)System.Net.HttpStatusCode.PreconditionFailed, "NOK");
        }

        /// <summary>
        /// Get underlying node information.
        /// </summary>
        /// <returns> See GetInfoResponse DTO. </returns>
        [HttpGet("status/info")]
        [SwaggerOperation("GetInfo")]
        [SwaggerResponse((int)System.Net.HttpStatusCode.OK, typeof(GetInfoResponse))]
        public async Task<ActionResult> GetInfo()
        {
            using (var getLastBlockResult = await GetLastBlock())
            {
                return Json
                (
                    new GetInfoResponse
                    {
                        info = new GetInfoData
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

        /// <summary>
        /// Get latest block hash.
        /// </summary>
        /// <returns> Latest block hash. </returns>
        [HttpGet("status/lastblockhash")]
        [SwaggerOperation("GetLastBlockHash")]
        [SwaggerResponse((int)System.Net.HttpStatusCode.OK, typeof(GetLastBlockHashResponse))]
        public async Task<ActionResult> GetLastBlockHash()
        {
            using (var getLastBlockResult = await GetLastBlock())
            {
                var hashHexString = Binary.ByteArrayToHexString(getLastBlockResult.Result.BlockData.Hash);
                return Json
                (
                    new GetLastBlockHashResponse
                    {
                        syncTipHash = hashHexString,
                        lastblockhash = hashHexString
                    }
                );
            }
        }

        /// <summary>
        /// Get various node status information.
        /// (getInfo: see GetInfo method | getDifficulty: see GetDifficulty method | getBestBlockHash: see GetBestBlockHash method |
        ///  getLastBlockHash: see GetLastBlockHash method)
        /// </summary>
        /// <param name="method"> (getInfo | getDifficulty | getBestBlockHash | getLastBlockHash). Default: getInfo.
        /// Use the name 'q' for this query parameter (it will be mapped to the 'method' parameter).
        /// </param>
        /// <returns> Depends on method; see the referenced API method for each case. </returns>
        [HttpGet("status")]
        [ResponseCache(CacheProfileName = Constants.Cache.SHORT_CACHE_PROFILE_NAME)]
        [SwaggerOperation("GetStatus")]
        [SwaggerResponse((int)System.Net.HttpStatusCode.OK, typeof(object))]
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

        /// <summary>
        /// Get node synchronization status, as in how up to date it is with the blockchain.
        /// </summary>
        /// <returns> See GetSyncStatusResponse DTO. </returns>
        [HttpGet("sync")]
        [ResponseCache(CacheProfileName = Constants.Cache.SHORT_CACHE_PROFILE_NAME)]
        [SwaggerOperation("GetSyncStatus")]
        [SwaggerResponse((int)System.Net.HttpStatusCode.OK, typeof(GetSyncStatusResponse))]
        public async Task<ActionResult> GetSyncStatus()
        {
            return Json(await DoGetSyncStatus());
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
                var syncDataString = await httpClient_.GetStringAsync(config_.BlockchainHeightServiceUrl);
                JObject syncData = JsonConvert.DeserializeObject<JObject>(syncDataString);
                dynamic[] parsingExpression = JsonConvert.DeserializeObject<dynamic[]>(config_.BlockchainHeightParsingExpression);
                dynamic result = syncData;
                foreach(dynamic property in parsingExpression)
                {
                    if(property is String)
                    {
                        result = result[property];
                    }
                    else
                    {
                        result = result[(Int32)property];
                    }
                }
                blockChainHeight = Convert.ToUInt64(result);
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

        private static string GetNetworkType(NetworkType networkType)
        {
            switch (networkType)
            {
                case NetworkType.Mainnet:
                    return "livenet";
                default:
                    return networkType.ToString().ToLower();
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