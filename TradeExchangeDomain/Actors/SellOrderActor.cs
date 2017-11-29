using Akka.Actor;
using TradeExchangeDomain.Orders;

namespace TradeExchangeDomain
{
    public class SellOrderActor : OrderActor
    {
      
        protected override void OnExecuting(Execute e)
        {
            e.OrderBook.Tell(new SellOrder(Order));
        }

        protected override void OnExecuted(OrderBookActor.OrderExecuted e, IActorRef sender)
        {
            BalanceRef.Tell(new UserBalance.AddDueOrderExecuted(e.OrderNum, e.Price * e.Amount));
        }
    }
}