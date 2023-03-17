using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Akka.Actor;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NexoAPI.Data;
using NexoAPI.Models;

namespace NexoAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SignResultsController : ControllerBase
    {
        private readonly NexoAPIContext _context;

        public SignResultsController(NexoAPIContext context)
        {
            _context = context;
        }

        // GET: api/SignResults/5
        [HttpGet("{id}")]
        public async Task<ActionResult<SignResult>> GetSignResult(int id)
        {
          if (_context.SignResult == null)
          {
              return NotFound();
          }
            var signResult = await _context.SignResult.FindAsync(id);

            if (signResult == null)
            {
                return NotFound();
            }

            return signResult;
        }

        // PUT: api/SignResults/5
        [HttpPut("{transactionHash}/{signer}")]
        public async Task<ObjectResult> PutSignResult([FromHeader] string authorization, [FromBody]SignResultRequest request, string transactionHash, string signer)
        {
            //Authorization 格式检查
            if (!authorization.StartsWith("Bearer "))
            {
                return StatusCode(StatusCodes.Status400BadRequest, new { code = 400, message = "Authorization format error", data = $"Authorization: {authorization}" });
            }

            //Authorization 有效性检查
            var token = authorization.Replace("Bearer ", string.Empty);
            var currentUser = _context.User.FirstOrDefault(p => p.Token == token);
            if (currentUser is null)
            {
                return StatusCode(StatusCodes.Status400BadRequest, new { code = 400, message = "Authorization incorrect.", data = $"Authorization: {authorization}" });
            }

            //signer 检查
            if (currentUser.Address != signer)
            {
                return StatusCode(StatusCodes.Status400BadRequest, new { code = 400, message = $"Signer must be the current login account.", data = $"Signer: {signer}, Current User: {currentUser.Address}" });
            }

            //transactionHash 检查
            var tx = _context.Transaction.Include(p => p.Account).FirstOrDefault(p => p.Hash == transactionHash);
            if (tx is null)
            {
                return StatusCode(StatusCodes.Status404NotFound, new { code = 404, message = $"Transaction {transactionHash} does not exist."});
            }

            //signer 参数必须在该交易的所属账户的 owners 中
            if(!tx.Account.Owners.Contains(signer))
            {
                return StatusCode(StatusCodes.Status400BadRequest, new { code = 400, message = $"The signer parameter must be in the owners of the account to which the transaction belongs", data = $"Transaction.Account.Owners: {tx.Account.Owners}, Signer: {signer}" });
            }

            //Signature 检查
            if (!Helper.SignatureIsValid(request.Signature))
            {
                return StatusCode(StatusCodes.Status400BadRequest, new { code = 400, message = "Signature incorrect.", data = $"Signature: {request.Signature}" });
            }

            //Approved 检查
            if (!bool.TryParse(request.Approved, out bool approved))
            {
                return StatusCode(StatusCodes.Status400BadRequest, new { code = 400, message = "Approved incorrect.", data = $"Approved: {request.Approved}" });
            }

            //验证签名
            if (!Helper.VerifySignature(transactionHash.HexToBytes(), currentUser.PublicKey, request.Signature))
            {
                return StatusCode(StatusCodes.Status400BadRequest, new { code = 400, message = "Signature verification failure.", data = $"Message: {transactionHash}" });
            }

            var sr = new SignResult() { Approved = approved, Signature = request.Signature, Signer = currentUser, Transaction = tx };
            _context.SignResult.Add(sr);
            await _context.SaveChangesAsync();

            return new ObjectResult(new { });
        }
    }
}
