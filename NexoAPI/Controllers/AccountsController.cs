using Akka.Actor;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Neo.Cryptography.ECC;
using Neo.SmartContract;
using Neo.Wallets;
using Newtonsoft.Json.Linq;
using NexoAPI.Data;
using NexoAPI.Migrations;
using NexoAPI.Models;
using System.Net;

namespace NexoAPI.Controllers
{
    [Route("[controller]")]
    [Produces("application/json")]
    [ApiController]
    public partial class AccountsController : ControllerBase
    {
        private readonly NexoAPIContext _context;

        public AccountsController(NexoAPIContext context)
        {
            _context = context;
        }

        [HttpGet]
        public ObjectResult GetAccountList(string owner, int? skip, int? limit, string? cursor)
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
                    cursorTime = DateTime.Parse(cursorJson?["createTime"]?.ToString() ?? DateTime.UtcNow.ToString());
                    list = _context.Account.Include(p => p.Remark).Where(p => !p.Remark.Any(r => r.User == currentUser) || !p.Remark.First(r => r.User == currentUser).IsDeleted).Where(p => p.Owners.Contains(owner)).Where(p => p.Remark.First(r => r.User == currentUser).CreateTime <= cursorTime).OrderByDescending(p => p.Remark.First(r => r.User == currentUser).CreateTime).ThenBy(p => p.Address).ToList();
                }
                catch (Exception)
                {
                    return StatusCode(StatusCodes.Status400BadRequest, new { code = "InvalidParameter", message = "The createTime in cursor is incorrect.", data = $"createTime: {cursorJson["createTime"]}" });
                }

                //address 检查
                var address = cursorJson?["address"]?.ToString() ?? string.Empty;
                try
                {
                    address.ToScriptHash();
                }
                catch (Exception)
                {
                    return StatusCode(StatusCodes.Status400BadRequest, new { code = "InvalidParameter", message = "Address is incorrect.", data = $"Address: {address}" });
                }
                //按时间倒序排序后，筛选从 Cursor Address 开始（含）的数据
                var startIndex = list.FindIndex(p => p.Address == address);
                if (startIndex > 0)
                    list.RemoveRange(0, startIndex);
            }
            else
            {
                list = _context.Account.Include(p => p.Remark).Where(p => !p.Remark.Any(r => r.User == currentUser) || !p.Remark.First(r => r.User == currentUser).IsDeleted).Where(p => p.Owners.Contains(owner)).OrderByDescending(p => p.Remark.First(r => r.User == currentUser).CreateTime).ThenBy(p => p.Address).ToList();
            }
            var result = list.Skip(skip ?? 0).Take(limit ?? 100).ToList().ConvertAll(p => new AccountResponse(p, currentUser));
            return new ObjectResult(result);
        }

        [HttpGet("{address}")]
        public ObjectResult GetAccount(string address)
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

            var account = _context.Account.Include(p => p.Remark).FirstOrDefault(p => p.Address == address);

            //Address 检查
            if (account is null)
            {
                return StatusCode(StatusCodes.Status400BadRequest, new { code = "NotFound", message = $"Multi-Sign Address {address} does not exist." });
            }
            //Address 不能被标记为已删除
            if (account.Remark.FirstOrDefault(p => p.User == currentUser)?.IsDeleted == true)
            {
                return StatusCode(StatusCodes.Status400BadRequest, new { code = "NotFound", message = $"Multi-Sign Address {address} is deleted." });
            }

            //仅限当前用户等于owner参数
            if (!account.Owners.Contains(currentUser.Address))
            {
                return StatusCode(StatusCodes.Status400BadRequest, new { code = "Forbidden", message = "The current user must be in the owners of the requested address account", data = $"Current User: {currentUser.Address}" });
            }

            account.Nep17ValueUsd = Helper.GetNep17AssetsValue(address);
            return new ObjectResult(new AccountResponse(account, currentUser));
        }

        [HttpGet("valuation-test/{address}")]
        public ObjectResult GetAccountValuation(string address) => new(Helper.GetNep17AssetsValue(address));

        [HttpPost]
        public async Task<ObjectResult> PostAccount([FromBody] AccountRequest request)
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

            //PublicKeys 检查
            var owners = new List<string>();
            foreach (var pubKey in request.PublicKeys)
            {
                if (!Helper.PublicKeyIsValid(pubKey))
                    return StatusCode(StatusCodes.Status400BadRequest, new { code = "InvalidParameter", message = "Public key incorrect.", data = $"Public key: {pubKey}" });
                owners.Add(Contract.CreateSignatureContract(ECPoint.Parse(pubKey, ECCurve.Secp256r1)).ScriptHash.ToAddress());
            }
            owners = owners.OrderBy(p => p).Distinct().ToList();

            //PublicKeys不允许重复
            if (owners.Count != request.PublicKeys.Length)
            {
                return StatusCode(StatusCodes.Status400BadRequest, new { code = "InvalidParameter", message = "Public keys are not allowed to be the same.", data = string.Empty });
            }

            //仅限当前用户的publicKey在publicKeys参数中
            if (!request.PublicKeys.Contains(currentUser.PublicKey))
            {
                return StatusCode(StatusCodes.Status400BadRequest, new { code = "Forbidden", message = "The current user's public key is not in the public key list.", data = $"Current user's public key: {currentUser.PublicKey}" });
            }

            //Threshold 检查
            if (request.Threshold < 1 || request.Threshold > request.PublicKeys.Length)
            {
                return StatusCode(StatusCodes.Status400BadRequest, new { code = "InvalidParameter", message = $"Threshold incorrect.", data = $"Threshold: {request.Threshold}" });
            }

            //创建多签账户
            var account = new Account()
            {
                Owners = string.Join(',', owners),
                PublicKeys = string.Join(',', request.PublicKeys),
                Threshold = request.Threshold,
                Address = Contract.CreateMultiSigContract(request.Threshold, request.PublicKeys.ToList().ConvertAll(p => ECPoint.Parse(p, ECCurve.Secp256r1))).ScriptHash.ToAddress()
            };
            var accountItem = _context.Account.FirstOrDefault(p => p.Address == account.Address);
            //重复值检查
            if (accountItem is null)
            {
                _context.Account.Add(account);
            }
            //创建和修改备注
            var remark = _context.Remark.FirstOrDefault(p => p.Account.Address == account.Address && p.User.Address == currentUser.Address);
            if (remark is null)
            {
                _context.Remark.Add(new Remark()
                {
                    User = currentUser,
                    Account = account,
                    RemarkName = request.Remark,
                    CreateTime = DateTime.UtcNow,
                    IsDeleted = false
                });
            }
            else
            {
                remark.RemarkName = request.Remark;
                remark.IsDeleted = false;
                _context.Update(remark);
            }

            await _context.SaveChangesAsync();

            return new(account.Address);
        }

        [HttpDelete("{address}")]
        public async Task<ObjectResult> DeleteAccount(string address)
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

            var account = _context.Account.Include(p => p.Remark).FirstOrDefault(p => p.Address == address);

            //Address 检查
            if (account is null)
            {
                return StatusCode(StatusCodes.Status400BadRequest, new { code = "NotFound", message = $"Multi-Sign Address {address} does not exist." });
            }
            //Address 不能被标记为已删除
            if (account.Remark.FirstOrDefault(p => p.User == currentUser)?.IsDeleted == true)
            {
                return StatusCode(StatusCodes.Status400BadRequest, new { code = "NotFound", message = $"Multi-Sign Address {address} is deleted." });
            }

            //仅限当前用户等于owner参数
            if (!account.Owners.Contains(currentUser.Address))
            {
                return StatusCode(StatusCodes.Status400BadRequest, new { code = "Forbidden", message = "The current user must be in the owners of the requested address account", data = $"Current User: {currentUser.Address}" });
            }

            //软删除
            var remark = _context.Remark.FirstOrDefault(p => p.Account.Address == address && p.User.Address == currentUser.Address);
            if (remark is null)
            {
                _context.Remark.Add(new Remark()
                {
                    User = currentUser,
                    Account = account,
                    RemarkName = string.Empty,
                    CreateTime = DateTime.UtcNow,
                    IsDeleted = true
                });
            }
            else
            {
                remark.IsDeleted = true;
            }
            await _context.SaveChangesAsync();

            return new(new { });
        }

        [HttpPut("{address}/actions/set-remark")]
        public async Task<ObjectResult> PutAccount([FromBody] SetRemarkViewModel body, string address)
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

            var account = _context.Account.Include(p => p.Remark).FirstOrDefault(p => p.Address == address);

            //Address 检查
            if (account is null)
            {
                return StatusCode(StatusCodes.Status400BadRequest, new { code = "NotFound", message = $"Multi-Sign Address {address} does not exist." });
            }
            //Address 不能被标记为已删除
            if (account.Remark.FirstOrDefault(p => p.User == currentUser)?.IsDeleted == true)
            {
                return StatusCode(StatusCodes.Status400BadRequest, new { code = "NotFound", message = $"Multi-Sign Address {address} is deleted." });
            }

            //仅限当前用户等于owner参数
            if (!account.Owners.Contains(currentUser.Address))
            {
                return StatusCode(StatusCodes.Status400BadRequest, new { code = "Forbidden", message = "The current user must be in the owners of the requested address account", data = $"Current User: {currentUser.Address}" });
            }

            //修改备注
            var remark = _context.Remark.FirstOrDefault(p => p.Account.Address == address && p.User.Address == currentUser.Address);
            if (remark is null)
            {
                _context.Remark.Add(new Remark()
                {
                    User = currentUser,
                    Account = account,
                    RemarkName = body.Remark,
                    CreateTime = DateTime.UtcNow,
                    IsDeleted = false
                });
            }
            else
            {
                remark.RemarkName = body.Remark;
                remark.IsDeleted = false;
                _context.Update(remark);
            }
            await _context.SaveChangesAsync();

            return new(new { });
        }
    }

    public class SetRemarkViewModel
    {
        public string? Remark { get; set; }
    }
}
