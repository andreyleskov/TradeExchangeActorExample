using Akka.Actor;
using TradeExchangeDomain.Orders;

namespace TradeExchangeDomain
{
    public class BuyOrderActor : OrderActor
    {

      
        protected override void OnExecuting(Execute e)
        {
            e.OrderBook.Tell(new BuyOrder(Order));

        }

        protected override void OnExecuted(OrderBookActor.OrderExecuted e, IActorRef sender)
        {
            BalanceRef.Tell(new UserBalance.AddDueOrderExecuted(e.OrderNum, Order.Position.Target.Emit(e.Amount)));
        }
    }
}