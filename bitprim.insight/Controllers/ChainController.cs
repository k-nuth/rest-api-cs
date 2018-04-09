using System;
using System.Collections.Generic;
using System.Dynamic;
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
        public ActionResult GetSyncStatus()
        {
            //TODO Try a more reliable way to know network max height (i.e. ask another node, or some service)
            ApiCallResult<UInt64> getLastHeightResult = chain_.GetLastHeight();
            Utils.CheckBitprimApiErrorCode(getLastHeightResult.ErrorCode, "GetLastHeight() failed");
            UInt64 currentHeight = getLastHeightResult.Result;
            bool synced = currentHeight >= config_.BlockchainHeight;
            dynamic syncStatus = new ExpandoObject();
            syncStatus.status = synced? "finished" : "synchronizing";
            syncStatus.blockChainHeight = config_.BlockchainHeight;
            syncStatus.syncPercentage = Math.Min((double)currentHeight / (double)config_.BlockchainHeight * 100.0, 100);
            syncStatus.error = null;
            syncStatus.type = config_.NodeType;
            return Json(syncStatus);   
        }

        [HttpGet("/api/status")]
        public ActionResult GetStatus(string method)
        {
            if(method == GET_DIFFICULTY)
            {
                return GetDifficulty();
            }
            else if(method == GET_BEST_BLOCK_HASH)
            {
                return GetBestBlockHash();
            }
            else if(method == GET_LAST_BLOCK_HASH)
            {
                return GetLastBlockHash();
            }
            else
            {
                return GetInfo();
            }   
        }

        [HttpGet("/api/utils/estimatefee")]
        public ActionResult GetEstimateFee([FromQuery] int? nbBlocks = 2)
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

        private ActionResult GetDifficulty()
        {
            using(DisposableApiCallResult<GetBlockDataResult<Block>> getLastBlockResult = GetLastBlock())
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

        private ActionResult GetBestBlockHash()
        {
            using(DisposableApiCallResult<GetBlockDataResult<Block>> getLastBlockResult = GetLastBlock())
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

        private ActionResult GetLastBlockHash()
        {
            using(DisposableApiCallResult<GetBlockDataResult<Block>> getLastBlockResult = GetLastBlock())
            {
                string hashHexString = Binary.ByteArrayToHexString(getLastBlockResult.Result.BlockData.Hash); 
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

        private ActionResult GetInfo()
        {
            using(DisposableApiCallResult<GetBlockDataResult<Block>> getLastBlockResult = GetLastBlock())
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

        private DisposableApiCallResult<GetBlockDataResult<Block>> GetLastBlock()
        {
            ApiCallResult<UInt64> getLastHeightResult = chain_.GetLastHeight();
            Utils.CheckBitprimApiErrorCode(getLastHeightResult.ErrorCode, "GetLastHeight() failed");
            UInt64 currentHeight = getLastHeightResult.Result;
            DisposableApiCallResult<GetBlockDataResult<Block>> getBlockResult = chain_.GetBlockByHeight(currentHeight);
            Utils.CheckBitprimApiErrorCode(getBlockResult.ErrorCode, "GetBlockByHeight(" + currentHeight + ") failed");
            return getBlockResult;
        }

    }
}