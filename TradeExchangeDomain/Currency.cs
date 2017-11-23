namespace TradeExchangeDomain
{
    public class Currency
    {
        private Currency(string name)
        {
        }

        public static Currency Usd { get; } = new Currency("USD");
        public static Currency Btc { get; } = new Currency("BTC");
    }
}