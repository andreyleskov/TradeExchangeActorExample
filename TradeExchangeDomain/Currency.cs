namespace TradeExchangeDomain
{
    public class Currency
    {
        public string Name { get; }

        private Currency(string name)
        {
            Name = name;
        }

        public static Currency Usd { get; } = new Currency("USD");
        public static Currency Btc { get; } = new Currency("BTC");
    }
}