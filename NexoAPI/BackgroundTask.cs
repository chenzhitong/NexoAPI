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

        public BackgroundTask(NexoAPIContext context)
        {
            _context = context;
            _logger = LogManager.LoadConfiguration("nlog.config").GetCurrentClassLogger();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.Info("BackgroundTask is running.");

            while (!stoppingToken.IsCancellationRequested)
            {
                //后台任务一：根据用户签名修改交易的RawData
                var list1 = _context.Transaction.Include(p => p.Account).Include(p => p.SignResult).ThenInclude(s => s.Signer).
                    Where(p => p.Status == Models.TransactionStatus.Signing);

                foreach (var tx in list1)
                {
                    var rawTx = Neo.Network.RPC.Models.RpcTransaction.FromJson((JObject)JToken.Parse(tx.RawData), ProtocolSettings.Default).Transaction;
                    
                    //FeePayer需要单独的签名
                    var feePayerSignResult = tx.SignResult.FirstOrDefault(p => !p.InWitness && p.Signer.Address == tx.FeePayer);
                    if (feePayerSignResult is not null)
                    {
                        rawTx.Signers.Append(new Signer()
                        {
                            Scopes = WitnessScope.CalledByEntry,
                            Account = tx.FeePayer.ToScriptHash(0x35)
                        });

                        using ScriptBuilder scriptBuilder = new();
                        scriptBuilder.EmitPush(feePayerSignResult.Signature.HexToBytes());
                        rawTx.Witnesses.Append(new Witness()
                        {
                            InvocationScript = scriptBuilder.ToArray(),
                            VerificationScript = feePayerSignResult.Signer.GetScript()
                        });
                        feePayerSignResult.InWitness = true;
                        tx.RawData = rawTx.ToJson(ProtocolSettings.Default).ToString();
                        _context.Update(feePayerSignResult);
                    }

                    //签名数满足阈值时，其他用户的签名合并为多签账户的签名
                    var otherSignResult = tx.SignResult.Where(p => !p.InWitness && p.Signer.Address != tx.FeePayer).OrderBy(p => p.Signer.PublicKey).ToList();
                    if (otherSignResult is not null && otherSignResult.Count() >= tx.Account.Threshold)
                    {
                        using ScriptBuilder scriptBuilder = new();
                        otherSignResult.ForEach(p => 
                        {
                            scriptBuilder.EmitPush(p.Signature.HexToBytes());
                            p.InWitness = true;
                            _context.Update(p);
                        });

                        rawTx.Witnesses.Append(new Witness()
                        {
                            InvocationScript = scriptBuilder.ToArray(),
                            VerificationScript = tx.Account.GetScript()
                        });
                        tx.RawData = rawTx.ToJson(ProtocolSettings.Default).ToString();

                        //发送交易
                        if(feePayerSignResult.InWitness)
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

                //后台任务二：检查交易是否上链并修改交易状态
                _context.Transaction.Where(p => p.Status == Models.TransactionStatus.Executing).ToList().ForEach(p => {
                    if (Helper.Client.GetTransactionHeightAsync(p.Hash).Result > 0)
                    {
                        p.Status = Models.TransactionStatus.Executed;
                        _context.Update(p);
                    }
                });

                //后台任务三：检查交易是否过期并修改交易状态
                _context.Transaction.Where(p => p.Status == Models.TransactionStatus.Signing).ToList().ForEach(p => {
                    if (Helper.Client.GetBlockCountAsync().Result > p.ValidUntilBlock)
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
