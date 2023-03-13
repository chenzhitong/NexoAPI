using System.ComponentModel.DataAnnotations.Schema;

namespace NexoAPI.Models
{
    [NotMapped]
    public class NonceInfo
    {
        public string Nonce { get; set; }

        public DateTime CreateTime { get; set; }
    }
}
