using System.Collections.Generic;
using Akka.Persistence;

namespace TradeExchangeDomain
{
    public class UserBalance : ReceivePersistentActor
    {
        public IDictionary<Currency, Money> Balances { get; } = new Dictionary<Currency, Money>();
        public override string PersistenceId { get; }

        public class NotEnoughFunds
        {
        }
    }
}