using System.ComponentModel.DataAnnotations;

namespace NexoAPI.Models
{
    public class Remark
    {
        [Key]
        public int Id { get; set; }

        public User User { get; set; }

        public Account Account { get; set; }

        public string RemarkName { get; set; }

        public DateTime CreateTime { get; set; }
    }
}
