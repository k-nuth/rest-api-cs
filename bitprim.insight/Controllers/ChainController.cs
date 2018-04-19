using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Threading.Tasks;
using Bitprim;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace bitprim.insight.Controllers
{
    [Route("api/[controller]")]
    public class ChainController : Controller
    {
        private Chain chain_;
        private Executor nodeExecutor_;
        private readonly NodeConfig config_;
        private const string GET_BEST_BLOCK_HASH = "getBestBlockHash";
        private const string GET_LAST_BLOCK_HASH = "getLastBlockHash";
        private const string GET_DIFFICULTY = "getDifficulty";

        public ChainController(IOptions<NodeConfig> config, Executor executor)
        {
            config_ = config.Value;
            nodeExecutor_ = executor;
            chain_ = executor.Chain;
        }

        [HttpGet("/api/sync")]
        public async Task<ActionResult> GetSyncStatus()
        {
            //TODO Try a more reliable way to know network max height (i.e. ask another node, or some service)
            var getLastHeightResult = await chain_.FetchLastHeightAsync();
            Utils.CheckBitprimApiErrorCode(getLastHeightResult.ErrorCode, "GetLastHeight() failed");
            
            var currentHeight = getLastHeightResult.Result;
            var synced = currentHeight >= config_.BlockchainHeight;
            
            dynamic syncStatus = new ExpandoObject();
            syncStatus.status = synced? "finished" : "synchronizing";
            syncStatus.blockChainHeight = config_.BlockchainHeight;
            syncStatus.syncPercentage = Math.Min((double)currentHeight / (double)config_.BlockchainHeight * 100.0, 100);
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
    }
}