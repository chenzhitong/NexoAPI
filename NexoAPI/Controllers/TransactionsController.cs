using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Neo;
using Neo.Cryptography.ECC;
using Neo.IO;
using Neo.Network.RPC;
using Neo.SmartContract;
using Neo.VM;
using Newtonsoft.Json.Linq;
using NexoAPI.Data;
using NexoAPI.Models;
using NLog;
using System.Numerics;

namespace NexoAPI.Controllers
{
    [Route("[controller]")]
    [Produces("application/json")]
    [ApiController]
    public class TransactionsController : ControllerBase
    {
        private readonly NexoAPIContext _context;
        public readonly Logger _logger;

        public TransactionsController(NexoAPIContext context)
        {
            _context = context; 
            _logger = LogManager.LoadConfiguration("nlog.config").GetCurrentClassLogger();
        }

        [HttpGet]
        public ObjectResult GetTransactionList(string account, string owner, string? signable, int? skip, int? limit, string? cursor)
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
                    .Where(p => p.Status != TransactionStatus.Signing || p.SignResult.Any(p => p.Signer.Address.Contains(owner))).ToList();
            }
            else
            {
                list = _context.Transaction.Include(p => p.Account).Include(p => p.SignResult)
                    .Where(p => p.Account.Address == account).ToList();
            }
            list = list.OrderByDescending(p => p.CreateTime).ThenBy(p => p.Hash).ToList();

            //根据 cursor 筛选符合条件的 Transaction
            if (cursor is not null)
            {
                var cursorJson = JObject.Parse(cursor);
                var cursorTime = DateTime.UtcNow;
                
                //createTime 检查
                try
                {
                    cursorTime = (DateTime)cursorJson["createTime"];
                    //按时间倒序排序后，筛选早于等于 Cursor CreateTime 时间的数据（精确到毫秒，忽略更小精度）
                    list = list.Where(p => new DateTime(p.CreateTime.Year, p.CreateTime.Month, p.CreateTime.Day, p.CreateTime.Hour, p.CreateTime.Minute, p.CreateTime.Second, p.CreateTime.Millisecond) <= cursorTime).ToList();
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

            var result = new List<object>();
            var temp = list.Skip(skip ?? 0).Take(limit ?? 100).ToList();
            Parallel.ForEach(temp, p => result.Add(TransactionResponse.GetResponse(p)));

            return new ObjectResult(result);
        }

        [HttpPost]
        public async Task<ObjectResult> PostTransaction(TransactionRequest request)
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

            //feePayer 格式检查
            try
            {
                request.FeePayer.ToScriptHash();
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
            ContractState contractState;
            try
            {
                contractState = Helper.Client.GetContractStateAsync(request.ContractHash).Result;
            }
            catch (Exception ex)
            {
                _logger.Error(ex.Message);
                return StatusCode(StatusCodes.Status400BadRequest, new { code = "InternalError", message = $"An error occurred while requesting the seed node: {ex.Message}", data = $"Seed node: {ConfigHelper.AppSetting("SeedNode")}, number=1" });
            }
            if (contractState is null)
            {
                return StatusCode(StatusCodes.Status400BadRequest, new { code = "InvalidParameter", message = "The contract is not found in the current network.", data = $"Contract hash: {request.ContractHash}, Network: {ProtocolSettings.Load(ConfigHelper.AppSetting("Config")).Network}" });
            }

            var tx = new Transaction()
            {
                Account = accountItem,
                FeePayer = request.FeePayer,
                Creator = currentUser.Address,
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
                    if (request.Params is null || string.IsNullOrEmpty(request.Params.ToString()))
                    {
                        return StatusCode(StatusCodes.Status400BadRequest, new { code = "InvalidParameter", message = "The Params field is required." });
                    }
                    tx.Params = request.Params.ToString();
                    try
                    {
                        var rawTx = InvocationFromMultiSignAccount(accountItem, request.FeePayer, contractHash, request.Operation, request.Params);
                        tx.RawData = rawTx.ToJson(ProtocolSettings.Load(ConfigHelper.AppSetting("Config"))).ToString();
                        tx.Hash = rawTx.Hash.ToString();
                    }
                    catch (ArgumentException ex)
                    {
                        return StatusCode(StatusCodes.Status400BadRequest, new { code = "InvalidParameter", message = "Unsupported parameters.", data = ex.Message });
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex.Message);
                        return StatusCode(StatusCodes.Status400BadRequest, new { code = "InternalError", message = $"An error occurred while requesting the seed node: {ex.Message}", data = $"Seed node: {ConfigHelper.AppSetting("SeedNode")}, number=2" });
                    }
                }
                else if (type == TransactionType.Nep17Transfer)
                {
                    decimal amount;
                    UInt160 receiver;
                    try
                    {
                        amount = Helper.ChangeToDecimal(request.Amount);
                        if (amount < 0)
                        {
                            return StatusCode(StatusCodes.Status400BadRequest, new { code = "InvalidParameter", message = "Amount is incorrect.", data = $"Amount: {request.Amount}" });
                        }
                        tx.Amount = request.Amount;
                    }
                    catch (Exception)
                    {
                        return StatusCode(StatusCodes.Status400BadRequest, new { code = "InvalidParameter", message = "Amount is incorrect.", data = $"Amount: {request.Amount}" });
                    }
                    try
                    {
                        receiver = request.Destination.ToScriptHash();
                        tx.Destination = request.Destination;
                    }
                    catch (Exception)
                    {
                        return StatusCode(StatusCodes.Status400BadRequest, new { code = "InvalidParameter", message = "Destination is incorrect.", data = $"Destination: {request.Destination}" });
                    }
                    try
                    {
                        var rawTx = TransferFromMultiSignAccount(accountItem, request.FeePayer, contractHash, amount, receiver);
                        tx.RawData = rawTx.ToJson(ProtocolSettings.Load(ConfigHelper.AppSetting("Config"))).ToString();
                        tx.Hash = rawTx.Hash.ToString();
                        tx.Params = string.Empty;
                    }
                    catch (ArgumentException)
                    {
                        return StatusCode(StatusCodes.Status400BadRequest, new { code = "InvalidParameter", message = $"Amount exceeds maximum accuracy.", data = $"Amount: {request.Amount}" });
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex.Message);
                        return StatusCode(StatusCodes.Status400BadRequest, new { code = "InternalError", message = $"An error occurred while requesting the seed node: {ex.Message}", data = $"Seed node: {ConfigHelper.AppSetting("SeedNode")}, number=3" });
                    }
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

            return new(tx.Hash);
        }

        private static Neo.Network.P2P.Payloads.Transaction TransferFromMultiSignAccount(Account account, string feePayer, UInt160 contractHash, decimal amount, UInt160 receiver)
        {
            var tokenInfo = new Nep17API(Helper.Client).GetTokenInfoAsync(contractHash).Result;
            var bigInteger = new BigInteger();
            try
            {
                bigInteger = new BigDecimal(amount, tokenInfo.Decimals).Value;
            }
            catch (Exception)
            {
                throw new ArgumentException();
            }
            var script = contractHash.MakeScript("transfer", account.GetScriptHash(), receiver, bigInteger, true);

            var signers = CalculateSigners(account, feePayer);
            var tx = new TransactionManagerFactory(Helper.Client).MakeTransactionAsync(script, signers).Result.Tx;
            tx.Witnesses = CalculateWitnesss(account, feePayer);
            tx.NetworkFee = Helper.Client.CalculateNetworkFeeAsync(tx).Result;
            return tx;
        }

        private static Neo.Network.P2P.Payloads.Transaction InvocationFromMultiSignAccount(Account account, string feePayer, UInt160 contractHash, string operation, JArray contractParameters)
        {
            var parameters = new List<ContractParameter>();
            foreach (var p in contractParameters)
            {
                var t = Neo.Json.JToken.Parse(p.ToString()) as Neo.Json.JObject;
                try
                {
                    parameters.Add(ContractParameter.FromJson(t));
                }
                catch (Exception)
                {
                    throw new ArgumentException(t?.ToString());
                }
            }

            byte[] script;
            using ScriptBuilder scriptBuilder = new();
            scriptBuilder.EmitDynamicCall(contractHash, operation, parameters.ToArray());
            script = scriptBuilder.ToArray();

            var signers = CalculateSigners(account, feePayer);
            var tx = new TransactionManagerFactory(Helper.Client).MakeTransactionAsync(script, signers).Result.Tx;
            tx.Witnesses = CalculateWitnesss(account, feePayer);
            var base64 = Convert.ToBase64String(tx.ToArray());
            tx.NetworkFee = Helper.Client.CalculateNetworkFeeAsync(tx).Result;
            return tx;
        }

        private static Neo.Network.P2P.Payloads.Signer[] CalculateSigners(Account account, string feePayer)
        {
            return feePayer == account.Address ? new[]
                {
                    new Neo.Network.P2P.Payloads.Signer
                    {
                        Scopes = Neo.Network.P2P.Payloads.WitnessScope.CalledByEntry,
                        Account = account.GetScriptHash()
                    }
                } : new[]
                {
                    new Neo.Network.P2P.Payloads.Signer
                    {
                        Scopes = Neo.Network.P2P.Payloads.WitnessScope.CalledByEntry,
                        Account = feePayer.ToScriptHash()
                    },
                    new Neo.Network.P2P.Payloads.Signer
                    {
                        Scopes = Neo.Network.P2P.Payloads.WitnessScope.CalledByEntry,
                        Account = account.GetScriptHash()
                    }
                };
        }

        private static Neo.Network.P2P.Payloads.Witness[] CalculateWitnesss(Account account, string feePayer)
        {
            if (feePayer == account.Address) return new[]
                {
                    new Neo.Network.P2P.Payloads.Witness()
                    {
                        InvocationScript = null,
                        VerificationScript = account.GetScript()
                    }
                };
            else
            {
                var feePayerPubkey = account.PublicKeys.Split(',').ToList().FirstOrDefault(p => Contract.CreateSignatureContract(ECPoint.Parse(p, ECCurve.Secp256r1)).ScriptHash.ToAddress() == feePayer);
                var script = Contract.CreateSignatureContract(ECPoint.Parse(feePayerPubkey, ECCurve.Secp256r1)).Script;
                var witness = new[]
                {
                    new Neo.Network.P2P.Payloads.Witness()
                    {
                        InvocationScript = null,
                        VerificationScript = script
                    },
                    new Neo.Network.P2P.Payloads.Witness()
                    {
                        InvocationScript = null,
                        VerificationScript = account.GetScript()
                    }
                };
                return witness;
            }
        }
    }
}
