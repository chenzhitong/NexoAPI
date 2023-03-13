using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NexoAPI.Models;
using NexoAPI.Data;

namespace NexoAPI.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class AddressesController : ControllerBase
    {
        private readonly NexoAPIContext _context;

        public AddressesController(NexoAPIContext context)
        {
            _context = context;
        }

        /// <summary>
        /// 查询包含某个公钥的所有多签地址
        /// </summary>
        /// <param name="pubkey">用户的公钥</param>
        /// <param name="signature">用上面的公钥对应的私钥对message字段进行签名</param>
        /// <param name="message">签名的文本，固定为“GetAddress”</param>
        /// <returns></returns>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Account>>> GetAddress(string pubkey, string signature, string message= "GetAddress")
        {
            if (!Helper.VerifySignature(pubkey, signature, message))
            {
                return BadRequest("Verify Signature Filed");
            }
            var address = await _context.Address.Where(p => p.Owners.Contains(pubkey)).ToListAsync();

            if (address == null)
            {
                return NotFound();
            }

            return address;
        }

        // POST: api/Addresses
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<Account>> PostAddress(Account address)
        {
            _context.Address.Add(address);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetAddress", new { id = address.Id }, address);
        }
    }
}
