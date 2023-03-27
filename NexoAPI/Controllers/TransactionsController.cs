using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Neo.Network.RPC;
using Neo.SmartContract.Native;
using Neo.Wallets;
using Neo;
using Newtonsoft.Json.Linq;
using NexoAPI.Data;
using NexoAPI.Models;
using Neo.SmartContract;
using Neo.Cryptography.ECC;
using Neo.VM;
using Akka.Actor;
using System.Security.Policy;
using NuGet.Protocol.Plugins;
using Neo.IO;
using Akka.Util.Internal;

namespace NexoAPI.Controllers
{
    [Route("[controller]")]
    [Produces("application/json")]
    [ApiController]
    public class TransactionsController : ControllerBase
    {
        private readonly NexoAPIContext _context;

        public TransactionsController(NexoAPIContext context)
        {
            _context = context;
        }

        [HttpGet]
        public ObjectResult GetTransactionList([FromHeader] string authorization, string account, string owner, string? signable, int? skip, int? limit, string? cursor)
        {
            //Authorization 格式检查
            if (!Helper.AuthorizationIsValid(authorization, out string token))
            {
                return StatusCode(StatusCodes.Status400BadRequest, new { code = "InvalidParameter", message = "Authorization format error", data = $"Authorization: {authorization}" });
            }

            //Authorization 有效性检查
            var currentUser = _context.User.FirstOrDefault(p => p.Token == token);
            if (currentUser is null)
            {
                return StatusCode(StatusCodes.Status400BadRequest, new { code = "TokenExpired", message = "Authorization incorrect.", data = $"Authorization: {authorization}" });
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

            //根据 cursor 筛选符合条件的 Transaction
            if (cursor is not null)
            {
                var cursorJson = JObject.Parse(cursor);
                var cursorTime = DateTime.UtcNow;

                //createTime 检查
                try
                {
                    //按时间倒序排序后，筛选早于等于 Cursor CreateTime 时间的数据
                    cursorTime = DateTime.Parse(cursorJson?["createTime"]?.ToString() ?? DateTime.UtcNow.ToString());
                    list = list.Where(p => p.CreateTime <= cursorTime).ToList();
                }
                catch (Exception)
                {
                    return StatusCode(StatusCodes.Status400BadRequest, new { code = "InvalidParameter", message = "The createTime in cursor is incorrect.", data = $"createTime: {cursorJson["createTime"]}" });
                }

                //按时间倒序排序后，筛选从 Cursor hash 开始（含）的数据
                var startIndex = list.FindIndex(p => p.Hash == cursorJson?["hash"]?.ToString());
                if (startIndex > 0)
                    list.RemoveRange(0, startIndex);
            }

            var result = list.Skip(skip ?? 0).Take(limit ?? 100).ToList().ConvertAll(p => new TransactionResponse(p));

            return new ObjectResult(result);
        }

        [HttpPost]
        public async Task<ObjectResult> PostTransaction([FromHeader] string authorization, TransactionRequest request)
        {
            //Authorization 格式检查
            if (!Helper.AuthorizationIsValid(authorization, out string token))
            {
                return StatusCode(StatusCodes.Status400BadRequest, new { code = "InvalidParameter", message = "Authorization format error", data = $"Authorization: {authorization}" });
            }

            //Authorization 有效性检查
            var currentUser = _context.User.FirstOrDefault(p => p.Token == token);
            if (currentUser is null)
            {
                return StatusCode(StatusCodes.Status400BadRequest, new { code = "TokenExpired", message = "Authorization incorrect.", data = $"Authorization: {authorization}" });
            }

            //account 检查
            var accountItem = _context.Account.FirstOrDefault(p => p.Address == request.Account);
            if (accountItem is null)
            {
                return StatusCode(StatusCodes.Status400BadRequest, new { code = "NotFound", message = $"Multi-Sign Address {request.Account} does not exist." });
            }

            //当前用户的地址必须在该账户的 owners 中
            if (!accountItem.Owners.Contains(currentUser.Address))
            {
                return StatusCode(StatusCodes.Status400BadRequest, new { code = "Forbidden", message = "The current user's address must be in the owners of the account" });
            }

            //feePayer 检查
            try
            {
                request.FeePayer.ToScriptHash(0x35);
            }
            catch (Exception)
            {
                return StatusCode(StatusCodes.Status400BadRequest, new { code = "InvalidParameter", message = "Fee payer is incorrect.", data = $"Fee payer: {request.FeePayer}" });
            }

            //feePayer 必须等于该账户或在该账户的 owners 中
            if (request.Account != request.FeePayer && !accountItem.Owners.Contains(request.FeePayer))
            {
                return StatusCode(StatusCodes.Status400BadRequest, new { code = "Forbidden", message = "FeePayer must be equal to the account or in the owners of the account", data = $"FeePayer: {request.FeePayer}" });
            }

            //验证ContractHash
            if (!UInt160.TryParse(request.ContractHash, out UInt160 contractHash))
            {
                return StatusCode(StatusCodes.Status400BadRequest, new { code = "InvalidParameter", message = "Contract hash is incorrect.", data = $"Contract hash: {request.ContractHash}" });
            }
            var tokenInfo = new Nep17API(Helper.Client).GetTokenInfoAsync(contractHash).Result;
            if (tokenInfo is null)
            {
                return StatusCode(StatusCodes.Status400BadRequest, new { code = "InvalidParameter", message = "The contract is not found in the current network.", data = $"Contract hash: {request.ContractHash}, Network: {ProtocolSettings.Default.Network}" });
            }

            var tx = new Transaction()
            {
                Account = accountItem,
                FeePayer = request.FeePayer,
                Creater = currentUser.Address,
                CreateTime = DateTime.UtcNow,
                Status = TransactionStatus.Signing,
                ContractHash = request.ContractHash,
                ValidUntilBlock = Helper.GetBlockCount().Result + 5760
            };

            if (Enum.TryParse(request.Type, out TransactionType type))
            {
                tx.Type = type;
                if (type == TransactionType.Invocation)
                {
                    tx.Operation = request.Operation;
                    tx.Params = request.Params.ToString();
                    var rawTx = InvocationFromMultiSignAccount(accountItem, contractHash, request.Operation, request.Params);
                    tx.RawData = rawTx.ToJson(ProtocolSettings.Default).ToString();
                    tx.Hash = rawTx.Hash.ToString();
                }
                else if (type == TransactionType.Nep17Transfer)
                {
                    decimal amount;
                    UInt160 receiver;
                    try
                    {
                        amount = Helper.ChangeToDecimal(request.Amount);
                        tx.Amount = request.Amount;
                    }
                    catch (Exception)
                    {
                        return StatusCode(StatusCodes.Status400BadRequest, new { code = "InvalidParameter", message = "Amount is incorrect.", data = $"Amount: {request.Amount}" });
                    }
                    try
                    {
                        receiver = request.Destination.ToScriptHash(0x35);
                        tx.Destination = request.Destination;
                    }
                    catch (Exception)
                    {
                        return StatusCode(StatusCodes.Status400BadRequest, new { code = "InvalidParameter", message = "Destination is incorrect.", data = $"Destination: {request.Destination}" });
                    }

                    var rawTx = TransferFromMultiSignAccount(accountItem, contractHash, amount, receiver);
                    tx.RawData = rawTx.ToJson(ProtocolSettings.Default).ToString();
                    tx.Hash = rawTx.Hash.ToString();
                    tx.Params = string.Empty;
                }
            }
            else
            {
                return StatusCode(StatusCodes.Status400BadRequest, new { code = "InvalidParameter", message = "Type is incorrect.", data = $"Type: {request.Type}" });
            }

            //交易重复性检查
            if (_context.Transaction.Any(p => p.Hash == tx.Hash))
            {
                return StatusCode(StatusCodes.Status400BadRequest, new { code = "NotSatisfied", message = $"Transaction already exists", data = $"Transaction: {tx.Hash}" });
            }

            _context.Transaction.Add(tx);
            await _context.SaveChangesAsync();

            return new(new { });
        }

        [HttpPost("Test")]
        public ObjectResult Test([FromBody] JArray array)
        {
            return new("ok");
        }

        private static Neo.Network.P2P.Payloads.Transaction TransferFromMultiSignAccount(Account account, UInt160 contractHash, decimal amount, UInt160 receiver)
        {
            var multiAccount = account.GetScriptHash();
            var tokenInfo = new Nep17API(Helper.Client).GetTokenInfoAsync(contractHash).Result;

            var script = contractHash.MakeScript("transfer", multiAccount, receiver, amount * (decimal)Math.Pow(10, tokenInfo.Decimals), string.Empty);

            var signers = new[]
            {
                new Neo.Network.P2P.Payloads.Signer
                { 
                    Scopes = Neo.Network.P2P.Payloads.WitnessScope.CalledByEntry, Account = multiAccount
                }
            };

            var tx = new TransactionManagerFactory(Helper.Client).MakeTransactionAsync(script, signers).Result.Tx;
            tx.Witnesses = Array.Empty<Neo.Network.P2P.Payloads.Witness>();
            return tx;
        }

        private static Neo.Network.P2P.Payloads.Transaction InvocationFromMultiSignAccount(Account account, UInt160 contractHash, string operation, JArray contractParameters)
        {
            var multiAccount = account.GetScriptHash();

            var parameters = new List<ContractParameter>();
            contractParameters?.ForEach(p => parameters.Add(ContractParameter.FromJson((Neo.Json.JObject)Neo.Json.JObject.Parse(p.ToString()))));

            byte[] script;
            using ScriptBuilder scriptBuilder = new();
            scriptBuilder.EmitDynamicCall(contractHash, operation, parameters.ToArray());
            script = scriptBuilder.ToArray();

            var signers = new[]
            {
                new Neo.Network.P2P.Payloads.Signer
                { 
                    Scopes = Neo.Network.P2P.Payloads.WitnessScope.CalledByEntry, 
                    Account = multiAccount
                }
            };

            var tx = new TransactionManagerFactory(Helper.Client).MakeTransactionAsync(script, signers).Result.Tx;
            tx.Witnesses = Array.Empty<Neo.Network.P2P.Payloads.Witness>();
            return tx;
        }
    }
}
