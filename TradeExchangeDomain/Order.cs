using System;

namespace TradeExchangeDomain
{
    public class Order
    {
        public Symbol Position { get; }
        public Money Price { get; } // per 1 Amount, no Lot support
        public decimal Amount { get; }
        public DateTime Time { get; }
        public string Id { get; }

        public Order(Symbol position, Money price, decimal amount, string id = null,DateTime? time = null)
        {
            Position = position;
            Price = price;
            Amount = amount;
            Time = time ?? DateTime.Now;
            Id = id ?? Guid.NewGuid().ToString();
        }
    }
}