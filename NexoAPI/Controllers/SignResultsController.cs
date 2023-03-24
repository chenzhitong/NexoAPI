using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Neo;
using NexoAPI.Data;
using NexoAPI.Models;

namespace NexoAPI.Controllers
{
    [Route("sign-results")]
    [Produces("application/json")]
    [ApiController]
    public class SignResultsController : ControllerBase
    {
        private readonly NexoAPIContext _context;

        public SignResultsController(NexoAPIContext context)
        {
            _context = context;
        }

        [HttpGet("{id}")]
        public ObjectResult GetSignResult([FromHeader] string authorization, string transactionHash)
        {
            //Authorization 格式检查
            if (!authorization.StartsWith("Bearer "))
            {
                return StatusCode(StatusCodes.Status400BadRequest, new { code = "InvalidParameter", message = "Authorization format error", data = $"Authorization: {authorization}" });
            }

            //Authorization 有效性检查
            var token = authorization.Replace("Bearer ", string.Empty);
            var currentUser = _context.User.FirstOrDefault(p => p.Token == token);
            if (currentUser is null)
            {
                return StatusCode(StatusCodes.Status400BadRequest, new { code = "TokenExpired", message = "Authorization incorrect.", data = $"Authorization: {authorization}" });
            }

            //transactionHash 检查
            var tx = _context.Transaction.Include(p => p.Account).FirstOrDefault(p => p.Hash == transactionHash);
            if (tx is null)
            {
                return StatusCode(StatusCodes.Status404NotFound, new { code = 404, message = $"Transaction {transactionHash} does not exist." });
            }

            //当前用户必须在该交易的所属账户的 owners 中
            if (!tx.Account.Owners.Contains(currentUser.Address))
            {
                return StatusCode(StatusCodes.Status400BadRequest, new { code = "Forbidden", message = $"The current user must be in the owners of the account to which the transaction belongs", data = $"Transaction.Account.Owners: {tx.Account.Owners}, Current User: {currentUser.Address}" });
            }

            var result = _context.SignResult.Where(p => p.Transaction.Hash == transactionHash).ToList().ConvertAll(p => new SignResultResponse() { TransactionHash = p.Transaction.Hash, Signer = p.Signer.Address, Approved = p.Approved, Signature = p.Signature });

            return new ObjectResult(result);
        }

        [HttpPut("{transactionHash}/{signer}")]
        public async Task<ObjectResult> PutSignResult([FromHeader] string authorization, [FromBody] SignResultRequest request, string transactionHash, string signer)
        {
            //Authorization 格式检查
            if (!authorization.StartsWith("Bearer "))
            {
                return StatusCode(StatusCodes.Status400BadRequest, new { code = "InvalidParameter", message = "Authorization format error", data = $"Authorization: {authorization}" });
            }

            //Authorization 有效性检查
            var token = authorization.Replace("Bearer ", string.Empty);
            var currentUser = _context.User.FirstOrDefault(p => p.Token == token);
            if (currentUser is null)
            {
                return StatusCode(StatusCodes.Status400BadRequest, new { code = "TokenExpired", message = "Authorization incorrect.", data = $"Authorization: {authorization}" });
            }

            //signer 检查
            if (currentUser.Address != signer)
            {
                return StatusCode(StatusCodes.Status400BadRequest, new { code = "Forbidden", message = $"Signer must be the current login account.", data = $"Signer: {signer}, Current User: {currentUser.Address}" });
            }

            //transactionHash 检查
            var tx = _context.Transaction.Include(p => p.Account).FirstOrDefault(p => p.Hash == transactionHash);
            if (tx is null)
            {
                return StatusCode(StatusCodes.Status404NotFound, new { code = 404, message = $"Transaction {transactionHash} does not exist." });
            }

            //signer 参数必须在该交易的所属账户的 owners 中
            if (!tx.Account.Owners.Contains(signer))
            {
                return StatusCode(StatusCodes.Status400BadRequest, new { code = "Forbidden", message = $"The signer parameter must be in the owners of the account to which the transaction belongs", data = $"Transaction.Account.Owners: {tx.Account.Owners}, Signer: {signer}" });
            }

            //Signature 检查
            if (!Helper.SignatureIsValid(request.Signature))
            {
                return StatusCode(StatusCodes.Status400BadRequest, new { code = "InvalidSignature", message = "Signature incorrect.", data = $"Signature: {request.Signature}" });
            }

            //重复值检查
            if (_context.SignResult.Include(p => p.Transaction).Any(p => p.Transaction.Hash == transactionHash && p.Signer.Address == currentUser.Address))
            {
                return StatusCode(StatusCodes.Status400BadRequest, new { code = "NotSatisfied", message = $"SignResult already exists", data = $"Transaction: {tx.Hash}, Signer: {currentUser.Address}" });
            }

            //Approved 检查
            if (!bool.TryParse(request.Approved, out bool approved))
            {
                return StatusCode(StatusCodes.Status400BadRequest, new { code = "InvalidParameter", message = "Approved incorrect.", data = $"Approved: {request.Approved}" });
            }

            if (approved)
            {
                //验证签名
                var message = Helper.GetSignData(new UInt256(tx.Hash.HexToBytes()));

                //也许不用验证
                if (!Helper.VerifySignature(message, currentUser.PublicKey, request.Signature))
                {
                    return StatusCode(StatusCodes.Status400BadRequest, new { code = "InvalidSignature", message = "Signature verification failure.", data = $"SignData: {message.ToHexString()}" });
                }
            }
            else 
            {
                //FeePayer 拒绝交易，改变交易状态为 Rejected
                if (currentUser.Address == tx.FeePayer)
                {
                    tx.Status = TransactionStatus.Rejected;
                    _context.Update(tx);
                }

                //拒绝人数超过可拒绝的最大人数，改变交易状态为 Rejected
                var maxRejectCount = tx.Account.Owners.Split(',').Length - tx.Account.Threshold;
                var currentRejectCount = _context.SignResult.Count(p => !p.Approved && p.Transaction.Hash == transactionHash) + 1;
                if (currentRejectCount > maxRejectCount)
                {
                    tx.Status = TransactionStatus.Rejected;
                    _context.Update(tx);
                }
            }

            var sr = new SignResult() { Approved = approved, Signature = request.Signature, Signer = currentUser, Transaction = tx };
            _context.SignResult.Add(sr);

            await _context.SaveChangesAsync();

            return new(new { });
        }
    }
}
