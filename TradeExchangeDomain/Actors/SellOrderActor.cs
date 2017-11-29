using Akka.Actor;
using TradeExchangeDomain.Orders;

namespace TradeExchangeDomain
{
    public class SellOrderActor : OrderActor
    {
        public SellOrderActor()
        {
            IActorRef orderReceivedWatcher = null;
            Command<Execute>(e =>
                             {
                                 e.OrderBook.Tell(new SellOrder(Order));
                                 orderReceivedWatcher = Sender;
                             });
            Command<OrderBookActor.OrderReceived>(r => orderReceivedWatcher.Forward(r));
            Command<GetBalance>(e => Sender.Tell(new OrderBalance(Order.Amount.Btc())));
        }

        protected override void OnExecuted(OrderBookActor.OrderExecuted e, IActorRef sender)
        {
            BalanceRef.Tell(new UserBalance.AddDueOrderExecuted(e.OrderNum, e.Price * e.Amount));
        }
    }
}