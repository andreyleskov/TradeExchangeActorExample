using Akka.Persistence;

namespace TradeExchangeDomain
{
    public class SellOrderActor : OrderActor
    {
        public SellOrderActor()
        {
            Command<Execute>(e => e.OrderBook.Tell(new NewSellOrder(Order), Self));
        }
    }
}