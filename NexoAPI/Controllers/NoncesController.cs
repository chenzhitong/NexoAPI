using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NexoAPI.Data;

namespace NexoAPI.Controllers
{
    [Route("[controller]")]
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
