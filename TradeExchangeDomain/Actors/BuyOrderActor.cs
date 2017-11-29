using Akka.Actor;
using TradeExchangeDomain.Orders;

namespace TradeExchangeDomain
{
    public class BuyOrderActor : OrderActor
    {

        public BuyOrderActor()
        {
            IActorRef orderReceivedWatcher = null;

            Command<Execute>(e =>
                             {
                                 e.OrderBook.Tell(new BuyOrder(Order));
                                 orderReceivedWatcher = Sender;
                             });
            Command<OrderBookActor.OrderReceived>(r => orderReceivedWatcher.Forward(r));
            Command<GetBalance>(e => Sender.Tell(new OrderBalance(Order.Price * Order.Amount)));
        }

        protected override void OnExecuted(OrderBookActor.OrderExecuted e, IActorRef sender)
        {
            BalanceRef.Tell(new UserBalance.AddDueOrderExecuted(e.OrderNum, Order.Position.Target.Emit(e.Amount)));
        }
    }
}