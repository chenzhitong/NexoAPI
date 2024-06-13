using Microsoft.AspNetCore.Mvc;
using Neo.Network.RPC;
using Neo.Network.RPC.Models;
using Newtonsoft.Json.Linq;
using NexoAPI.Models;

namespace NexoAPI.Controllers
{
    [Route("nep17-balances")]
    [Produces("application/json")]
    [ApiController]
    public class Nep17BalancesController : Controller
    {
        private readonly IConfiguration _config;

        public Nep17BalancesController(IConfiguration config)
        {
            _config = config;
        }

        [HttpGet]
        public ObjectResult GetList(string address)
        {
            //address 检查
            string scriptHash;
            try
            {
                scriptHash = address.ToScriptHash().ToString();
            }
            catch (Exception)
            {
                return StatusCode(StatusCodes.Status400BadRequest, new { code = "InvalidParameter", message = "Address is incorrect.", data = $"Address: {address}" });
            }
            JObject jobject;
            try
            {
                var response = Helper.PostWebRequest(_config["OneGateExplorerAPI"], "{\"jsonrpc\":\"2.0\",\"id\":1,\"params\":{\"Address\":\"" + scriptHash + "\",\"Limit\":100,\"Skip\":0},\"method\":\"GetAssetsHeldByAddress\"}");
                jobject = JObject.Parse(response);
                if ((double)(jobject?["result"]?["totalCount"] ?? 0) > 100)
                {
                    return StatusCode(StatusCodes.Status400BadRequest, new { code = "InternalError", message = "Too many assets in this address, more than 100 assets.", data = $"Address: {address}" });
                }
            }
            catch (Exception)
            {
                return StatusCode(StatusCodes.Status400BadRequest, new { code = "InternalError", message = "An error occurs when requesting the OneGateExplorer API." });
            }
            var result = new List<Nep17BalanceResponse>();
            foreach (var item in jobject?["result"]?["result"] ?? Enumerable.Empty<JToken>())
            {
                if (string.IsNullOrEmpty(item?["tokenid"]?.ToString()))
                {
                    var tokenId = item?["asset"]?.ToString() ?? string.Empty;
                    RpcNep17TokenInfo tokenInfo;
                    try
                    {
                        tokenInfo = new Nep17API(Helper.Client).GetTokenInfoAsync(tokenId).Result;
                    }
                    catch (Exception ex)
                    {
                        return StatusCode(StatusCodes.Status400BadRequest, new { code = "InternalError", message = $"An error occurred while requesting the seed node: {ex.Message}", data = $"Seed node: {ConfigHelper.AppSetting("SeedNode")}" });
                    }
                    var amount = Helper.ChangeToDecimal(item?["balance"]?.ToString() ?? "0") / (decimal)Math.Pow(10, tokenInfo.Decimals);
                    result.Add(new Nep17BalanceResponse() { Address = address, Amount = amount.ToString(), ContractHash = tokenId });
                }
            }
            return new ObjectResult(result);
        }
    }
}