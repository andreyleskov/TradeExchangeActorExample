using System;
using System.Reflection.Emit;

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

        public static Money operator +(Money a, Money b)
        {
            CheckCurrency(a, b);
            
            return new Money(a.Amount + b.Amount,a.Currency);
        }

        public static Money operator -(Money a, Money b)
        {
            CheckCurrency(a, b);
            
            return new Money(a.Amount - b.Amount,a.Currency);
        }
        
        public static Money operator *(Money a, decimal b)
        {
            return new Money(a.Amount * b,a.Currency);
        }
        
        public static Money operator /(Money a, decimal b)
        {
            return new Money(a.Amount / b,a.Currency);
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

        public bool IsNegative()
        {
            return Amount < 0;
        }
    }

    public class CurrencyMismatchException : Exception
    {
    }
}