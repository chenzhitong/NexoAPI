using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NexoAPI.Models;
using NexoAPI.Data;
using System.Text.RegularExpressions;
using Neo.SmartContract;
using Neo.Cryptography.ECC;
using Neo.Wallets;
using System.Net;
using Neo.Json;
using Akka.Actor;
using NuGet.Protocol;
using System.Security.Cryptography.X509Certificates;
using NexoAPI.Migrations;

namespace NexoAPI.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public partial class AccountsController : ControllerBase
    {
        private readonly NexoAPIContext _context;

        public AccountsController(NexoAPIContext context)
        {
            _context = context;
        }

        [HttpGet]
        [Produces("application/json")]
        public ObjectResult GetAccountList([FromHeader] string authorization, string owner, int? skip, int? limit, string? cursor)
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

            var list = new List<Account>();

            //根据 cursor 筛选符合条件的 Accounts
            if (cursor is not null)
            {
                var cursorJson = JObject.Parse(cursor);
                var cursorTime = DateTime.UtcNow;

                //createTime 检查
                try
                {
                    //按时间倒序排序后，筛选早于等于 Cursor CreateTime 时间的数据
                    cursorTime = DateTime.Parse(cursorJson["createTime"].AsString());
                    list = _context.Account.Include(p => p.Remark).Where(p => !p.Remark.First(r => r.User == currentUser).IsDeleted).Where(p => p.Owners.Contains(owner)).Where(p => p.Remark.First(r => r.User == currentUser).CreateTime <= cursorTime).OrderByDescending(p => p.Remark.First(r => r.User == currentUser).CreateTime).ThenBy(p => p.Address).ToList();
                }
                catch (Exception)
                {
                    return StatusCode(StatusCodes.Status400BadRequest, new { code = 400, message = "The createTime in cursor is incorrect.", data = $"createTime: {cursorJson["createTime"]}" });
                }

                //address 检查
                var address = cursorJson["address"].AsString();
                try
                {
                    address.ToScriptHash(0x35);
                }
                catch (Exception)
                {
                    return StatusCode(StatusCodes.Status400BadRequest, new { code = 400, message = "Address is incorrect.", data = $"Address: {address}" });
                }
                //按时间倒序排序后，筛选从 Cursor Address 开始（含）的数据
                var startIndex = list.FindIndex(p => p.Address == address);
                if (startIndex > 0)
                    list.RemoveRange(0, startIndex);
            }
            else
            {
                list = _context.Account.Include(p => p.Remark).Where(p => !p.Remark.First(r => r.User == currentUser).IsDeleted).Where(p => p.Owners.Contains(owner)).OrderByDescending(p => p.Remark.First(r => r.User == currentUser).CreateTime).ThenBy(p => p.Address).ToList();
            }

            var result = list.Skip(skip ?? 0).Take(limit ?? 100).ToList().ConvertAll(p => new AccountResponse(p));

            return new ObjectResult(result);
        }

        [HttpGet("{address}")]
        [Produces("application/json")]
        public async Task<ObjectResult> GetAccount([FromHeader] string authorization, string address)
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

            var account = _context.Account.Include(p => p.Remark).Where(p => !p.Remark.First(r => r.User == currentUser).IsDeleted).FirstOrDefault(p => p.Address == address);

            //Address 检查
            if (account is null)
            {
                return StatusCode(StatusCodes.Status404NotFound, new { code = 404, message = $"Multi-Sign Address {address} does not exist." });
            }

            //仅限当前用户等于owner参数
            if (!account.Owners.Contains(currentUser.Address))
            {
                return StatusCode(StatusCodes.Status400BadRequest, new { code = 400, message = "The current user must be in the owners of the requested address account", data = $"Current User: {currentUser.Address}" });
            }

            return new ObjectResult(new AccountResponse(account));
        }

        // Swagger has bugs, do not test in swagger
        // https://github.com/domaindrivendev/Swashbuckle.AspNetCore/issues/1938#issuecomment-1205715331
        [HttpPost]
        public async Task<ObjectResult> PostAccount([FromHeader] string authorization, [FromBody] AccountRequest request)
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

            //PublicKeys 检查
            var owners = new List<string>();
            foreach (var pubKey in request.PublicKeys)
            {
                if (!Helper.PublicKeyIsValid(pubKey))
                    return StatusCode(StatusCodes.Status400BadRequest, new { code = 400, message = "Public key incorrect.", data = $"Public key: {pubKey}" });
                owners.Add(Contract.CreateSignatureContract(ECPoint.Parse(pubKey, ECCurve.Secp256r1)).ScriptHash.ToAddress(0x35));
            }
            owners.OrderBy(p => p);

            //仅限当前用户的publicKey在publicKeys参数中
            if (!request.PublicKeys.Contains(currentUser.PublicKey))
            {
                return StatusCode(StatusCodes.Status400BadRequest, new { code = 400, message = "The current user's public key is not in the public key list.", data = $"Current user's public key: {currentUser.PublicKey}" });
            }

            //Threshold 检查
            if (request.Threshold < 1 || request.Threshold > request.PublicKeys.Length)
            {
                return StatusCode(StatusCodes.Status400BadRequest, new { code = 400, message = $"Threshold incorrect.", data = $"Threshold: {request.Threshold}" });
            }

            //创建多签账户
            var account = new Account()
            {
                Owners = string.Join(',', request.PublicKeys.ToArray()),
                Threshold = request.Threshold,
                Address = Contract.CreateMultiSigContract(request.Threshold, request.PublicKeys.ToList().ConvertAll(p => ECPoint.Parse(p, ECCurve.Secp256r1))).ScriptHash.ToAddress(0x35)
            };

            //重复值检查
            if (_context.Account.Any(p => p.Address == account.Address))
            {
                return StatusCode(StatusCodes.Status400BadRequest, new { code = 400, message = $"Account already exists", data = $"Address: {account.Address}" });
            }

            _context.Account.Add(account);

            //创建备注

            _context.Remark.Add(new Remark()
            {
                User = currentUser,
                Account = account,
                RemarkName = request.Remark,
                CreateTime = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            return new ObjectResult(new { });
        }

        [HttpDelete("{address}")]
        public async Task<ObjectResult> DeleteAccount([FromHeader] string authorization, string address)
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

            var account = _context.Account.Include(p => p.Remark).Where(p => !p.Remark.First(r => r.User == currentUser).IsDeleted).FirstOrDefault(p => p.Address == address);

            //Address 检查
            if (account is null)
            {
                return StatusCode(StatusCodes.Status404NotFound, new { code = 404, message = $"Multi-Sign Address {address} does not exist." });
            }

            //仅限当前用户等于owner参数
            if (!account.Owners.Contains(currentUser.Address))
            {
                return StatusCode(StatusCodes.Status400BadRequest, new { code = 400, message = "The current user must be in the owners of the requested address account", data = $"Current User: {currentUser.Address}" });
            }

            //软删除
            var remark = _context.Remark.FirstOrDefault(p => p.Account.Address == address && p.User.Address == currentUser.Address);
            remark.IsDeleted = true;
            await _context.SaveChangesAsync();

            return new ObjectResult(new { });
        }

        [HttpPut("{address}/actions/set-remark")]
        public async Task<ObjectResult> PutAccount([FromHeader] string authorization, [FromBody] SetRemarkViewModel body, string address)
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

            var account = _context.Account.Include(p => p.Remark).Where(p => !p.Remark.First(r => r.User == currentUser).IsDeleted).FirstOrDefault(p => p.Address == address);

            //Address 检查
            if (account is null)
            {
                return StatusCode(StatusCodes.Status404NotFound, new { code = 404, message = $"Multi-Sign Address {address} does not exist." });
            }

            //仅限当前用户等于owner参数
            if (!account.Owners.Contains(currentUser.Address))
            {
                return StatusCode(StatusCodes.Status400BadRequest, new { code = 400, message = "The current user must be in the owners of the requested address account", data = $"Current User: {currentUser.Address}" });
            }

            //修改备注
            var remark = _context.Remark.FirstOrDefault(p => p.Account.Address == address && p.User.Address == currentUser.Address);
            remark.RemarkName = body.Remark;
            await _context.SaveChangesAsync();

            return new ObjectResult(new { });
        }
    }

    public class SetRemarkViewModel
    {
        public string Remark { get; set; }
    }
}
