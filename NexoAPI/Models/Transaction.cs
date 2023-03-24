using Newtonsoft.Json.Linq;

namespace NexoAPI.Models
{
    public class Transaction
    {
        public int Id { get; set; }

        public Account Account { get; set; }

        public TransactionType Type{ get; set; }

        public string RawData { get; set; } = string.Empty;

        public string Hash { get; set; } = string.Empty;

        public uint ValidUntilBlock { get; set; }

        public string Creater { get; set; }

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

        public JArray Params { get; set; }

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
        public TransactionResponse(Transaction p)
        {
            Account = p.Account.Address;
            Type = p.Type.ToString();
            RawData = p.RawData;
            Hash = p.Hash;
            ValidUntilBlock = p.ValidUntilBlock;
            Creater = p.Creater;
            FeePayer = p.FeePayer;
            CreateTime = p.CreateTime;
            ExecuteTime = p.ExecuteTime;
            Status = p.Status.ToString();
            ContractHash = p.ContractHash;
            Operation = p.Operation;
            Params = p.Params;
            Amount = p.Amount;
            Destination = p.Destination;
        }

        public string Account { get; set; }

        public string Type { get; set; }

        public string RawData { get; set; }

        public string Hash { get; set; }

        public uint ValidUntilBlock { get; set; }

        public string Creater { get; set; }

        public string FeePayer { get; set; }

        public DateTime CreateTime { get; set; }

        public DateTime ExecuteTime { get; set; }

        public string Status { get; set; }

        public string ContractHash { get; set; }

        public string Operation { get; set; }

        public string Params { get; set; }

        public string Amount { get; set; }

        public string Destination { get; set; }
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
