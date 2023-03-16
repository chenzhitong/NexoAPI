namespace NexoAPI.Models
{
    public class SignResult
    {
        public int Id { get; set; }

        public Transaction Transaction { get; set; }

        public Account Signer {get; set; }

        public bool Approved { get; set; }

        public bool Signature { get; set; }
    }
}
