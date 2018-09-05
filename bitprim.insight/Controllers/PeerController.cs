using bitprim.insight.DTOs;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.SwaggerGen;
using bitprim.insight.Websockets;

namespace bitprim.insight.Controllers
{
    /// <summary>
    /// Peer/Bitprim node related operations.
    /// </summary>
    [Route("[controller]")]
    public class PeerController : Controller
    {
        /// <summary>
        /// Get bitprim-insight API version.
        /// </summary>
        /// <returns> See GetApiVersionResponse DTO. </returns>
        [HttpGet("version")]
        [ResponseCache(CacheProfileName = Constants.Cache.SHORT_CACHE_PROFILE_NAME)]
        [SwaggerOperation("GetApiVersion")]
        [SwaggerResponse((int)System.Net.HttpStatusCode.OK, typeof(GetApiVersionResponse))]
        public ActionResult GetApiVersion()
        {
            //TODO Implement versioning (RA-6)
            return Json(new GetApiVersionResponse
            {
                version = "1.0.0"
            });
        }

        /// <summary>
        /// Get peer/Bitprim node status information.
        /// </summary>
        /// <returns> See GetPeerStatusResponse DTO. </returns>
        [HttpGet("peer")]
        [ResponseCache(CacheProfileName = Constants.Cache.SHORT_CACHE_PROFILE_NAME)]
        [SwaggerOperation("GetPeerStatus")]
        [SwaggerResponse((int)System.Net.HttpStatusCode.OK, typeof(GetPeerStatusResponse))]
        public ActionResult GetPeerStatus()
        {
            //TODO Get this information from node-cint
            return Json(new GetPeerStatusResponse
            {
                connected = true,
                host = "127.0.0.1",
                port = null
            });
        }

        /// <summary>
        /// Get websocket stats.
        /// </summary>
        /// <returns> See WebSocketStatsDto. </returns>
        [HttpGet("stats")]
        [ResponseCache(CacheProfileName = Constants.Cache.SHORT_CACHE_PROFILE_NAME)]
        [SwaggerOperation("GetStats")]
        [SwaggerResponse((int)System.Net.HttpStatusCode.OK, typeof(WebSocketStatsDto))]
        public ActionResult GetStats()
        {
            return Json(new WebSocketStatsDto
            {
                wss_input_messages = WebSocketStats.InputMessages
                ,wss_output_messages=WebSocketStats.OutputMessages
                ,wss_pending_queue_size=WebSocketStats.PendingQueueSize
                ,wss_sent_messages=WebSocketStats.SendMessages
                ,wss_subscriber_count=WebSocketStats.SubscriberCount
            });
        }
    }
}
