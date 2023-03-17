using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Akka.Actor;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Neo;
using Neo.Json;
using NexoAPI.Data;
using NexoAPI.Models;

namespace NexoAPI.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class TransactionsController : ControllerBase
    {
        private readonly NexoAPIContext _context;

        public TransactionsController(NexoAPIContext context)
        {
            _context = context;
        }

        // GET: api/Transactions
        [HttpGet]
        public ObjectResult GetTransactionList([FromHeader] string authorization, string account, string owner, string signable, int? skip, int? limit, string? cursor)
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

            //仅限当前用户等于owner参数
            if (currentUser.Address != owner)
            {
                return StatusCode(StatusCodes.Status400BadRequest, new { code = 400, message = "The 'owner' parameter must be the same as the current user's address", data = $"Owner: {owner}, Current User: {currentUser.Address}" });
            }

            //account 检查
            var accountItem = _context.Account.FirstOrDefault(p => p.Address == account);
            if (accountItem is null)
            {
                return StatusCode(StatusCodes.Status404NotFound, new { code = 404, message = $"Multi-Sign Address {account} does not exist." });
            }

            //owner 参数必须在该账户的 owners 中
            if(!accountItem.Owners.Contains(owner))
            {
                return StatusCode(StatusCodes.Status400BadRequest, new { code = 400, message = "The owner parameter must be in the owners of the account" });
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

            //根据 cursor 筛选符合条件的 Transaction
            if (cursor is not null)
            {
                var cursorJson = JObject.Parse(cursor);
                var cursorTime = DateTime.UtcNow;

                //createTime 检查
                try
                {
                    //按时间倒序排序后，筛选早于等于 Cursor CreateTime 时间的数据
                    cursorTime = DateTime.Parse(cursorJson["createTime"].AsString());
                    list = list.Where(p => p.CreateTime <= cursorTime).ToList();
                }
                catch (Exception)
                {
                    return StatusCode(StatusCodes.Status400BadRequest, new { code = 400, message = "The createTime in cursor is incorrect.", data = $"createTime: {cursorJson["createTime"]}" });
                }

                //按时间倒序排序后，筛选从 Cursor hash 开始（含）的数据
                var startIndex = list.FindIndex(p => p.Hash == cursorJson["hash"]?.AsString());
                if (startIndex > 0)
                    list.RemoveRange(0, startIndex);
            }

            var result = list.Skip(skip ?? 0).Take(limit ?? 100).ToList().ConvertAll(p => new TransactionResponse(p));

            return new ObjectResult(result);
        }

        // POST: api/Transactions
        [HttpPost]
        public async Task<ObjectResult> PostTransaction([FromHeader] string authorization, TransactionRequest request)
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

            //account 检查
            var accountItem = _context.Account.FirstOrDefault(p => p.Address == request.Account);
            if (accountItem is null)
            {
                return StatusCode(StatusCodes.Status404NotFound, new { code = 404, message = $"Multi-Sign Address {request.Account} does not exist." });
            }

            //当前用户的地址必须在该账户的 owners 中
            if (!accountItem.Owners.Contains(currentUser.Address))
            {
                return StatusCode(StatusCodes.Status400BadRequest, new { code = 400, message = "The current user's address must be in the owners of the account" });
            }

            //feePayer 检查
            var feePayerItem = _context.User.FirstOrDefault(p => p.Address == request.FeePayer);
            if (feePayerItem is null)
            {
                return StatusCode(StatusCodes.Status404NotFound, new { code = 404, message = $"FeePayer {request.FeePayer} does not exist." });
            }

            //feePayer 必须等于该账户或在该账户的 owners 中
            if (request.Account != request.FeePayer && !accountItem.Owners.Contains(request.FeePayer))
            {
                return StatusCode(StatusCodes.Status400BadRequest, new { code = 400, message = "FeePayer must be equal to the account or in the owners of the account", data = $"FeePayer: {request.FeePayer}" });
            }

            var tx = new Transaction()
            {
                Account = accountItem,
                FeePayer = feePayerItem,
                ContractHash = request.ContractHash,
            };

            if (Enum.TryParse(request.Type, out TransactionType type))
            {
                tx.Type = type;
                if (type == TransactionType.Invocation)
                {
                    tx.Operation = request.Operation;
                    tx.Params = request.Params;
                }
                else if (type == TransactionType.Nep17Transfer)
                {
                    tx.Amount = request.Amount;
                    tx.Destination = request.Destination;
                }
            }
            else
            {
                return StatusCode(StatusCodes.Status400BadRequest, new { code = 400, message = "Type is incorrect.", data = $"Type: {request.Type}" });
            }

            _context.Transaction.Add(tx);
            await _context.SaveChangesAsync();

            return new ObjectResult(new { });
        }

        [HttpPost("Test")]
        public async Task<ObjectResult> Test()
        {
            var blockcount = await Helper.Client.GetBlockCountAsync().ConfigureAwait(false);
            return new ObjectResult(blockcount);
        }
    }
}
