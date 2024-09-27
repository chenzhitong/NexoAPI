using Neo.Cryptography.ECC;
using Neo.SmartContract;

namespace NexoAPI.Models
{
    public class User
    {
        public int Id { get; set; }

        public string Address { get; set; }

        public string PublicKey { get; set; }

        public string Token { get; set; }

        public DateTime CreateTime { get; set; }

        public ICollection<Remark> Remark { get; set; }

        public ICollection<SignResult> SignResult { get; set; }

        public byte[] GetScript() => Contract.CreateSignatureContract(ECPoint.Parse(PublicKey, ECCurve.Secp256r1)).Script;
    }

    public class UserRequest
    {
        public string Nonce { get; set; }

        public string Signature { get; set; }

        public string PublicKey { get; set; }

        public int SignatureVersion { get; set; } = 1;
    }

    public class UserResponse
    {
        public UserResponse(User p)
        {
            Address = p.Address;
            PublicKey = p.PublicKey;
        }

        public string Address { get; set; }

        public string PublicKey { get; set; }
    }
}