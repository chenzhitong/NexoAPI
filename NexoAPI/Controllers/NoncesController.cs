using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace NexoAPI.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class NoncesController : ControllerBase
    {
        [HttpPost]
        public string GenerateGUID()
        {
            var nonce = Guid.NewGuid().ToString();
            Helper.Nonces.Add(new Models.NonceInfo() { Nonce = nonce, CreateTime = DateTime.UtcNow });
            return nonce;
        }
    }
}
