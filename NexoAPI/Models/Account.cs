using Neo;
using Neo.Cryptography.ECC;
using Neo.SmartContract;
using System.ComponentModel.DataAnnotations.Schema;

namespace NexoAPI.Models
{
    public class Account
    {
        public int Id { get; set; }

        public string Address { get; set; }

        public string Owners { get; set; }

        public string PublicKeys { get; set; }

        public int Threshold { get; set; }

        public ICollection<Remark> Remark { get; set; }

        [NotMapped]
        public string ScriptHash { get => GetScriptHash().ToString(); }

        [NotMapped]
        public decimal Nep17ValueUsd { get; set; }

        public UInt160 GetScriptHash() => Contract.CreateMultiSigContract(Threshold, PublicKeys.Split(',').ToList().ConvertAll(p => ECPoint.Parse(p, ECCurve.Secp256r1))).ScriptHash;

        public byte[] GetScript() => Contract.CreateMultiSigContract(Threshold, PublicKeys.Split(',').ToList().ConvertAll(p => ECPoint.Parse(p, ECCurve.Secp256r1))).Script;
    }

    public class AccountRequest
    {
        public string[] PublicKeys { get; set; }

        public int Threshold { get; set; }

        public string? Remark { get; set; }
    }

    public class AccountResponse
    {
        public AccountResponse(Account p, User currentUser)
        {
            Address = p.Address;
            Owners = p.Owners.Split(',');
            Threshold = p.Threshold;
            Remark = p.Remark.FirstOrDefault(p => p.User == currentUser)?.RemarkName;
            CreateTime = p.Remark.Min(p => p.CreateTime);
            Nep17ValueUsd = Helper.GetNep17AssetsValue(p.Address);
        }

        public string Address { get; set; }

        public string[] Owners { get; set; }

        public int Threshold { get; set; }

        public string? Remark { get; set; }

        public DateTime CreateTime { get; set; }

        public decimal Nep17ValueUsd { get; set; }
    }
}