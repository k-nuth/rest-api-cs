using System.Dynamic;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace bitprim.insight.Controllers
{
    /// <summary>
    /// Message related operations.
    /// </summary>
    [Route("[controller]")]
    [ApiController]
    public class MessageController : Controller
    {
        /// <summary>
        /// Validate message.
        /// </summary>
        /// <param name="address"> Destination address. For BCH, cashaddr format is accepted. </param>
        /// <param name="signature"> To identify message sender, created using his private key. </param>
        /// <param name="message"> Message to verify. </param>
        [HttpGet("messages/verify")]
        [HttpPost("messages/verify")]
        [SwaggerOperation("VerifyMessage")]
        public ActionResult Verify(string address, string signature, string message)
        {
            //Dummy return
            return StatusCode((int)System.Net.HttpStatusCode.BadRequest, "Unexpected error:");
            
            //TODO full implementation
            //dynamic result = new ExpandoObject();
            //return Json(result);  
        }
    }
}