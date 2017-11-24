using Akka.Actor;
using Akka.Persistence;

namespace TradeExchangeDomain
{
    public class SellOrderActor : OrderActor
    {
        public SellOrderActor()
        {
            Command<Execute>(e => e.OrderBook.Tell(new NewSellOrder(Order), Self));
        }

        protected override void OnExecuted(OrderBookActor.OrderExecuted e, IActorRef sender)
        {
            BalanceRef.Tell(new UserBalance.AddDueOrderExecuted(e.OrderNum, e.Price * e.Amount));
        }
    }
}