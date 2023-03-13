using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Neo.Json;

namespace NexoAPI.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        [HttpPut("{address}/actions/sign-in")]
        public string SignInResult([FromBody] SignInViewModel body, string address)
        {
            return body.Nonce;
        }
    }

    public class SignInViewModel
    {
        public string Nonce { get; set; }
        public string Signature { get; set; }
        public string PublicKey { get; set; }
    }
}
