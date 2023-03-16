using Neo;
using Neo.Cryptography.ECC;
using Neo.SmartContract;
using Neo.Wallets;
using System.ComponentModel.DataAnnotations.Schema;

namespace NexoAPI.Models
{
    public class Account
    {
        public int Id { get; set; }

        public string Address { get; set; }

        public string Owners { get; set; }

        public int Threshold { get; set; }

        public ICollection<Remark> Remark { get; set; }

        [NotMapped]
        public string ScriptHash { get => GetScriptHash().ToString(); }

        [NotMapped]
        public decimal Nep17ValueUsd { get; set; }

        public UInt160 GetScriptHash() => Contract.CreateMultiSigContract(Threshold, Owners.Split(',').ToList().ConvertAll(p => ECPoint.Parse(p, ECCurve.Secp256r1))).ScriptHash;
    }

    public class AccountRequest
    {
        public string[] PublicKeys { get; set; }

        public int Threshold { get; set; }

        public string Remark { get; set; }
    }

    public class AccountResponse
    {
        public AccountResponse(Account p)
        {
            Address = p.Address;
            Owners = p.Owners.Split(',');
            Threshold = p.Threshold;
            Remark = p.Remark.First().RemarkName;
            CreateTime = p.Remark.First().CreateTime;
        }

        public string Address { get; set; }

        public string[] Owners { get; set; }

        public int Threshold { get; set; }

        public string Remark { get; set; }

        public DateTime CreateTime{ get; set; }

        public decimal Nep17ValueUsd { get; set; }
    }
}
