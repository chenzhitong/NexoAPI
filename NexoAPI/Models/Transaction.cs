using Akka.Actor;
using Neo.Network.RPC;
using Newtonsoft.Json.Linq;
using NexoAPI.Migrations;
using Org.BouncyCastle.Asn1.Pkcs;
using System.Security.Policy;

namespace NexoAPI.Models
{
    public class Transaction
    {
        public int Id { get; set; }

        public Account Account { get; set; }

        public TransactionType Type { get; set; }

        public string RawData { get; set; } = string.Empty;

        public string Hash { get; set; } = string.Empty;

        public uint ValidUntilBlock { get; set; }

        public string Creator { get; set; }

        public string FeePayer { get; set; }

        public DateTime CreateTime { get; set; }

        public DateTime ExecuteTime { get; set; }

        public TransactionStatus Status { get; set; }

        public ICollection<SignResult> SignResult { get; set; }

        public string ContractHash { get; set; } = string.Empty;

        public string Operation { get; set; } = string.Empty;

        public string Params { get; set; } = string.Empty;

        public string Amount { get; set; } = string.Empty;

        public string Destination { get; set; } = string.Empty;
    }

    public class TransactionRequest
    {
        public string Account { get; set; }

        public string Type { get; set; }

        public string FeePayer { get; set; }

        public string ContractHash { get; set; }

        public string Operation { get; set; } = string.Empty;

        public JArray? Params { get; set; }

        public string Amount { get; set; } = string.Empty;

        public string Destination { get; set; } = string.Empty;
    }

    public class Params
    {
        public string Type { get; set; }

        public string Value { get; set; }
    }

    public class TransactionResponse
    {
        public static object GetResponse(Transaction p)
        {
            if (p.Type == TransactionType.Invocation)
            {
                return new InvocationTransactionResponse()
                {
                    Account = p.Account.Address,
                    Type = p.Type.ToString(),
                    RawData = p.RawData,
                    Hash = p.Hash,
                    ValidUntilBlock = p.ValidUntilBlock,
                    Creator = p.Creator,
                    FeePayer = p.FeePayer,
                    CreateTime = p.CreateTime,
                    ExecuteTime = p.ExecuteTime > new DateTime(2023, 1, 1) ? p.ExecuteTime : null,
                    Status = p.Status.ToString(),
                    ContractHash = p.ContractHash,
                    Operation = p.Operation,
                    Params = string.IsNullOrEmpty(p.Params) ? null : JArray.Parse(p.Params)
                };
            }
            else if (p.Type == TransactionType.Nep17Transfer)
            {
                var temp =  new Nep17TransferTransactionResponse()
                {
                    Account = p.Account.Address,
                    Type = p.Type.ToString(),
                    RawData = p.RawData,
                    Hash = p.Hash,
                    ValidUntilBlock = p.ValidUntilBlock,
                    Creator = p.Creator,
                    FeePayer = p.FeePayer,
                    CreateTime = p.CreateTime,
                    ExecuteTime = p.ExecuteTime > new DateTime(2023, 1, 1) ? p.ExecuteTime : null,
                    Status = p.Status.ToString(),
                    ContractHash = p.ContractHash,
                    Amount = p.Amount,
                    Destination = p.Destination,
                    
                };
                try
                {
                    temp.TokenSymbol = new Nep17API(Helper.Client).GetTokenInfoAsync(p.ContractHash).Result.Symbol;
                }
                catch (Exception)
                {
                    temp.TokenSymbol = "TokenSymbolException";
                }
                return temp;
            }
            return null;
        }

    }

    public class InvocationTransactionResponse
    {

        public string Account { get; set; }

        public string Type { get; set; }

        public string RawData { get; set; }

        public string Hash { get; set; }

        public uint ValidUntilBlock { get; set; }

        public string Creator { get; set; }

        public string FeePayer { get; set; }

        public DateTime CreateTime { get; set; }

        public DateTime? ExecuteTime { get; set; }

        public string Status { get; set; }

        public string ContractHash { get; set; }

        public string Operation { get; set; }

        public JArray Params { get; set; }
    }
    public class Nep17TransferTransactionResponse
    {

        public string Account { get; set; }

        public string Type { get; set; }

        public string RawData { get; set; }

        public string Hash { get; set; }

        public uint ValidUntilBlock { get; set; }

        public string Creator { get; set; }

        public string FeePayer { get; set; }

        public DateTime CreateTime { get; set; }

        public DateTime? ExecuteTime { get; set; }

        public string Status { get; set; }

        public string ContractHash { get; set; }

        public string Amount { get; set; }

        public string Destination { get; set; }

        public string TokenSymbol { get; set; }
    }
    public enum TransactionType
    {
        Invocation,
        Nep17Transfer
    }

    public enum TransactionStatus
    {
        Signing, //签名中，等待足够的签名
        Rejected, //已拒绝，由于被拒绝导致签名无法完成
        Expired, //已失效，过了交易有效块高
        Executing, //执行中，已被发到链上执行
        Executed, //已执行，链上已执行完成（可能失败）
    }
}
