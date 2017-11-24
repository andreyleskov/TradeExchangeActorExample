namespace TradeExchangeDomain
{
    public static class MoneyExtensions
    {
        public static Money Usd(this decimal amount)
        {
            return new Money(amount, Currency.Usd);
        }

        public static Money Usd(this int amount)
        {
            return new Money(amount, Currency.Usd);
        }
    }
}