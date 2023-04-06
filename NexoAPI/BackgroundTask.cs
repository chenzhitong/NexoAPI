using Akka.Util.Internal;
using Microsoft.EntityFrameworkCore;
using Neo;
using Neo.Json;
using Neo.VM;
using Neo.Wallets;
using NexoAPI.Data;
using NLog;
using Neo.Network.P2P.Payloads;

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
                    break;
                }

                //后台任务一：根据用户签名修改交易的RawData
                var list1 = _context.Transaction.Include(p => p.Account).Include(p => p.SignResult).ThenInclude(s => s.Signer).
                    Where(p => p.Status == Models.TransactionStatus.Signing);

                foreach (var tx in list1)
                {
                    var jtTx = JToken.Parse(tx.RawData);
                    if (jtTx == null)
                    {
                        _logger.Error($"JToken.Parse(tx.RawData) 时出错，tx.RawData = {tx.RawData}");
                        continue;
                    }
                    var rawTx = Neo.Network.RPC.Models.RpcTransaction.FromJson((JObject)jtTx, ProtocolSettings.Load(ConfigHelper.AppSetting("Config"))).Transaction;
                    if (rawTx == null)
                    {
                        _logger.Error($"RpcTransaction.FromJson() 时出错，tx.RawData = {tx.RawData}");
                        continue;
                    }

                    //FeePayer需要单独的签名
                    var feePayerSignResult = tx.SignResult.FirstOrDefault(p => p.Signer.Address == tx.FeePayer);
                    if (feePayerSignResult is not null)
                    {
                        //FeePayer的签名未添加到交易的Witness中
                        if (!rawTx.Witnesses.Any(p => p.VerificationScript.ToArray().ToHexString() == feePayerSignResult.Signer.GetScript().ToHexString()))
                        {
                            rawTx.Signers = rawTx.Signers.Append(new Signer()
                            {
                                Scopes = WitnessScope.CalledByEntry,
                                Account = tx.FeePayer.ToScriptHash()
                            }).ToArray();

                            using ScriptBuilder scriptBuilder = new();
                            scriptBuilder.EmitPush(feePayerSignResult.Signature.HexToBytes());
                            rawTx.Witnesses = rawTx.Witnesses.Append(new Witness()
                            {
                                InvocationScript = scriptBuilder.ToArray(),
                                VerificationScript = feePayerSignResult.Signer.GetScript()
                            }).ToArray();
                            tx.RawData = rawTx.ToJson(ProtocolSettings.Load(ConfigHelper.AppSetting("Config"))).ToString();
                            _context.Update(feePayerSignResult);
                        }
                    }

                    //签名数满足阈值时，其他用户的签名合并为多签账户的签名
                    var otherSignResult = tx.SignResult.Where(p => tx.Account.Owners.Contains(p.Signer.Address)).OrderBy(p => p.Signer.PublicKey).ToList();
                    if (otherSignResult is not null && otherSignResult.Count >= tx.Account.Threshold)
                    {
                        //如果没添加到Witness中，则构造Witness添加到交易中
                        if (!rawTx.Witnesses.Any(p => p.VerificationScript.ToArray().ToHexString() == tx.Account.GetScript().ToHexString()))
                        {
                            rawTx.Signers = rawTx.Signers.Append(new Signer()
                            {
                                Scopes = WitnessScope.CalledByEntry,
                                Account = tx.Account.Address.ToScriptHash()
                            }).ToArray();

                            using ScriptBuilder scriptBuilder = new();
                            otherSignResult.ForEach(p =>
                            {
                                scriptBuilder.EmitPush(p.Signature.HexToBytes());
                                _context.Update(p);
                            });

                            rawTx.Witnesses = rawTx.Witnesses.Append(new Witness()
                            {
                                InvocationScript = scriptBuilder.ToArray(),
                                VerificationScript = tx.Account.GetScript()
                            }).ToArray();
                            tx.RawData = rawTx.ToJson(ProtocolSettings.Load(ConfigHelper.AppSetting("Config"))).ToString();
                        }

                        //发送交易
                        if (feePayerSignResult != null && rawTx.Witnesses.Any(p => p.VerificationScript.ToArray().ToHexString() == feePayerSignResult.Signer.GetScript().ToHexString()))
                        {
                            try
                            {
                                var send = Helper.Client.SendRawTransactionAsync(rawTx).Result;
                                tx.Status = Models.TransactionStatus.Executing;
                            }
                            catch (Exception e)
                            {
                                _logger.Error($"发送交易时出错，TxId = {tx.Hash}, Exception: {e.Message}");
                            }
                        }
                        _context.Update(tx);
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
