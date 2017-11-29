namespace TradeExchangeDomain
{
    /// <summary>
    ///     did not find good money package for netstandard with custom curriencies support
    /// </summary>
    public class Money
    {
        private readonly string _description;

        public Money(decimal amount, Currency cur)
        {
            Amount = amount;
            Currency = cur;
            _description = $"{amount} {cur}";
        }

        public decimal Amount { get; }
        public Currency Currency { get; }

        public override bool Equals(object obj)
        {
            if (obj is Money m)
                return m.Equals(this);
            return false;
        }

        protected bool Equals(Money other)
        {
            return Amount == other.Amount
                   && Equals(Currency, other.Currency);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Amount.GetHashCode() * 397) ^ (Currency != null ? Currency.GetHashCode() : 0);
            }
        }

        public static bool operator ==(Money a, Money b)
        {
            CheckCurrency(a, b);

            return a?.Amount == b?.Amount;
        }
        public static bool operator !=(Money a, Money b)
        {
            return ! (a== b);
        }
        
        public static Money operator +(Money a, Money b)
        {
            CheckCurrency(a, b);

            return new Money(a.Amount + b.Amount, a.Currency);
        }

        public static Money operator -(Money a, Money b)
        {
            CheckCurrency(a, b);

            return new Money(a.Amount - b.Amount, a.Currency);
        }

        public static Money operator *(Money a, decimal b)
        {
            return new Money(a.Amount * b, a.Currency);
        }

        public static Money operator /(Money a, decimal b)
        {
            return new Money(a.Amount / b, a.Currency);
        }

        public static bool operator >(Money a, Money b)
        {
            CheckCurrency(a, b);
            return a.Amount > b.Amount;
        }

        public static bool operator >=(Money a, Money b)
        {
            CheckCurrency(a, b);
            return a.Amount >= b.Amount;
        }

        public static bool operator <(Money a, Money b)
        {
            CheckCurrency(a, b);
            return a.Amount < b.Amount;
        }

        public static bool operator <=(Money a, Money b)
        {
            CheckCurrency(a, b);
            return a.Amount <= b.Amount;
        }

        private static void CheckCurrency(Money a, Money b)
        {
            if (a.Currency.Name != b.Currency.Name)
                throw new CurrencyMismatchException();
        }


        public override string ToString()
        {
            return _description;
        }
    }
}