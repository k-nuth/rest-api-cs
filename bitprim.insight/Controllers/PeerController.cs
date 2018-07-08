using bitprim.insight.DTOs;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Dynamic;

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

    }
}
