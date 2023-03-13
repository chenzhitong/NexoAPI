using Neo;
using Neo.Cryptography.ECC;
using Neo.SmartContract;
using Neo.Wallets;
using System.ComponentModel.DataAnnotations.Schema;

namespace NexoAPI.Models
{
    public class Account
    {
        public int Id { get; }

        public string Address { get; set; }

        public string Owners { get; set; }

        public int Threshold { get; set; }

        public string MultiSignScriptHash { get => GetScriptHash().ToString(); }

        public UInt160 GetScriptHash() => Contract.CreateMultiSigContract(Threshold, Owners.Split(',').ToList().ConvertAll(p => ECPoint.Parse(p, ECCurve.Secp256r1))).ScriptHash;

        public string GetAddress() => GetScriptHash().ToAddress(0x35);

        public DateTime CreateTime { get; set; }

        [NotMapped]
        public decimal Nep17ValueUsd { get; set; }
    }
}
