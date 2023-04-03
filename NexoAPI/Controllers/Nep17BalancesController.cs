﻿using Microsoft.AspNetCore.Mvc;
using Neo.Network.RPC;
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
                scriptHash = address.ToScriptHash(0x35).ToString();
            }
            catch (Exception)
            {
                return StatusCode(StatusCodes.Status400BadRequest, new { code = "InvalidParameter", message = "Address is incorrect.", data = $"Address: {address}" });
            }
            var response = Helper.PostWebRequest(_config["OneGateExplorerAPI"], "{\"jsonrpc\":\"2.0\",\"id\":1,\"params\":{\"Address\":\"" + scriptHash + "\",\"Limit\":100,\"Skip\":0},\"method\":\"GetAssetsHeldByAddress\"}");
            var jobject = JObject.Parse(response);
            if ((double)(jobject?["result"]?["totalCount"] ?? 0) > 100)
            {
                return StatusCode(StatusCodes.Status400BadRequest, new { code = "InternalError", message = "Too many assets in this address, more than 100 assets.", data = $"Address: {address}" });
            }
            var result = new List<Nep17BalanceResponse>();
            foreach (var item in jobject?["result"]?["result"] ?? Enumerable.Empty<JToken>())
            {
                if (string.IsNullOrEmpty(item?["tokenid"]?.ToString()))
                {
                    var tokenId = item?["asset"]?.ToString() ?? string.Empty;
                    var tokenInfo = new Nep17API(Helper.Client(_config)).GetTokenInfoAsync(tokenId).Result;
                    var amount = Helper.ChangeToDecimal(item?["balance"]?.ToString() ?? "0") / (decimal)Math.Pow(10, tokenInfo.Decimals);
                    result.Add(new Nep17BalanceResponse() { Address = address, Amount = amount.ToString(), ContractHash = tokenId });
                }
            }
            return new ObjectResult(result);
        }
    }
}
