using Akka.Actor;

namespace TradeExchangeDomain
{
    public class SellOrderActor : OrderActor
    {
        public SellOrderActor()
        {
            Command<Execute>(e => e.OrderBook.Forward(new NewSellOrder(Order)));
        }

        protected override void OnExecuted(OrderBookActor.OrderExecuted e, IActorRef sender)
        {
            BalanceRef.Tell(new UserBalance.AddDueOrderExecuted(e.OrderNum, e.Price * e.Amount));
        }
    }
}