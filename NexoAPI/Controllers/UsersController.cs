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
        public async Task<ObjectResult> _([FromBody] SignInViewModel body, string address)
        {
            //address 检查
            try
            {
                address.ToScriptHash(0x35);
            }
            catch (Exception)
            {
                return StatusCode(StatusCodes.Status400BadRequest, new { code = 400, message = "Address is incorrect.", data = $"Address: {address}" } );
            }

            //nonce 检查
            var nonce = Helper.Nonces.FirstOrDefault(p => p.Nonce == body.Nonce);
            if (nonce == null)
            {
                return StatusCode(StatusCodes.Status400BadRequest, new { code = 400, message = "Unauthorized, nonce is incorrect.", data = $"Nonce: {body.Nonce}" });
            }

            //nonce 有效期检查
            if ((DateTime.UtcNow - nonce.CreateTime).TotalMinutes > 20)
            {
                return StatusCode(StatusCodes.Status400BadRequest, new { code = 400, message = "Unauthorized, nonce has been expired.", data = $"Nonce create time: {nonce.CreateTime}" });
            }

            //检查公钥和地址是否匹配
            var publicKeyToAddress = Contract.CreateSignatureContract(Neo.Cryptography.ECC.ECPoint.Parse(body.PublicKey, Neo.Cryptography.ECC.ECCurve.Secp256r1)).ScriptHash.ToAddress(0x35);
            if (publicKeyToAddress != address)
            {
                return StatusCode(StatusCodes.Status400BadRequest, new { code = 400, message = "Public key and address mismatch.", data = "" });
            }

            //生成待签名的消息和消息哈希
            var message = string.Format(System.IO.File.ReadAllText("message.txt"), address, nonce.Nonce);
            var messageHash = message.Sha256();

            //验证签名
            if (!Helper.VerifySignature(messageHash, body.PublicKey, body.Signature))
            {
                return StatusCode(StatusCodes.Status400BadRequest, new { code = 400, message = "Signature verification failure.", data = $"Message: {message} Message Hash: {messageHash}" });
            }

            //创建 User
            var user = new User() { Address = address, CreateTime = DateTime.UtcNow, PublicKey = body.PublicKey, Token = Guid.NewGuid().ToString() };
            _context.User.Add(user);
            await _context.SaveChangesAsync();

            //Nonce 使用后删除
            Helper.Nonces.Remove(nonce);

            //返回 Token
            return new ObjectResult(user.Token);
        }

        [HttpPut("sign-in-test")]
        public string _()
        {
            using CngKey key = CngKey.Create(CngAlgorithm.ECDsaP256, null, new CngKeyCreationParameters { ExportPolicy = CngExportPolicies.AllowPlaintextArchiving });
            var privateKey = key.Export(CngKeyBlobFormat.EccPrivateBlob).Take(32).ToArray();
            var publicKey = new KeyPair(privateKey).PublicKey;
            var address = Contract.CreateSignatureContract(publicKey).ScriptHash.ToAddress(0x35);
            var nonce = new NoncesController().GenerateGUID();
            var message = string.Format(System.IO.File.ReadAllText("message.txt"), address, nonce);
            var messageHash = message.Sha256();
            var signature = Crypto.Sign(Encoding.UTF8.GetBytes(messageHash), privateKey, publicKey.EncodePoint(false)[1..]);
            return new { Address = address, Nonce = nonce, PublicKey = publicKey.ToArray().ToHexString(), Signature = signature.ToHexString(), Message = message, MessageHash = messageHash }.ToJson();
        }

        [HttpGet]
        public IEnumerable<UserResult> _([FromQuery] string[] addresses)
        {
            return _context.User.Where(p => addresses.Contains(p.Address)).Select(p => new UserResult() { Address = p.Address, PublicKey = p.PublicKey });
        }
    }

    public class UserResult
    {
        public string Address { get; set; }
        public string PublicKey { get; set; }
    }

    public class SignInViewModel
    {
        public string Nonce { get; set; }
        public string Signature { get; set; }
        public string PublicKey { get; set; }
    }
}
