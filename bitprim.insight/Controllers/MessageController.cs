using Bitprim;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace bitprim.insight.Controllers
{
    /// <summary>
    /// Message related operations.
    /// </summary>
    [Route("[controller]")]
    public class MessageController : Controller
    {
        /// <summary>
        /// Validate message.
        /// </summary>
        /// <param name="address"> Destination address. For BCH, cashaddr format is accepted. </param>
        /// <param name="signature"> To identify message sender, created using his private key. </param>
        /// <param name="message"> Message to verify. </param>
        [HttpGet("messages/verify")]
        [SwaggerOperation("VerifyMessage")]
        public ActionResult Verify(string address, string signature, string message)
        {
            if( !Validations.IsValidPaymentAddress(address) )
            {
                return BadRequest(address + " is not a valid address");
            }
            //TODO Validate signature
            return VerifyMessage(address, signature, message);  
        }

        /// <summary>
        /// Validate message.
        /// </summary>
        /// <param name="address"> Destination address. For BCH, cashaddr format is accepted. </param>
        /// <param name="signature"> To identify message sender, created using his private key. </param>
        /// <param name="message"> Message to verify. </param>
        [HttpPost("messages/verify")]
        [SwaggerOperation("VerifyMessagePost")]
        public ActionResult VerifyPost(string address, string signature, string message)
        {
            if( !Validations.IsValidPaymentAddress(address) )
            {
                return BadRequest(address + " is not a valid address");
            }
            //TODO Validate signature
            return VerifyMessage(address, signature, message);
        }

        private ActionResult VerifyMessage(string address, string signature, string message)
        {
            //Dummy return
            return BadRequest("Unexpected error:");
            
            //TODO full implementation
            //dynamic result = new ExpandoObject();
            //return Json(result);
        }
    }
}