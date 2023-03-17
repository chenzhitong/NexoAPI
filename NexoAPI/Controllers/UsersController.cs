using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Neo.Json;
using Neo.SmartContract;
using Neo.Cryptography.ECC;
using Neo.Wallets;
using NuGet.Protocol;
using NexoAPI.Data;
using Microsoft.EntityFrameworkCore;
using NexoAPI.Models;
using System.Security.Cryptography;
using System.Text;
using Akka.Actor;
using Akka.IO;
using Neo.Cryptography;
using System.Security.Policy;
using Neo;
using Neo.IO;
using NexoAPI.Migrations;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;

namespace NexoAPI.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private readonly NexoAPIContext _context;

        public UsersController(NexoAPIContext context)
        {
            _context = context;
        }

        [HttpPut("{address}/actions/sign-in")]
        public async Task<ObjectResult> PutUser([FromBody] UserRequest request, string address)
        {
            //address 检查
            try
            {
                address.ToScriptHash(0x35);
            }
            catch (Exception)
            {
                return StatusCode(StatusCodes.Status400BadRequest, new { code = 400, message = "Address is incorrect.", data = $"Address: {address}" });
            }

            //nonce 检查
            var nonce = Helper.Nonces.FirstOrDefault(p => p.Nonce == request.Nonce);
            if (nonce is null)
            {
                return StatusCode(StatusCodes.Status400BadRequest, new { code = 400, message = "Unauthorized, nonce is incorrect.", data = $"Nonce: {request.Nonce}" });
            }

            //nonce 有效期检查
            if ((DateTime.UtcNow - nonce.CreateTime).TotalMinutes > 20)
            {
                return StatusCode(StatusCodes.Status400BadRequest, new { code = 400, message = "Unauthorized, nonce has been expired.", data = $"Nonce create time: {nonce.CreateTime}" });
            }

            //publicKey 检查
            if (!Helper.PublicKeyIsValid(request.PublicKey))
            {
                return StatusCode(StatusCodes.Status400BadRequest, new { code = 400, message = "Public key incorrect.", data = $"Public key: {request.PublicKey}" });
            }

            //signature 检查
            if (!Helper.SignatureIsValid(request.Signature))
            {
                return StatusCode(StatusCodes.Status400BadRequest, new { code = 400, message = "Signature incorrect.", data = $"Signature: {request.Signature}" });
            }

            //检查公钥和地址是否匹配
            var publicKeyToAddress = Contract.CreateSignatureContract(Neo.Cryptography.ECC.ECPoint.Parse(request.PublicKey, Neo.Cryptography.ECC.ECCurve.Secp256r1)).ScriptHash.ToAddress(0x35);
            if (publicKeyToAddress != address)
            {
                return StatusCode(StatusCodes.Status400BadRequest, new { code = 400, message = "Public key and address mismatch." });
            }

            //生成待签名的消息
            var message = string.Format(System.IO.File.ReadAllText("message.txt"), address, nonce.Nonce);

            //验证签名
            if (!Helper.VerifySignature(message, request.PublicKey, request.Signature))
            {
                return StatusCode(StatusCodes.Status400BadRequest, new { code = 400, message = "Signature verification failure.", data = $"Message: {message}" });
            }

            //创建 User
            var user = new Models.User()
            {
                Address = address,
                CreateTime = DateTime.UtcNow,
                PublicKey = request.PublicKey,
                Token = Guid.NewGuid().ToString()
            };
            var oldUser = _context.User.FirstOrDefault(p => p.Address == user.Address);

            //首次登录，创建 Token
            if (oldUser is null)
                _context.User.Add(user);
            //再次登录，更新 Token
            else
                oldUser.Token = user.Token;

            await _context.SaveChangesAsync();

            //Nonce 使用后删除
            Helper.Nonces.Remove(nonce);

            //返回 Token
            return new ObjectResult(user.Token);
        }

        [HttpPut("sign-in-test")]
        [Produces("application/json")]
        public ObjectResult Test()
        {
            var privateKey = new byte[32];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(privateKey);
            var publicKey = new KeyPair(privateKey).PublicKey;
            var address = Contract.CreateSignatureContract(publicKey).ScriptHash.ToAddress(0x35);
            var nonce = new NoncesController().PostNonce();
            var message = string.Format(System.IO.File.ReadAllText("message.txt"), address, nonce);
            var signature = Crypto.Sign(Encoding.UTF8.GetBytes(message), privateKey, publicKey.EncodePoint(false)[1..]);
            return new ObjectResult(new { Address = address, Nonce = nonce, Signature = signature.ToHexString(), PublicKey = publicKey.ToArray().ToHexString(), Message = message });
        }

        [HttpGet]
        public IEnumerable<UserResponse> GetUser([FromQuery] string[] addresses)
            => _context.User.Where(p => addresses.Contains(p.Address)).Select(p => new UserResponse(p));
    }
}
