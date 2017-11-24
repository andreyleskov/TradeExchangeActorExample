namespace TradeExchangeDomain
{
    public static class CurrencyExtensions
    {
        public static Money Emit(this Currency currency, decimal amount)
        {
            return new Money(amount, currency);
        }
    }
}