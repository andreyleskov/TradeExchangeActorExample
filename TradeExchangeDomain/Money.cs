namespace TradeExchangeDomain
{
    /// <summary>
    ///  did not find good money package for netstandard with custom curriencies support
    /// </summary>
    public class Money
    {
        public decimal Amount { get; }
        public Currency Currency { get; }

        public Money(decimal amount, Currency cur)
        {
            Amount = amount;
            Currency = cur;
        }
    }
}