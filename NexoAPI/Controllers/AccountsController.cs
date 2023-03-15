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
        public async Task<ObjectResult> _([FromHeader] string authorization, string owner, int? skip, int? limit, string? cursor)
        {
            //Authorization 格式检查
            if (!authorization.StartsWith("Bearer "))
            {
                return StatusCode(StatusCodes.Status400BadRequest, new { code = 400, message = "Authorization format error", data = $"Authorization: {authorization}" });
            }

            //Authorization 有效性检查
            var token = authorization.Replace("Bearer ", "");
            var currentUser = _context.User.FirstOrDefault(p => p.Token == token);
            if (currentUser == null) 
            {
                return StatusCode(StatusCodes.Status400BadRequest, new { code = 400, message = "Authorization incorrect.", data = $"Authorization: {authorization}" });
            }

            //仅限当前用户等于owner参数
            if(currentUser.Address != owner)
            {
                return StatusCode(StatusCodes.Status400BadRequest, new { code = 400, message = "The 'owner' parameter must be the same as the current user's address", data = $"Owner: {owner}, Current User: {currentUser.Address}" });
            }

            var list = _context.Account.Include(p => p.Remark).Where(p => p.Owners == owner).OrderByDescending(p => p.Remark.FirstOrDefault(r => r.User == currentUser && r.Account == p).CreateTime);
            return new ObjectResult("");
        }

        // Swagger has bugs, do not test in swagger
        // https://github.com/domaindrivendev/Swashbuckle.AspNetCore/issues/1938#issuecomment-1205715331
        [HttpPost]
        public async Task<ObjectResult> _([FromHeader] string authorization, [FromBody]CreateAccountModels ca)
        {
            //Authorization 格式检查
            if (!authorization.StartsWith("Bearer "))
            {
                return StatusCode(StatusCodes.Status400BadRequest, new { code = 400, message = "Authorization format error", data = $"Authorization: {authorization}" });
            }

            //Authorization 有效性检查
            var token = authorization.Replace("Bearer ", "");
            var currentUser = _context.User.FirstOrDefault(p => p.Token == token);
            if (currentUser == null) 
            {
                return StatusCode(StatusCodes.Status400BadRequest, new { code = 400, message = "Authorization incorrect.", data = $"Authorization: {authorization}" });
            }

            //PublicKeys 检查
            var owners = new List<string>();
            foreach (var pubKey in ca.PublicKeys)
            {
                if (!PubKeyRegex().IsMatch(pubKey))
                    return StatusCode(StatusCodes.Status400BadRequest, new { code = 400, message = "Public key incorrect.", data = $"Public key: {pubKey}" });
                owners.Add(Contract.CreateSignatureContract(ECPoint.Parse(pubKey, ECCurve.Secp256r1)).ScriptHash.ToAddress(0x35));
            }
            owners.OrderBy(p => p);

            //仅限当前用户的publicKey在publicKeys参数中
            if (!ca.PublicKeys.Contains(currentUser.PublicKey))
            {
                return StatusCode(StatusCodes.Status400BadRequest, new { code = 400, message = "The current user's public key is not in the public key list.", data = $"Current user's public key: {currentUser.PublicKey}" });
            }

            //Threshold 检查
            if (ca.Threshold < 1 || ca.Threshold > ca.PublicKeys.Length)
            {
                return StatusCode(StatusCodes.Status400BadRequest, new { code = 400, message = $"Threshold incorrect.", data = $"Threshold: {ca.Threshold}" });
            }

            //创建多签账户
            var address = Contract.CreateMultiSigContract(ca.Threshold, ca.PublicKeys.ToList().ConvertAll(p => ECPoint.Parse(p, ECCurve.Secp256r1))).ScriptHash.ToAddress(0x35);
            var account = new Account() { Address = address, Owners = string.Join(',', owners.ToArray()), Threshold = ca.Threshold };

            //重复值检查
            if (_context.Account.Any(p => p.Address == address))
            {
                return StatusCode(StatusCodes.Status400BadRequest, new { code = 400, message = $"Account already exists", data = $"Address: {address}" });
            }

            _context.Account.Add(account);

            //创建备注
            _context.Remark.Add(new Remark() { User = currentUser, Account = account, RemarkName = ca.Remark, CreateTime = DateTime.Now });
            await _context.SaveChangesAsync();

            return new ObjectResult(new { });
        }

        [GeneratedRegex("^(0[23][0-9a-f]{64})+$")]
        private static partial Regex PubKeyRegex();
    }


    public class CreateAccountModels
    {
        public string[] PublicKeys { get; set; }

        public int Threshold { get; set; }

        public string Remark { get; set; }
    }
}
