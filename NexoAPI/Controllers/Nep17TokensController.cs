using Microsoft.AspNetCore.Mvc;
using Neo;
using Neo.Network.RPC;
using Newtonsoft.Json.Linq;
using NexoAPI.Models;
using NuGet.Protocol;

namespace NexoAPI.Controllers
{
    [Route("nep17-tokens")]
    [Produces("application/json")]
    [ApiController]
    public class Nep17TokensController : Controller
    {
        [HttpGet]
        public ObjectResult GetList([FromQuery] string[] contractHashes)
        {
            var result = new List<Nep17TokenResponse>();
            foreach (var item in contractHashes)
            {
                if (UInt160.TryParse(item, out UInt160 hash))
                {
                    // get nep17 token info
                    try
                    {
                        var tokenInfo = new Nep17API(Helper.Client).GetTokenInfoAsync(hash).Result;
                        result.Add(new Nep17TokenResponse() { ContractHash = item, Symbol = tokenInfo.Symbol, Decimals = tokenInfo.Decimals });
                    }
                    catch (Exception)
                    {
                        return StatusCode(StatusCodes.Status400BadRequest, new { code = 400, message = "The contract is not found in the current network.", data = $"Contract hash: {item}, Network: {ProtocolSettings.Default.Network}" });
                    }
                }
                else
                {
                    return StatusCode(StatusCodes.Status400BadRequest, new { code = 400, message = "Contract hash format error.", data = $"Contract hash: {item}" });
                }
            }
            var response = JToken.Parse(Helper.PostWebRequest("https://onegate.space/api/quote?convert=usd", contractHashes.ToJson()));
            for (int i = 0; i < result.Count; i++)
            {
                result[i].PriceUsd = response?[i]?.ToString() ?? "0";
            }

            return new ObjectResult(result);
        }
    }
}
