namespace NexoAPI.Models
{
    public class SignResult
    {
        public int Id { get; set; }

        public Transaction Transaction { get; set; }

        public User Signer { get; set; }

        public bool Approved { get; set; }

        public string? Signature { get; set; }
    }

    public class SignResultRequest
    {
        public string Approved { get; set; }

        public string? Signature { get; set; }
    }

    public class SignResultResponse
    {
        public string TransactionHash { get; set; }

        public string Signer { get; set; }

        public bool Approved { get; set; }

        public string? Signature { get; set; }
    }
}