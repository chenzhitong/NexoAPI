using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NexoAPI.Models
{
    [NotMapped]
    public class NonceInfo
    {
        [Key]
        public string Nonce { get; set; }

        public DateTime CreateTime { get; set; }
    }
}
