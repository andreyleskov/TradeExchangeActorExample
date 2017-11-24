using Akka.Actor;

namespace TradeExchangeDomain
{
    public class BuyOrderActor : OrderActor
    {
        public BuyOrderActor()
        {
            Command<Execute>(e => e.OrderBook.Tell(new NewBuyOrder(Order), Self));
        }

        protected override void OnExecuted(OrderBookActor.OrderExecuted e, IActorRef sender)
        {
            BalanceRef.Tell(new UserBalance.AddDueOrderExecuted(e.OrderNum, Order.Position.Target.Emit(e.Amount)));
        }
    }
}