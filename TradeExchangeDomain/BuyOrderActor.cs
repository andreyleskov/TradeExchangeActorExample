using Akka.Persistence;

namespace TradeExchangeDomain
{
    public class BuyOrderActor : OrderActor
    {
        public BuyOrderActor()
        {
          Command<Execute>(e => e.OrderBook.Tell(new NewBuyOrder(Order), Self));
        }
    }
}