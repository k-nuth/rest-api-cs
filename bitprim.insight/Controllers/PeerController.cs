using System.Dynamic;
using Microsoft.AspNetCore.Mvc;

namespace bitprim.insight.Controllers
{
    [Route("api/[controller]")]
    public class PeerController : Controller
    {
        [HttpGet("/api/peer")]
        public ActionResult GetPeerStatus()
        {
            //TODO Get this information from node-cint
            dynamic peerStatus = new ExpandoObject();
            peerStatus.connected = true;
            peerStatus.host = "127.0.0.1";
            peerStatus.port = null;
            return Json(peerStatus);   
        }

        [HttpGet("/api/version")]
        public ActionResult GetApiVersion()
        {
            //TODO Implement versioning (RA-6)
            return Json(new{version = "0.1.0"});
        }
    }
}
