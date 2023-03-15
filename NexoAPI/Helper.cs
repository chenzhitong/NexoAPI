using Microsoft.CodeAnalysis.Elfie.Extensions;
using Neo;
using Neo.Cryptography.ECC;
using Neo.SmartContract;
using Neo.Wallets;
using NexoAPI.Models;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace NexoAPI
{
    public static class Helper
    {
        public static List<NonceInfo> Nonces = new List<NonceInfo>();

        public static byte[] HexToBytes(this string value)
        {
            if (value == null || value.Length == 0)
                return Array.Empty<byte>();
            if (value.Length % 2 == 1)
                throw new FormatException();
            byte[] result = new byte[value.Length / 2];
            for (int i = 0; i < result.Length; i++)
                result[i] = byte.Parse(value.Substring(i * 2, 2), NumberStyles.AllowHexSpecifier);
            return result;
        }
        public static string Sha256(this string input)
        {
            using SHA256 obj = SHA256.Create();
            return BitConverter.ToString(obj.ComputeHash(Encoding.UTF8.GetBytes(input))).Replace("-", "");
        }

        public static bool VerifySignature(string messageHash, string pubkey, string signature)
         => VerifySignature(Encoding.UTF8.GetBytes(messageHash), signature.HexToBytes(), pubkey.HexToBytes());

        public static bool VerifySignature(byte[] message, byte[] signature, byte[] pubkey)
        {
            if (pubkey.Length == 33 && (pubkey[0] == 0x02 || pubkey[0] == 0x03))
            {
                try
                {
                    pubkey = Neo.Cryptography.ECC.ECPoint.DecodePoint(pubkey, Neo.Cryptography.ECC.ECCurve.Secp256r1).EncodePoint(false).Skip(1).ToArray();
                }
                catch
                {
                    return false;
                }
            }
            else if (pubkey.Length == 65 && pubkey[0] == 0x04)
            {
                pubkey = pubkey.Skip(1).ToArray();
            }
            else if (pubkey.Length != 64)
            {
                throw new ArgumentException("pubkey is incorrect.");
            }
            using var ecdsa = ECDsa.Create(new ECParameters
            {
                Curve = System.Security.Cryptography.ECCurve.NamedCurves.nistP256,
                Q = new System.Security.Cryptography.ECPoint
                {
                    X = pubkey.Take(32).ToArray(),
                    Y = pubkey.Skip(32).ToArray()
                }
            });
            return ecdsa.VerifyData(message, signature, HashAlgorithmName.SHA256);
        }
    }
}
