using Akka.IO;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.CodeAnalysis.Elfie.Extensions;
using Neo;
using Neo.Cryptography;
using Neo.Cryptography.ECC;
using Neo.IO;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Network.RPC;
using Neo.SmartContract;
using Neo.Wallets;
using NexoAPI.Models;
using System.Globalization;
using System.Security.Cryptography;
using System.Security.Policy;
using System.Text;
using System.Text.RegularExpressions;

namespace NexoAPI
{
    public static class Helper
    {
        public static uint Network = 0x334F454Eu;

        public static List<NonceInfo> Nonces = new List<NonceInfo>();

        public static bool PublicKeyIsValid(string input) => new Regex("^(0[23][0-9a-f]{64})$").IsMatch(input);

        public static bool SignatureIsValid(string input) => new Regex("^([0-9a-f][0-9a-f])+$").IsMatch(input);

        public static RpcClient Client = new (new Uri("http://seed1.neo.org:20332"), null, null, ProtocolSettings.Load("config.json"));

        //https://neoline.io/signMessage/
        public static byte[] Message2ParameterOfNeoLineSignMessageFunction(string message)
        {
            var parameterHexString = Encoding.UTF8.GetBytes(message).ToHexString();
            var variableBytes = Num2VarInt(parameterHexString.Length / 2);
            return ("010001f0" + variableBytes + parameterHexString + "0000").HexToBytes();

            static string Num2VarInt(long num)
            {
                return num switch
                {
                    < 0xfd => Num2hexstring(num, 1),                // uint8
                    <= 0xffff => "fd" + Num2hexstring(num, 2),      // uint16
                    <= 0xffffffff => "fe" + Num2hexstring(num, 4),  // uint32
                    _ => "ff" + Num2hexstring(num, 8)               // uint64
                };
            }

            static string Num2hexstring(long num, int size) => BitConverter.GetBytes(num).Take(size).ToArray().ToHexString();
        }

        public static byte[] GetSignData(UInt256 txHash)
        {
            using MemoryStream ms = new();
            using BinaryWriter writer = new(ms);
            writer.Write(Network);
            writer.Write(txHash);
            writer.Flush();
            return ms.ToArray();
        }

        public static byte[] HexToBytes(this string value)
        {
            if (value is null || value.Length == 0)
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
            return BitConverter.ToString(obj.ComputeHash(Encoding.UTF8.GetBytes(input))).Replace("-", string.Empty);
        }

        public static bool VerifySignature(byte[] message, string pubkey, string signatureHex)
         => VerifySignature(message, signatureHex.HexToBytes(), Neo.Cryptography.ECC.ECPoint.Parse(pubkey, Neo.Cryptography.ECC.ECCurve.Secp256r1));

        //https://github.com/neo-project/neo/blob/master/src/Neo/Cryptography/Crypto.cs#L73
        public static bool VerifySignature(ReadOnlySpan<byte> message, ReadOnlySpan<byte> signature, Neo.Cryptography.ECC.ECPoint pubkey)
        {
            if (signature.Length != 64) return false;
            byte[] buffer = pubkey.EncodePoint(false);
            using var ecdsa = ECDsa.Create(new ECParameters
            {
                Curve = System.Security.Cryptography.ECCurve.NamedCurves.nistP256,
                Q = new System.Security.Cryptography.ECPoint
                {
                    X = buffer[1..33],
                    Y = buffer[33..]
                }
            });
            return ecdsa.VerifyData(message, signature, HashAlgorithmName.SHA256);
        }

        public static byte[] GetSignData(byte[] hash, uint network)
        {
            using MemoryStream ms = new();
            using BinaryWriter writer = new(ms);
            writer.Write(network);
            writer.Write(new UInt256(hash));
            writer.Flush();
            return ms.ToArray();
        }
    }
}
