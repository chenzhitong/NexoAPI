using Microsoft.AspNetCore.Mvc;

namespace NexoAPI.Controllers
{
    [Route("[controller]")]
    [Produces("application/json")]
    [ApiController]
    public class NoncesController : ControllerBase
    {
        [HttpPost]
        public string PostNonce()
        {
            var nonce = Guid.NewGuid().ToString();
            Helper.Nonces.RemoveAll(p => (DateTime.UtcNow - p.CreateTime).TotalMinutes > 20);
            Helper.Nonces.Add(new Models.NonceInfo() { Nonce = nonce, CreateTime = DateTime.UtcNow });
            return nonce;
        }
    }
}