using Akka.Util.Internal;
using Microsoft.EntityFrameworkCore;
using Neo;
using Neo.Json;
using Neo.VM;
using Neo.Wallets;
using NexoAPI.Data;
using NLog;
using Neo.Network.P2P.Payloads;
using Neo.IO;
using Neo.Network.RPC.Models;

namespace NexoAPI
{
    public class BackgroundTask : BackgroundService
    {

        public readonly Logger _logger;
        private readonly NexoAPIContext _context;

        public BackgroundTask(IServiceScopeFactory _serviceScopeFactory)
        {
            var scope = _serviceScopeFactory.CreateScope();
            _context = scope.ServiceProvider.GetRequiredService<NexoAPIContext>();
            _logger = LogManager.LoadConfiguration("nlog.config").GetCurrentClassLogger();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.Info("BackgroundTask is running.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var temp = _context.User.FirstOrDefault();
                }
                catch (Exception ex)
                {
                    _logger.Error($"后台任务运行时数据库连接失败 {ex.Message}");
                    break;
                }
                try
                {
                    var temp = Helper.Client.GetBlockCountAsync().Result;
                }
                catch (Exception ex)
                {

                    _logger.Error($"后台任务运行时种子节点连接失败 {ex.Message}");
                    Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
                }

                //后台任务一：根据用户签名修改交易的RawData
                var list1 = _context.Transaction.Include(p => p.Account).Include(p => p.SignResult).ThenInclude(s => s.Signer).
                    Where(p => p.Status == Models.TransactionStatus.Signing);

                foreach (var tx in list1)
                {
                    var rawTx = RpcTransaction.FromJson((JObject)JToken.Parse(tx.RawData), ProtocolSettings.Load(ConfigHelper.AppSetting("Config"))).Transaction;

                    //FeePayer需要单独的签名
                    var feePayerSignResult = tx.SignResult.FirstOrDefault(p => p.Approved && p.Signer.Address == tx.FeePayer);
                    if (feePayerSignResult is not null)
                    {
                        if (!rawTx.Witnesses.Any(p => p.VerificationScript.ToArray().ToHexString() == feePayerSignResult.Signer.GetScript().ToHexString() && p.InvocationScript.Length > 0))
                        {
                            using ScriptBuilder scriptBuilder = new();
                            scriptBuilder.EmitPush(feePayerSignResult.Signature.HexToBytes());
                            var feePayerWitness = rawTx.Witnesses.FirstOrDefault(p => p.ScriptHash.ToAddress() == feePayerSignResult.Signer.Address);
                            if (feePayerWitness == null)
                                _logger.Error($"构造交易时出错，feePayer不在交易的Witness中，TxId = {tx.Hash}, feePayer: {feePayerSignResult.Signer.Address}");
                            else
                                feePayerWitness.InvocationScript = scriptBuilder.ToArray();
                            tx.RawData = rawTx.ToJson(ProtocolSettings.Load(ConfigHelper.AppSetting("Config"))).ToString();
                            _context.Update(feePayerSignResult);
                        }
                    }

                    //签名数满足阈值时，其他用户的签名合并为多签账户的签名
                    var otherSignResult = tx.SignResult.Where(p => p.Approved && tx.Account.Owners.Contains(p.Signer.Address)).OrderBy(p => p.Signer.PublicKey).Take(tx.Account.Threshold).ToList();
                    if (otherSignResult?.Count == tx.Account.Threshold)
                    {
                        if (!rawTx.Witnesses.Any(p => p.VerificationScript.ToArray().ToHexString() == tx.Account.GetScript().ToHexString() && p.InvocationScript.Length > 0))
                        {
                            using ScriptBuilder scriptBuilder = new();
                            otherSignResult.OrderBy(p => p.Signer.PublicKey).ForEach(p => scriptBuilder.EmitPush(p.Signature.HexToBytes()));
                            rawTx.Witnesses.First(p => p.VerificationScript.ToArray().ToHexString() == tx.Account.GetScript().ToHexString()).InvocationScript = scriptBuilder.ToArray();
                            tx.RawData = rawTx.ToJson(ProtocolSettings.Load(ConfigHelper.AppSetting("Config"))).ToString();
                            _context.Update(tx);
                        }
                    }

                    //发送交易
                    if (rawTx.Witnesses.All(p => p.InvocationScript.Length > 0))
                    {
                        try
                        {
                            var send = Helper.Client.SendRawTransactionAsync(rawTx).Result;
                            tx.Status = Models.TransactionStatus.Executing;
                            tx.ExecuteTime = DateTime.UtcNow;
                        }
                        catch (Exception e)
                        {
                            _logger.Error($"发送交易时出错，TxId = {tx.Hash}, Exception: {e.Message}");
                            tx.Status = Models.TransactionStatus.Failed;
                            tx.FailReason = e.Message;
                            tx.ExecuteTime = DateTime.UtcNow;
                        }
                    }
                }

                //后台任务二：检查交易是否上链并修改交易状态
                _context.Transaction.Where(p => p.Status == Models.TransactionStatus.Executing).ToList().ForEach(p =>
                {
                    if (Helper.Client.GetTransactionHeightAsync(p.Hash).Result > 0)
                    {
                        p.Status = Models.TransactionStatus.Executed;
                        _context.Update(p);
                    }
                });

                //后台任务三：检查交易是否过期并修改交易状态
                var blockCount = Helper.Client.GetBlockCountAsync().Result;
                _context.Transaction.Where(p => p.Status == Models.TransactionStatus.Signing).ToList().ForEach(p =>
                {
                    if (blockCount > p.ValidUntilBlock)
                    {
                        p.Status = Models.TransactionStatus.Expired;
                        _context.Update(p);
                    }
                });

                _context.SaveChanges();
                await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
            }
        }
    }
}
