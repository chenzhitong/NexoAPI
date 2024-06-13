using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NexoAPI.Data;
using NexoAPI.Models;

namespace NexoAPI.Controllers
{
    [Route("transactions-count")]
    [Produces("application/json")]
    [ApiController]
    public class TransactionsCountController : ControllerBase
    {
        private readonly NexoAPIContext _context;

        public TransactionsCountController(NexoAPIContext context)
        {
            _context = context;
        }

        [HttpGet]
        public ObjectResult GetTransactionCount(string account, string owner, string? signable)
        {
            //Authorization 格式检查
            var authorization = Request.Headers["Authorization"].ToString();
            if (!Helper.AuthorizationIsValid(authorization, out string token))
            {
                return StatusCode(StatusCodes.Status400BadRequest, new { code = "Unauthorized", message = Helper.AuthFormatError, data = $"Authorization: {authorization}" });
            }

            //Authorization 有效性检查
            var currentUser = _context.User.FirstOrDefault(p => p.Token == token);
            if (currentUser is null)
            {
                return StatusCode(StatusCodes.Status400BadRequest, new { code = "TokenExpired", message = "Authorization incorrect.", data = $"Token: {token}" });
            }

            //仅限当前用户等于owner参数
            if (currentUser.Address != owner)
            {
                return StatusCode(StatusCodes.Status400BadRequest, new { code = "Forbidden", message = "The 'owner' parameter must be the same as the current user's address", data = $"Owner: {owner}, Current User: {currentUser.Address}" });
            }

            //account 检查
            var accountItem = _context.Account.FirstOrDefault(p => p.Address == account);
            if (accountItem is null)
            {
                return StatusCode(StatusCodes.Status400BadRequest, new { code = "NotFound", message = $"Multi-Sign Address {account} does not exist." });
            }

            //owner 参数必须在该账户的 owners 中
            if (!accountItem.Owners.Contains(owner))
            {
                return StatusCode(StatusCodes.Status400BadRequest, new { code = "Forbidden", message = "The owner parameter must be in the owners of the account" });
            }

            var list = new List<Transaction>();

            if (bool.TryParse(signable, out bool signableBool))
            {
                list = signableBool ?
                    //交易是Signing状态且该owner没签过名
                    _context.Transaction.Include(p => p.Account).Include(p => p.SignResult)
                    .Where(p => p.Account.Address == account)
                    .Where(p => p.Status == TransactionStatus.Signing && !p.SignResult.Any(p => p.Signer.Address.Contains(owner))).ToList() :
                    //交易不是Signing状态或该owner签过名
                    _context.Transaction.Include(p => p.Account).Include(p => p.SignResult)
                    .Where(p => p.Account.Address == account)
                    .Where(p => p.Status != TransactionStatus.Signing || p.SignResult.Any(p => p.Signer.Address.Contains(owner)))
                    .OrderByDescending(p => p.CreateTime).ThenBy(p => p.Hash).ToList();
            }
            else
            {
                list = _context.Transaction.Include(p => p.Account).Include(p => p.SignResult)
                    .Where(p => p.Account.Address == account)
                    .OrderByDescending(p => p.CreateTime).ThenBy(p => p.Hash).ToList();
            }
            return new ObjectResult(list.Count);
        }
    }
}