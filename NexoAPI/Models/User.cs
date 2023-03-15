namespace NexoAPI.Models
{
    public class User
    {
        public int Id { get; set; }

        public string Address { get; set; }

        public string PublicKey { get; set; }

        public string Token { get; set; }

        public DateTime CreateTime { get; set; }

        public ICollection<Remark> Remark { get; set; }
    }
}
