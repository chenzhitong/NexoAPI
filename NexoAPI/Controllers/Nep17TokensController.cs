using Microsoft.AspNetCore.Mvc;

namespace NexoAPI.Controllers
{
    [Route("nep17-tokens")]
    [ApiController]
    public class Nep17TokensController : Controller
    {
        [HttpGet]
        public ObjectResult GetList([FromQuery] string[] contractHashes)
        {
            return new ObjectResult("abc");
        }
    }
}
