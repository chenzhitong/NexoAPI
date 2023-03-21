namespace NexoAPI.Models
{
    public class Nep17TokenResponse
    {
        public string ContractHash { get; set; }
        public string Symbol { get; set; }
        public int Decimals { get; set; }
        public string PriceUsd { get; set; }
    }

    public class Nep17BalanceResponse
    {
        public string ContractHash { get; set; }
        public string Address { get; set; }
        public string Amount { get; set; }
    }
}
