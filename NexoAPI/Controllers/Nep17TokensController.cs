using Microsoft.AspNetCore.Mvc;
using Neo;
using Neo.Network.RPC;
using Neo.Network.RPC.Models;
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
        private readonly IConfiguration _config;

        public Nep17TokensController(IConfiguration config)
        {
            _config = config;
        }

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
                        RpcNep17TokenInfo tokenInfo;
                        try
                        {
                            tokenInfo = new Nep17API(Helper.Client).GetTokenInfoAsync(hash).Result;
                        }
                        catch (Exception)
                        {
                            return StatusCode(StatusCodes.Status400BadRequest, new { code = "InternalError", message = "Unable to connect to seed node.", data = $"Seed node: {ConfigHelper.AppSetting("SeedNode")}" });
                        }
                        result.Add(new Nep17TokenResponse() { ContractHash = item, Symbol = tokenInfo.Symbol, Decimals = tokenInfo.Decimals });
                    }
                    catch (Exception)
                    {
                        return StatusCode(StatusCodes.Status400BadRequest, new { code = "InvalidParameter", message = "The contract is not found in the current network.", data = $"Contract hash: {item}, Network: {ProtocolSettings.Default.Network}" });
                    }
                }
                else
                {
                    return StatusCode(StatusCodes.Status400BadRequest, new { code = "InvalidParameter", message = "Contract hash format error.", data = $"Contract hash: {item}" });
                }
            }
            
            var response = JToken.Parse(Helper.PostWebRequest(_config["OneGateQuoteAPI"], contractHashes.ToJson()));
            for (int i = 0; i < result.Count; i++)
            {
                result[i].PriceUsd = response?[i]?.ToString() ?? "0";
            }

            return new ObjectResult(result);
        }
    }
}
