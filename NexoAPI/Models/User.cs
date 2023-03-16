using Akka.Actor;

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

    public class UserRequest
    {
        public string Nonce { get; set; }

        public string Signature { get; set; }

        public string PublicKey { get; set; }
    }

    public class UserResponse
    {
        public UserResponse(User p)
        {
            Address = p.Address;
            PublicKey = p.PublicKey;
        }

        public string Address { get; set; }

        public string PublicKey { get; set; }
    }
}
