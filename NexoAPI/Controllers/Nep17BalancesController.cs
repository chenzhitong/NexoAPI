using Microsoft.AspNetCore.Mvc;
using Neo.Wallets;
using Newtonsoft.Json.Linq;
using NexoAPI.Models;

namespace NexoAPI.Controllers
{
    [Route("nep17-balances")]
    [Produces("application/json")]
    [ApiController]
    public class Nep17BalancesController : Controller
    {
        [HttpGet]
        public ObjectResult GetList(string address)
        {
            //address 检查
            string scriptHash;
            try
            {
                scriptHash = address.ToScriptHash(0x35).ToString();
            }
            catch (Exception)
            {
                return StatusCode(StatusCodes.Status400BadRequest, new { code = "InvalidParameter", message = "Address is incorrect.", data = $"Address: {address}" });
            }
            var response = Helper.PostWebRequest("https://explorer.onegate.space/api", "{\"jsonrpc\":\"2.0\",\"id\":1,\"params\":{\"Address\":\"" + scriptHash + "\",\"Limit\":100,\"Skip\":0},\"method\":\"GetAssetsHeldByAddress\"}");
            var jobject = JObject.Parse(response);
            if ((double)(jobject?["result"]?["totalCount"] ?? 0) > 100)
            {
                return StatusCode(StatusCodes.Status400BadRequest, new { code = "InternalError", message = "Too many assets in this address, more than 100 assets.", data = $"Address: {address}" });
            }
            var result = new List<Nep17BalanceResponse>();
            foreach (var item in jobject?["result"]?["result"] ?? Enumerable.Empty<JToken>())
            {
                if (string.IsNullOrEmpty(item?["tokenid"]?.ToString()))
                    result.Add(new Nep17BalanceResponse() { Address = address, Amount = item?["balance"]?.ToString() ?? "0", ContractHash = item?["asset"]?.ToString() ?? string.Empty });
            }
            return new ObjectResult(result);
        }
    }
}
