using Microsoft.CodeAnalysis.Elfie.Extensions;
using Microsoft.EntityFrameworkCore;
using Neo;
using Neo.IO;
using Neo.Network.RPC;
using Neo.SmartContract;
using Neo.Wallets;
using Newtonsoft.Json.Linq;
using NexoAPI.Models;
using NLog;
using NuGet.Protocol;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace NexoAPI
{
    public static partial class Helper
    {
        private static readonly uint Network = 0x334F454Eu;

        public static List<NonceInfo> Nonces = new();

        [GeneratedRegex("^Bearer [0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$")]
        private static partial Regex AuthorizationRegex();
        public static bool AuthorizationIsValid(string input, out string output)
        {
            output = input.Replace("Bearer ", string.Empty);
            return AuthorizationRegex().IsMatch(input);
        }

        [GeneratedRegex("^0[23][0-9a-f]{64}$")]
        private static partial Regex PublicKeyRegex();
        public static bool PublicKeyIsValid(string input) => PublicKeyRegex().IsMatch(input);

        [GeneratedRegex("^[0-9a-f]{128}$")]
        private static partial Regex SignatureRegex();
        public static bool SignatureIsValid(string input) => SignatureRegex().IsMatch(input);

        public static RpcClient Client = new(new Uri("http://seed1.neo.org:10332"), null, null, null);

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
            return BitConverter.ToString(SHA256.HashData(Encoding.UTF8.GetBytes(input))).Replace("-", string.Empty);
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
                Curve = ECCurve.NamedCurves.nistP256,
                Q = new ECPoint
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

        public static string PostWebRequest(string postUrl, string paramData)
        {
            try
            {
                var result = string.Empty;
                var httpContent = new StringContent(paramData);
                httpContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json")
                {
                    CharSet = "utf-8"
                };
                using var httpClient = new HttpClient();
                var response = httpClient.PostAsync(postUrl, httpContent).Result;
                if (response.IsSuccessStatusCode)
                {
                    result = response.Content.ReadAsStringAsync().Result;
                }
                return result;
            }
            catch (Exception)
            {
                throw;
            }
        }

        public async static Task<uint> GetBlockCount() => await Client.GetBlockCountAsync().ConfigureAwait(false);

        public static decimal GetNep17AssetsValue(string address)
        {
            var scriptHash = address.ToScriptHash(0x35).ToString();
            // 查询该地址上所有NEP-17资产的合约地址
            var response = PostWebRequest("https://explorer.onegate.space/api", "{\"jsonrpc\":\"2.0\",\"id\":1,\"params\":{\"Address\":\"" + scriptHash + "\",\"Limit\":100,\"Skip\":0},\"method\":\"GetAssetsHeldByAddress\"}");
            var jobject = JObject.Parse(response);
            var list = new List<TokenBalance>();
            if (jobject?["result"]?["result"] is null)
                return 0;
            foreach (var item in (jobject?["result"]?["result"] ?? Enumerable.Empty<JToken>()).Where(item => string.IsNullOrEmpty(item?["tokenid"]?.ToString())))
            {
                var asset = item["asset"]?.ToString() ?? string.Empty;
                var amount = ChangeToDecimal(item["balance"]?.ToString() ?? "0");
                var tokenInfo = new Nep17API(Client).GetTokenInfoAsync(asset).Result;
                var trueBalance = amount / (decimal)Math.Pow(10, tokenInfo.Decimals);
                list.Add(new TokenBalance() { ContractHash = asset, TrueBalcnce = trueBalance });
            }

            var response2 = JToken.Parse(PostWebRequest("https://onegate.space/api/quote?convert=usd", list.Select(p => p.ContractHash).ToArray().ToJson()));
            var sum = 0m;
            for (int i = 0; i < list.Count; i++)
            {
                sum += ChangeToDecimal(response2?[i]?.ToString() ?? "0") * list[i].TrueBalcnce; ;
            }

            return sum;
        }
        public static decimal ChangeToDecimal(string strData)
        {
            return strData.Contains('E', StringComparison.OrdinalIgnoreCase) ? Convert.ToDecimal(decimal.Parse(strData.ToString(), NumberStyles.Float)) : Convert.ToDecimal(strData);
        }
    }

    class TokenBalance
    {
        public string ContractHash { get; set; }

        public decimal TrueBalcnce { get; set; }
    }
}
