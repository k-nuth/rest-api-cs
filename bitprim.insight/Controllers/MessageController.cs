using System.Dynamic;
using Microsoft.AspNetCore.Mvc;

namespace bitprim.insight.Controllers
{
    [Route("[controller]")]
    public class MessageController : Controller
    {
        [HttpGet("messages/verify")]
        [HttpPost("messages/verify")]
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