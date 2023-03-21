using Akka.Actor;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Neo.IO;
using Neo.Wallets;
using Newtonsoft.Json.Linq;
using NexoAPI.Models;
using System.Collections.Generic;

namespace NexoAPI.Controllers
{
    [Route("nep17-balances")]
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
                return StatusCode(StatusCodes.Status400BadRequest, new { code = 400, message = "Address is incorrect.", data = $"Address: {address}" });
            }
            var response = Helper.PostWebRequest("https://explorer.onegate.space/api", "{\"jsonrpc\":\"2.0\",\"id\":1,\"params\":{\"Address\":\"" + scriptHash + "\",\"Limit\":100,\"Skip\":0},\"method\":\"GetAssetsHeldByAddress\"}");
            var jobject = JObject.Parse(response);
            if ((double)jobject["result"]["totalCount"] > 100)
            {
                return StatusCode(StatusCodes.Status400BadRequest, new { code = 400, message = "Too many assets in this address, more than 100 assets.", data = $"Address: {address}" });
            }
            var result = new List<Nep17BalanceResponse>();
            foreach (var item in jobject["result"]["result"])
            {
                if(string.IsNullOrEmpty(item["tokenid"].ToString()))
                result.Add(new Nep17BalanceResponse() { Address = address, Amount = item["balance"].ToString(), ContractHash = item["asset"].ToString() });
            }
            return new ObjectResult(result);
        }
    }
}
