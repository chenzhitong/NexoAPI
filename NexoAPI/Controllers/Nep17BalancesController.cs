using Microsoft.AspNetCore.Mvc;

namespace NexoAPI.Controllers
{
    [Route("/nep17-balances")]
    [ApiController]
    public class Nep17BalancesController : Controller
    {
        [HttpGet]
        public ObjectResult GetList(string address)
        {
            return new ObjectResult("abc");
        }
    }
}
